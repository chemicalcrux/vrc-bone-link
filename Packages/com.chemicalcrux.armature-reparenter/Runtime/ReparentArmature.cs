using ChemicalCrux.ArmatureReparenter.Runtime.Freeze;
using ChemicalCrux.ArmatureReparenter.Runtime.Model;
using UnityEngine;
using VRC.SDKBase;

namespace ChemicalCrux.ArmatureReparenter.Runtime
{
    public class ReparentArmature : MonoBehaviour, IEditorOnly
    {
        [SerializeReference] public ReparentArmatureModel model = new ReparentArmatureModelV1();
        [SerializeReference] public ReparentArmatureFreeze freeze = new ReparentArmatureFreezeV1();

        void Reset()
        {
            model = new ReparentArmatureModelV1();
            freeze = new ReparentArmatureFreezeV1();
        }
    }
}
