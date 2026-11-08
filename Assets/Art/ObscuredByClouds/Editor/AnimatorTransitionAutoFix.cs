#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ObscuredByClouds.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ObscuredByClouds.EditorTools
{
    /// <summary>
    /// 把 SK_Mannequin_DSS3.controller 修成「一套能用、且不会打架」的配置：
    ///
    ///   参数: IsRun (Bool)
    ///   Idle --[IsRun == true,  0.15s, 无 Exit Time]--> Run_F
    ///   Run_F --[IsRun == false, 0.15s, 无 Exit Time]--> Idle
    ///   默认状态: Idle
    ///
    /// 为什么要重建而不是沿用手里那版：
    /// 1) Animator 里新建的过渡默认勾选 Has Exit Time，切过去要等当前动作播到 75%，
    ///    Idle 长 1.83s，等于点完鼠标要等 1.3 秒才开始跑 —— 这就是「表现不对」的根源。
    /// 2) 脚本同时在用 CrossFade 硬切状态，和过渡线互相打断，动作会抖。
    ///    现在统一由 IsRun 参数驱动，过渡时长交给 Animator 自己跑，两边不再冲突。
    ///
    /// 全程自动执行一次；菜单入口可以随时重跑（幂等）。
    /// </summary>
    [InitializeOnLoad]
    internal static class AnimatorTransitionAutoFix
    {
        const string PrefsKey = "ObscuredByClouds.AnimatorFix.v2";
        const string ControllerPath = "Assets/Art/ObscuredByClouds/SK_Mannequin_DSS3.controller";
        const string ParamName = "IsRun";
        const float DefaultBlend = 0.15f;
        const double RetryWindowSeconds = 240.0;

        static double _nextTry;
        static double _deadline;

        static AnimatorTransitionAutoFix()
        {
            if (EditorPrefs.GetBool(PrefsKey, false)) return;

            _deadline = EditorApplication.timeSinceStartup + RetryWindowSeconds;
            _nextTry = EditorApplication.timeSinceStartup + 0.8;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < _nextTry) return;
            _nextTry = EditorApplication.timeSinceStartup + 0.5;

            if (EditorApplication.timeSinceStartup > _deadline)
            {
                EditorApplication.update -= Tick;
                return;
            }

            // 先写标记，再动资产：修改 controller 会触发重导入 + 域重载，
            // 标记写晚了会变成无限重入。
            EditorPrefs.SetBool(PrefsKey, true);
            EditorApplication.update -= Tick;

            string log;
            Fix(out log);
            if (!string.IsNullOrEmpty(log)) Debug.Log(log);
        }

        [MenuItem("Tools/ObscuredByClouds/修复：Idle→Run 过渡（重建标准配置）", false, 12)]
        public static void FixMenu()
        {
            string log;
            bool ok = Fix(out log);
            Debug.Log(log);
            EditorUtility.DisplayDialog(ok ? "过渡已重建" : "没修成", log, "好");
        }

        static bool Fix(out string log)
        {
            AnimatorController controller = ResolveController(out string source);
            if (controller == null)
            {
                log = "[AnimatorFix] 没找到 SK_Mannequin_DSS3.controller，跳过。";
                return false;
            }

            ClickToMoveCharacter comp = UnityEngine.Object.FindObjectOfType<ClickToMoveCharacter>();
            string idleName = comp != null && !string.IsNullOrEmpty(comp.idleState) ? comp.idleState : "Anim_DSS3_Idle";
            string runName = comp != null && !string.IsNullOrEmpty(comp.runState) ? comp.runState : "Anim_DSS3_Run_F";
            string param = comp != null && !string.IsNullOrEmpty(comp.runBoolParam) ? comp.runBoolParam : ParamName;
            float blend = comp != null ? Mathf.Max(DefaultBlend, comp.animBlendTime) : DefaultBlend;

            AnimatorStateMachine sm = controller.layers != null && controller.layers.Length > 0
                ? controller.layers[0].stateMachine
                : null;
            if (sm == null)
            {
                log = "[AnimatorFix] Controller 没有 Base Layer，跳过。";
                return false;
            }

            AnimatorState idle = FindState(sm, idleName, "idle");
            AnimatorState run = FindState(sm, runName, "run");
            if (idle == null || run == null)
            {
                var all = new List<AnimatorState>();
                CollectStates(sm, all);
                var names = new List<string>();
                foreach (AnimatorState s in all) names.Add(s.name);
                log = $"[AnimatorFix] 没找到状态（期望 idle={idleName}, run={runName}）。" +
                      $"控制器里现有: {(names.Count > 0 ? string.Join(" / ", names) : "（空）")}";
                return false;
            }

            var report = new List<string>();

            // ---- 1. 参数 ----
            bool hasParam = false;
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (!string.Equals(p.name, param, StringComparison.Ordinal)) continue;
                if (p.type == AnimatorControllerParameterType.Bool) hasParam = true;
                else
                {
                    controller.RemoveParameter(p);
                    report.Add($"参数 {param} 原来是 {p.type}，已改成 Bool");
                    break;
                }
            }
            if (!hasParam)
            {
                controller.AddParameter(param, AnimatorControllerParameterType.Bool);
                report.Add($"新增参数 {param} (Bool)");
            }

            // ---- 2. 清掉所有旧过渡（含 AnyState，避免残留规则抢跳）----
            int removed = 0;
            removed += ClearTransitions(idle);
            removed += ClearTransitions(run);
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
                sm.RemoveAnyStateTransition(t);
            if (removed > 0) report.Add($"清掉旧过渡 {removed} 条");

            // ---- 3. 重建两条过渡：不等 Exit Time，纯靠参数 ----
            AnimatorStateTransition toRun = idle.AddTransition(run);
            Configure(toRun, param, true, blend);

            AnimatorStateTransition toIdle = run.AddTransition(idle);
            Configure(toIdle, param, false, blend);

            report.Add($"Idle → Run：{param}=true，{blend:0.00}s，无 Exit Time");
            report.Add($"Run → Idle：{param}=false，{blend:0.00}s，无 Exit Time");

            // ---- 4. 进场默认待机 ----
            if (sm.defaultState != idle)
            {
                sm.defaultState = idle;
                report.Add("默认状态改成 Idle（原来进场会先演一段跑步）");
            }

            EditorUtility.SetDirty(idle);
            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(sm);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            log = "[AnimatorFix] 过渡已重建（" + source + "）\n  " + string.Join("\n  ", report);
            return true;
        }

        static void Configure(AnimatorStateTransition t, string param, bool runValue, float blend)
        {
            t.hasExitTime = false;          // 关键：不等当前动作播完
            t.exitTime = 1f;
            t.hasFixedDuration = true;
            t.duration = blend;
            t.offset = 0f;
            t.interruptionSource = TransitionInterruptionSource.None;
            t.orderedInterruption = true;
            t.canTransitionToSelf = false;
            t.mute = false;
            t.solo = false;
            t.AddCondition(runValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        static int ClearTransitions(AnimatorState state)
        {
            var copy = new List<AnimatorStateTransition>(state.transitions);
            foreach (AnimatorStateTransition t in copy) state.RemoveTransition(t);
            return copy.Count;
        }

        static AnimatorState FindState(AnimatorStateMachine sm, string exactName, string keyword)
        {
            var all = new List<AnimatorState>();
            CollectStates(sm, all);

            foreach (AnimatorState s in all)
                if (string.Equals(s.name, exactName, StringComparison.OrdinalIgnoreCase)) return s;

            foreach (AnimatorState s in all)
                if (s.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return s;

            return null;
        }

        static void CollectStates(AnimatorStateMachine sm, List<AnimatorState> outStates)
        {
            if (sm == null) return;
            foreach (ChildAnimatorState cs in sm.states)
                if (cs.state != null) outStates.Add(cs.state);
            foreach (ChildAnimatorStateMachine csm in sm.stateMachines)
                CollectStates(csm.stateMachine, outStates);
        }

        static AnimatorController ResolveController(out string source)
        {
            source = "";

            ClickToMoveCharacter comp = UnityEngine.Object.FindObjectOfType<ClickToMoveCharacter>();
            if (comp != null && comp.animator != null)
            {
                RuntimeAnimatorController rac = comp.animator.runtimeAnimatorController;
                if (rac is AnimatorController ac)
                {
                    source = "场景 " + comp.gameObject.name + " 上的 Controller";
                    return ac;
                }
                if (rac is AnimatorOverrideController aoc && aoc.runtimeAnimatorController is AnimatorController inner)
                {
                    source = "Override Controller 里的 " + inner.name;
                    return inner;
                }
            }

            AnimatorController byPath = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (byPath != null)
            {
                source = ControllerPath;
                return byPath;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).IndexOf("DSS3", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (c != null)
                {
                    source = path;
                    return c;
                }
            }

            return null;
        }
    }
}
#endif
