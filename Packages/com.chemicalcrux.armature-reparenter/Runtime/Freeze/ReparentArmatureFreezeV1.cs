using System;
using ChemicalCrux.CruxCore.Runtime.Upgrades;

namespace ChemicalCrux.ArmatureReparenter.Runtime.Freeze
{
    [Serializable]
    [UpgradableVersion(version: 1)]
    public class ReparentArmatureFreezeV1 : ReparentArmatureFreeze
    {
        public bool addFreeze;
        public bool addGlobalParameter;
        public string freezeParameter = "chemicalcrux/Armature Reparenter/Freeze";

        public bool addFreezeControl;
        public string freezeControlPath = "";
        
        public override ReparentArmatureFreeze Upgrade()
        {
            return this;
        }
    }
}