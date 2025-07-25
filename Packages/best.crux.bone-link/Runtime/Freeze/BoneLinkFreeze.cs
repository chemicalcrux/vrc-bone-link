using System;
using Crux.Core.Runtime.Attributes;
using Crux.Core.Runtime.Upgrades;

namespace Crux.BoneLink.Runtime.Freeze
{
    [Serializable]
    [UpgradableLatestVersion(version: 1)]
    [DocRef(manualRef: "9fae0cf57fc1c44b99676a7db24745d0,11400000", pageRef: "21f3700e39ecd4b49a8dc0a13aae8a36,11400000")]
    [UpgradablePropertyDrawer("Packages/best.crux.bone-link/UI/Property Drawers/Freeze/FreezeV1.uxml")]
    public abstract class BoneLinkFreeze : Upgradable<BoneLinkFreeze>
    {
        
    }
}