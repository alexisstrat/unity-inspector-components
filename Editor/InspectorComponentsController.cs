using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace InspectorComponents.Editor
{
    internal class InspectorComponentsController : IDisposable
    {
        private const string UnityInspectorEditorListClassName = "unity-inspector-editors-list";

        private readonly Dictionary<Component, VisualElement> _componentToVisualElementMap = new Dictionary<Component, VisualElement>();
       
        private InspectorButtonsContainer _inspectorButtonsContainer;
        private VisualTreeAsset _buttonTemplate;
        private VisualElement _listParent;
        private GameObject _currentTarget;
        private Component[] _allComponents;
        private StyleSheet _styleSheet;
        private PropertyInfo _isLockedProperty;
        
        internal EditorWindow InspectorEditorWindow { get; private set; }
        internal bool IsLocked => (bool) _isLockedProperty.GetValue(InspectorEditorWindow);

        private bool _disposed;

        internal InspectorComponentsController(EditorWindow inspectorEditorWindow, GameObject currentTarget,
            InspectorComponentResources resources)
        {
            InspectorEditorWindow = inspectorEditorWindow;
            _currentTarget = currentTarget;
            
            _buttonTemplate = resources.ButtonTemplate;
            _styleSheet = resources.StyleSheet;
            
            _isLockedProperty = inspectorEditorWindow.GetType().GetProperty("isLocked", BindingFlags.Instance | BindingFlags.Public);
            
            _listParent = InspectorEditorWindow.rootVisualElement.Q(null, UnityInspectorEditorListClassName);
            
            _allComponents = GetAllComponents();
            _inspectorButtonsContainer = new InspectorButtonsContainer(_listParent, _allComponents, _buttonTemplate,
                _styleSheet, OnComponentSelectUpdate);
        }
        
        internal void InjectButtonsContainer()
        {
            if (_listParent == null || _listParent.childCount == 0) return;
            
            _componentToVisualElementMap.Clear();

            if (_inspectorButtonsContainer.Container.parent != _listParent)
            {
                _inspectorButtonsContainer.Container.RemoveFromHierarchy();
                _listParent.Insert(1, _inspectorButtonsContainer.Container);
            }
            
            var allChildren = _listParent.Children().ToList();
            foreach (var comp in _allComponents)
            {
                if (TryGetVisualElement(allChildren, comp, out var visualElement))
                {
                    visualElement.style.display = DisplayStyle.Flex;
                    _componentToVisualElementMap.TryAdd(comp, visualElement);
                    
                    // remove the visual element from the list to avoid iterating over it again
                    // and avoid potential issues with multiple components of the same type
                    allChildren.Remove(visualElement);
                }
            }

            if (!IsLocked)
            {
                _inspectorButtonsContainer.Update(_allComponents, _buttonTemplate);
            }
            else
            {
                _inspectorButtonsContainer.ShowPreviousSelection();
            }
        }
        
        internal void UpdateCurrentTarget(GameObject activeGameObject)
        {
            _currentTarget = activeGameObject;
            _allComponents = GetAllComponents();
        }

        private void OnComponentSelectUpdate(Component component, DisplayStyle displayStyle)
        {
            if (_componentToVisualElementMap.TryGetValue(component, out var visualElement))
            {
                visualElement.style.display = displayStyle;
            }
        }

        private Component[] GetAllComponents()
        {
            if (!_currentTarget) return Array.Empty<Component>();
            
            var components = _currentTarget.GetComponents<Component>();
            return components ?? Array.Empty<Component>();
        }
        
        private static bool TryGetVisualElement(List<VisualElement> allElements, Component targetComponent, out VisualElement visualElement)
        {
            foreach (var element in allElements)
            {
                if (element == null) continue;
        
                // Try to match by naming convention
                var elementName = element.name.ToLower();
                var componentName = targetComponent.GetType().Name.ToLower();

                if (!elementName.Contains(componentName)) continue;
                
                visualElement = element;
                return true;
            }

            visualElement = null;
            return false;
        }

        public void Dispose()
        {
            foreach (var visualElement in _componentToVisualElementMap.Values)
            {
                visualElement.style.display = DisplayStyle.Flex;
            }
            
            _inspectorButtonsContainer.Dispose();
            _componentToVisualElementMap.Clear();
            _inspectorButtonsContainer = null;
            _listParent = null;
            _styleSheet = null;
            _isLockedProperty = null;
            _buttonTemplate = null;
            _allComponents = null;
            _currentTarget = null;
            InspectorEditorWindow = null;

            _disposed = true;
        }
    }
}