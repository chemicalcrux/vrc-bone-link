using System;
using ChemicalCrux.CruxCore.Runtime.Upgrades;

namespace ChemicalCrux.ArmatureReparenter.Runtime.Freeze
{
    [Serializable]
    [UpgradableLatestVersion(version: 1)]
    public abstract class ReparentArmatureFreeze : Upgradable<ReparentArmatureFreeze>
    {
        
    }
}