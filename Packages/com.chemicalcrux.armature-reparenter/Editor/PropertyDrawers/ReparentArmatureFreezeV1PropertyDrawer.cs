using ChemicalCrux.ArmatureReparenter.Runtime.Freeze;
using ChemicalCrux.CruxCore.Editor.PropertyDrawers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ChemicalCrux.ArmatureReparenter.Editor.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(ReparentArmatureFreezeV1))]
    public class ReparentArmatureFreezeV1PropertyDrawer : UpgradablePropertyDrawer
    {
        protected override bool CreateMainInterface(SerializedProperty property, VisualElement area)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.chemicalcrux.armature-reparenter/UI/Property Drawers/Freeze/FreezeV1.uxml");

            uxml.CloneTree(area);

            foreach (var (toggleID, foldoutID) in new[]
                     {
                         ("Enabled", "EnabledFoldout"),
                         ("AddGlobalParameter", "AddGlobalParameterFoldout"),
                         ("AddControl", "AddControlFoldout")
                     })
            {
                var toggle = area.Q<PropertyField>(toggleID);
                VisualElement foldout = area.Q(foldoutID);

                toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
                {
                    foldout.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                });
            }

            return true;
        }
    }
}