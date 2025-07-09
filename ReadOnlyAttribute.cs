using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SpookNet.MLAgents
{
    /// <summary>
    /// Makes a field read-only in the Unity Inspector
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute
    {
        
    }

#if UNITY_EDITOR
    /// <summary>
    /// Custom property drawer for ReadOnly attribute
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
} 