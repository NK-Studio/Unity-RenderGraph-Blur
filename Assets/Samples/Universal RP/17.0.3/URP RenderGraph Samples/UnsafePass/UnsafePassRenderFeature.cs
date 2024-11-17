using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 이 예제에서는 활성 색상 텍스처를 새 텍스처에 복사한 다음 소스 텍스처를 두 번 다운샘플링합니다. 이 예는 API 데모용입니다.
// 새 텍스처가 프레임의 다른 곳에서는 사용되지 않도록 프레임 디버거를 사용하여 해당 내용을 확인할 수 있습니다.
// 이 예제의 핵심 개념은 UnsafePass 사용법입니다. 이러한 유형의 패스는 안전하지 않으며 다음과 같은 SetRenderTarget()과 같은 명령을 사용할 수 있습니다.
// RasterRenderPass와 호환되지 않습니다. UnsafePasses를 사용한다는 것은 RenderGraph가 패스를 NativeRenderPass 내부에 병합하여 최적화하려고 시도하지 않는다는 것을 의미합니다.
// 어떤 경우에는 UnsafePasses를 사용하는 것이 합리적입니다. 예를 들어 인접한 패스 세트를 병합할 수 없다는 것을 알고 있으면 RenderGraph를 최적화할 수 있습니다.
// 다중 패스 설정을 단순화하는 것 외에도 시간을 컴파일합니다.
public class UnsafePassRenderFeature : ScriptableRendererFeature
{
    private static readonly int DownsampleTex = Shader.PropertyToID("_DownsampleTex");
    private static readonly int BlurOffset = Shader.PropertyToID("_blurOffset");


    
    class UnsafePass : ScriptableRenderPass
    {
        private Material m_TargetMaterial;

        private int _blurIteration = 4;
        private float _blurOffset = 1.0f;
        
        // 이 클래스는 패스에 필요한 데이터를 저장하고 패스를 실행하는 대리자 함수에 매개변수로 전달됩니다.
        private class PassData
        {
            internal Material TargetMaterial;
            internal float BlurOffset;
            internal TextureHandle Source;
            internal TextureHandle[] Scratches;
        }

        // 이 정적 메서드는 패스를 실행하는 데 사용되며 RenderGraph 렌더 패스에 RenderFunc 대리자로 전달됩니다.
        static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            // 각 블릿에 대해 RenderTarget을 수동으로 설정합니다. 원하는 경우 각 SetRenderTarget 호출에는 별도의 RasterCommandPass가 필요합니다.
            // 가능한 경우 패스 병합을 위해 RenderGraph를 설정합니다.
            // 이 경우 우리는 RenderTargets의 차원이 다르기 때문에 이 3개의 하위 패스가 병합에 호환되지 않는다는 것을 알고 있습니다. 
            // 따라서 안전하지 않은 패스를 사용하도록 코드를 단순화하고 RenderGraph 처리 시간도 절약합니다.

            // copy the current scene color

            // 블러 오프셋을 설정합니다.
            context.cmd.SetGlobalFloat("_blurOffset", BlurOffset);
            
            CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            var sourceHandle = data.Source;
            var scratchesCount = data.Scratches.Length;
            for (int i = 0; i < scratchesCount; i++)
            {
                context.cmd.SetGlobalTexture(DownsampleTex, sourceHandle);
                context.cmd.SetRenderTarget(data.Scratches[i]);
                Blitter.BlitTexture(unsafeCmd, data.Source, new Vector4(1, 1, 0, 0), data.TargetMaterial, 0);
                sourceHandle = data.Scratches[i];
            }
        }

        // This is where the renderGraph handle can be accessed.
        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            string passName = "Unsafe Pass";

            // add a raster render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var passData))
            {
                // UniversalResourceData에는 활성 색상 및 깊이 텍스처를 포함하여 렌더러에서 사용하는 모든 텍스처 핸들이 포함됩니다.
                // 활성 색상 및 깊이 텍스처는 카메라가 렌더링하는 기본 색상 및 깊이 버퍼입니다.
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                // 패스에 필요한 데이터로 passData를 채우세요.

                // 프레임 데이터를 통해 활성 색상 텍스처를 가져오고 이를 블릿의 소스 텍스처로 설정합니다.
                passData.Source = resourceData.activeColorTexture;

                // Setup Material
                passData.TargetMaterial = m_TargetMaterial;
                
                // 텍스처의 설명을 가져와서 수정합니다.
                var descriptor = passData.Source.GetDescriptor(renderGraph);
                descriptor.msaaSamples = MSAASamples.None; // blit 작업에 대해 MSAA를 비활성화합니다.
                descriptor.clearBuffer = false;
                
                int iteration = _blurIteration;
                int scratchesCount = Mathf.Max(iteration * 2 - 1, 1);

                int sourceSizeWidth = descriptor.width;
                int sourceSizeHeight = descriptor.height;

                passData.Scratches = new TextureHandle[scratchesCount];
                
                // 다운 샘플링 Blit 반복
                for (int i = 0; i < scratchesCount; i++)
                {
                    int downsampleIndex = SimplePingPong(i, iteration - 1);
                    descriptor.name = $"Scratch_{i}";
                    descriptor.width = sourceSizeWidth >> downsampleIndex + 1;
                    descriptor.height = sourceSizeHeight >> downsampleIndex + 1;

                    passData.Scratches[i] = renderGraph.CreateTexture(descriptor);
                    builder.UseTexture(passData.Scratches[i], AccessFlags.ReadWrite);
                }

                // UseTexture()를 통해 src 텍스처를 이 패스에 대한 입력 종속성으로 선언합니다.
                builder.UseTexture(passData.Source);

                // 일반적으로 이 패스가 컬링되므로 이 샘플의 시연 목적으로 이 패스에 대한 컬링을 비활성화합니다.
                // 대상 텍스처는 다른 곳에서는 사용되지 않기 때문에
                builder.AllowPassCulling(false);

                // 패스를 실행할 때 렌더 그래프에 의해 호출되는 렌더 패스 대리자에 ExecutePass 함수를 할당합니다.
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }
        
        private static int SimplePingPong(int t, int max)
        {
            if (t > max) return 2 * max - t;
            return t;
        }

        public void Setup(Material targetMaterial)
        {
            m_TargetMaterial = targetMaterial;
        }
    }

    UnsafePass m_UnsafePass;
    public Material TargetMaterial;

    /// <inheritdoc/>
    public override void Create()
    {
        m_UnsafePass = new UnsafePass();

        // Configures where the render pass should be injected.
        m_UnsafePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_UnsafePass.Setup(TargetMaterial);
        renderer.EnqueuePass(m_UnsafePass);
    }
}