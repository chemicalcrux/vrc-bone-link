using System;
using System.Collections.Generic;
using System.Text;
using com.vrcfury.api;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.VFX;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using YamlDotNet.Core.Tokens;

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
        HashSet<HumanBodyBones> keepPositionSet = new();
        HashSet<HumanBodyBones> keepRotationSet = new();
        Dictionary<HumanBodyBones, ReparentArmature.ConstraintType> constraintTypes = new();

        List<VRCConstraintBase> constraints = new();

        foreach (var overrides in reparentArmature.overrides)
        {
            if (overrides.keepPositionOffset)
            {
                keepPositionSet.UnionWith(overrides.bones);
            }

            if (overrides.keepRotationOffset)
            {
                keepRotationSet.UnionWith(overrides.bones);
            }

            foreach (var bone in overrides.bones)
                constraintTypes[bone] = overrides.constraintType;
        }

        Debug.Log(reparentArmature.source, reparentArmature.source);
        Debug.Log(reparentArmature.target, reparentArmature.target);

        foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone == HumanBodyBones.LastBone)
                break;

            var sourceBone = reparentArmature.source.GetBoneTransform(bone);
            var targetBone = reparentArmature.target.GetBoneTransform(bone);

            Debug.Log(sourceBone, sourceBone);
            Debug.Log(targetBone, targetBone);
            Debug.Log("???");

            if (!sourceBone || !targetBone)
                continue;

            var constraintType = constraintTypes.GetValueOrDefault(bone, ReparentArmature.ConstraintType.Parent);

            Vector3 position = default;

            if (keepPositionSet.Contains(bone))
            {
                position = targetBone.InverseTransformDirection(sourceBone.position - targetBone.position);
            }

            Quaternion rotation = default;

            if (keepRotationSet.Contains(bone))
            {
                rotation = Quaternion.Inverse(targetBone.rotation) * sourceBone.rotation;
            }

            VRCConstraintBase constraint;

            switch (constraintType)
            {
                case ReparentArmature.ConstraintType.Parent:
                    constraint = sourceBone.gameObject.AddComponent<VRCParentConstraint>();
                    break;
                case ReparentArmature.ConstraintType.Position:
                    constraint = sourceBone.gameObject.AddComponent<VRCPositionConstraint>();
                    break;
                case ReparentArmature.ConstraintType.Rotation:
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

        var controller = new AnimatorController();

        fc.AddController(controller);
        fc.AddGlobalParam(reparentArmature.freezeParameter);

        controller.AddParameter(reparentArmature.freezeParameter, AnimatorControllerParameterType.Float);
        
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
            blendParameter = reparentArmature.freezeParameter,
            useAutomaticThresholds = true
        };

        blend.motion = tree;

        var idleClip = new AnimationClip();
        var freezeClip = new AnimationClip();

        idleClip.name = "Idle";
        freezeClip.name = "Freeze";

        foreach (var constraint in constraints)
        {
            string path = GetPath(reparentArmature.source.transform, constraint.transform);
            Type type = constraint.GetType();
            string property = "FreezeToWorld";
            var zeroCurve = AnimationCurve.Constant(0, 1, 0);
            var oneCurve = AnimationCurve.Constant(0, 1, 1);

            Debug.Log(path);

            idleClip.SetCurve(path, type, property, zeroCurve);
            freezeClip.SetCurve(path, type, property, oneCurve);
        }

        tree.AddChild(idleClip);
        tree.AddChild(freezeClip);

        if (reparentArmature.addFreezeControl)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            
            menu.Parameters = parameters;
            
            menu.controls = new List<VRCExpressionsMenu.Control>
            {
                new VRCExpressionsMenu.Control
                {
                    name = "Freeze",
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = reparentArmature.freezeParameter
                    }
                }
            };

            parameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = reparentArmature.freezeParameter,
                    saved = false,
                    defaultValue = 0,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Bool
                }
            };

            fc.AddMenu(menu, reparentArmature.freezeControlPath);
            fc.AddParams(parameters);
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