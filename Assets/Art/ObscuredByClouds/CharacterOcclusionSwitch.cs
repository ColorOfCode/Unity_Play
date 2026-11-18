using UnityEngine;

namespace ObscuredByClouds.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ObscuredByClouds/角色遮挡渐变开关")]
    public sealed class CharacterOcclusionSwitch : MonoBehaviour
    {
        [InspectorName("遮挡渐变")]
        [Tooltip("控制相机上的角色遮挡渐变，包括 Terrain 树。场景没有启用的遮挡渐变组件时自动关闭。")]
        public bool enableOcclusionFade = true;

        public bool CanFade => isActiveAndEnabled && enableOcclusionFade;

        // All scene OnEnable calls have completed before Start. Checking in Awake can
        // observe the camera behaviour before it becomes active and clear a saved true flag.
        void Start()
        {
            if (!HasSceneFader()) enableOcclusionFade = false;
        }

        public bool HasSceneFader()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var fader in root.GetComponentsInChildren<CharacterOcclusionFader>(true))
                    if (fader.enabled && fader.gameObject.activeInHierarchy) return true;
            return false;
        }
    }
}
