using System;
using System.Collections.Generic;
using System.Linq;
using com.vrcfury.api;
using Crux.BoneLink.Runtime;
using Crux.BoneLink.Runtime.Core;
using Crux.BoneLink.Runtime.Freeze;
using HarmonyLib;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace Crux.BoneLink.Editor.Editor
{
    public class BoneLinkProcessor : IVRCSDKPreprocessAvatarCallback
    {
        private struct SmoothingKey : IEquatable<SmoothingKey>
        {
            public float basePower;
            public BoneLinkCoreV1.SmoothingType type;
            public String toggleParameter;

            public bool Equals(SmoothingKey other)
            {
                return basePower.Equals(other.basePower) && type == other.type && toggleParameter == other.toggleParameter;
            }

            public override bool Equals(object obj)
            {
                return obj is SmoothingKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(basePower, (int)type, toggleParameter);
            }
        }
        
        private class SmoothingGroup
        {
            public List<VRCConstraintBase> constraints = new();
            public float basePower;
            public String toggleParameter;
            public BoneLinkCoreV1.SmoothingType type;
        }

        private struct SmoothMenuItems : IEquatable<SmoothMenuItems>
        {
            public string controlPath;
            public string parameter;

            public bool Equals(SmoothMenuItems other)
            {
                return controlPath == other.controlPath && parameter == other.parameter;
            }

            public override bool Equals(object obj)
            {
                return obj is SmoothMenuItems other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(controlPath, parameter);
            }
        }
        
        public int callbackOrder => -10001;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var thing in avatarGameObject.GetComponentsInChildren<BoneLinkBuilder>(true))
                Process(thing);

            return true;
        }

        void Process(BoneLinkBuilder boneLinkBuilder)
        {
            Dictionary<SmoothingKey, SmoothingGroup> smoothingGroups = new();
            Dictionary<Transform, Transform> bonePairs = new();
            Dictionary<Transform, BoneLinkCoreV1.AttachSettings> overrideMap = new();

            HashSet<string> smoothParameters = new();
            HashSet<SmoothMenuItems> smoothMenuItems = new();
            

            List<VRCConstraintBase> constraints = new();

            if (!boneLinkBuilder.core.TryUpgradeTo(out BoneLinkCoreV1 model))
            {
                Debug.LogError("Failed to upgrade the model data.");
                return;
            }

            if (!boneLinkBuilder.freeze.TryUpgradeTo(out BoneLinkFreezeV1 freeze))
            {
                Debug.LogError("Failed to upgrade the freeze data.");
                return;
            }

            if (model.humanoid)
            {
                foreach (HumanBodyBones humanBodyBone in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (humanBodyBone == HumanBodyBones.LastBone)
                        break;
                
                    var sourceTransform = model.sourceAnimator.GetBoneTransform(humanBodyBone);
                    var targetTransform = model.targetAnimator.GetBoneTransform(humanBodyBone);

                    if (sourceTransform && targetTransform)
                    {
                        bonePairs[sourceTransform] = targetTransform;
                        overrideMap[sourceTransform] = model.humanoidSettings;
                    }
                }

                foreach (var overrideData in model.humanoidOverrides)
                {
                    foreach (var (sourceBone, targetBone) in overrideData.GetAllTransforms(model.sourceAnimator, model.targetAnimator))
                    {
                        bonePairs[sourceBone] = targetBone;
                        overrideMap[sourceBone] = overrideData.settings;
                    }
                }
            }

            foreach (var overrideData in model.overrides)
            {
                foreach (var (sourceBone, targetBone) in overrideData.GetAllTransforms())
                {
                    bonePairs[sourceBone] = targetBone;
                    overrideMap[sourceBone] = overrideData.settings;
                }
            }

            foreach ((Transform sourceBone, Transform targetBone) in bonePairs)
            {
                var settings = overrideMap[sourceBone];

                Vector3 position = Vector3.zero;

                if (settings.keepPosition)
                {
                    position = targetBone.InverseTransformDirection(sourceBone.position - targetBone.position);
                }

                Quaternion rotation = Quaternion.identity;

                if (settings.keepRotation)
                {
                    rotation = Quaternion.Inverse(targetBone.rotation) * sourceBone.rotation;
                }

                VRCConstraintBase constraint;

                switch (settings.constraintType)
                {
                    case BoneLinkCoreV1.ConstraintType.Parent:
                        constraint = sourceBone.gameObject.AddComponent<VRCParentConstraint>();
                        break;
                    case BoneLinkCoreV1.ConstraintType.Position:
                        constraint = sourceBone.gameObject.AddComponent<VRCPositionConstraint>();
                        break;
                    case BoneLinkCoreV1.ConstraintType.Rotation:
                        constraint = sourceBone.gameObject.AddComponent<VRCRotationConstraint>();
                        break;
                    case BoneLinkCoreV1.ConstraintType.None:
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

                if (settings.smoothingType != BoneLinkCoreV1.SmoothingType.None)
                {
                    SmoothingKey key = new SmoothingKey
                    {
                        basePower = settings.smoothingFixedPower,
                        type = settings.smoothingType,
                        toggleParameter = settings.smoothingToggleParameter
                    };

                    if (!smoothingGroups.TryGetValue(key, out var group))
                    {
                        group = new SmoothingGroup
                        {
                            basePower = settings.smoothingFixedPower,
                            type = settings.smoothingType,
                            toggleParameter = settings.smoothingToggleParameter
                        };
                        
                        smoothingGroups[key] = group;
                    }

                    group.constraints.Add(constraint);
                    
                    constraint.Sources.Add(new VRCConstraintSource
                    {
                        Weight = 0f,
                        SourceTransform = sourceBone,
                        ParentPositionOffset = Vector3.zero,
                        ParentRotationOffset = Vector3.zero
                    });

                    if (settings.smoothingType == BoneLinkCoreV1.SmoothingType.Toggled)
                    {
                        smoothParameters.Add(settings.smoothingToggleParameter);

                        if (settings.smoothingToggleControl)
                        {
                            smoothMenuItems.Add(new()
                            {
                                controlPath = settings.smoothingToggleControlPath,
                                parameter = settings.smoothingToggleParameter
                            });
                        }
                    }
                }
                
                constraint.SolveInLocalSpace = true;
                constraint.Locked = true;
                constraint.IsActive = true;
            }

            var fc = FuryComponents.CreateFullController(boneLinkBuilder.gameObject);

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
                    string path = GetPath(model.sourceAnimator.transform, constraint.transform);
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

            var smoothingParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            smoothingParameters.parameters = smoothParameters.Select(param => new VRCExpressionParameters.Parameter
            {
                name = param,
                defaultValue = 1f,
                networkSynced = true,
                saved = false,
                valueType = VRCExpressionParameters.ValueType.Bool
            }).ToArray();

            fc.AddParams(smoothingParameters);

            foreach (var param in smoothParameters)
            {
                fc.AddGlobalParam(param);
            }

            foreach (var item in smoothMenuItems)
            {
                var result = ScriptableObject.CreateInstance<VRCExpressionsMenu>();

                result.controls.Add(new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    name = "Smoothing Toggle",
                    parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = item.parameter
                    }
                });

                fc.AddMenu(result, item.controlPath);
            }

            AnimatorController smoothingController = new AnimatorController();

            smoothingController.AddParameter("Shared/Time/Delta", AnimatorControllerParameterType.Float);
            
            fc.AddController(smoothingController);
            fc.AddGlobalParam("Shared/Time/Delta");

            int smoothingCount = 1;
            
            foreach (var group in smoothingGroups.Values)
            {
                var smoothingMachine = new AnimatorStateMachine
                {
                    name = $"Smoothing Machine {smoothingCount}"
                };

                var smoothingLayer = new AnimatorControllerLayer
                {
                    name = $"Smoothing Layer {smoothingCount}",
                    defaultWeight = 1f,
                    stateMachine = smoothingMachine
                };

                smoothingController.AddLayer(smoothingLayer);

                var smoothingState = smoothingMachine.AddState("Smooth");
                smoothingState.timeParameterActive = true;
                smoothingState.timeParameter = "Shared/Time/Delta";
                
                var smoothingClip = new AnimationClip
                {
                    name = $"Smoothing Clip {smoothingCount}"
                };

                var smoothingTargetCurve = new AnimationCurve();
                var smoothingSelfCurve = new AnimationCurve();
                
                // TODO denser near zero?

                for (int i = 0; i < 50; ++i)
                {
                    // assume a worst-case framerate of 10
                    float t = Mathf.InverseLerp(0, 49, i);
                    float result = 1 - Mathf.Exp(-t * group.basePower);
                    smoothingSelfCurve.AddKey(t, 1 - result);
                    smoothingTargetCurve.AddKey(t, result);
                }

                foreach (var constraint in group.constraints)
                {
                    String path = GetPath(model.sourceAnimator.transform, constraint.transform);
                    Type type = constraint.GetType();
                    String targetProperty = "Sources.source0.Weight";
                    String selfProperty = "Sources.source1.Weight";

                    smoothingClip.SetCurve(path, type, targetProperty, smoothingTargetCurve);
                    smoothingClip.SetCurve(path, type, selfProperty, smoothingSelfCurve);
                }

                smoothingState.motion = smoothingClip;
                
                if (group.type == BoneLinkCoreV1.SmoothingType.Toggled)
                {
                    if (smoothingController.MakeUniqueParameterName(group.toggleParameter) == group.toggleParameter)
                    {
                        smoothingController.AddParameter(group.toggleParameter, AnimatorControllerParameterType.Bool);
                    }
                    
                    var noSmoothingState = smoothingMachine.AddState($"No Smoothing {smoothingCount}");
                    
                    var noSmoothingClip = new AnimationClip
                    {
                        name = $"No Smoothing Clip {smoothingCount}"
                    };

                    foreach (var constraint in group.constraints)
                    {
                        String path = GetPath(model.sourceAnimator.transform, constraint.transform);
                        Type type = constraint.GetType();
                        String targetProperty = "Sources.source0.Weight";
                        String selfProperty = "Sources.source1.Weight";

                        noSmoothingClip.SetCurve(path, type, targetProperty, AnimationCurve.Constant(0, 1, 1));
                        noSmoothingClip.SetCurve(path, type, selfProperty, AnimationCurve.Constant(0, 1, 0));
                    }

                    noSmoothingState.motion = noSmoothingClip;

                    var stopSmoothingTransition = smoothingState.AddTransition(noSmoothingState);
                    stopSmoothingTransition.hasExitTime = false;
                    stopSmoothingTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, group.toggleParameter);
                    stopSmoothingTransition.duration = 0.5f;

                    var startSmoothingTransition = noSmoothingState.AddTransition(smoothingState);
                    startSmoothingTransition.hasExitTime = false;
                    startSmoothingTransition.AddCondition(AnimatorConditionMode.If, 0f, group.toggleParameter);
                    stopSmoothingTransition.duration = 0.5f;
                }

                ++smoothingCount;
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