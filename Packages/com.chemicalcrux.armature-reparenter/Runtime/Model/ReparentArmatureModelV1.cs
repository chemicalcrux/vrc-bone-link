using System;
using System.Collections.Generic;
using ChemicalCrux.CruxCore.Runtime.Upgrades;
using UnityEngine;

namespace ChemicalCrux.ArmatureReparenter.Runtime.Model
{
    [Serializable]
    [UpgradableVersion(version: 1)]
    public class ReparentArmatureModelV1 : ReparentArmatureModel
    {
        [Flags]
        public enum BoneGroups
        {
            LeftFingers = 1 << 0,
            RightFingers = 1 << 1,
            EntireLeftArm = 1 << 2,
            EntireRightArm = 1 << 3,
            EntireLeftFoot = 1 << 4,
            EntireRightFoot = 1 << 5
        }

        public enum ConstraintType
        {
            Parent,
            Position,
            Rotation
        }

        public AttachSettings defaultSettings;

        [Serializable]
        public struct OverrideGroup
        {
            public List<HumanBodyBones> bones;
            public BoneGroups boneGroups;
            
            public AttachSettings settings;
        }

        [Serializable]
        public struct AttachSettings
        {
            public ConstraintType constraintType;
            public bool keepPositionOffset;
            public bool keepRotationOffset;
        }

        public Animator source;
        public Animator target;

        public List<OverrideGroup> overrides;

        public override ReparentArmatureModel Upgrade()
        {
            return this;
        }
    }
}