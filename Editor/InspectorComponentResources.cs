using UnityEngine.UIElements;

namespace InspectorComponents.Editor
{
    internal class InspectorComponentResources
    {
        internal VisualTreeAsset ButtonTemplate { get; private set; }
        internal StyleSheet StyleSheet { get; private set; }
        
        internal InspectorComponentResources(VisualTreeAsset buttonTemplate, StyleSheet styleSheet)
        {
            ButtonTemplate = buttonTemplate;
            StyleSheet = styleSheet;
        }
    }
}