using Crux.BoneLink.Runtime.Core;
using UnityEditor;
using UnityEngine.UIElements;

namespace Crux.BoneLink.Editor.Editor.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(BoneLinkCoreV1.AttachSettings))]
    public class CoreV1AttachSettingsPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/best.crux.bone-link/UI/Property Drawers/Core/CoreV1_AttachSettings.uxml").Instantiate();

            root.Q<Foldout>().text = property.displayName;
            
            return root;
        }
    }
}