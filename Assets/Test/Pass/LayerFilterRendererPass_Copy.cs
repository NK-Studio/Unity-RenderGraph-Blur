using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NKStudio
{
    public class LayerFilterRendererPass_Copy : ScriptableRenderPass
    {
        private const string k_TexturePropertyName = "_LayerFilterCopypassBufferTex";
        
        private readonly RenderQueueType _renderQueueType;
        
        private Material _material;
        
        private int _blurIteration = 3;
        private float _blurOffset = 1.0f;

        public LayerFilterRendererPass_Copy(RenderPassEvent injectionPoint, Material material)
        {
            renderPassEvent = injectionPoint;
            _material = material;
            
            requiresIntermediateTexture = true;
        }
        
        public void Setup(int blurIteration = 3, float blurOffset = 1.0f)
        {
            _blurIteration = blurIteration;
            _blurOffset = blurOffset;
        }



        private static void ExecutePass(RasterCommandBuffer cmd, PassData data, RasterGraphContext context)
        {
            // 렌더 대상을 검은색으로 지움
            context.cmd.ClearRenderTarget(RTClearFlags.None, Color.white, 1, 0);

            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.CopyColor)))
            {
                Vector2 viewportScale = source.useScaling ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y) : Vector2.one;

                switch (downsamplingMethod)
                {
                    case Downsampling.None:
                        
                        Blitter.BlitTexture(cmd, source, viewportScale, copyColorMaterial, 0);
                        break;
                    case Downsampling._2xBilinear:
                        Blitter.BlitTexture(cmd, source, viewportScale, copyColorMaterial, 1);
                        break;
                    case Downsampling._4xBox:
                        samplingMaterial.SetFloat(sampleOffsetShaderHandle, 2);
                        Blitter.BlitTexture(cmd, source, viewportScale, samplingMaterial, 0);
                        break;
                    case Downsampling._4xBilinear:
                        Blitter.BlitTexture(cmd, source, viewportScale, copyColorMaterial, 1);
                        break;
                }
            }
        }
        
        private class PassData
        {
            internal Material TargetMaterial;
        }

        internal TextureHandle Render(RenderGraph renderGraph, ContextContainer frameData, out TextureHandle destination, in TextureHandle source, Downsampling downsampling)
        {
            m_DownsamplingMethod = downsampling;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            ConfigureDescriptor(downsampling, ref descriptor, out var filterMode);

            destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_CameraOpaqueTexture", true, filterMode);
            
            RenderInternal(renderGraph, destination, source, cameraData.xr.enabled);                    

            return destination;
        }
        
        // This will not create a new texture, but will reuse an existing one as destination.
        // Typical use case is a persistent texture imported to the render graph. For example history textures.
        // Note that the amount of downsampling is determined by the destination size.
        // Therefore, the downsampling param controls only the algorithm (shader) used for the downsampling, not size.
        internal void RenderToExistingTexture(RenderGraph renderGraph, ContextContainer frameData, in TextureHandle destination, in TextureHandle source, Downsampling downsampling = Downsampling.None)
        {
            m_DownsamplingMethod = downsampling;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            RenderInternal(renderGraph, destination, source, cameraData.xr.enabled);
        }
        
        private void RenderInternal(RenderGraph renderGraph, in TextureHandle destination, in TextureHandle source, bool useProceduralBlit)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.destination = destination;
                builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                passData.source = source;
                builder.UseTexture(source, AccessFlags.Read);
                passData.useProceduralBlit = useProceduralBlit;
                passData.samplingMaterial = m_SamplingMaterial;
                passData.copyColorMaterial = m_CopyColorMaterial;
                passData.downsamplingMethod = m_DownsamplingMethod;
                passData.sampleOffsetShaderHandle = m_SampleOffsetShaderHandle;

                if (destination.IsValid())
                    builder.SetGlobalTextureAfterPass(destination, Shader.PropertyToID("_CameraOpaqueTexture"));

                // TODO RENDERGRAPH: 선별? 테스트를 위해 강제로 선별
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    ExecutePass(context.cmd, data, data.source, data.useProceduralBlit);
                });
            }
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder =
                   renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                // UniversalResourceData에는 활성 색상 및 깊이 텍스처를 포함하여 렌더러에서 사용하는 모든 텍스처 핸들이 포함됩니다.
                // 활성 색상 및 깊이 텍스처는 카메라가 렌더링하는 기본 색상 및 깊이 버퍼입니다.
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                passData.TargetMaterial = _material;
                
                // 텍스처의 설명을 가져와서 수정합니다.
                var cameraColorDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                cameraColorDesc.name = "LayerFilter_Copypass"; // 텍스처 이름 설정
                cameraColorDesc.msaaSamples = MSAASamples.None; // MSAA 설정

                var destination = renderGraph.CreateTexture(cameraColorDesc);
                builder.SetRenderAttachment(destination, 0);
                
                int iteration = _blurIteration;
                int stepCount = Mathf.Max(iteration * 2 - 1, 1);
                string[] shaderIDStr = new string[stepCount];
                int[] shaderID = new int[stepCount];
                
                int sourceSizeWidth = cameraColorDesc.width;
                int sourceSizeHeight = cameraColorDesc.height;

                // 다운 샘플링 Blit 반복
                for (int i = 0; i < stepCount; i++)
                {
                    int downsampleIndex = SimplePingPong(i, iteration - 1);
                    cameraColorDesc.width = sourceSizeWidth >> downsampleIndex + 1;
                    cameraColorDesc.height = sourceSizeHeight >> downsampleIndex + 1;
                    shaderIDStr[i] = k_TexturePropertyName + i.ToString();
                    shaderID[i] = Shader.PropertyToID(shaderIDStr[i]);
                }

                // 렌더링 함수 설정
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }

        private void Dispose()
        {
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }
        
        private static int SimplePingPong(int t, int max)
        {
            if (t > max) return 2 * max - t;
            return t;
        }
    }
}