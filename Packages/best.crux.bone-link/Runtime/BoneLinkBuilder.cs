using Crux.BoneLink.Runtime.Core;
using Crux.BoneLink.Runtime.Freeze;
using UnityEngine;
using VRC.SDKBase;

namespace Crux.BoneLink.Runtime
{
    [AddComponentMenu(menuName: Consts.ComponentRootPath + "Bone Link")]
    public class BoneLinkBuilder : MonoBehaviour, IEditorOnly
    {
        [SerializeReference] public BoneLinkCore core = new BoneLinkCoreV1();
        [SerializeReference] public BoneLinkFreeze freeze = new BoneLinkFreezeV1();

        void Reset()
        {
            core = new BoneLinkCoreV1();
            freeze = new BoneLinkFreezeV1();
        }
    }
}
