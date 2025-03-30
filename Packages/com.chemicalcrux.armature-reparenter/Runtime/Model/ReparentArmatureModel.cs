using System;
using ChemicalCrux.CruxCore.Runtime.Upgrades;

namespace ChemicalCrux.ArmatureReparenter.Runtime.Model
{
    [Serializable]
    [UpgradableLatestVersion(version: 1)]
    public abstract class ReparentArmatureModel : Upgradable<ReparentArmatureModel>
    {
        
    }
}