using UnityEngine;
using UnityEditorAlias = UnityEditor;

namespace Shuimian.Gameplay.Editor
{
    [UnityEditorAlias.CustomEditor(typeof(ShuimianWaterMesh))]
    public sealed class ShuimianWaterMeshEditor : UnityEditorAlias.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var water = (ShuimianWaterMesh)target;
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                using (new UnityEditorAlias.EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    if (property.name == nameof(ShuimianWaterMesh.MyMeshRenderer))
                        DrawMirrorRenderer(property, water);
                    else
                        UnityEditorAlias.EditorGUILayout.PropertyField(property, true);
                }

                if (property.name == nameof(ShuimianWaterMesh.MyForwardTransform))
                    UnityEditorAlias.EditorGUILayout.HelpBox("Renderer 留空自动使用生成水面；方向留空使用水平海面。", UnityEditorAlias.MessageType.None);
                else if (property.name == nameof(ShuimianWaterMesh.recursiveDarkeningCurve))
                    UnityEditorAlias.EditorGUILayout.HelpBox("递归曲线仅影响第二层以上；单平面没有第二层。", UnityEditorAlias.MessageType.None);
                else if (property.name == nameof(ShuimianWaterMesh.reflectionDistanceCurve))
                    UnityEditorAlias.EditorGUILayout.HelpBox("X：0 最近、1 最大距离；Y：Alpha，乘总强度与 MaxBlend。", UnityEditorAlias.MessageType.None);
            }
            serializedObject.ApplyModifiedProperties();
        }

        static void DrawMirrorRenderer(UnityEditorAlias.SerializedProperty property, ShuimianWaterMesh water)
        {
            // Keep this field's standard Header decoration when drawing its
            // automatic, non-serialized preview through an ObjectField.
            var field = typeof(ShuimianWaterMesh).GetField(nameof(ShuimianWaterMesh.MyMeshRenderer));
            var header = System.Attribute.GetCustomAttribute(field, typeof(HeaderAttribute)) as HeaderAttribute;
            if (header != null)
            {
                UnityEditorAlias.EditorGUILayout.Space(8f);
                UnityEditorAlias.EditorGUILayout.LabelField(header.header, UnityEditorAlias.EditorStyles.boldLabel);
            }

            var rect = UnityEditorAlias.EditorGUILayout.GetControlRect();
            var label = new GUIContent(property.displayName, property.tooltip);
            label = UnityEditorAlias.EditorGUI.BeginProperty(rect, label, property);
            var reference = property.objectReferenceValue as MeshRenderer;
            var displayed = reference != null ? reference : water.EffectiveMirrorRenderer;
            UnityEditorAlias.EditorGUI.BeginChangeCheck();
            bool allowSceneObjects = !UnityEditorAlias.EditorUtility.IsPersistent(water);
            var selected = UnityEditorAlias.EditorGUI.ObjectField(rect, label, displayed, typeof(MeshRenderer), allowSceneObjects) as MeshRenderer;
            if (UnityEditorAlias.EditorGUI.EndChangeCheck())
            {
                // The generated renderer has HideAndDontSave and must never be
                // persisted as a scene/prefab reference. Null means automatic.
                property.objectReferenceValue = selected == water.SurfaceRenderer ? null : selected;
            }
            UnityEditorAlias.EditorGUI.EndProperty();
        }
    }
}
