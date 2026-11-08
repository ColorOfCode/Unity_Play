#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ObscuredByClouds.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObscuredByClouds.EditorTools
{
    /// <summary>
    /// 把 ClickToMoveCharacter 挂到场景里的 SK_Mannequin 上并接线：
    /// 自动找 Animator、从 Controller 里读真实状态名、绑定 FX_Tap、
    /// Avatar 为空时从 SK_Mannequin.FBX 补回来。
    /// 菜单入口保留，方便以后重跑。
    /// </summary>
    public static class ClickToMoveCharacterSetup
    {
        public const string FxPrefabPath = "Assets/Art/ObscuredByClouds/FX_Tap.prefab";
        public const string ModelPath = "Assets/Art/ObscuredByClouds/SK_Mannequin.FBX";
        const string CharacterNameHint = "Mannequin";

        [MenuItem("Tools/ObscuredByClouds/一键挂载：点击移动角色", false, 10)]
        public static void SetupMenu()
        {
            GameObject character = FindCharacter();
            if (character == null)
            {
                EditorUtility.DisplayDialog("没找到角色",
                    "请在 Hierarchy 里选中角色（带 Animator 的那个）再执行一次。", "好");
                return;
            }

            var report = new List<string>();
            Apply(character, report, true);

            report.Add("场景已标记脏，按 Ctrl+S 才会写盘。");
            Debug.Log("[ClickToMoveCharacter] 配置完成\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("配置完成", string.Join("\n", report), "好");
        }

        /// <summary>自动配置：返回 false 表示这次条件不满足（比如角色还没加载完），可以稍后重试</summary>
        public static bool TryAutoConfigure(out List<string> report, out GameObject character)
        {
            report = new List<string>();
            character = FindCharacter();
            if (character == null) return false;

            Apply(character, report, false);
            return true;
        }

        /// <summary>核心接线逻辑，菜单和自动配置共用</summary>
        public static void Apply(GameObject character, List<string> report, bool useUndo)
        {
            if (character == null) return;

            if (useUndo) Undo.RegisterFullObjectHierarchyUndo(character, "Setup ClickToMoveCharacter");

            ClickToMoveCharacter comp = character.GetComponent<ClickToMoveCharacter>();
            if (comp == null)
                comp = useUndo ? Undo.AddComponent<ClickToMoveCharacter>(character)
                               : character.AddComponent<ClickToMoveCharacter>();

            report.Add($"角色: {character.name}（{(comp == null ? "挂载失败" : "已挂 ClickToMoveCharacter")}）");
            if (comp == null) return;

            // ---- Animator ----
            if (comp.animator == null) comp.animator = character.GetComponent<Animator>();
            if (comp.animator == null) comp.animator = character.GetComponentInChildren<Animator>();

            // ---- Avatar 保持为空：这套动作是 Generic（逐骨骼 Transform 曲线），
            //      塞 Humanoid Avatar 反而会把播放搞坏，所以这里什么都不做 ----
            if (comp.animator != null && comp.animator.avatar != null)
                report.Add($"注意: Animator 上挂了 Avatar({comp.animator.avatar.name})，" +
                           "但这套 Generic 动作不需要它，如果动作异常就把它清空");

            // ---- 从 Controller 里读真实状态名，避免手填错 ----
            if (comp.animator != null &&
                comp.animator.runtimeAnimatorController is AnimatorController controller)
            {
                var names = new List<string>();
                CollectStateNames(controller.layers.Length > 0 ? controller.layers[0].stateMachine : null, names);
                report.Add("Controller 状态: " + string.Join(" / ", names));

                foreach (string n in names)
                {
                    string lower = n.ToLowerInvariant();
                    if (lower.Contains("idle")) comp.idleState = n;
                    else if (lower.Contains("run") || lower.Contains("walk")) comp.runState = n;
                }
                report.Add($"待机 = {comp.idleState}，跑动 = {comp.runState}");
            }

            // ---- 相机 ----
            if (comp.clickCamera == null)
            {
                comp.clickCamera = Camera.main;
                if (comp.clickCamera == null)
                {
                    Camera[] cams = Object.FindObjectsOfType<Camera>();
                    if (cams.Length > 0) comp.clickCamera = cams[0];
                }
                report.Add(comp.clickCamera != null
                    ? $"相机: {comp.clickCamera.name}"
                    : "警告: 没找到相机，鼠标射线发不出去");
            }

            // ---- 特效 ----
            if (comp.fxPrefab == null)
            {
                comp.fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxPrefabPath);
                report.Add(comp.fxPrefab != null ? "特效: FX_Tap" : $"警告: 没加载到 {FxPrefabPath}");
            }

            // ---- 根运动必须关，否则原地动作会把角色带飘 ----
            if (comp.animator != null && comp.animator.applyRootMotion)
            {
                if (useUndo) Undo.RecordObject(comp.animator, "Disable Root Motion");
                comp.animator.applyRootMotion = false;
                EditorUtility.SetDirty(comp.animator);
                report.Add("已关闭 Apply Root Motion（原地动作必须关）");
            }

            EditorUtility.SetDirty(comp);
        }

        [MenuItem("Tools/ObscuredByClouds/检查：点击移动角色", false, 11)]
        public static void Validate()
        {
            ClickToMoveCharacter comp = Object.FindObjectOfType<ClickToMoveCharacter>();
            if (comp == null)
            {
                EditorUtility.DisplayDialog("检查结果", "场景里没有挂 ClickToMoveCharacter。", "好");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"物体: {comp.gameObject.name}");
            sb.AppendLine($"Animator: {(comp.animator != null ? comp.animator.name : "缺失")}");
            sb.AppendLine($"Avatar: {(comp.animator != null && comp.animator.avatar != null ? comp.animator.avatar.name : "空（Generic 动作正常状态，不用管）")}");
            sb.AppendLine($"Controller: {(comp.animator != null && comp.animator.runtimeAnimatorController != null ? comp.animator.runtimeAnimatorController.name : "缺失")}");
            sb.AppendLine($"Apply Root Motion: {(comp.animator != null ? comp.animator.applyRootMotion.ToString() : "-")}（必须 False）");
            sb.AppendLine($"待机状态: {comp.idleState}");
            sb.AppendLine($"跑动状态: {comp.runState}");
            sb.AppendLine($"朝向: 出场基准用骨骼现量，微调 = {comp.facingTrimDeg:0}°");
            sb.AppendLine($"相机: {(comp.clickCamera != null ? comp.clickCamera.name : "缺失")}");
            sb.AppendLine($"特效: {(comp.fxPrefab != null ? comp.fxPrefab.name : "缺失")}");
            sb.AppendLine($"速度: {comp.moveSpeed} m/s");
            sb.AppendLine();
            sb.AppendLine("---- 动画状态机 ----");
            sb.AppendLine(DescribeAnimator(comp));

            Debug.Log("[ClickToMoveCharacter] 检查\n" + sb);
            EditorUtility.DisplayDialog("检查结果", sb.ToString(), "好");
        }

        // ---------------- 工具 ----------------

        /// <summary>把参数 / 默认状态 / 每条过渡的实际配置列出来，排查「过渡不生效」用</summary>
        static string DescribeAnimator(ClickToMoveCharacter comp)
        {
            if (comp.animator == null) return "没有 Animator";

            var ctrl = comp.animator.runtimeAnimatorController as AnimatorController;
            if (ctrl == null) return "Controller 不是 AnimatorController（可能是 Override Controller）";

            var sb = new System.Text.StringBuilder();

            sb.Append("参数: ");
            if (ctrl.parameters.Length == 0) sb.Append("（无）");
            else
            {
                for (int i = 0; i < ctrl.parameters.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ctrl.parameters[i].name).Append('(').Append(ctrl.parameters[i].type).Append(')');
                }
            }
            sb.AppendLine();

            bool hasParam = false;
            foreach (AnimatorControllerParameter p in ctrl.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == comp.runBoolParam) hasParam = true;
            sb.AppendLine($"驱动参数 {comp.runBoolParam}: " + (hasParam ? "存在" : "缺失（脚本会退回 CrossFade）"));

            AnimatorStateMachine sm = ctrl.layers.Length > 0 ? ctrl.layers[0].stateMachine : null;
            if (sm == null) return sb.Append("Base Layer 为空").ToString();

            sb.AppendLine("默认状态: " + (sm.defaultState != null ? sm.defaultState.name : "无"));
            sb.AppendLine($"AnyState 过渡: {sm.anyStateTransitions.Length} 条");

            foreach (ChildAnimatorState cs in sm.states)
            {
                if (cs.state == null) continue;
                sb.AppendLine($"状态「{cs.state.name}」过渡 {cs.state.transitions.Length} 条:");
                foreach (AnimatorStateTransition t in cs.state.transitions)
                {
                    string conds = "";
                    foreach (AnimatorCondition c in t.conditions)
                        conds += $"{c.parameter}={c.mode} ";
                    string dst = t.destinationState != null ? t.destinationState.name : "(?)";
                    sb.AppendLine($"    → {dst}  {t.duration:0.00}s  ExitTime={t.hasExitTime}  " +
                                  $"条件[{(conds.Length > 0 ? conds.Trim() : "无")}]");
                }
            }

            return sb.ToString();
        }

        public static GameObject FindCharacter()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                Animator a = selected.GetComponent<Animator>();
                if (a == null) a = selected.GetComponentInParent<Animator>();
                if (a == null) a = selected.GetComponentInChildren<Animator>();
                if (a != null) return a.gameObject;
            }

            foreach (Animator a in Object.FindObjectsOfType<Animator>())
            {
                if (a.name.IndexOf(CharacterNameHint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return a.gameObject;
            }

            foreach (Animator a in Object.FindObjectsOfType<Animator>())
            {
                var c = a.runtimeAnimatorController;
                if (c != null && c.name.IndexOf("DSS3", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return a.gameObject;
            }

            return null;
        }

        static void CollectStateNames(AnimatorStateMachine machine, List<string> outNames)
        {
            if (machine == null) return;

            foreach (ChildAnimatorState cs in machine.states)
                if (cs.state != null) outNames.Add(cs.state.name);

            foreach (ChildAnimatorStateMachine csm in machine.stateMachines)
                CollectStateNames(csm.stateMachine, outNames);
        }

        static Avatar FindAvatar(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar avatar) return avatar;
            return null;
        }
    }
    /// <summary>
    /// 零操作自动布置：脚本编译完之后，只要编辑器停在 ObscuredByClouds 场景，
    /// 就自动挂组件 + 接线 + 存盘，全程不需要点任何菜单。
    /// 每个工程只跑一次（记在 EditorPrefs），以后想重跑用菜单。
    /// </summary>
    [InitializeOnLoad]
    internal static class ClickToMoveAutoSetup
    {
        const string PrefsKey = "ObscuredByClouds.ClickToMove.AutoSetup.v1";
        const string TargetSceneName = "ObscuredByClouds";
        const double RetryWindowSeconds = 180.0;

        static double _nextTryTime;
        static double _deadline;

        static ClickToMoveAutoSetup()
        {
            if (EditorPrefs.GetBool(PrefsKey, false)) return;

            _deadline = EditorApplication.timeSinceStartup + RetryWindowSeconds;
            _nextTryTime = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextTryTime) return;
            _nextTryTime = EditorApplication.timeSinceStartup + 0.5;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isPlaying) return;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.name != TargetSceneName)
            {
                if (EditorApplication.timeSinceStartup > _deadline) Stop();
                return;
            }

            if (!ClickToMoveCharacterSetup.TryAutoConfigure(out List<string> report, out GameObject character))
            {
                if (EditorApplication.timeSinceStartup > _deadline) Stop();
                return;
            }

            // 挂好了 ---- 存盘 + 收工 ----
            EditorSceneManager.MarkSceneDirty(character.scene);
            string saveInfo = SaveSceneWithBackup(character.scene);

            EditorPrefs.SetBool(PrefsKey, true);
            Stop();

            Debug.Log("[ClickToMoveCharacter] 已自动完成布置，无需手动操作。\n" +
                      string.Join("\n", report) + "\n" + saveInfo);
            Selection.activeGameObject = character;
            EditorGUIUtility.PingObject(character);
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
        }

        static string SaveSceneWithBackup(Scene scene)
        {
            try
            {
                string assetPath = scene.path;
                if (string.IsNullOrEmpty(assetPath)) return "场景没有磁盘路径，未存盘（Ctrl+S 即可）";

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string backupDir = Path.Combine(projectRoot, ".workbuddy", "scene-backup");
                Directory.CreateDirectory(backupDir);

                string backupPath = Path.Combine(backupDir,
                    Path.GetFileName(assetPath) + "." + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");
                File.Copy(Path.GetFullPath(assetPath), backupPath, true);

                EditorSceneManager.SaveScene(scene);
                return $"场景已自动存盘（改前备份: {backupPath}）";
            }
            catch (System.Exception e)
            {
                return "自动存盘失败，按 Ctrl+S 手动保存即可：" + e.Message;
            }
        }
    }
}
#endif
