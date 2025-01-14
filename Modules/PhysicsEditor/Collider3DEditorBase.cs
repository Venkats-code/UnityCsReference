// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;

namespace UnityEditor
{
    internal class Collider3DEditorBase : ColliderEditorBase
    {
        protected SerializedProperty m_Material;
        protected SerializedProperty m_IsTrigger;

        protected static class BaseStyles
        {
            public static readonly GUIContent materialContent = EditorGUIUtility.TrTextContent("Material", "Reference to the Physics Material that determines how this Collider interacts with others.");
            public static readonly GUIContent triggerContent = EditorGUIUtility.TrTextContent("Is Trigger", "If enabled, this Collider is used for triggering events and is ignored by the physics engine.");
            public static readonly GUIContent centerContent = EditorGUIUtility.TrTextContent("Center", "The position of the Collider in the GameObject's local space.");
        }

        public override void OnEnable()
        {
            base.OnEnable();
            m_Material = serializedObject.FindProperty("m_Material");
            m_IsTrigger = serializedObject.FindProperty("m_IsTrigger");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_IsTrigger, BaseStyles.triggerContent);
            EditorGUILayout.PropertyField(m_Material, BaseStyles.materialContent);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
