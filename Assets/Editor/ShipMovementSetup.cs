using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Shuimian.Gameplay;

namespace Shuimian.Gameplay.EditorTools
{
    /// <summary>
    /// 一键装配：给场景里的 Ship 挂 WASD 平移控制，给 Main Camera 挂跟随
    /// 菜单：Tools / 水面控制 / 给场景 Ship 挂 WASD 控制 + 相机跟随
    /// </summary>
    public static class ShipMovementSetup
    {
        const string MenuPath = "Tools/水面控制/给场景 Ship 挂 WASD 控制 + 相机跟随";

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // ---------- 1. 找场景里的 Ship（支持 Prefab 实例） ----------
            Transform ship = null;
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in all)
            {
                if (t == null || t.gameObject.scene != scene) continue; // 排除 Prefab 资源本身
                if (t.gameObject.name == "Ship" || t.gameObject.name.StartsWith("Ship ("))
                {
                    ship = t;
                    break;
                }
            }

            if (ship == null)
            {
                EditorUtility.DisplayDialog("没找到 Ship",
                    "当前场景里没有名为 Ship 的物体。\n\n请先把 Assets/Art/Water_JiaoHu/Ship.prefab 拖进场景，再执行本菜单。",
                    "知道了");
                return;
            }

            // ---------- 2. 确定移动目标：优先子物体 Cube ----------
            Transform moveTarget = ship.Find("Cube");
            if (moveTarget == null)
            {
                var mr = ship.GetComponentInChildren<MeshRenderer>();
                moveTarget = mr != null ? mr.transform : ship;
            }

            // ---------- 3. 挂 ShipController ----------
            var ctrl = ship.GetComponent<ShipController>();
            if (ctrl == null) ctrl = Undo.AddComponent<ShipController>(ship.gameObject);
            Undo.RecordObject(ctrl, "设置移动目标");
            ctrl.moveTarget = moveTarget;

            // ---------- 4. 挂 CameraFollow 到主相机 ----------
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) cam = camGo.GetComponent<Camera>();
            }

            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null) follow = Undo.AddComponent<CameraFollow>(cam.gameObject);
                Undo.RecordObject(follow, "设置跟随目标");
                // 关键：target 与 controller 指向同一个移动目标，避免相机跟到不动的父物体
                follow.controller = ctrl;
                follow.target = ctrl.Target;
                follow.offset = cam.transform.position - ctrl.Target.position;
                follow.lookAtTarget = false;   // 朝向交给 MouseLookController

                // ---------- 4.5 挂鼠标视角控制 ----------
                var look = cam.GetComponent<MouseLookController>();
                if (look == null) look = Undo.AddComponent<MouseLookController>(cam.gameObject);
                Undo.RecordObject(look, "设置鼠标视角控制");
                look.yawTarget = ship;               // 鼠标左右 → 船旋转
                look.cameraTarget = cam.transform;   // 鼠标上下 → 相机俯仰
            }
            else
            {
                Debug.LogWarning("[ShipMovementSetup] 场景里没有 MainCamera 标签的相机，请手动给相机挂 CameraFollow 并指定 Target。");
            }

            // ---------- 5. 收尾 ----------
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = ship.gameObject;
            Debug.Log($"[ShipMovementSetup] 完成：Ship 已挂 ShipController（移动目标={ctrl.Target.name}），" +
                      $"Main Camera 已挂 CameraFollow（实际跟随={ctrl.Target.name}）。按 Play 后用 WASD 测试。", ship.gameObject);
        }

        [MenuItem("Tools/水面控制/只添加鼠标视角控制")]
        public static void AddMouseLookOnly()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("没找到相机", "场景里没有带 MainCamera 标签的相机。", "知道了");
                return;
            }

            var look = cam.GetComponent<MouseLookController>();
            if (look == null) look = Undo.AddComponent<MouseLookController>(cam.gameObject);

            var ctrl = Object.FindObjectOfType<ShipController>();
            Undo.RecordObject(look, "接线鼠标视角控制");
            look.cameraTarget = cam.transform;
            look.yawTarget = ctrl != null ? ctrl.Target : null;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = cam.gameObject;
            Debug.Log($"[ShipMovementSetup] 鼠标视角控制已挂到 {cam.gameObject.name}，" +
                      $"旋转目标={(look.yawTarget ? look.yawTarget.name : "null（请在 Inspector 手动指定）")}。按 Play 后鼠标左右转船、上下看。", cam.gameObject);
        }

        [MenuItem("Tools/水面控制/把相机摆到船正后方")]
        public static void PlaceCameraBehindShip()
        {
            var ctrl = Object.FindObjectOfType<ShipController>();
            if (ctrl == null || ctrl.Target == null)
            {
                EditorUtility.DisplayDialog("没找到船", "场景里没有 ShipController，或它的 Move Target 为空。", "知道了");
                return;
            }
            var cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("没找到相机", "场景里没有带 MainCamera 标签的相机。", "知道了");
                return;
            }

            var follow = cam.GetComponent<CameraFollow>();
            Transform t = ctrl.Target;

            // 距离/高度沿用当前 offset，只把方向改到船尾
            float dist = follow != null ? new Vector2(follow.offset.x, follow.offset.z).magnitude : 10f;
            float height = follow != null ? follow.offset.y : 3f;
            if (dist < 0.5f) dist = 10f;

            // 用模型朝向（而非根物体朝向）判断船头
            Vector3 fwd = ctrl.ForwardFlat;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            Undo.RecordObject(cam.transform, "相机摆到船后方");
            cam.transform.position = t.position - fwd * dist + Vector3.up * height;
            cam.transform.LookAt(t.position + Vector3.up * 0.5f); // 同时朝向船，避免看向别处

            if (follow != null)
            {
                Undo.RecordObject(follow, "更新跟随偏移");
                follow.offset = cam.transform.position - follow.ResolveFollowTarget().position;
                follow.autoOffsetOnStart = false; // 已手动摆好，运行时不再重算
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[ShipMovementSetup] 相机已摆到船后方：距离={dist:F1}m，高度={height:F1}m。", cam.gameObject);
        }

        [MenuItem("Tools/水面控制/检查相机跟随配置")]
        public static void CheckSetup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var ctrl = Object.FindObjectOfType<ShipController>();
            var follow = Object.FindObjectOfType<CameraFollow>();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[检查] 场景 = {scene.name}");
            sb.AppendLine(ctrl != null
                ? $"[检查] ShipController 在 {ctrl.gameObject.name} 上，移动目标 = {(ctrl.Target ? ctrl.Target.name : "null")}，启用={ctrl.enabled}"
                : "[检查] 场景里没有 ShipController");
            sb.AppendLine(follow != null
                ? $"[检查] CameraFollow 在 {follow.gameObject.name} 上，target={(follow.target ? follow.target.name : "null")}，" +
                  $"controller={(follow.controller ? follow.controller.name : "null")}，实际跟随={(follow.ResolveFollowTarget() ? follow.ResolveFollowTarget().name : "null")}，" +
                  $"offset={follow.offset}，smoothTime={follow.smoothTime}"
                : "[检查] 场景里没有 CameraFollow");

            var look = Object.FindObjectOfType<MouseLookController>();
            sb.AppendLine(look != null
                ? $"[检查] MouseLookController：旋转物体={(look.yawTarget ? look.yawTarget.name : "null")}，" +
                  $"俯仰物体={(look.cameraTarget ? look.cameraTarget.name : "null")}，" +
                  $"向上={look.pitchUpLimit}° 向下={look.pitchDownLimit}°，灵敏度 X={look.sensitivityX} Y={look.sensitivityY}"
                : "[检查] 场景里没有 MouseLookController");
            Debug.Log(sb.ToString());
        }
    }
}
