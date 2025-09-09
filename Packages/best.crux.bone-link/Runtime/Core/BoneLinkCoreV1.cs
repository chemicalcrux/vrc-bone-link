using System;
using System.Collections.Generic;
using System.Linq;
using Crux.Core.Runtime.Attributes;
using Crux.Core.Runtime.Upgrades;
using UnityEngine;
using UnityEngine.Pool;

namespace Crux.BoneLink.Runtime.Core
{
    [Serializable]
    [UpgradableVersion(version: 1)]
    [UpgradablePropertyDrawer("ab666225cf34d431e9b41800697d764b,9197481963319205126")]
    public class BoneLinkCoreV1 : BoneLinkCore
    {
        public enum BoneGroup
        {
            LeftFingers = 0,
            RightFingers = 1,
            LeftArm = 2,
            RightArm = 3,
            LeftLeg = 4,
            RightLeg = 5,
            HipsThruHead = 6
        }

        public enum ConstraintType
        {
            Parent,
            Position,
            Rotation,
            None
        }

        public enum SmoothingType
        {
            None,
            Constant,
            Toggled
        }
        
        public enum BoneMatchMode
        {
            Single,
            ChildNames,
            ChildHierarchy
        }

        [Serializable]
        public struct BonePair
        {
            public BoneMatchMode mode;
            public Transform sourceBone;
            public Transform targetBone;

            public IEnumerable<(Transform sourceBone, Transform targetBone)> Resolve()
            {
                yield return (sourceBone, targetBone);

                switch (mode)
                {
                    case BoneMatchMode.Single:
                        break;
                    case BoneMatchMode.ChildNames:
                    {
                        using var handle = DictionaryPool<string, Transform>.Get(out var sourceMap);
                        foreach (Transform sourceChild in sourceBone)
                        {
                            sourceMap[sourceChild.name] = sourceChild;
                        }

                        foreach (Transform targetChild in targetBone)
                        {
                            if (sourceMap.TryGetValue(targetChild.name, out var sourceChild))
                            {
                                yield return (sourceChild, targetChild);
                                var recurse = new BonePair()
                                {
                                    sourceBone = sourceChild,
                                    targetBone = targetChild,
                                    mode = mode
                                };

                                foreach (var result in recurse.Resolve())
                                    yield return result;
                            }
                        }

                        break;
                    }
                    case BoneMatchMode.ChildHierarchy:
                    {
                        int limit = Mathf.Min(sourceBone.childCount, targetBone.childCount);

                        for (int i = 0; i < limit; ++i)
                        {
                            var sourceChild = sourceBone.GetChild(i);
                            var targetChild = targetBone.GetChild(i);

                            yield return (sourceChild, targetChild);

                            var recurse = new BonePair()
                            {
                                sourceBone = sourceChild,
                                targetBone = targetChild,
                                mode = mode
                            };

                            foreach (var result in recurse.Resolve())
                                yield return result;
                        }

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        [Serializable]
        public struct HumanoidOverrideGroup
        {
            public List<HumanBodyBones> humanBodyBones;
            public List<BoneGroup> boneGroups;

            private static IEnumerable<HumanBodyBones> GetBonesInRange(HumanBodyBones start,
                HumanBodyBones endExclusive)
            {
                for (int i = (int)start; i < (int)endExclusive; ++i)
                {
                    yield return (HumanBodyBones)i;
                }
            }

            private static IEnumerable<HumanBodyBones> GetBonesInGroups(List<BoneGroup> groups)
            {
                foreach (var group in groups)
                {
                    switch (group)
                    {
                        case BoneGroup.LeftFingers:
                            foreach (var bone in GetBonesInRange(HumanBodyBones.LeftThumbProximal,
                                         HumanBodyBones.RightThumbProximal))
                                yield return bone;
                            break;
                        case BoneGroup.RightFingers:
                            foreach (var bone in GetBonesInRange(HumanBodyBones.RightThumbProximal,
                                         HumanBodyBones.UpperChest))
                                yield return bone;
                            break;
                        case BoneGroup.LeftArm:
                            yield return HumanBodyBones.LeftShoulder;
                            yield return HumanBodyBones.LeftUpperArm;
                            yield return HumanBodyBones.LeftLowerArm;
                            yield return HumanBodyBones.LeftHand;
                            break;
                        case BoneGroup.RightArm:
                            yield return HumanBodyBones.RightShoulder;
                            yield return HumanBodyBones.RightUpperArm;
                            yield return HumanBodyBones.RightLowerArm;
                            yield return HumanBodyBones.RightHand;
                            break;
                        case BoneGroup.LeftLeg:
                            yield return HumanBodyBones.LeftUpperLeg;
                            yield return HumanBodyBones.LeftLowerArm;
                            yield return HumanBodyBones.LeftFoot;
                            break;
                        case BoneGroup.RightLeg:
                            yield return HumanBodyBones.RightUpperLeg;
                            yield return HumanBodyBones.RightLowerArm;
                            yield return HumanBodyBones.RightFoot;
                            break;
                        case BoneGroup.HipsThruHead:
                            yield return HumanBodyBones.Hips;
                            yield return HumanBodyBones.Spine;
                            yield return HumanBodyBones.Chest;
                            yield return HumanBodyBones.UpperChest;
                            yield return HumanBodyBones.Neck;
                            yield return HumanBodyBones.Head;
                            break;
                    }
                }
            }

            public IEnumerable<HumanBodyBones> GetBones()
            {
                return humanBodyBones
                    .Concat(GetBonesInGroups(boneGroups))
                    .Distinct();
            }

            public IEnumerable<(Transform sourceBone, Transform targetBone)> GetAllTransforms(Animator source,
                Animator target)
            {
                if (source && target)
                {
                    foreach (var humanBodyBone in GetBones())
                    {
                        var sourceTransform = source.GetBoneTransform(humanBodyBone);
                        var targetTransform = target.GetBoneTransform(humanBodyBone);

                        if (sourceTransform && targetTransform)
                            yield return (sourceTransform, targetTransform);
                    }
                }
            }

            public AttachSettings settings;
        }

        [Serializable]
        public struct OverrideGroup
        {
            public List<BonePair> bonePairs;

            public IEnumerable<(Transform sourceBone, Transform targetBone)> GetAllTransforms()
            {
                foreach (var bonePair in bonePairs)
                {
                    foreach (var result in bonePair.Resolve())
                    {
                        yield return result;
                    }
                }
            }

            public AttachSettings settings;
        }

        [Serializable]
        public struct AttachSettings
        {
            [TooltipRef(assetRef: "ec807bd54b68c4de3a7501ecdfe7a260,9197481963319205126")]
            public ConstraintType constraintType;
            [TooltipRef(assetRef: "c6dd706c60f8e4e4da97e68f6152a291,9197481963319205126")]
            public bool keepPosition;
            [TooltipRef(assetRef: "00cece11ac38746fa92e1d1a6a6c1ab3,9197481963319205126")]
            public bool keepRotation;

            public SmoothingType smoothingType;
            public float smoothingFixedPower;
            public string smoothingToggleParameter;
            public bool smoothingToggleControl;
            public string smoothingToggleControlPath;
        }
        
        [TooltipRef(assetRef: "099ba10bbffa2432c9ae815417d2ec0a,9197481963319205126")]
        public bool humanoid = true;
        
        [TooltipRef(assetRef: "c998a8524785d41b089046cc3440dfd7,9197481963319205126")]
        public Animator sourceAnimator;
        [TooltipRef(assetRef: "532045a36877140e1a86dc44e9053a20,9197481963319205126")]
        public Animator targetAnimator; 
        
        public AttachSettings humanoidSettings;

        public List<HumanoidOverrideGroup> humanoidOverrides;
        
        public List<OverrideGroup> overrides;

        public override BoneLinkCore Upgrade()
        {
            return this;
        }
    }
}