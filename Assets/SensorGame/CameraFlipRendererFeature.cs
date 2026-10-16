using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace WhackAMole.Sensors
{
    // Flip the composed camera image (video + AR), before screen-space overlay UI.
    public sealed class CameraFlipRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader flipShader;
        private Material material;
        private FlipPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (flipShader != null) material = CoreUtils.CreateEngineMaterial(flipShader);
            pass = new FlipPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var settings = CameraDisplaySettings.Instance;
            var camera = renderingData.cameraData.camera;
            if (material == null || settings == null || !settings.IsFlipped ||
                camera.cameraType != CameraType.Game || camera != Camera.main) return;
            material.SetVector("_FlipAxes", new Vector4(settings.FlipHorizontal ? 1 : 0, settings.FlipVertical ? 1 : 0, 0, 0));
            pass.Material = material;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); }

        private sealed class FlipPass : ScriptableRenderPass
        {
            public Material Material;
            public FlipPass() { requiresIntermediateTexture = true; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "Camera orientation";
                descriptor.clearBuffer = false;
                var destination = renderGraph.CreateTexture(descriptor);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, Material, 0),
                    passName: "Camera horizontal / vertical flip");
                resources.cameraColor = destination;
            }
        }
    }
}
