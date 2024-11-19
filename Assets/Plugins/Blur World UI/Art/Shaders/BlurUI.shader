Shader "UI/World Blur UI"
{
    Properties
    {
        [NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}
        _BlendAmount("BlendAmount", Range(0, 1)) = 0
        _Vibrancy("Vibrancy", Range(-1, 3)) = 1.8
        _Brightness("Brightness", Range(-1, 1)) = 0
        _Flatten("Flatten", Range(0, 1)) = 0.15

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
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

    struct Attributes
    {
        float3 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 color : COLOR;
        float4 uv0 : TEXCOORD0;
        float4 uv1 : TEXCOORD1;
        #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
        #endif
        uint vertexID : VERTEXID_SEMANTIC;
    };

    struct SurfaceDescriptionInputs
    {
        float2 NDCPosition;
        float2 PixelPosition;
        float4 uv0;
        float4 VertexColor;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS;
        float3 normalWS;
        float4 texCoord0;
        float4 texCoord1;
        float4 color;
        #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                     uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                     uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                     uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
    };

    struct VertexDescriptionInputs
    {
    };

    struct PackedVaryings
    {
        float4 positionCS : SV_POSITION;
        float4 texCoord0 : INTERP0;
        float4 texCoord1 : INTERP1;
        float4 color : INTERP2;
        float3 positionWS : INTERP3;
        float3 normalWS : INTERP4;
        #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                     uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                     uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                     uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
    };

    // Graph Pixel
    struct SurfaceDescription
    {
        float3 BaseColor;
        float Alpha;
        float3 Emission;
    };

    PackedVaryings PackVaryings(Varyings input)
    {
        PackedVaryings output;
        ZERO_INITIALIZE(PackedVaryings, output);
        output.positionCS = input.positionCS;
        output.texCoord0.xyzw = input.texCoord0;
        output.texCoord1.xyzw = input.texCoord1;
        output.color.xyzw = input.color;
        output.positionWS.xyz = input.positionWS;
        output.normalWS.xyz = input.normalWS;
        #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                    output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                    output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                    output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        return output;
    }

    // --------------------------------------------------
    // Structs and Packing

    Varyings UnpackVaryings(PackedVaryings input)
    {
        Varyings output;
        output.positionCS = input.positionCS;
        output.texCoord0 = input.texCoord0.xyzw;
        output.texCoord1 = input.texCoord1.xyzw;
        output.color = input.color.xyzw;
        output.positionWS = input.positionWS.xyz;
        output.normalWS = input.normalWS.xyz;
        #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                    output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                    output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                    output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        return output;
    }

    // -- Property used by ScenePickingPass
    #ifdef SCENEPICKINGPASS
                float4 _SelectionID;
    #endif

    // -- Properties used by SceneSelectionPass
    #ifdef SCENESELECTIONPASS
                int _ObjectId;
                int _PassValue;
    #endif

    //UGUI has no keyword for when a renderer has "bloom", so its nessecary to hardcore it here, like all the base UI shaders.
    half4 _TextureSampleAdd;

    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
        float _BlendAmount;
        float _Vibrancy;
        float _Brightness;
        float _Flatten;
        float4 _MainTex_TexelSize;
        float _Stencil;
        float _StencilOp;
        float _StencilWriteMask;
        float _StencilReadMask;
        float _ColorMask;
        float4 _ClipRect;
        float _UIMaskSoftnessX;
        float _UIMaskSoftnessY;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
    CBUFFER_END

    // Object and Global properties
    SAMPLER(SamplerState_Linear_Repeat);
    TEXTURE2D(_BlurTex);
    SAMPLER(sampler_BlurTex);
    float4 _BlurTex_TexelSize;
    TEXTURE2D(_OriginTex);
    SAMPLER(sampler_OriginTex);
    float4 _OriginTex_TexelSize;
    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
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
                "LightMode" = "WorldUIPrePass"
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

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

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

            #define REQUIRE_DEPTH_TEXTURE
            #define REQUIRE_NORMAL_TEXTURE

            #define SHADERPASS SHADERPASS_CUSTOM_UI

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;

                // Main Color
                UnityTexture2D mainTexure2D = UnityBuildTexture2DStructNoScale(_MainTex);
                float4 mainTexColor = SAMPLE_TEXTURE2D(mainTexure2D.tex, mainTexure2D.samplerstate,
                                               mainTexure2D.GetTransformedUV(IN.uv0.xy));

                surface.BaseColor = IN.VertexColor.xyz * mainTexColor.rgb;
                surface.Alpha = mainTexColor.a;
                surface.Emission = float3(0, 0, 0);
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #if UNITY_UV_STARTS_AT_TOP
                output.PixelPosition = float2(input.positionCS.x,
                       (_ProjectionParams.x < 0)
                           ? (_ScreenParams.y - input.positionCS.y)
                           : input.positionCS.y);
                #else
                    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
                #endif

                output.NDCPosition = output.PixelPosition.xy / _ScreenParams.xy;
                output.NDCPosition.y = 1.0f - output.NDCPosition.y;

                output.uv0 = input.texCoord0;
                output.VertexColor = input.color;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }


            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/CanvasPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DrawPass"
            Tags
            {
                "LightMode" = "WorldUIDrawPass"
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

            // --------------------------------------------------
            // Pass

            HLSLPROGRAM
            // Pragmas
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

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

            #define REQUIRE_DEPTH_TEXTURE
            #define REQUIRE_NORMAL_TEXTURE

            #define SHADERPASS SHADERPASS_CUSTOM_UI

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;

                UnityTexture2D originTexture2D = UnityBuildTexture2DStructNoScale(_OriginTex);
                float2 screenUV = IN.NDCPosition.xy;

                float4 originColor = SAMPLE_TEXTURE2D(_OriginTex, originTexture2D.samplerstate,
                    originTexture2D.GetTransformedUV(screenUV));

                UnityTexture2D blurTexture2D = UnityBuildTexture2DStructNoScale(_BlurTex);
                float4 blurColor = SAMPLE_TEXTURE2D(blurTexture2D.tex, blurTexture2D.samplerstate,
                                                      blurTexture2D.GetTransformedUV(screenUV));

                float3 fgScaled = lerp(float3(0, 0, 0), originColor.rgb * _BlendAmount.xxx, _Flatten.xxx);
                float3 color = saturate(blurColor.rgb + fgScaled - float3(2, 2, 2) * fgScaled * blurColor.rgb);

                // Vibrancy
                color = saturate(lerp(Luminance(color), color, _Vibrancy.xxx));

                // Brightness
                color = saturate(color + _Brightness.xxx);

                float3 resultSampleColor = lerp(originColor.rgb, color, _BlendAmount.xxx);

                // Mask Map
                UnityTexture2D mainTexure2D = UnityBuildTexture2DStructNoScale(_MainTex);
                float4 mainTexColor = SAMPLE_TEXTURE2D(mainTexure2D.tex, mainTexure2D.samplerstate,
                                                                   mainTexure2D.GetTransformedUV(IN.uv0.xy));

                surface.BaseColor = IN.VertexColor.xyz * resultSampleColor;
                surface.Alpha = mainTexColor.a;
                surface.Emission = float3(0, 0, 0);
                return surface;
            }

            // --------------------------------------------------
            // Build Graph Inputs

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

                #if UNITY_UV_STARTS_AT_TOP
                output.PixelPosition = float2(input.positionCS.x,
             (_ProjectionParams.x < 0)
                 ? (_ScreenParams.y - input.positionCS.y)
                 : input.positionCS.y);
                #else
                    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
                #endif

                output.NDCPosition = output.PixelPosition.xy / _ScreenParams.xy;
                output.NDCPosition.y = 1.0f - output.NDCPosition.y;

                output.uv0 = input.texCoord0;
                output.VertexColor = input.color;
                #if UNITY_ANY_INSTANCING_ENABLED
                #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.



                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

                return output;
            }


            // --------------------------------------------------
            // Main

            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/CanvasPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "NKStudio.WorldShaderGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}