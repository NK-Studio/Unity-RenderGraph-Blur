Shader "Hidden/Universal Render Pipeline/UI/ScreenBlurRT"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    SAMPLER(sampler_linear_clamp);
    TEXTURE2D(_DownSampleTex);
    float4 _DownSampleTex_TexelSize;
    float _blurOffset;

    float4 BlurFrag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float4 outputColor = float4(0.0, 0.0, 0.0, 0.0);
        float offset = _blurOffset;

        // 4방향으로 텍스쳐를 샘플링하기 위한 오프셋을 계산합니다.
        float2 offsets[4] = {
            float2(_DownSampleTex_TexelSize.x * -offset, 0.0),
            float2(_DownSampleTex_TexelSize.x * offset, 0.0),
            float2(0.0, _DownSampleTex_TexelSize.y * -offset),
            float2(0.0, _DownSampleTex_TexelSize.y * offset)
        };

        // 4방향으로 텍스쳐를 샘플링합니다.
        for (int i = 0; i < 4; i++)
            outputColor += SAMPLE_TEXTURE2D_X(_DownSampleTex, sampler_linear_clamp, uv + offsets[i]);

        float4 finalColor = outputColor * 0.25f; // 0.25를 곱하면 4로 나누는 효과와 같습니다.
        return finalColor;
    }
    ENDHLSL

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"
        }
        Pass
        {
            Name "ScreenBlurRT"
            Tags
            {
                "LightMode" = "ScreenBlurRT"
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex Vert
            #pragma fragment BlurFrag
            ENDHLSL
        }
    }
}