using System;
using Crux.Core.Runtime.Attributes;
using Crux.Core.Runtime.Upgrades;

namespace ChemicalCrux.ArmatureReparenter.Runtime.Freeze
{
    [Serializable]
    [UpgradableVersion(version: 1)]
    public class ReparentArmatureFreezeV1 : ReparentArmatureFreeze
    {
        [TooltipRef(assetRef: "d23f70b6969084f41bf1b3e2ec2d5b66,9197481963319205126")]
        public bool enabled;
        [TooltipRef(assetRef: "27c184ae64e0c40e6a46373658036f76,9197481963319205126")]
        public bool addGlobalParameter;
        public string globalParameterName = "chemicalcrux/Armature Reparenter/Freeze";

        [TooltipRef(assetRef: "cdf347fedf2a741e0ae52044808e51ae,9197481963319205126")]
        public bool addControl;
        public string controlPath = "Freeze";
        
        public override ReparentArmatureFreeze Upgrade()
        {
            return this;
        }
    }
}