// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.Profiling.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Profiling.ModuleEditor
{
    class ModuleEditorWindow : EditorWindow
    {
        const string k_UxmlResourceName = "ModuleEditorWindow.uxml";
        const string k_UssSelectorModuleEditorWindowDark = "module-editor-window__dark";
        const string k_UssSelectorModuleEditorWindowLight = "module-editor-window__light";
        const int k_InvalidIndex = -1;
        const string k_NewProfilerModuleDefaultName = "New Profiler Module {0}";
        static readonly Vector2 k_MinimumWindowSize = new Vector2(720f, 405f);
        const int kMaxModuleIndex = 256;

        bool m_IsInitialized;
        [SerializeField] List<ModuleData> m_Modules;
        [SerializeField] int m_SelectedIndex;
        [SerializeField] List<ModuleData> m_CreatedModules = new List<ModuleData>();
        [SerializeField] List<ModuleData> m_DeletedModules = new List<ModuleData>();
        bool m_ChangesHaveBeenConfirmed;
        [SerializeField] int m_LastModuleNameIndex = 1;
        bool m_isConnectedToEditor;

        ModuleListViewController m_ModuleListViewController;
        ModuleDetailsViewController m_ModuleDetailsViewController;

        internal List<ModuleData> Modules => m_Modules;

        public event Action<ReadOnlyCollection<ModuleData>, ReadOnlyCollection<ModuleData>> onChangesConfirmed;

        public static ModuleEditorWindow Present(List<ProfilerModule> modules, bool isConnectedToEditor)
        {
            var window = GetWindowDontShow<ModuleEditorWindow>();
            window.Initialize(modules, isConnectedToEditor);
            window.ShowUtility();
            return window;
        }

        public static bool TryGetOpenInstance(out ModuleEditorWindow moduleEditorWindow)
        {
            moduleEditorWindow = null;
            if (HasOpenInstances<ModuleEditorWindow>())
            {
                // We must use GetWindowDontShow. GetWindow currently has a bug whereby it will create a second instance for utility windows.
                moduleEditorWindow = GetWindowDontShow<ModuleEditorWindow>();
            }

            return (moduleEditorWindow != null);
        }

        void Initialize(List<ProfilerModule> modules, bool isConnectedToEditor)
        {
            if (m_IsInitialized)
            {
                return;
            }

            minSize = k_MinimumWindowSize;
            titleContent = new GUIContent(LocalizationDatabase.GetLocalizedString("Profiler Module Editor"));
            m_Modules = ModuleData.CreateDataRepresentationOfProfilerModules(modules);
            m_SelectedIndex = IndexOfFirstEditableModule();
            m_IsInitialized = true;
            m_isConnectedToEditor = isConnectedToEditor;

            BuildWindow();
        }

        void OnEnable()
        {
            // OnEnable is invoked on the EditorWindow when it is created for the first time, before we get a chance to call Initialize(). Return early in this case and build the window in Initialize().
            if (m_IsInitialized == false)
            {
                return;
            }

            BuildWindow();
        }

        void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.Repaint)
            {
                hasUnsavedChanges = HasUnsavedChanges();
            }
        }

        public override void SaveChanges()
        {
            base.SaveChanges();
            ConfirmChanges(false);
        }

        void BuildWindow()
        {
            var template = EditorGUIUtility.Load(k_UxmlResourceName) as VisualTreeAsset;
            template.CloneTree(rootVisualElement);

            var themeUssClass = (EditorGUIUtility.isProSkin) ? k_UssSelectorModuleEditorWindowDark : k_UssSelectorModuleEditorWindowLight;
            rootVisualElement.AddToClassList(themeUssClass);

            m_ModuleListViewController = new ModuleListViewController(m_Modules);
            m_ModuleListViewController.ConfigureView(rootVisualElement);
            m_ModuleListViewController.onCreateModule += CreateModule;
            m_ModuleListViewController.onModuleAtIndexSelected += OnModuleAtIndexSelected;

            m_ModuleDetailsViewController = new ModuleDetailsViewController(m_isConnectedToEditor);
            m_ModuleDetailsViewController.ConfigureView(rootVisualElement);
            m_ModuleDetailsViewController.onDeleteModule += DeleteModule;
            m_ModuleDetailsViewController.onConfirmChanges += ConfirmChanges;
            m_ModuleDetailsViewController.onModuleNameChanged += OnModuleNameChanged;

            saveChangesMessage = LocalizationDatabase.GetLocalizedString("Do you want to save the changes you made before closing?");

            if (m_SelectedIndex != k_InvalidIndex)
            {
                m_ModuleListViewController.SelectModuleAtIndex(m_SelectedIndex);
            }
        }

        void OnModuleAtIndexSelected(ModuleData module, int index)
        {
            m_SelectedIndex = index;
            if (index != k_InvalidIndex)
                m_ModuleDetailsViewController.SetModule(module);
            else
                m_ModuleDetailsViewController.SetNoModuleSelected();
        }

        void CreateModule()
        {
            // Find first free module name
            var namesList = m_Modules.Select(x => x.name).Distinct();
            var moduleName = String.Format(k_NewProfilerModuleDefaultName, "-");
            for (; m_LastModuleNameIndex < kMaxModuleIndex; m_LastModuleNameIndex++)
            {
                var newModuleName = String.Format(k_NewProfilerModuleDefaultName, m_LastModuleNameIndex);
                if (namesList.Contains(newModuleName))
                    continue;

                moduleName = newModuleName;
                break;
            }

            var identifier = moduleName; // Dynamic modules use their name as identifier.
            var module = new ModuleData(identifier, moduleName, true, true);
            m_Modules.Add(module);
            m_CreatedModules.Add(module);

            m_ModuleListViewController.Refresh();

            var lastIndex = m_Modules.Count - 1;
            m_ModuleListViewController.SelectModuleAtIndex(lastIndex);
        }

        void DeleteModule(ModuleData module)
        {
            m_Modules.Remove(module);

            var indexInCreatedModules = IndexOfModuleInCollection(module, m_CreatedModules);
            if (indexInCreatedModules != k_InvalidIndex)
            {
                m_CreatedModules.RemoveAt(indexInCreatedModules);
            }
            else
            {
                m_DeletedModules.Add(module);
            }

            m_ModuleListViewController.Refresh();

            var firstEditableModuleIndex = IndexOfFirstEditableModule();
            if (firstEditableModuleIndex != k_InvalidIndex)
                m_ModuleListViewController.SelectModuleAtIndex(firstEditableModuleIndex);
            else
                m_ModuleListViewController.ClearSelection();
        }

        void ConfirmChanges()
        {
            ConfirmChanges(true);
        }

        void ConfirmChanges(bool closeWindow)
        {
            if (ValidateChanges(out string localizedErrorDescription))
            {
                onChangesConfirmed?.Invoke(m_Modules.AsReadOnly(), m_DeletedModules.AsReadOnly());
                m_ChangesHaveBeenConfirmed = true;

                if (closeWindow)
                {
                    Close();
                }
            }
            else if (closeWindow)
            {
                var title = LocalizationDatabase.GetLocalizedString("Save Changes Failed");
                var message = localizedErrorDescription;
                var ok = LocalizationDatabase.GetLocalizedString("OK");
                EditorUtility.DisplayDialog(title, message, ok);
            }
            else
            {
                throw new InvalidOperationException(localizedErrorDescription);
            }
        }

        void OnModuleNameChanged()
        {
            m_ModuleListViewController.RefreshSelectedListItem();
        }

        int IndexOfFirstEditableModule()
        {
            int index = k_InvalidIndex;
            for (int i = 0; i < m_Modules.Count; i++)
            {
                var module = m_Modules[i];
                if (module.isEditable)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        int IndexOfModuleInCollection(ModuleData module, List<ModuleData> modules)
        {
            int index = k_InvalidIndex;
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                if (m.Equals(module))
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        bool HasUnsavedChanges()
        {
            if (m_ChangesHaveBeenConfirmed)
            {
                return false;
            }

            // Do we have any deleted modules?
            var hasDeletedModules = m_DeletedModules.Count > 0;
            if (hasDeletedModules)
            {
                return true;
            }

            // Are there any modules with changes?
            bool hasAnyModuleWithChanges = false;
            foreach (var module in m_Modules)
            {
                if (module.editedState != ModuleData.EditedState.NoChanges)
                {
                    hasAnyModuleWithChanges = true;
                    break;
                }
            }

            return hasAnyModuleWithChanges;
        }

        bool ValidateChanges(out string localizedErrorDescription)
        {
            localizedErrorDescription = null;

            var identifiers = new List<string>(m_Modules.Count);
            foreach (var module in m_Modules)
            {
                // Is there a duplicate identifier? Because dynamic modules use their user-typed name as their identifier, we are checking that none of the other user-created dynamic modules have the same name. Type-defined Profiler modules will never clash because they use their assembly-qualified type name as identifier (which the user cannot type in here due to character limits). This is why we then use the module's name in the error message below.
                var moduleIdentifier = module.identifier;
                if (!identifiers.Contains(moduleIdentifier))
                {
                    identifiers.Add(moduleIdentifier);
                }
                else
                {
                    localizedErrorDescription = LocalizationDatabase.GetLocalizedString($"There are two modules called '{module.name}'. Module names must be unique.");
                    break;
                }

                // Is the name valid?
                if (string.IsNullOrEmpty(module.name))
                {
                    localizedErrorDescription = LocalizationDatabase.GetLocalizedString($"All modules must have a name.");
                    break;
                }

                // Does the module have at least one counter?
                if (module.chartCounters.Count == 0)
                {
                    localizedErrorDescription = LocalizationDatabase.GetLocalizedString($"The module '{module.name}' has no counters. All modules must have at least one counter.");
                }
            }

            return (localizedErrorDescription == null);
        }
    }
}
