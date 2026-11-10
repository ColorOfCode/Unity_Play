using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ObscuredByClouds.Gameplay
{
    // Keep the existing renderer asset references, but replace the old fullscreen overlay
    // with a depth-input request for the real horizontal mesh shader.
    public sealed class FogOfWarRendererFeature : ScriptableRendererFeature
    {
        [HideInInspector] public Shader fogShader;
        DepthInputPass _pass;
        public override void Create() { _pass = new DepthInputPass(); }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var surface = FogOfWarSurface.Active;
            if (surface == null || !surface.isActiveAndEnabled) return;
            if (data.cameraData.cameraType == CameraType.Game ||
                (data.cameraData.cameraType == CameraType.SceneView && surface.showInSceneView)) renderer.EnqueuePass(_pass);
        }
        sealed class DepthInputPass : ScriptableRenderPass
        {
            public DepthInputPass()
            { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents; ConfigureInput(ScriptableRenderPassInput.Depth); }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data) { }
        }
    }
}
