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
            Dictionary<Transform, Transform> bonePairs = new();
            Dictionary<Transform, ReparentArmatureModelV1.AttachSettings> overrideMap = new();

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

            foreach (HumanBodyBones humanBodyBone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (humanBodyBone == HumanBodyBones.LastBone)
                    break;
                
                var sourceTransform = model.source.GetBoneTransform(humanBodyBone);
                var targetTransform = model.target.GetBoneTransform(humanBodyBone);

                if (sourceTransform && targetTransform)
                {
                    bonePairs[sourceTransform] = targetTransform;
                    overrideMap[sourceTransform] = model.defaultSettings;
                }
            }

            foreach (var overrideData in model.overrides)
            {
                foreach (var (sourceBone, targetBone) in overrideData.GetAllTransforms(model.source, model.target))
                {
                    bonePairs[sourceBone] = targetBone;
                    overrideMap[sourceBone] = overrideData.settings;
                }
            }

            foreach ((Transform sourceBone, Transform targetBone) in bonePairs)
            {
                var settings = overrideMap[sourceBone];

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
                    case ReparentArmatureModelV1.ConstraintType.None:
                        constraint = null;
                        break;
                    default:
                        throw new Exception("What did you do");
                }

                if (constraint == null)
                    continue;

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

            if (freeze.enabled)
            {
                var controller = new AnimatorController();

                string parameterName = "Control/Freeze";

                if (freeze.addGlobalParameter)
                {
                    parameterName = freeze.globalParameterName;
                    fc.AddGlobalParam(parameterName);
                }

                fc.AddController(controller);

                controller.AddParameter(parameterName, AnimatorControllerParameterType.Float);

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
                    blendParameter = parameterName,
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

                if (freeze.addControl)
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
                                name = parameterName
                            }
                        }
                    };

                    parameters.parameters = new[]
                    {
                        new VRCExpressionParameters.Parameter
                        {
                            name = parameterName,
                            saved = false,
                            defaultValue = 0,
                            networkSynced = true,
                            valueType = VRCExpressionParameters.ValueType.Bool
                        }
                    };

                    fc.AddMenu(menu, freeze.controlPath);
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