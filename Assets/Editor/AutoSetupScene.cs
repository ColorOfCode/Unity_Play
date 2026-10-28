using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Shuimian.Gameplay;

namespace Shuimian.Gameplay.EditorTools
{
    /// <summary>
    /// 自动装配：脚本编译完成 / 打开场景后，自动检查并补齐缺失的组件与引用。
    /// 目的：新增脚本后用户不用手动挂组件、不用点菜单，直接按 Play 验证效果。
    /// 可用菜单 Tools / 水面控制 / 自动装配：开 / 关 临时关闭。
    /// </summary>
    [InitializeOnLoad]
    public static class AutoSetupScene
    {
        const string PrefKey = "Shuimian.AutoSetup.Enabled";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            private set => EditorPrefs.SetBool(PrefKey, value);
        }

        static AutoSetupScene()
        {
            // 构造函数里不能改场景，延迟一帧执行
            EditorApplication.delayCall += TrySetup;
            EditorSceneManager.sceneOpened += (scene, mode) => TrySetup();
        }

        [MenuItem("Tools/水面控制/自动装配: 开", true)]
        static bool EnableValidate() => !Enabled;
        [MenuItem("Tools/水面控制/自动装配: 开")]
        static void Enable()
        {
            Enabled = true;
            Debug.Log("[自动装配] 已开启：编译后会自动补齐组件。");
            TrySetup();
        }

        [MenuItem("Tools/水面控制/自动装配: 关", true)]
        static bool DisableValidate() => Enabled;
        [MenuItem("Tools/水面控制/自动装配: 关")]
        static void Disable()
        {
            Enabled = false;
            Debug.Log("[自动装配] 已关闭：不会再自动添加组件。");
        }

        public static void TrySetup()
        {
            if (!Enabled) return;
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) return;

            // ---------- 找船 ----------
            Transform ship = null;
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in all)
            {
                if (t == null || t.gameObject.scene != scene) continue;
                if (t.gameObject.name == "Ship" || t.gameObject.name.StartsWith("Ship ("))
                {
                    ship = t;
                    break;
                }
            }
            if (ship == null) return; // 场景里没船就不动，避免误加

            bool changed = false;
            var log = new System.Text.StringBuilder("[自动装配] ");

            // ---------- 1. 船：ShipController ----------
            var ctrl = ship.GetComponent<ShipController>();
            if (ctrl == null)
            {
                ctrl = Undo.AddComponent<ShipController>(ship.gameObject);
                changed = true;
                log.Append("Ship ← ShipController; ");
            }

            // ---------- 2. 相机：CameraFollow + MouseLookController ----------
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) cam = camGo.GetComponent<Camera>();
            }

            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null)
                {
                    follow = Undo.AddComponent<CameraFollow>(cam.gameObject);
                    // 沿用当前机位，不擅自改动用户的构图
                    follow.offset = cam.transform.position - ctrl.Target.position;
                    changed = true;
                    log.Append("Main Camera ← CameraFollow; ");
                }
                if (follow.controller == null)
                {
                    Undo.RecordObject(follow, "自动装配");
                    follow.controller = ctrl;
                    changed = true;
                }
                if (follow.target == null)
                {
                    Undo.RecordObject(follow, "自动装配");
                    follow.target = ctrl.Target;
                    changed = true;
                }

                var look = cam.GetComponent<MouseLookController>();
                if (look == null)
                {
                    look = Undo.AddComponent<MouseLookController>(cam.gameObject);
                    changed = true;
                    log.Append("Main Camera ← MouseLookController; ");
                }
                if (look.yawTarget == null)
                {
                    Undo.RecordObject(look, "自动装配");
                    look.yawTarget = ctrl.Target;
                    changed = true;
                }
                if (look.cameraTarget == null)
                {
                    Undo.RecordObject(look, "自动装配");
                    look.cameraTarget = cam.transform;
                    changed = true;
                }

                // ---------- 3. 相机在船头一侧时，自动挪到船尾并朝向船 ----------
                Transform tp = ctrl.Target;
                Vector3 toCam = cam.transform.position - tp.position;
                toCam.y = 0f;
                // 用模型（带 MeshRenderer 的子物体）的朝向判断船头，而不是根物体朝向
                Vector3 shipFwd = ctrl.ForwardFlat;
                shipFwd.y = 0f;
                if (shipFwd.sqrMagnitude > 0.0001f && toCam.sqrMagnitude > 0.01f)
                {
                    shipFwd.Normalize();
                    if (Vector3.Dot(shipFwd, toCam.normalized) > 0.25f) // 相机位于船头方向
                    {
                        float dist = toCam.magnitude;
                        float height = cam.transform.position.y - tp.position.y;
                        Undo.RecordObject(cam.transform, "相机挪到船尾");
                        cam.transform.position = tp.position - shipFwd * dist + Vector3.up * height;
                        cam.transform.LookAt(tp.position + Vector3.up * 0.5f);
                        if (follow != null)
                        {
                            Undo.RecordObject(follow, "更新跟随偏移");
                            follow.offset = cam.transform.position - follow.ResolveFollowTarget().position;
                        }
                        changed = true;
                        log.Append("相机在船头侧→已挪到船尾并朝向船; ");
                    }
                }
            }
            else
            {
                // 场景没相机时只提示一次，不刷屏
                Debug.LogWarning("[自动装配] 场景里没有 MainCamera 标签的相机，跳过相机部分。");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log(log.Append("全部就绪，可直接按 Play 验证。").ToString());
            }
        }
    }
}
