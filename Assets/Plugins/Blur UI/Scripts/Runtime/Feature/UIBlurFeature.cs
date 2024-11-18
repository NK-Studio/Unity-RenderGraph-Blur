using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace NKStudio
{
    public sealed class UIBlurFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// 렌더 객체 렌더러 기능에 사용되는 설정 클래스입니다.
        /// </summary>
        [System.Serializable]
        public class RenderObjectsSettings
        {
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;

            [Header("Blur Settings")] [Range(1, 5)]
            public int BlurIteration = 3;

            [Range(0.1f, 3.0f)] public float BlurOffset = 1.0f;
        }

        public RenderObjectsSettings Settings = new();

        private UIBlurPass _uiBlurPass;

        private Material _blurMaterial;

        public override void Create()
        {
            if (Settings.Event < RenderPassEvent.BeforeRenderingPrePasses)
                Settings.Event = RenderPassEvent.BeforeRenderingPrePasses;
            
            _blurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/UI/ScreenBlurRT"));
            _uiBlurPass = new UIBlurPass(Settings.Event, _blurMaterial);
            _uiBlurPass.Setup(Settings.BlurIteration, Settings.BlurOffset);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;
            
            renderer.EnqueuePass(_uiBlurPass);
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