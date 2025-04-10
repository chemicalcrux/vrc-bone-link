using System;
using Crux.Core.Runtime.Attributes;
using Crux.Core.Runtime.Upgrades;

namespace Crux.BoneLink.Runtime.Core
{
    [Serializable]
    [UpgradableLatestVersion(version: 1)]
    [DocRef(manualRef: "9fae0cf57fc1c44b99676a7db24745d0,11400000", pageRef: "5bc7e0159ad644ac599cad45de3c1d0e,11400000")]
    [UpgradablePropertyDrawer("Packages/best.crux.bone-link/UI/Property Drawers/Core/CoreV1.uxml")]
    public abstract class BoneLinkCore : Upgradable<BoneLinkCore>
    {
        
    }
}