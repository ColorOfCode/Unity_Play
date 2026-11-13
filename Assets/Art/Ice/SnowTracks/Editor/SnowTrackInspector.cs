using UnityEditor;
using UnityEngine;
namespace Snowfield.EditorTools
{
    [CustomEditor(typeof(SnowTrackSystem))]
    public class SnowTrackInspector:Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("雪厚来自 Terrain 上的 SnowDepthSurface。草地为零，薄雪留浅痕，厚雪生成连续犁雪沟槽。雪痕保留 holdSeconds 秒，再经 fadeSeconds 秒回填。",MessageType.Info);
            DrawDefaultInspector();
            var s=(SnowTrackSystem)target;
            using(new EditorGUI.DisabledScope(Application.isPlaying))if(GUILayout.Button("一键检查并接入当前场景")){SnowSceneConnector.ConnectMenu();GUIUtility.ExitGUI();}
            if(s.snowSurface && s.followTarget)EditorGUILayout.LabelField("角色位置雪厚",(s.snowSurface.ThicknessAt(s.followTarget.position)*100).ToString("F1")+" cm");
            if(Application.isPlaying)
            {
                EditorGUILayout.LabelField("当前脚印",s.FootprintCount.ToString());
                if(GUILayout.Button("清空运行时雪痕"))s.ClearFootprints();
                for(int i=0;i<3;i++)EditorGUILayout.ObjectField("级联 "+i,s.GetCascadeTexture(i),typeof(RenderTexture),false);
            }
        }
    }
    [CustomEditor(typeof(SnowDepthSurface))]
    public class SnowDepthSurfaceInspector:Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("R 通道记录归一化雪厚；CPU 接触和 GPU 形变共用。重新绘制地形图层后会延迟约两秒重建。修改厚雪厚度或过渡区尺寸后，可用下方按钮重建。",MessageType.Info);
            DrawDefaultInspector();
            if(GUILayout.Button("根据地形图层与厚度参数重建"))SnowDepthTerrainTools.Bake((SnowDepthSurface)target);
        }
    }
}
