using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace ChemicalCrux.ArmatureReparenter.Runtime
{
    public class ReparentArmature : MonoBehaviour, IEditorOnly
    {
        public enum ConstraintType
        {
            Parent,
            Position,
            Rotation
        }
    
        [Serializable]
        public struct OverrideInfo
        {
            public List<HumanBodyBones> bones;
        
            public ConstraintType constraintType;
        
            public bool keepPositionOffset;
            public bool keepRotationOffset;
        }
    
        public Animator source;
        public Animator target;

        public List<OverrideInfo> overrides;

        [Tooltip("Use a global parameter with this name to freeze the armature")]
        public string freezeParameter = "chemicalcrux/Armature Reparenter/Freeze";

        [Tooltip("Creates a synced bool parameter and adds it your menu.")]
        public bool addFreezeControl;
        public string freezeControlPath = "";
    }
}
