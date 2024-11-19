using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NKStudio
{
    public sealed class WorldUIBlurFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// 렌더 객체 렌더러 기능에 사용되는 설정 클래스입니다.
        /// </summary>
        [System.Serializable]
        public class RenderObjectsSettings
        {
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingPostProcessing;

            public LayerMask LayerMask = 0;

            public List<string> FilterShaderTag = new() { "WorldUIPrePass" };
            public List<string> DrawShaderTag = new() { "WorldUIDrawPass" };

            [Header("Blur Settings")] [Range(1, 5)]
            public int BlurIteration = 3;

            [Range(0.1f, 3.0f)] public float BlurOffset = 1.0f;
        }

        public RenderObjectsSettings Settings = new();

        private WorldUIBlurPass _worldUIBlurPass;

        private Material _blurMaterial;

        public override void Create()
        {
            // 피쳐의 이름을 지정합니다. (Option)
            name = "Blur UI Feature";

            // 렌더 패스 이벤트가 BeforeRenderingPrePasses보다 작으면 BeforeRenderingPrePasses로 설정합니다.
            if (Settings.Event < RenderPassEvent.BeforeRenderingPrePasses)
                Settings.Event = RenderPassEvent.BeforeRenderingPrePasses;

            // 블러 머티리얼을 생성합니다.
            _blurMaterial =
                CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/UI/ScreenBlurRT"));

            // 블러 패스를 생성합니다.
            _worldUIBlurPass = new WorldUIBlurPass(Settings.LayerMask, Settings.FilterShaderTag, Settings.DrawShaderTag,
                Settings.Event, _blurMaterial);
            _worldUIBlurPass.Setup(Settings.BlurIteration, Settings.BlurOffset);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Reflection
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            renderer.EnqueuePass(_worldUIBlurPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CoreUtils.Destroy(_blurMaterial);
                _blurMaterial = null;
            }
        }
    }
}