using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NKStudio
{
    public sealed class LayerFilterRendererFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// 렌더 객체 렌더러 기능에 사용되는 설정 클래스입니다.
        /// </summary>
        [System.Serializable]
        public class RenderObjectsSettings
        {
            /// <summary>
            /// Controls when the render pass executes.
            /// </summary>
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;

            public List<string> ShaderTagList = new() { "SpriteRenderPrepass" };

            public LayerMask LayerMask = 0;

            public Shader TestShader;

            [Header("Blur Settings")] [Range(1, 5)]
            public int BlurIteration = 3;

            [Range(0.1f, 3.0f)] public float BlurOffset = 1.0f;
        }

        public RenderObjectsSettings Settings = new();

        private LayerFilterRendererPass _layerFilterRendererPass;

        private Material _blurMaterial;

        public override void Create()
        {
            if (Settings.Event < RenderPassEvent.BeforeRenderingPrePasses)
                Settings.Event = RenderPassEvent.BeforeRenderingPrePasses;

            if (!Settings.TestShader)
                return;
            
            _blurMaterial = CoreUtils.CreateEngineMaterial(Settings.TestShader);
            _layerFilterRendererPass = new LayerFilterRendererPass(Settings.LayerMask,
                Settings.ShaderTagList, Settings.Event, _blurMaterial);
            _layerFilterRendererPass.Setup(Settings.BlurIteration, Settings.BlurOffset);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Settings.TestShader)
                return;
            
            if (renderingData.cameraData.cameraType == CameraType.Preview
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;
            
            renderer.EnqueuePass(_layerFilterRendererPass);
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