using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class SpriteRenderLayoutFilter : ScriptableRendererFeature
{
    private class SpriteLayerTestPass : ScriptableRenderPass
    {
        private readonly RenderQueueType _renderQueueType;
        private readonly LayerMask _layerMask;
        private readonly List<ShaderTagId> _shaderTagIdList = new();

        private const string k_textureName = "_LayerFilterCopypassBufferTex";

        public SpriteLayerTestPass(LayerMask layerMask, List<string> shaderTagIdList, RenderPassEvent injectionPoint)
        {
            _layerMask = layerMask;
            renderPassEvent = injectionPoint;

            _shaderTagIdList.Clear();
            foreach (string tag in shaderTagIdList)
                _shaderTagIdList.Add(new ShaderTagId(tag));

            requiresIntermediateTexture = true;
        }

        private class PassData
        {
            internal RendererListHandle RendererList;
            internal TextureHandle InputTexture;
        }

        private void InitRendererLists(ContextContainer frameData, ref PassData passData, RenderGraph renderGraph)
        {
            // 유니버설 렌더 파이프라인에서 관련 프레임 데이터에 액세스
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            // 렌더 큐 범위 설정
            RenderQueueRange renderQueueRange = RenderQueueRange.transparent;

            // 정렬 기준 설정
            SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;

            // 드로우 설정 생성
            DrawingSettings drawSettings =
                CreateDrawingSettings(_shaderTagIdList, renderingData, cameraData, lightData, sortingCriteria);

            // 필터링 설정 생성
            FilteringSettings filteringSettings = new FilteringSettings(renderQueueRange, _layerMask);

            // 그려야 할 오브젝트 목록 생성
            RendererListParams rendererListParams =
                new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);

            // 렌더 그래프 시스템에서 사용할 수 있는 목록 핸들로 변환
            passData.RendererList = renderGraph.CreateRendererList(rendererListParams);
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            // 렌더 대상을 검은색으로 지움
            context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black, 1, 0);

            // 목록의 오브젝트 그리기
            context.cmd.DrawRendererList(data.RendererList);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder =
                   renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                // UniversalResourceData에는 활성 색상 및 깊이 텍스처를 포함하여 렌더러에서 사용하는 모든 텍스처 핸들이 포함됩니다.
                // 활성 색상 및 깊이 텍스처는 카메라가 렌더링하는 기본 색상 및 깊이 버퍼입니다.
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                passData.InputTexture = resourceData.cameraColor;
                builder.UseTexture(passData.InputTexture);

                var cameraColorDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                cameraColorDesc.name = "_LayerFilter";
                cameraColorDesc.clearBuffer = false;

                var destination = renderGraph.CreateTexture(cameraColorDesc);
                builder.SetRenderAttachment(destination, 0);

                //  렌더러 목록 초기화
                InitRendererLists(frameData, ref passData, renderGraph);

                // 이 Pass에서 사용할 리소스로 선언하기
                builder.UseRendererList(passData.RendererList);

                // UseTextureFragment 및 UseTextureFragmentDepth를 통해 렌더 대상으로 설정합니다.
                // 이는 이전 cmd.SetRenderTarget(color,length)를 사용하는 것과 동일합니다.
                //builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                // 렌더링 함수 설정
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }
    }

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

        public List<string> ShaderTagList = new() { "Universal2D" };

        public LayerMask LayerMask = 0;
    }

    public RenderObjectsSettings Settings = new();

    private SpriteLayerTestPass _spriteLayerTestPass;

    public override void Create()
    {
        if (Settings.Event < RenderPassEvent.BeforeRenderingPrePasses)
            Settings.Event = RenderPassEvent.BeforeRenderingPrePasses;

        _spriteLayerTestPass = new SpriteLayerTestPass(Settings.LayerMask,
            Settings.ShaderTagList, Settings.Event);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview
            || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
            return;

        renderer.EnqueuePass(_spriteLayerTestPass);
    }
}