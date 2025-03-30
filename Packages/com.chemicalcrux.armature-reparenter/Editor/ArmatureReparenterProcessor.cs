using System;
using System.Collections.Generic;
using ChemicalCrux.ArmatureReparenter.Runtime;
using ChemicalCrux.ArmatureReparenter.Runtime.Freeze;
using ChemicalCrux.ArmatureReparenter.Runtime.Model;
using com.vrcfury.api;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace ChemicalCrux.ArmatureReparenter.Editor
{
    public class ReparentArmatureProcessor : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -10001;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var thing in avatarGameObject.GetComponentsInChildren<ReparentArmature>(true))
                Process(thing);

            return true;
        }

        void Process(ReparentArmature reparentArmature)
        {
            Dictionary<HumanBodyBones, ReparentArmatureModelV1.AttachSettings> overrideMap = new();

            List<VRCConstraintBase> constraints = new();

            if (!reparentArmature.model.TryUpgradeTo(out ReparentArmatureModelV1 model))
            {
                Debug.LogError("Failed to upgrade the model data.");
                return;
            }

            if (!reparentArmature.freeze.TryUpgradeTo(out ReparentArmatureFreezeV1 freeze))
            {
                Debug.LogError("Failed to upgrade the freeze data.");
                return;
            }

            foreach (var overrideData in model.overrides)
            {
                foreach (var bone in overrideData.bones)
                {
                    overrideMap[bone] = overrideData.settings;
                }
            }

            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone)
                    break;

                var sourceBone = model.source.GetBoneTransform(bone);
                var targetBone = model.target.GetBoneTransform(bone);

                if (!sourceBone || !targetBone)
                    continue;

                var settings = overrideMap.GetValueOrDefault(bone, model.defaultSettings);

                Vector3 position = default;

                if (settings.keepPositionOffset)
                {
                    position = targetBone.InverseTransformDirection(sourceBone.position - targetBone.position);
                }

                Quaternion rotation = default;

                if (settings.keepRotationOffset)
                {
                    rotation = Quaternion.Inverse(targetBone.rotation) * sourceBone.rotation;
                }

                VRCConstraintBase constraint;

                switch (settings.constraintType)
                {
                    case ReparentArmatureModelV1.ConstraintType.Parent:
                        constraint = sourceBone.gameObject.AddComponent<VRCParentConstraint>();
                        break;
                    case ReparentArmatureModelV1.ConstraintType.Position:
                        constraint = sourceBone.gameObject.AddComponent<VRCPositionConstraint>();
                        break;
                    case ReparentArmatureModelV1.ConstraintType.Rotation:
                        constraint = sourceBone.gameObject.AddComponent<VRCRotationConstraint>();
                        break;
                    default:
                        throw new Exception("What did you do");
                }

                constraints.Add(constraint);

                constraint.Sources.Add(new VRCConstraintSource
                {
                    Weight = 1f,
                    SourceTransform = targetBone,
                    ParentPositionOffset = position,
                    ParentRotationOffset = rotation.eulerAngles
                });

                constraint.Locked = true;
                constraint.IsActive = true;
            }

            var fc = FuryComponents.CreateFullController(reparentArmature.gameObject);

            if (freeze.addFreeze)
            {
                var controller = new AnimatorController();

                fc.AddController(controller);
                fc.AddGlobalParam(freeze.freezeParameter);

                controller.AddParameter(freeze.freezeParameter, AnimatorControllerParameterType.Float);

                var machine = new AnimatorStateMachine
                {
                    name = "Freeze Machine"
                };

                var layer = new AnimatorControllerLayer
                {
                    name = "Freeze",
                    defaultWeight = 1f,
                    stateMachine = machine
                };

                controller.AddLayer(layer);

                var blend = machine.AddState("Blend");

                var tree = new BlendTree
                {
                    name = "Blend",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = freeze.freezeParameter,
                    useAutomaticThresholds = true
                };

                blend.motion = tree;

                var idleClip = new AnimationClip();
                var freezeClip = new AnimationClip();

                idleClip.name = "Idle";
                freezeClip.name = "Freeze";

                foreach (var constraint in constraints)
                {
                    string path = GetPath(model.source.transform, constraint.transform);
                    Type type = constraint.GetType();
                    string property = "FreezeToWorld";
                    var zeroCurve = AnimationCurve.Constant(0, 1, 0);
                    var oneCurve = AnimationCurve.Constant(0, 1, 1);

                    idleClip.SetCurve(path, type, property, zeroCurve);
                    freezeClip.SetCurve(path, type, property, oneCurve);
                }

                tree.AddChild(idleClip);
                tree.AddChild(freezeClip);

                if (freeze.addFreezeControl)
                {
                    var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

                    menu.Parameters = parameters;

                    menu.controls = new List<VRCExpressionsMenu.Control>
                    {
                        new()
                        {
                            name = "Freeze",
                            type = VRCExpressionsMenu.Control.ControlType.Toggle,
                            parameter = new VRCExpressionsMenu.Control.Parameter
                            {
                                name = freeze.freezeParameter
                            }
                        }
                    };

                    parameters.parameters = new[]
                    {
                        new VRCExpressionParameters.Parameter
                        {
                            name = freeze.freezeParameter,
                            saved = false,
                            defaultValue = 0,
                            networkSynced = true,
                            valueType = VRCExpressionParameters.ValueType.Bool
                        }
                    };

                    fc.AddMenu(menu, freeze.freezeControlPath);
                    fc.AddParams(parameters);
                }
            }
        }

        static string GetPath(Transform root, Transform target)
        {
            string path = target.name;
            target = target.parent;

            while (target != root)
            {
                path = target.name + "/" + path;
                target = target.parent;
            }

            return path;
        }
    }
}