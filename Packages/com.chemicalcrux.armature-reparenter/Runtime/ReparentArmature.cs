using ChemicalCrux.ArmatureReparenter.Runtime.Freeze;
using ChemicalCrux.ArmatureReparenter.Runtime.Core;
using UnityEngine;
using VRC.SDKBase;

namespace ChemicalCrux.ArmatureReparenter.Runtime
{
    public class ReparentArmature : MonoBehaviour, IEditorOnly
    {
        [SerializeReference] public ReparentArmatureCore core = new ReparentArmatureCoreV1();
        [SerializeReference] public ReparentArmatureFreeze freeze = new ReparentArmatureFreezeV1();

        void Reset()
        {
            core = new ReparentArmatureCoreV1();
            freeze = new ReparentArmatureFreezeV1();
        }
    }
}
