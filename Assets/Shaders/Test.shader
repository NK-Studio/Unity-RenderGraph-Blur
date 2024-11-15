Shader "Hidden/URP/BlurUI"
{
    Properties
    {
        [NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}

        _BlendAmount("Blur Amount(Use UI Alpha)", Range(0,1)) = 0.5

        [HideInInspector]_StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask("ColorMask", Float) = 15
        [HideInInspector]_ClipRect("ClipRect", Vector) = (0, 0, 0, 0)
        [HideInInspector]_UIMaskSoftnessX("UIMaskSoftnessX", Float) = 1
        [HideInInspector]_UIMaskSoftnessY("UIMaskSoftnessY", Float) = 1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
    //#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/CanvasPass.hlsl"

    struct Attributes
    {
        float3 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 color : COLOR;
        float4 uv0 : TEXCOORD0;
        float4 uv1 : TEXCOORD1;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float4 color : COLOR;
        float4 texCoord0 : TEXCOORD0;
        float4 texCoord1 : TEXCOORD1;
        float3 positionWS : TEXCOORD2;
        float3 normalWS : TEXCOORD3;
        float4 screenPosition : TEXCOORD4;
    };


    // -- ScenePickingPass에서 사용하는 속성
    #ifdef SCENEPICKINGPASS
                float4 _SelectionID;
    #endif

    // -- SceneSelectionPass에서 사용되는 속성
    #ifdef SCENESELECTIONPASS
                int _ObjectId;
                int _PassValue;
    #endif

    // UGUI에는 렌더러에 "bloom"이 있는 경우에 대한 키워드가 없으므로 모든 기본 UI 셰이더와 마찬가지로 여기서 하드코어해야 합니다.
    half4 _TextureSampleAdd;

    // Properties
    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _Stencil;
        float _StencilOp;
        float _StencilWriteMask;
        float _StencilReadMask;
        float _ColorMask;
        float4 _ClipRect;
        float _UIMaskSoftnessX;
        float _UIMaskSoftnessY;
        float _BlendAmount;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
    CBUFFER_END

    SAMPLER(SamplerState_Linear_Repeat);

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    TEXTURE2D(_LayerFilterCopypassBufferTex);
    SAMPLER(sampler_LayerFilterCopypassBufferTex);

    // Transforms position from object space to homogenous space
    float4 TransformModelToHClip(float3 positionOS)
    {
        return mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(positionOS, 1.0)));
    }

    Varyings Vert(Attributes input)
    {
        Varyings output = (Varyings)0;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        // Returns the camera relative position (if enabled)
        float3 positionWS = TransformObjectToWorld(input.positionOS);

        output.positionCS = TransformWorldToHClip(positionWS);

        #if UNITY_UV_STARTS_AT_TOP
        // UI가 오버레이에서 렌더링되도록 설정되면 GFX장치에 씁니다.
        // 전체 화면 백 버퍼에 쓰는 것과 같습니다.
        // 카메라에서 제공하지 않는 행렬을 포착하기 위한 작업,
        // 원시 unity_ObjectToWorld 및 unity_MatrixVP를 사용하여 Clipspace가 다시 계산됩니다.
        output.positionCS = TransformModelToHClip(input.positionOS);
        output.texCoord0.y = 1.0 - output.texCoord0.y;
        #endif
        #ifdef ATTRIBUTES_NEED_NORMAL
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        #else
        // 노멀을 사용하지 않는 ApplyVertexModification을 컴파일하는 데 필요합니다.
        float3 normalWS = float3(0.0, 0.0, 0.0);
        #endif

        #ifdef VARYINGS_NEED_POSITION_WS
        output.positionWS = positionWS;
        #endif

        #ifdef VARYINGS_NEED_NORMAL_WS
        output.normalWS = normalWS;         // TransformObjectToWorldNormal()에서 정규화됨
        #endif

        #if defined(VARYINGS_NEED_TEXCOORD0) || defined(VARYINGS_DS_NEED_TEXCOORD0)
        output.texCoord0 = input.uv0;
        #endif

        // UI "Mask"
        #if defined(VARYINGS_NEED_TEXCOORD1) || defined(VARYINGS_DS_NEED_TEXCOORD1)
        #ifdef UNITY_UI_CLIP_RECT
            float2 pixelSize =  output.positionCS.w;
            pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

            float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
            float2 maskUV = (input.positionOS.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
            output.texCoord1 = float4(input.positionOS.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
        #endif
        #endif

        #if defined(VARYINGS_NEED_COLOR) || defined(VARYINGS_DS_NEED_COLOR)
        output.color = input.color;
        #endif

        #if defined( VARYINGS_NEED_SCREENPOSITION)
        output.screenPosition = GetVertexPositionNDC(output.positionCS); // vertexInput.positionNDC;
        #endif
        return output;
    }

    half4 PrePassFragment(Varyings IN) : SV_TARGET
    {
        float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texCoord0.xy) + _TextureSampleAdd;

        #if !defined(HAVE_VFX_MODIFICATION) && !defined(_DISABLE_COLOR_TINT)
        color *= IN.color;
        #endif

        #ifdef UNITY_UI_CLIP_RECT
        //mask = Uv2
        half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.texCoord1.xy)) * IN.texCoord1.zw);
        color.a *= m.x * m.y;
        #endif

        #if _ALPHATEST_ON
        clip(alpha - 0.5);
        #endif

        color.rgb *= color.a;

        return color;
    }

    half4 DrawPassFragment(Varyings IN) : SV_TARGET
    {
        half blendAmount = IN.color.a * _BlendAmount;
        blendAmount = saturate(pow(blendAmount, 2.2));

        UnityTexture2D targetTexture = UnityBuildTexture2DStructNoScale(_MainTex);
        float4 color = SAMPLE_TEXTURE2D_LOD(targetTexture.tex, targetTexture.samplerstate,
                                              targetTexture.GetTransformedUV(IN.texCoord0.xy),
                                              blendAmount * 3.2) + _TextureSampleAdd;

        float2 uv_prepass = IN.screenPosition.xy / IN.screenPosition.w;
        float4 prepassMap = SAMPLE_TEXTURE2D(_LayerFilterCopypassBufferTex, sampler_LayerFilterCopypassBufferTex,
 uv_prepass) + _TextureSampleAdd;

        half4 finalColor = lerp(color, prepassMap, blendAmount);
        finalColor.a = color.a;

        // #if !defined(HAVE_VFX_MODIFICATION) && !defined(_DISABLE_COLOR_TINT)
        // finalColor *= IN.color;
        // #endif

        #ifdef UNITY_UI_CLIP_RECT
        //mask = Uv2
        half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.texCoord1.xy)) * IN.texCoord1.zw);
        finalColor.a *= m.x * m.y;
        #endif

        #if _ALPHATEST_ON
        clip(finalColor.a - 0.5);
        #endif

        finalColor.rgb *= finalColor.a;

        return finalColor;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
            // DisableBatching: <None>
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalCanvasSubTarget"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "PrePass"
            Tags
            {
                "LightMode" = "UGUIPrepass"
            }

            // Render State
            Cull Off
            Blend One OneMinusSrcAlpha
            ZTest [unity_GUIZTestMode]
            ZWrite Off
            ColorMask [_ColorMask]
            Stencil
            {
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
                Ref [_Stencil]
                CompFront [_StencilComp]
                PassFront [_StencilOp]
                CompBack [_StencilComp]
                PassBack [_StencilOp]
            }

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment PrePassFragment

            // Keywords
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #define CANVAS_SHADERGRAPH

            // Defines
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_COLOR
            #define ATTRIBUTES_NEED_VERTEXID
            #define ATTRIBUTES_NEED_INSTANCEID
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_TEXCOORD1
            #define VARYINGS_NEED_COLOR
            #define VARYINGS_NEED_SCREENPOSITION

            #define REQUIRE_DEPTH_TEXTURE
            #define REQUIRE_NORMAL_TEXTURE

            #define SHADERPASS SHADERPASS_CUSTOM_UI
            ENDHLSL
        }

        Pass
        {
            Name "DrawPass"
            Tags
            {
                "LightMode" = "UGUIDrawpass"
            }

            // Render State
            Cull Off
            Blend One OneMinusSrcAlpha
            ZTest [unity_GUIZTestMode]
            ZWrite Off
            ColorMask [_ColorMask]
            Stencil
            {
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
                Ref [_Stencil]
                CompFront [_StencilComp]
                PassFront [_StencilOp]
                CompBack [_StencilComp]
                PassBack [_StencilOp]
            }

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment DrawPassFragment

            // Keywords
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #define CANVAS_SHADERGRAPH

            // Defines
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_COLOR
            #define ATTRIBUTES_NEED_VERTEXID
            #define ATTRIBUTES_NEED_INSTANCEID
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_TEXCOORD1
            #define VARYINGS_NEED_COLOR
            #define VARYINGS_NEED_SCREENPOSITION

            #define REQUIRE_DEPTH_TEXTURE
            #define REQUIRE_NORMAL_TEXTURE

            #define SHADERPASS SHADERPASS_CUSTOM_UI
            ENDHLSL
        }
    }
}