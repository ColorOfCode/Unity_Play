using UnityEditor;
using UnityEngine;
using ObscuredByClouds.Gameplay;

[CustomEditor(typeof(OcclusionFadeTarget)),CanEditMultipleObjects]
public sealed class OcclusionFadeTargetEditor : Editor
{
    void OnEnable()
    {
        EditorApplication.delayCall+=RefreshOnInspect;
    }
    void RefreshOnInspect()
    {
        if(!this||EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(var item in targets)
        {
            var configuration=item as OcclusionFadeTarget;if(!configuration)continue;
            string before=EditorJsonUtility.ToJson(configuration);
            configuration.RefreshMaterialBindings();
            if(before!=EditorJsonUtility.ToJson(configuration))EditorUtility.SetDirty(configuration);
        }
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using(new EditorGUI.DisabledScope(true))EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("allowFade"));
        EditorGUILayout.HelpBox("场景摆放与 Terrain 绘制共用此开关。没有本组件、取消勾选或停用本组件的模型，不参与遮挡渐变。触发阈值和 20% 不透明度仍由摄像机统一控制。",MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("renderers"),true);
        bool refresh;
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))refresh=GUILayout.Button("扫描全部 LOD 材质 / 自动填充渐变 Shader");
        if(refresh)
        {
            serializedObject.ApplyModifiedProperties();
            foreach(var item in targets){var c=(OcclusionFadeTarget)item;Undo.RecordObject(c,"Refresh occlusion material bindings");c.RefreshMaterialBindings();EditorUtility.SetDirty(c);}
            serializedObject.Update();
        }
        if(targets.Length>1)
        {EditorGUILayout.HelpBox("逐个选择 Prefab 可查看其材质对应关系。",MessageType.None);serializedObject.ApplyModifiedProperties();return;}
        var config=(OcclusionFadeTarget)target;
        var bindings=serializedObject.FindProperty("materialBindings");
        EditorGUILayout.Space();EditorGUILayout.LabelField("渐变材质对应关系（包含全部 LOD）",EditorStyles.boldLabel);
        if(bindings.arraySize==0)EditorGUILayout.HelpBox("点击上方按钮扫描材质。",MessageType.Warning);
        for(int i=0;i<bindings.arraySize;i++)
        {
            var binding=bindings.GetArrayElementAtIndex(i);var source=binding.FindPropertyRelative("source");
            var template=binding.FindPropertyRelative("transitionMaterial");var shader=binding.FindPropertyRelative("transitionShader");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using(new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(source,new GUIContent("原材质"));
                var original=source.objectReferenceValue as Material;EditorGUILayout.ObjectField("原 Shader",original?original.shader:null,typeof(Shader),false);
            }
            EditorGUILayout.PropertyField(template,new GUIContent("替换材质（可选）"));
            using(new EditorGUI.DisabledScope(template.objectReferenceValue!=null))EditorGUILayout.PropertyField(shader,new GUIContent("渐变 Shader"));
            var sourceMaterial=source.objectReferenceValue as Material;
            var material=template.objectReferenceValue as Material;
            var resolved=material?material.shader:shader.objectReferenceValue as Shader;
            if(!resolved&&sourceMaterial)resolved=sourceMaterial.shader;
            if(!CharacterOcclusionFader.SupportsFadeShader(resolved))
                EditorGUILayout.HelpBox("此 Shader 尚不支持渐变。请选择支持透明状态及 Alpha 控制的替换材质或 Shader。",MessageType.Warning);
            else EditorGUILayout.LabelField(material?"运行时克隆此替换材质":"运行时克隆原材质，保留同名贴图和参数",EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.HelpBox("替换材质优先于渐变 Shader。项目现有树、雪树和岩石默认复用原兼容 Shader 的透明状态，可保留雪地 Layer 和昼夜光照。",MessageType.None);
        serializedObject.ApplyModifiedProperties();
    }
}
