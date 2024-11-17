using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NKStudio
{
    public class LayerFilterRendererPass : ScriptableRenderPass
    {
        private class FilterPassData
        {
            internal RendererListHandle RendererList;
        }
        
        private class MipMapPassData
        {
            internal TextureHandle Source;
            internal TextureHandle[] Scratches;
            internal Material TargetMaterial;
            internal float BlurOffset;
        }


        private readonly RenderQueueType _renderQueueType;
        private readonly LayerMask _layerMask;
        private readonly List<ShaderTagId> _filterShaderTagIdList = new();
        private readonly List<ShaderTagId> _drawShaderTagIdList = new();

        private Material _material;
        
        private int _blurIteration = 3;
        private float _blurOffset = 1.0f;
        
        private static readonly int k_MainTexPropertyName = Shader.PropertyToID("_MainTex");
        private static readonly int k_BlurTexPropertyName = Shader.PropertyToID("_BlurTex");
        private static readonly int k_BlurOffsetPropertyName = Shader.PropertyToID("_blurOffset");
        
        public LayerFilterRendererPass(LayerMask layerMask, List<string> shaderTagIdList, List<string> drawShaderTagIdList,
            RenderPassEvent injectionPoint, Material material)
        {
            _layerMask = layerMask;
            renderPassEvent = injectionPoint;
            _material = material;

            // 셰이더 태그 ID 목록 초기화 및 설정
            _filterShaderTagIdList.Clear();
            foreach (string tag in shaderTagIdList)
                _filterShaderTagIdList.Add(new ShaderTagId(tag));
            
            _drawShaderTagIdList.Clear();
            foreach (string tag in drawShaderTagIdList)
                _drawShaderTagIdList.Add(new ShaderTagId(tag));
            
            requiresIntermediateTexture = true;
        }
        
        public void Setup(int blurIteration = 3, float blurOffset = 1.0f)
        {
            _blurIteration = blurIteration;
            _blurOffset = blurOffset;
        }

        /// <summary>
        /// 렌더러 목록을 초기화합니다.
        /// </summary>
        /// <param name="frameData">프레임 데이터를 포함하는 컨텍스트 컨테이너입니다.</param>
        /// <param name="passData">초기화할 패스 데이터입니다.</param>
        /// <param name="renderGraph">렌더러 목록을 생성하는 데 사용할 렌더 그래프입니다.</param>
        private void InitRendererList(ContextContainer frameData, ref FilterPassData passData, RenderGraph renderGraph, List<ShaderTagId> shaderTagIdList)
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
                CreateDrawingSettings(shaderTagIdList, renderingData, cameraData, lightData, sortingCriteria);

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
        private static void ExecuteFilterPass(FilterPassData data, RasterGraphContext context)
        {
            // 렌더 대상의 배경을 검은색으로 지움
            //context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black, 1, 0);

            // 목록의 오브젝트 그리기
            context.cmd.DrawRendererList(data.RendererList);
        }
        
        // 이 정적 메서드는 패스를 실행하는 데 사용되며 RenderGraph 렌더 패스에 RenderFunc 대리자로 전달됩니다.
        static void ExecuteMipmapPass(MipMapPassData data, UnsafeGraphContext context)
        {
            // 블러 오프셋을 설정합니다.
            context.cmd.ClearRenderTarget(RTClearFlags.None, Color.white, 1, 0);
            context.cmd.SetGlobalFloat(k_BlurOffsetPropertyName, data.BlurOffset);
            
            var unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            var source = data.Source;
            var stepCount = data.Scratches.Length;
            for (int i = 0; i < stepCount; i++)
            {
                context.cmd.SetGlobalTexture(k_MainTexPropertyName, source);
                context.cmd.SetRenderTarget(data.Scratches[i]); // Target Destination
                Blitter.BlitTexture(unsafeCmd, data.Source, new Vector4(1, 1, 0, 0), data.TargetMaterial, 0); // Blit
                source = data.Scratches[i]; // Set Next Source
            }
            
            context.cmd.SetGlobalTexture(k_BlurTexPropertyName, source);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // UniversalResourceData에는 활성 색상 및 깊이 텍스처를 포함하여 렌더러에서 사용하는 모든 텍스처 핸들이 포함됩니다.
            // 활성 색상 및 깊이 텍스처는 카메라가 렌더링하는 기본 색상 및 깊이 버퍼입니다.
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            
            // 텍스처의 설명을 가져와서 수정합니다.
            TextureDesc cameraColorDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
            cameraColorDesc.name = "LayerFilterPrepass"; // 텍스처 이름 설정
            cameraColorDesc.format = GetGraphicsFormat(); // 그래픽 포맷 설정
            cameraColorDesc.msaaSamples = MSAASamples.None; // MSAA 설정

            // 새로운 렌더 타겟 생성
            TextureHandle maskTextureHandle = renderGraph.CreateTexture(cameraColorDesc);
            
            using (var builder =
                   renderGraph.AddRasterRenderPass<FilterPassData>("NK Mask", out var passData, profilingSampler))
            {
                //  렌더러 목록 초기화
                InitRendererList(frameData, ref passData, renderGraph, _filterShaderTagIdList);

                // 이 Pass에서 사용할 리소스로 선언하기
                builder.UseRendererList(passData.RendererList);
                builder.SetRenderAttachment(maskTextureHandle, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                // 렌더링 함수 설정
                builder.SetRenderFunc((FilterPassData data, RasterGraphContext context) => ExecuteFilterPass(data, context));
            }
            
            // add a raster render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
            using (var builder = renderGraph.AddUnsafePass<MipMapPassData>("NK Blur Mipmap", out var passData))
            {
                // 패스에 필요한 데이터로 passData를 채우세요.
            
                // 프레임 데이터를 통해 활성 색상 텍스처를 가져오고 이를 블릿의 소스 텍스처로 설정합니다.
                passData.Source = maskTextureHandle;
                
                // Setup Material
                passData.TargetMaterial = _material;
                
                // 텍스처의 설명을 가져와서 수정합니다.
                var descriptor = passData.Source.GetDescriptor(renderGraph);
                descriptor.msaaSamples = MSAASamples.None; // blit 작업에 대해 MSAA를 비활성화합니다.
                descriptor.clearBuffer = false;
                
                int iteration = _blurIteration;
                int scratchesCount = Mathf.Max(iteration * 2 - 1, 1);

                int sourceSizeWidth = descriptor.width;
                int sourceSizeHeight = descriptor.height;

                passData.Scratches = new TextureHandle[scratchesCount];
                passData.BlurOffset = _blurOffset;
                
                // 다운 샘플링 Blit 반복
                for (int i = 0; i < scratchesCount; i++)
                {
                    int downsampleIndex = SimplePingPong(i, iteration - 1);
                    descriptor.name = $"NK Mipmap_{i}";
                    descriptor.width = sourceSizeWidth >> downsampleIndex + 1;
                    descriptor.height = sourceSizeHeight >> downsampleIndex + 1;

                    passData.Scratches[i] = renderGraph.CreateTexture(descriptor);
                    builder.UseTexture(passData.Scratches[i], AccessFlags.ReadWrite);
                }

                // UseTexture()를 통해 src 텍스처를 이 패스에 대한 입력 종속성으로 선언합니다.
                builder.UseTexture(maskTextureHandle);

                // 일반적으로 이 패스가 컬링되므로 이 샘플의 시연 목적으로 이 패스에 대한 컬링을 비활성화합니다.
                // 대상 텍스처는 다른 곳에서는 사용되지 않기 때문에
                builder.AllowPassCulling(false);

                // 패스를 실행할 때 렌더 그래프에 의해 호출되는 렌더 패스 대리자에 ExecutePass 함수를 할당합니다.
                builder.SetRenderFunc((MipMapPassData data, UnsafeGraphContext context) => ExecuteMipmapPass(data, context));
            }
            
            // add a raster render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
            using (var builder = renderGraph.AddRasterRenderPass<FilterPassData>("Final Draw", out var passData))
            {
                //  렌더러 목록 초기화
                InitRendererList(frameData, ref passData, renderGraph, _drawShaderTagIdList);
                
                // 방금 생성한 RendererList를 UseRendererList()를 통해 이 패스에 대한 입력 종속성으로 선언합니다.
                builder.UseRendererList(passData.RendererList);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
                builder.SetRenderFunc((FilterPassData data, RasterGraphContext context) => ExecuteFilterPass(data, context));
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
        
        private static int SimplePingPong(int t, int max)
        {
            if (t > max) return 2 * max - t;
            return t;
        }
    }
}