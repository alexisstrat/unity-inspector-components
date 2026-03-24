using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace InspectorComponents.Editor
{
    internal class InspectorButtonsContainer : IDisposable
    {
        private readonly VisualElement _parentContainer;
        private readonly Action<Component, DisplayStyle> _onComponentViewUpdate;

        private Button _allButton;
        private readonly Dictionary<Component, Button> _componentToButtonMap = new Dictionary<Component, Button>();
        private readonly List<int> _selectedComponentsIds = new List<int>();
        private bool _allSelected;
        
        private const string ComponentButtonClassName = "component-button";
        private const string ComponentButtonElementName = "component-button";
        private const string ImageElementName = "component-icon";
        private const string LabelElementName = "component-name";
        private const string PressedButtonClassName = "pressed";

        internal VisualElement Container { get; private set; }
        private bool _disposed;

        internal InspectorButtonsContainer(VisualElement parentContainer, Component[] components,
            VisualTreeAsset buttonTemplate, StyleSheet styleSheet, Action<Component, DisplayStyle> onComponentViewUpdate)
        {
            _parentContainer = parentContainer;
            _onComponentViewUpdate = onComponentViewUpdate;
            
            Container = new VisualElement()
            {   name = "Component Buttons Container",
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, }
            };
            Container.styleSheets.Add(styleSheet);
            
            CreateButtons(components, buttonTemplate);
        }
        
        internal void ShowPreviousSelection()
        {
            foreach (var component in _componentToButtonMap.Keys)
            {
                var id = component.GetInstanceID();
                if (_selectedComponentsIds.Contains(id))
                {
                    _onComponentViewUpdate?.Invoke(component, DisplayStyle.Flex);
                }
                else
                {
                    _onComponentViewUpdate?.Invoke(component, DisplayStyle.None);
                }
            }
        }
        
        internal void Update(Component[] components, VisualTreeAsset buttonTemplate)
        {
            CleanupButtons();
            
            _selectedComponentsIds.Clear();
            _componentToButtonMap.Clear();
            Container.Clear();
            
            if (components == null || components.Length == 0) return;
            CreateButtons(components, buttonTemplate);
        }

        private void CleanupButtons()
        {
            _allButton?.UnregisterCallback<ClickEvent>(OnAllButtonClick);

            foreach (var button in _componentToButtonMap.Values)
            {
                if (button == null) continue;
                button.UnregisterCallback<ClickEvent>(OnComponentButtonClick);
                button.userData = null;
            }
        }

        private void CreateButtons(Component[] components, VisualTreeAsset buttonTemplate)
        {
            Container.Add(CreateAllButton());
            foreach (var component in components)
            {
                var button = CreateComponentButton(component, buttonTemplate);
                _componentToButtonMap.Add(component, button);
                Container.Add(button);
            }
        }
        
        private Button CreateAllButton()
        {
            _allButton = new Button() { text = "All" };
            _allButton.AddToClassList(ComponentButtonClassName);
            _allButton.AddToClassList(PressedButtonClassName);
            _allButton.RegisterCallback<ClickEvent>(OnAllButtonClick);
            return _allButton;
        }
        
        private void OnAllButtonClick(ClickEvent evt)
        {
            _allSelected = true;
            _allButton.AddToClassList(PressedButtonClassName);
            _selectedComponentsIds.Clear();
            
            foreach (var visualElement in _parentContainer.Children())
            {
                visualElement.style.display = DisplayStyle.Flex;
            }
            
            foreach (var button in _componentToButtonMap.Values)
            {
                button.RemoveFromClassList(PressedButtonClassName);
            }
        }
        
        private Button CreateComponentButton(Component component, VisualTreeAsset buttonTemplate)
        {
            var componentType = component.GetType();

            var templateContainer = buttonTemplate.Instantiate();
            var componentButton = templateContainer.Q<Button>(ComponentButtonElementName);
            componentButton.userData = component;

            var label = componentButton.Q<Label>(LabelElementName);
            label.text = componentType.Name;

            var componentIcon = componentButton.Q<Image>(ImageElementName);
            var icon = InspectorComponents.GetComponentIcon(component);
            componentIcon.image = icon;
            
            componentButton.RegisterCallback<ClickEvent>(OnComponentButtonClick);
            return componentButton;
        }

        private void OnComponentButtonClick(ClickEvent evt)
        {
            if (evt.currentTarget is not Button button) return;
            if (button.userData is not Component component) return;
            
            var multiSelect = evt.modifiers.HasFlag(EventModifiers.Control) ||
                              evt.modifiers.HasFlag(EventModifiers.Command);
            
            if (multiSelect)
            {
                MultiSelect(component);
            }
            else
            {
                SingleSelect(component);
            }
            
            _allSelected = false;
            _allButton.RemoveFromClassList(PressedButtonClassName);
        }

        private void MultiSelect(Component component)
        {
            var id = component.GetInstanceID();
            var isSelected = _selectedComponentsIds.Contains(id);
            
            if (isSelected)
            {
                if (_selectedComponentsIds.Count <= 1)
                    return;
                        
                DeselectComponent(id, component);
            }
            else
            {
                if (_allSelected) // all button is selected
                {
                    foreach (var otherComponent in _componentToButtonMap.Keys)
                    {
                        DeselectComponent(otherComponent.GetInstanceID(), otherComponent);
                    }
                }
                
                SelectComponent(id, component);
            }
        }

        private void SingleSelect(Component selectedComponent)
        {
            var selectedId = selectedComponent.GetInstanceID();
            foreach (var component in _componentToButtonMap.Keys)
            {
                var id = component.GetInstanceID();
                if (id == selectedId)
                {
                    if (!_selectedComponentsIds.Contains(id))
                    {
                        SelectComponent(id, component);
                    }
                }
                else
                {
                    DeselectComponent(id, component);
                }
            }
        }
        
        private void SelectComponent(int id, Component component)
        {
            _selectedComponentsIds.Add(id);
            _componentToButtonMap[component].AddToClassList(PressedButtonClassName);
            _onComponentViewUpdate?.Invoke(component, DisplayStyle.Flex);
        }
        
        private void DeselectComponent(int id, Component component)
        {
            _selectedComponentsIds.Remove(id);
            _componentToButtonMap[component].RemoveFromClassList(PressedButtonClassName);
            _onComponentViewUpdate?.Invoke(component, DisplayStyle.None);
        }

        public void Dispose()
        {
            if (_disposed) return;

            CleanupButtons();
            
            Container?.Clear();
            Container?.RemoveFromHierarchy();
            _componentToButtonMap.Clear();
            _selectedComponentsIds.Clear();
            Container = null;
            _allButton = null;
            
            _disposed = true;
        }
    }
}