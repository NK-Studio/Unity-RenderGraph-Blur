using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NKStudio
{
    public class UIBlurPass : ScriptableRenderPass
    {
        // Material
        private readonly Material _material;

        // Blur Settings
        private int _blurIteration = 3;
        private float _blurOffset = 1.0f;
        private bool _alwaysShow;

        // Constants
        private static readonly int DownSampleTexPropertyName = Shader.PropertyToID("_DownSampleTex");
        private static readonly int OriginTexPropertyName = Shader.PropertyToID("_OriginTex");
        private static readonly int BlurTexPropertyName = Shader.PropertyToID("_BlurTex");
        private static readonly int BlurOffsetPropertyName = Shader.PropertyToID("_blurOffset");

        public UIBlurPass(
            RenderPassEvent injectionPoint, Material material)
        {
            // 렌더 패스 이벤트를 설정합니다.
            renderPassEvent = injectionPoint;

            // 렌더 패스에 사용할 머티리얼을 설정합니다.
            _material = material;

            // BackBuffer를 Input으로 사용할 수 없으므로 중간 텍스처를 사용합니다.
            requiresIntermediateTexture = true;
        }

        /// <summary>
        /// 블러에 대한 세팅을 셋업합니다.
        /// </summary>
        /// <param name="blurIteration">블러를 이터레이션할 횟수</param>
        /// <param name="blurOffset">블러 오프셋</param>
        /// <param name="alwaysShow">플레이 모드가 되지 않아도 블러가 연출될지 처리합니다.</param>
        public void Setup(int blurIteration, float blurOffset, bool alwaysShow)
        {
            _blurIteration = blurIteration;
            _blurOffset = blurOffset;
            _alwaysShow = alwaysShow;
        }

        private class MipMapPassData
        {
            internal TextureHandle Source;
            internal TextureHandle[] Scratches;
            internal Material TargetMaterial;
            internal float BlurOffset;
            internal bool AlwaysShow;
        }

        // 이 정적 메서드는 패스를 실행하는 데 사용되며 RenderGraph 렌더 패스에 RenderFunc 대리자로 전달됩니다.
        static void ExecuteMipmapPass(MipMapPassData data, UnsafeGraphContext context)
        {
            if (data.AlwaysShow || Application.isPlaying) 
                context.cmd.SetGlobalTexture(OriginTexPropertyName, data.Source);
            
            var unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            var source = data.Source;
            var stepCount = data.Scratches.Length;
            for (int i = 0; i < stepCount; i++)
            {
                if (data.AlwaysShow || Application.isPlaying)
                    context.cmd.SetGlobalTexture(DownSampleTexPropertyName, source);

                context.cmd.SetRenderTarget(data.Scratches[i]); // 그림을 그릴 대상체를 지정합니다.
                Blitter.BlitTexture(unsafeCmd, data.Source, new Vector4(1, 1, 0, 0), data.TargetMaterial, 0); // Draw!
                source = data.Scratches[i]; // Set Next Source
            }

            if (data.AlwaysShow || Application.isPlaying)
            {
                context.cmd.SetGlobalFloat(BlurOffsetPropertyName, data.BlurOffset);  
                context.cmd.SetGlobalTexture(BlurTexPropertyName, source);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // UniversalResourceData에는 활성 색상 및 깊이 텍스처를 포함하여 렌더러에서 사용하는 모든 텍스처 핸들이 포함됩니다.
            // 활성 색상 및 깊이 텍스처는 카메라가 렌더링하는 기본 색상 및 깊이 버퍼입니다.
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // ExecutePass 함수에 전달될 이름과 데이터 유형을 지정하여 렌더 그래프에 렌더 패스를 추가합니다.
            using (var builder = renderGraph.AddUnsafePass<MipMapPassData>("Blur UI Mipmap", out var passData))
            {
                // 플레이 모드가 되지 않아도 동작시킬지 여부를 설정합니다.
                passData.AlwaysShow = _alwaysShow;
                
                // 프레임 데이터를 통해 활성 색상 텍스처를 가져오고 이를 블릿의 소스 텍스처로 설정합니다.
                passData.Source = resourceData.activeColorTexture;

                // 블러 오프셋을 설정합니다.
                passData.BlurOffset = _blurOffset;
                
                // 머티리얼을 설정합니다.
                passData.TargetMaterial = _material;
                
                // 반복 횟수의 2배로 만들어서 절반은 다운 샘플링으로 활용하고, 나머지 절반은 업 샘플링으로 활용합니다.
                int scratchesCount = Mathf.Max(_blurIteration * 2, 1);
                passData.Scratches = new TextureHandle[scratchesCount];
                
                // 텍스처의 설명을 가져와서 수정합니다.
                var descriptor = passData.Source.GetDescriptor(renderGraph);
                descriptor.msaaSamples = MSAASamples.None; // blit 작업에 대해 MSAA를 비활성화합니다.
                descriptor.clearBuffer = false;
                
                // 소스 텍스처의 너비와 높이를 가져옵니다. (예 : 모니터가 FHD일 경우 1920x1080으로 처리 됩니다.)
                int sourceSizeWidth = descriptor.width;
                int sourceSizeHeight = descriptor.height;
                
                // 다운/업 샘플링 텍스처 생성
                for (int i = 0; i < scratchesCount-1; i++)
                {
                    int downsampleIndex = SimplePingPong(i, _blurIteration - 1);
                    descriptor.name = $"Blur UI Mipmap_{i}";
                    descriptor.width = sourceSizeWidth >> downsampleIndex + 1;
                    descriptor.height = sourceSizeHeight >> downsampleIndex + 1;

                    passData.Scratches[i] = renderGraph.CreateTexture(descriptor);
                    builder.UseTexture(passData.Scratches[i], AccessFlags.ReadWrite);
                }
                
                // 마지막 텍스처는 원본 텍스처의 크기와 동일합니다.
                descriptor.width = sourceSizeWidth;
                descriptor.height = sourceSizeHeight;
                descriptor.name = $"Blur UI Mipmap_{scratchesCount-1}";
                passData.Scratches[scratchesCount-1] = renderGraph.CreateTexture(descriptor);
                builder.UseTexture(passData.Scratches[scratchesCount-1], AccessFlags.ReadWrite);

                // 화면 컬러 텍스처를 사용함을 선언
                builder.UseTexture(resourceData.activeColorTexture);
                
                // 최적화로 인해 Pass가 컬링되는 것을 방지합니다.
                builder.AllowPassCulling(false);

                // 패스를 실행할 때 렌더 그래프에 의해 호출되는 렌더 패스 대리자에 ExecutePass 함수를 할당합니다.
                builder.SetRenderFunc((MipMapPassData data, UnsafeGraphContext context) =>
                    ExecuteMipmapPass(data, context));
            }
        }

        private static int SimplePingPong(int t, int max)
        {
            if (t > max) return 2 * max - t;
            return t;
        }
    }
}