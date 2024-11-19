using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingPostProcessing;

            [Header("Blur Settings")] [Range(1, 5)]
            public int BlurIteration = 3;

            [Range(0.1f, 3.0f)] public float BlurOffset = 1.0f;

            [Tooltip("플레이 모드가 되지 않아도 블러가 연출될지 처리합니다.")]
            public bool AlwaysShow;
        }

        public RenderObjectsSettings Settings = new();

        private UIBlurPass _uiBlurPass;

        private Material _blurMaterial;

        public override void Create()
        {
            // 피쳐의 이름을 지정합니다. (Option)
            name = "UI Blur Feature";
            
            // 렌더 패스 이벤트가 BeforeRenderingPrePasses보다 작으면 BeforeRenderingPrePasses로 설정합니다.
            if (Settings.Event < RenderPassEvent.BeforeRenderingPrePasses)
                Settings.Event = RenderPassEvent.BeforeRenderingPrePasses;
            
            // 블러 머티리얼을 생성합니다.
            _blurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/UI/ScreenBlurRT"));
            
            // 블러 패스를 생성합니다.
            _uiBlurPass = new UIBlurPass(Settings.Event, _blurMaterial);
            _uiBlurPass.Setup(Settings.BlurIteration, Settings.BlurOffset , Settings.AlwaysShow);
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