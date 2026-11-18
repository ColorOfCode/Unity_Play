using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ObscuredByClouds.Gameplay;
using Snowfield;

// Editor only. Scene references are taken from the live template, not its prefab asset.
public sealed class ReplaceMMOAoter : EditorWindow
{
    GameObject source;
    GameObject target;
    bool takeOver = true;
    string result;

    [MenuItem("Tools/替换MMOAoter", false, 2000)]
    public static void Open()
    {
        var window = GetWindow<ReplaceMMOAoter>("替换MMOAoter");
        window.minSize = new Vector2(480, 385);
        window.FindSource();
        window.target = Selection.activeGameObject;
        window.Show();
    }

    void OnSelectionChange() { target = Selection.activeGameObject; result = null; Repaint(); }

    void FindSource()
    {
        var candidates = SceneObjects<ClickToMoveCharacter>()
            .Where(c => c.gameObject.name == "SK_Mannequin").ToArray();
        source = candidates.Length == 1 ? candidates[0].gameObject : null;
    }

    static IEnumerable<T> SceneObjects<T>() where T : Component
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<T>(true)) yield return item;
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("将 SK_Mannequin 的功能配置到选中角色", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("在 Hierarchy 选择角色根节点，然后点击下方按钮。\n选择 Project 中的 Prefab 时，会在模板所在场景创建实例，不修改 Prefab 资源。", MessageType.Info);
        source = (GameObject)EditorGUILayout.ObjectField("功能模板（场景角色）", source, typeof(GameObject), true);
        target = (GameObject)EditorGUILayout.ObjectField("当前目标角色", target, typeof(GameObject), true);
        takeOver = EditorGUILayout.ToggleLeft("切换相机跟随，并停用旧角色的移动、涉水和雪痕脚本", takeOver);
        EditorGUILayout.LabelField("迁移草交互到目标角色；自动重新绑定雪痕管理器和遮挡渐变目标。", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("目标场景没有启用的雪痕管理器 / 遮挡渐变组件时，关闭对应功能。", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("保留目标模型、材质、Avatar 和位置；骨骼引用重新绑定。", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !source || !target))
        {
            if (GUILayout.Button("给当前选中角色配置功能", GUILayout.Height(36)))
            {
                try
                {
                    target = Apply(source, target, takeOver);
                    Selection.activeGameObject = target;
                    result = "配置完成：" + target.name + "\n草交互：已迁移到角色\n雪痕：" +
                        (target.GetComponent<SnowFootEmitter>().enabled ? "已匹配管理器" : "已关闭（无启用的管理器）") +
                        "\n遮挡渐变：" + (target.GetComponent<CharacterOcclusionSwitch>().enableOcclusionFade ? "已开启并绑定" : "已关闭（无启用的场景组件）") +
                        "\n请保存场景；Ctrl+Z 可撤销本次配置。";
                    Debug.Log("[替换MMOAoter] " + result, target);
                }
                catch (Exception e) { result = "配置失败：" + e.Message; Debug.LogException(e); }
            }
        }
        if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
    }

    public static GameObject Apply(GameObject template, GameObject selected, bool takeOver)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出播放模式后配置。");
        if (!template || EditorUtility.IsPersistent(template) || !template.scene.IsValid() || PrefabStageUtility.GetPrefabStage(template) != null)
            throw new InvalidOperationException("模板必须是已加载普通场景中的角色。");
        if (!selected) throw new InvalidOperationException("请选择角色根节点。");
        if (selected == template || selected.transform.IsChildOf(template.transform) || template.transform.IsChildOf(selected.transform))
            throw new InvalidOperationException("目标不能是模板自身、其父级或子节点。");
        if (!EditorUtility.IsPersistent(selected) && (!selected.scene.IsValid() || PrefabStageUtility.GetPrefabStage(selected) != null))
            throw new InvalidOperationException("请在普通场景中选择角色，或在 Project 中选择 Prefab。");
        if (EditorUtility.IsPersistent(selected) && !PrefabUtility.IsPartOfPrefabAsset(selected))
            throw new InvalidOperationException("请选择角色 Prefab。");
        if (EditorUtility.IsPersistent(selected) && AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(selected)) != selected)
            throw new InvalidOperationException("请选择 Prefab 资源根节点。");

        var movement = template.GetComponent<ClickToMoveCharacter>();
        var wake = template.GetComponent<CharacterWaterWake>();
        var snow = template.GetComponent<SnowFootEmitter>();
        if (!movement || !wake || !movement.animator || !movement.animator.runtimeAnimatorController)
            throw new InvalidOperationException("模板缺少移动、涉水脚本或 Animator 控制器。");
        var animators = selected.GetComponentsInChildren<Animator>(true);
        if (animators.Length != 1) throw new InvalidOperationException("目标必须包含一个 Animator，请选择正确的角色根节点。");
        if (selected.GetComponentsInChildren<ClickToMoveCharacter>(true).Any(c => c.gameObject != selected))
            throw new InvalidOperationException("目标子节点已有移动脚本，请选择对应的控制根节点。");
        var bones = new[] { movement.boneFootL, movement.boneBallL, movement.bonePelvis, movement.boneSpine,
            snow && snow.leftFoot ? snow.leftFoot.name : "foot_l", snow && snow.rightFoot ? snow.rightFoot.name : "foot_r",
            snow && snow.leftToe ? snow.leftToe.name : "ball_l", snow && snow.rightToe ? snow.rightToe.name : "ball_r" };
        foreach (string bone in bones.Distinct()) RequireBone(selected, bone);
        // Generic clips animate transform paths. Validate bindings before changing anything.
        foreach (var clip in movement.animator.runtimeAnimatorController.animationClips.Distinct())
        {
            if (clip.humanMotion)
            {
                if (!animators[0].avatar || !animators[0].avatar.isValid || !animators[0].avatar.isHuman)
                    throw new InvalidOperationException("Humanoid 动画需要目标具有有效的人形 Avatar。");
                continue;
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.type == typeof(Transform) && !string.IsNullOrEmpty(binding.path) && !animators[0].transform.Find(binding.path))
                    throw new InvalidOperationException("目标骨骼层级不兼容动画：" + clip.name + " / " + binding.path);
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("替换MMOAoter");
        try
        {
            var destination = selected;
            if (EditorUtility.IsPersistent(selected))
            {
                destination = (GameObject)PrefabUtility.InstantiatePrefab(selected, template.scene);
                Undo.RegisterCreatedObjectUndo(destination, "创建角色实例");
                destination.transform.position = template.transform.position;
            }
            var animator = destination.GetComponentsInChildren<Animator>(true).Single();
            Undo.RecordObject(animator, "配置角色动画");
            animator.runtimeAnimatorController = movement.animator.runtimeAnimatorController;
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.updateMode = movement.animator.updateMode;
            animator.cullingMode = movement.animator.cullingMode;
            Changed(animator);

            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>
            {
                { template, destination }, { template.transform, destination.transform }, { movement.animator, animator }
            };
            foreach (var bone in template.GetComponentsInChildren<Transform>(true))
            {
                if (bone == template.transform) continue;
                string path = AnimationUtility.CalculateTransformPath(bone, template.transform);
                var mapped = destination.transform.Find(path);
                if (!mapped)
                {
                    var matches = destination.GetComponentsInChildren<Transform>(true).Where(t => t.name == bone.name).ToArray();
                    if (matches.Length == 1) mapped = matches[0];
                }
                if (mapped) { map[bone] = mapped; map[bone.gameObject] = mapped.gameObject; }
            }
            var originals = new MonoBehaviour[] { movement, wake, snow }.Where(c => c).ToArray();
            var copies = new List<MonoBehaviour>();
            foreach (var original in originals)
            {
                var copy = (MonoBehaviour)destination.GetComponent(original.GetType());
                if (!copy) copy = (MonoBehaviour)Undo.AddComponent(destination, original.GetType());
                Undo.RecordObject(copy, "复制角色功能参数");
                EditorUtility.CopySerialized(original, copy);
                copy.enabled = true;
                map[original] = copy;
                copies.Add(copy);
            }
            foreach (var copy in copies) { Remap(copy, map, template, true); Changed(copy); }
            var newMovement = destination.GetComponent<ClickToMoveCharacter>();
            newMovement.animator = animator;
            var newSnow = destination.GetComponent<SnowFootEmitter>();
            if (!newSnow) newSnow = Undo.AddComponent<SnowFootEmitter>(destination);
            newSnow.leftFoot = RequireBone(destination, bones[4]); newSnow.rightFoot = RequireBone(destination, bones[5]);
            newSnow.leftToe = RequireBone(destination, bones[6]); newSnow.rightToe = RequireBone(destination, bones[7]);
            Changed(newMovement); Changed(newSnow);

            ConfigureEnvironment(template, destination, newSnow);

            if (takeOver)
            {
                foreach (var camera in SceneObjects<Camera>())
                {
                    if (camera.gameObject.scene != destination.scene) continue;
                    foreach (var follow in camera.GetComponents<MonoBehaviour>())
                    {
                        if (!follow || (follow.GetType().Name != "CameraRotateQE" && follow.GetType().Name != "CameraFollow")) continue;
                        Undo.RecordObject(follow, "切换相机跟随");
                        Remap(follow, map, template, false);
                        // CameraRotateQE otherwise falls back to finding SK_Mannequin by name at runtime.
                        if (follow is CameraRotateQE orbit && !orbit.followTarget) orbit.followTarget = destination.transform;
                        Changed(follow);
                    }
                }
                foreach (var original in originals)
                {
                    Undo.RecordObject(original, "停用旧角色功能"); original.enabled = false; Changed(original);
                }
            }
            // Exploration is another scene system that follows the player.
            foreach (var fog in InScene<FogOfWarMap>(destination.scene))
                if (!fog.target || fog.target == template.transform)
                {
                    Undo.RecordObject(fog, "切换探索角色"); fog.target = destination.transform; Changed(fog);
                }
            EditorSceneManager.MarkSceneDirty(destination.scene);
            Undo.CollapseUndoOperations(group);
            return destination;
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    static T[] InScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }

    static void ConfigureEnvironment(GameObject template, GameObject destination, SnowFootEmitter emitter)
    {
        var scene = destination.scene;
        var grassComponents = InScene<TerrainGrassInteraction>(scene);
        var grassTemplate = grassComponents.FirstOrDefault(g => g.character == template.transform || g.gameObject == template);
        if (!grassTemplate) grassTemplate = grassComponents.FirstOrDefault(g => g.gameObject == destination);
        if (!grassTemplate) grassTemplate = grassComponents.FirstOrDefault();
        if (!grassTemplate) grassTemplate = template.GetComponent<TerrainGrassInteraction>();
        var grass = destination.GetComponent<TerrainGrassInteraction>();
        if (!grass) grass = Undo.AddComponent<TerrainGrassInteraction>(destination);
        Undo.RecordObject(grass, "迁移地形草交互");
        if (grassTemplate && grassTemplate != grass) EditorUtility.CopySerialized(grassTemplate, grass);
        grass.character = destination.transform;
        grass.enabled = true;
        // The grass shader has one global interaction field: leave exactly one writer in this scene.
        foreach (var old in grassComponents)
            if (old != grass) Undo.DestroyObjectImmediate(old);
        Changed(grass);

        var managers = InScene<SnowTrackSystem>(scene).Where(m => m.isActiveAndEnabled).ToArray();
        var manager = managers.FirstOrDefault(m => m.followTarget == destination.transform);
        if (!manager) manager = managers.FirstOrDefault(m => m.followTarget == template.transform);
        if (!manager && emitter.system && managers.Contains(emitter.system)) manager = emitter.system;
        if (!manager && managers.Length == 1) manager = managers[0];
        if (!manager && managers.Length > 1)
            throw new InvalidOperationException("目标场景有多个雪痕管理器且无法确定角色归属，请先指定管理器的 Follow Target。");
        Undo.RecordObject(emitter, "匹配雪痕系统");
        emitter.system = manager;
        emitter.snowSurface = manager ? manager.snowSurface : null;
        if (manager && !emitter.snowSurface)
        {
            var surfaces = InScene<SnowDepthSurface>(scene);
            if (surfaces.Length == 1) emitter.snowSurface = surfaces[0];
        }
        emitter.enabled = manager;
        emitter.emit = manager;
        if (manager)
        {
            Undo.RecordObject(manager, "切换雪痕跟随角色");
            manager.followTarget = destination.transform;
            Changed(manager);
        }
        Changed(emitter);

        var faders = InScene<CharacterOcclusionFader>(scene).Where(f => f.isActiveAndEnabled).ToArray();
        var control = destination.GetComponent<CharacterOcclusionSwitch>();
        if (!control) control = Undo.AddComponent<CharacterOcclusionSwitch>(destination);
        Undo.RecordObject(control, "配置遮挡渐变开关");
        control.enabled = true;
        control.enableOcclusionFade = faders.Length > 0;
        foreach (var fader in faders)
        {
            Undo.RecordObject(fader, "切换遮挡检测角色");
            fader.target = destination.transform;
            Changed(fader);
        }
        Changed(control);
    }

    static Transform RequireBone(GameObject root, string name)
    {
        var matches = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("目标骨骼缺失或重名：" + name + "。本工具要求兼容的骨骼和动画层级。");
        return matches[0];
    }

    static void Remap(UnityEngine.Object obj, Dictionary<UnityEngine.Object, UnityEngine.Object> map, GameObject template, bool strict)
    {
        var serialized = new SerializedObject(obj);
        var p = serialized.GetIterator();
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference || p.propertyPath == "m_Script") continue;
            var value = p.objectReferenceValue;
            if (!value) continue;
            if (map.TryGetValue(value, out var replacement)) p.objectReferenceValue = replacement;
            else if (strict)
            {
                Transform transform = value is Component c ? c.transform : value is GameObject g ? g.transform : null;
                if (transform && transform.IsChildOf(template.transform))
                    throw new InvalidOperationException("无法重新绑定角色内部引用：" + p.propertyPath);
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Changed(UnityEngine.Object obj)
    {
        EditorUtility.SetDirty(obj);
        if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        var component = obj as Component;
        if (component && component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }
}
