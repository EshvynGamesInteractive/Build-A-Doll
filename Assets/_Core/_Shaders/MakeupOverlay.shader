// Makeup layer (lips, eyeshadow, blush) drawn over the doll's face by DollMakeup.
// Paint colour times vertex alpha (soft edges made by DollMakeup), lit by the main light so it sits on the face.
Shader "BuildADoll/MakeupOverlay"
{
    Properties
    {
        _BaseColor ("Paint Colour", Color) = (1, 0.4, 0.7, 1)
        _Strength ("Strength", Range(0, 1)) = 0.85
        [HideInInspector] _UsePaintMask ("Use Paint Mask", Float) = 0
        _Ambient ("Ambient", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Makeup"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Strength;
                half _Ambient;
                float _UsePaintMask;
                float4x4 _WorldToPaint;
                float _PaintMask[144];
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light light = GetMainLight();
                half ndl = saturate(dot(normalize(input.normalWS), light.direction));
                half lighting = lerp(_Ambient, 1.0h, ndl);
                half3 rgb = _BaseColor.rgb * lighting * light.color;
                half mask = 1;
                if (_UsePaintMask > 0.5)
                {
                    float2 uv = mul(_WorldToPaint, float4(input.positionWS, 1)).xy + 0.5;
                    if (any(uv < 0) || any(uv >= 1)) mask = 0;
                    else
                    {
                        int2 cell = min((int2)(uv * 12), int2(11, 11));
                        mask = _PaintMask[cell.y * 12 + cell.x];
                    }
                }
                return half4(rgb, _BaseColor.a * input.color.a * _Strength * mask);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
