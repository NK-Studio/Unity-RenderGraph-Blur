using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NKStudio
{
    public class LayerFilterRendererPass_Prepass : ScriptableRenderPass
    {
        private class PassData
        {
            internal RendererListHandle RendererList;
        }

        private readonly RenderQueueType _renderQueueType;
        private readonly LayerMask _layerMask;
        private readonly List<ShaderTagId> _shaderTagIdList = new();

        public LayerFilterRendererPass_Prepass(LayerMask layerMask, List<string> shaderTagIdList,
            RenderPassEvent injectionPoint)
        {
            _layerMask = layerMask;
            renderPassEvent = injectionPoint;

            // 셰이더 태그 ID 목록 초기화 및 설정
            _shaderTagIdList.Clear();
            foreach (string tag in shaderTagIdList)
                _shaderTagIdList.Add(new ShaderTagId(tag));
        }

        /// <summary>
        /// 렌더러 목록을 초기화합니다.
        /// </summary>
        /// <param name="frameData">프레임 데이터를 포함하는 컨텍스트 컨테이너입니다.</param>
        /// <param name="passData">초기화할 패스 데이터입니다.</param>
        /// <param name="renderGraph">렌더러 목록을 생성하는 데 사용할 렌더 그래프입니다.</param>
        private void InitRendererList(ContextContainer frameData, ref PassData passData, RenderGraph renderGraph)
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

        /// <summary>
        /// 패스를 실행합니다.
        /// </summary>
        /// <param name="data">패스 실행에 필요한 데이터입니다.</param>
        /// <param name="context">패스가 실행되는 컨텍스트입니다.</param>
        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            // 렌더 대상의 배경을 검은색으로 지움
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

                //  렌더러 목록 초기화
                InitRendererList(frameData, ref passData, renderGraph);

                // 이 Pass에서 사용할 리소스로 선언하기
                builder.UseRendererList(passData.RendererList);

                // 텍스처의 설명을 가져와서 수정합니다.
                var cameraColorDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                cameraColorDesc.name = "LayerFilterPrepass"; // 텍스처 이름 설정
                cameraColorDesc.format = GetGraphicsFormat(); // 그래픽 포맷 설정
                cameraColorDesc.msaaSamples = MSAASamples.None; // MSAA 설정

                // 새로운 렌더 타겟 생성
                var destination = renderGraph.CreateTexture(cameraColorDesc);
                builder.SetRenderAttachment(destination, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                // 렌더링 함수 설정
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }

        private static GraphicsFormat GetGraphicsFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32,
                    GraphicsFormatUsage.Linear | GraphicsFormatUsage.Render))
                return GraphicsFormat.B10G11R11_UFloatPack32;

            return QualitySettings.activeColorSpace == ColorSpace.Linear
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_UNorm;
        }
    }
}