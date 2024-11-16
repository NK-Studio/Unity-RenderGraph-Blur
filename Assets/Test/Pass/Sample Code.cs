namespace NKStudio
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;

    namespace CatDarkGame.RendererFeature
    {
        public class SampleCode : ScriptableRenderPass
        {
            private const string k_ProfilingSamplerName = "LayerFilter_Copypass";
            private static readonly int k_BlurOffsetPropertyName = Shader.PropertyToID("_blurOffset");

            private ProfilingSampler m_ProfilingSampler;
            private Material _material;
            private Shader _shader;

            private int _blurIteration = 3;
            private float _blurOffset = 1.0f;

            private RTHandle _sourceRTHandle;
            private RTHandle _tempRTHandle;

            public SampleCode(RenderPassEvent passEvent, Shader shader)
            {
                renderPassEvent = passEvent;
                _shader = shader;

                m_ProfilingSampler = new ProfilingSampler(k_ProfilingSamplerName);
            }

            public void Setup(RTHandle source, int blurIteration = 3, float blurOffset = 1.0f)
            {
                _sourceRTHandle = source;
                _blurIteration = blurIteration;
                _blurOffset = blurOffset;

                if (_material == null && _shader != null)
                {
                    _material = CoreUtils.CreateEngineMaterial(_shader);
                }
            }

            public void Destroy()
            {
                if (_material != null)
                {
                    CoreUtils.Destroy(_material);
                    _material = null;
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || _sourceRTHandle == null) return;

                CommandBuffer cmd = CommandBufferPool.Get(k_ProfilingSamplerName);

                using (new ProfilingScope(cmd, m_ProfilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    // Blur 처리
                    int stepCount = Mathf.Max(_blurIteration * 2 - 1, 1);
                    RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                    desc.msaaSamples = 1;

                    for (int i = 0; i < stepCount; i++)
                    {
                        // Ping-pong 인덱스 계산
                        int downsampleIndex = SimplePingPong(i, _blurIteration - 1);

                        // 해상도 축소
                        desc.width >>= downsampleIndex + 1;
                        desc.height >>= downsampleIndex + 1;

                        // 임시 RTHandle 생성
                        _tempRTHandle = RTHandles.Alloc(desc);

                        // Material 설정
                        _material.SetFloat(k_BlurOffsetPropertyName, _blurOffset);

                        // Blitter를 사용한 Blit
                        Blitter.BlitCameraTexture(cmd, _sourceRTHandle, _tempRTHandle, _material, 0);

                        // 다음 iteration에서 소스 업데이트
                        CoreUtils.Swap(ref _sourceRTHandle, ref _tempRTHandle);

                        // 현재 RTHandle 해제
                        RTHandles.Release(_tempRTHandle);
                    }

                    // 최종 블리트
                    Blitter.BlitCameraTexture(cmd, _sourceRTHandle,
                        renderingData.cameraData.renderer.cameraColorTargetHandle, _material, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            private static int SimplePingPong(int t, int max)
            {
                return t > max ? 2 * max - t : t;
            }
        }
    }
}