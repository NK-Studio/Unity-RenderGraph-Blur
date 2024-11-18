Shader "Universal Render Pipeline/2D/Sprite-Unlit-Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlendAmount("Blur Amount(Use Sprite Alpha)", Range(0,1)) = 0.5

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
    #if defined(DEBUG_DISPLAY)
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"
    #endif

    #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

    struct Attributes
    {
        float3 positionOS : POSITION;
        float4 color : COLOR;
        float2 uv : TEXCOORD0;
        UNITY_SKINNED_VERTEX_INPUTS
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float4 color : COLOR;
        float2 uv : TEXCOORD0;
        float4 screenPosition : TEXCOORD1;
        #if defined(DEBUG_DISPLAY)
        float3 positionWS : TEXCOORD2;
        #endif
        UNITY_VERTEX_OUTPUT_STEREO
    };


    CBUFFER_START(UnityPerMaterial)
        half _BlendAmount;
        half4 _Color;
    CBUFFER_END

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_MainTex);

    TEXTURE2D(_BlurTex);
    SAMPLER(sampler_BlurTex);


    Varyings vert(Attributes i)
    {
        Varyings o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        UNITY_SKINNED_VERTEX_COMPUTE(v);

        i.positionOS = UnityFlipSprite(i.positionOS, unity_SpriteProps.xy);
        o.positionCS = TransformObjectToHClip(i.positionOS);

        #if defined(DEBUG_DISPLAY)
        o.positionWS = TransformObjectToWorld(i.positionOS);
        #endif

        o.screenPosition = ComputeScreenPos(o.positionCS);
        o.uv = i.uv;
        o.color = i.color * _Color * unity_SpriteColor;
        return o;
    }

    half4 PrePassFragment(Varyings i) : SV_TARGET
    {
        half4 mainTex = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

        #if defined(DEBUG_DISPLAY)
        SurfaceData2D surfaceData;
        InputData2D inputData;
        half4 debugColor = 0;

        InitializeSurfaceData(mainTex.rgb, mainTex.a, surfaceData);
        InitializeInputData(i.uv, inputData);
        SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, i.positionWS, i.positionCS, _MainTex);

        if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
        {
            return debugColor;
        }
        #endif

        return mainTex;
    }

    half4 DrawPassFragment(Varyings i) : SV_TARGET
    {
        half lodAmount = i.color.a * _BlendAmount;
        lodAmount = saturate(pow(abs(lodAmount), 2.2));

        half4 mainColor = i.color * SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv, lodAmount * 3.2);

        float2 screenUV = i.screenPosition.xy / i.screenPosition.w;
        half4 blurColor = SAMPLE_TEXTURE2D(_BlurTex, sampler_BlurTex, screenUV);

        #if defined(DEBUG_DISPLAY)
        SurfaceData2D surfaceData;
        InputData2D inputData;
        half4 debugColor = 0;

        InitializeSurfaceData(mainColor.rgb, mainColor.a, surfaceData);
        InitializeInputData(i.uv, inputData);
        SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, i.positionWS, i.positionCS, _MainTex);

        if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
            return debugColor;

        #endif

        half4 finalColor = lerp(mainColor, blurColor, lodAmount);
        finalColor.a = mainColor.a;
        return finalColor;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off


        Pass
        {
            Tags
            {
                "LightMode" = "SpriteRenderPrepass"
            }
            Name "PrePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment PrePassFragment
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "SpriteRenderDrawpass"
            }
            Name "DrawPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment DrawPassFragment
            ENDHLSL
        }
    }
    CustomEditor "NKStudio.SpriteShaderGUI"
}