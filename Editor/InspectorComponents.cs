using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace InspectorComponents.Editor
{
    internal static class InspectorComponents
    {
        private const string EnabledMenuItem = "Tools/Inspector Components/Enabled";
        private const string EditorInspectorWindowType = "UnityEditor.InspectorWindow";
        private const string AllInspectorsField = "m_AllInspectors";
        
        private const string ButtonTemplateUxml = "UIToolkit Resources/ComponentButtonTemplate.uxml";
        private const string Styles = "UIToolkit Resources/InspectorComponentsStyles.uss";

        // https://discussions.unity.com/t/type-of-inspector/136285/2
        private static readonly Type InspectorWindow = typeof(UnityEditor.Editor).Assembly.GetType(EditorInspectorWindowType);
        private static readonly FieldInfo AllInspectorsFieldInfo = InspectorWindow.GetField(AllInspectorsField, BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly List<InspectorComponentsController> InspectorControllers = new List<InspectorComponentsController>();
        private static readonly Dictionary<InspectorComponentsController, bool> InspectorsLockState = new Dictionary<InspectorComponentsController, bool>();
        
        private static string _rootFolderPath;
        private static InspectorComponentResources _resources;
        
        private static string EditorPrefsKey => Application.productName + "_InspectorComponentsEnabled";
        
        private static readonly Dictionary<Type, Texture> TypeIconCache = new();
        private static Texture _defaultScriptIcon;                                                                                                        
        
        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EditorPrefsKey, false);
            set => EditorPrefs.SetBool(EditorPrefsKey, value);
        }

        internal static void Initialize()
        {
            CleanUp();
            
            if (!IsEnabled) return;

            _rootFolderPath = GetRootFolderPath();
            
            var buttonTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Path.Combine(_rootFolderPath, ButtonTemplateUxml));
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.Combine(_rootFolderPath, Styles));
            if (!buttonTemplate || !styleSheet)
            {
                Debug.LogError("Failed to load resources, disabling inspector components.");
                IsEnabled = false;
                return;
            }
            
            _resources ??= new InspectorComponentResources(buttonTemplate, styleSheet);
            SubscribeToEditorCallbacks();
        }

        private static void CleanUp()
        {
            try
            {
                UnsubscribeFromEditorCallbacks();
                foreach (var controller in InspectorControllers)
                {
                    try
                    { 
                        controller.Dispose();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                InspectorControllers.Clear();
                InspectorsLockState.Clear();
                TypeIconCache.Clear();
                _resources = null;
                _defaultScriptIcon = null;
            }
        }

        private static void SubscribeToEditorCallbacks()
        {
            EditorApplication.update += FirstSelection;
            EditorApplication.update += ReleaseDestroyedInspectors;
            EditorApplication.update += CheckLockedState;
            
            Selection.selectionChanged += UpdateInspectorWindows;
            Selection.selectionChanged += SelectionChanged;
            
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.quitting += CleanUp;
        }

        private static void UnsubscribeFromEditorCallbacks()
        {
            EditorApplication.update -= FirstSelection;
            EditorApplication.update -= ReleaseDestroyedInspectors;
            EditorApplication.update -= CheckLockedState;
            
            Selection.selectionChanged -= UpdateInspectorWindows;
            Selection.selectionChanged -= SelectionChanged;
            
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.quitting -= CleanUp;
        }
        
        // run first selection in case we already have a selected object
        private static void FirstSelection()
        {
            EditorApplication.update -= FirstSelection;
            if (!Selection.activeGameObject) return;
            
            UpdateInspectorWindows();
            SelectionChanged();
        }
        
        private static void ReleaseDestroyedInspectors()
        {
            for (var i = InspectorControllers.Count - 1; i >= 0; i--)
            {
                var controller = InspectorControllers[i];
                if (controller.InspectorEditorWindow) continue;
                
                controller.Dispose();
                InspectorControllers.RemoveAt(i);
                InspectorsLockState.Remove(controller);
            }
        }

        private static void CheckLockedState()
        {
            foreach (var controller in InspectorControllers)
            {
                if (!controller.InspectorEditorWindow) continue;
                if (InspectorsLockState.TryGetValue(controller, out var previousLockedState))
                {
                    var isInspectorLocked =  controller.IsLocked;
                    InspectorsLockState[controller] = isInspectorLocked;
                    
                    var wasUnlocked = !isInspectorLocked && previousLockedState;
                    if (wasUnlocked)
                    {
                        controller.UpdateCurrentTarget(Selection.activeGameObject);
                        controller.InjectButtonsContainer();
                    }
                }
            }
        }

        private static void UpdateInspectorWindows()
        {
            var inspectors = (IList) AllInspectorsFieldInfo.GetValue(InspectorWindow);
            if (inspectors is not { Count: > 0 })
            {
                foreach (var controller in InspectorControllers)
                {
                    controller.Dispose();
                }
                
                InspectorControllers.Clear();
                InspectorsLockState.Clear();
                return;
            }
            
            foreach (var inspector in inspectors)
            {
                // this should work because of inheritance InspectorWindow -> PropertyEditor -> EditorWindow (until Unity changes...)
                var inspectorEditorWindow = inspector as EditorWindow; 
                if (!inspectorEditorWindow) continue;
                
                var inspectorPresent = false;
                foreach (var controller in InspectorControllers)
                {
                    if (controller.InspectorEditorWindow.GetInstanceID() == inspectorEditorWindow.GetInstanceID())
                    {
                        inspectorPresent = true;
                        break;
                    }
                }

                if (!inspectorPresent)
                {
                    var controller = new InspectorComponentsController(inspectorEditorWindow, Selection.activeGameObject, _resources);
                    InspectorControllers.Add(controller);
                    InspectorsLockState.Add(controller, controller.IsLocked);
                }
            }

            ReleaseDestroyedInspectors();
        }
        
        private static void SelectionChanged()
        {
            foreach (var controller in InspectorControllers)
            {
                if (!controller.IsLocked)
                {
                    controller.UpdateCurrentTarget(Selection.activeGameObject);
                }
                controller.InjectButtonsContainer();
            }
        }

        // used when adding or removing components
        private static void OnHierarchyChanged()
        {
            SelectionChanged();
        }
        
        internal static Texture GetComponentIcon(Component component)
        {                                                                                                                                                 
            var type = component.GetType();
            if (TypeIconCache.TryGetValue(type, out var icon))
            {
                return icon;
            }

            _defaultScriptIcon ??= EditorGUIUtility.ObjectContent(null, typeof(MonoBehaviour)).image;                                                     
   
            icon = EditorGUIUtility.GetIconForObject(component);
            if (!icon)
            {
                icon = EditorGUIUtility.ObjectContent(null, type).image;
            }

            if (icon == _defaultScriptIcon)
            {
                return _defaultScriptIcon;
            }
                  
            TypeIconCache[type] = icon;
            return icon;
        }
        
        private static string GetRootFolderPath()
        {
            var guids = AssetDatabase.FindAssets($"{nameof(InspectorComponents)} t:monoScript");

            if (guids.Length == 0)
            {
                throw new Exception("Could not find InspectorComponents script.");
            }
            
            var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return Path.GetDirectoryName(scriptPath);
        }
        
        [MenuItem(EnabledMenuItem)]
        public static void EnableInspectorComponentsList()
        {
            IsEnabled = !IsEnabled;
            Initialize();
        }
        
        [MenuItem(EnabledMenuItem, true)]
        public static bool ValidateEnable()
        {
            Menu.SetChecked(EnabledMenuItem, IsEnabled);
            return true;
        }
    }
}
