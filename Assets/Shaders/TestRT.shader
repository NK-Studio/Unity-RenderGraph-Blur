Shader "Hidden/CatDarkGame/LayerFilterRendererFeature/LayerFilterBlurRT"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_linear_clamp);
    float4 _MainTex_TexelSize;
    float _blurOffset;

    float4 blurFrag(Varyings input) : SV_Target
    {
        float2 baseMapUV = input.texcoord;

        float offset = _blurOffset;
        float4 outputColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_linear_clamp,
                                                baseMapUV + float2(_MainTex_TexelSize.x * -offset, 0.0));
        outputColor += SAMPLE_TEXTURE2D_X(_MainTex, sampler_linear_clamp,
            baseMapUV + float2(_MainTex_TexelSize.x *offset, 0.0));
        outputColor += SAMPLE_TEXTURE2D_X(_MainTex, sampler_linear_clamp,
            baseMapUV + float2(0.0, _MainTex_TexelSize.y * -offset));
        outputColor += SAMPLE_TEXTURE2D_X(_MainTex, sampler_linear_clamp,
            baseMapUV + float2(0.0,_MainTex_TexelSize.y * offset));

        float4 finalColor = outputColor * 0.25f;
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
            Name "LayerFilterBlurRT"
            Tags
            {
                "LightMode" = "LayerFilterBlurRT"
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex Vert
            #pragma fragment blurFrag
            ENDHLSL
        }
    }
}