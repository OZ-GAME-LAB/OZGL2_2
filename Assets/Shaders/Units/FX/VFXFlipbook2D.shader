// 외부 그래프를 포함하지 않는 프로젝트 전용 2D 파티클 셰이더.
// 기존 머티리얼의 _Texture2D / _Color 이름과 파티클 UV0를 유지한다.
Shader "Units/FX/Flipbook2D"
{
    Properties
    {
        _Texture2D("Texture", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Texture2D);
            SAMPLER(sampler_Texture2D);
            CBUFFER_START(UnityPerMaterial)
                float4 _Texture2D_ST;
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _Texture2D);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sampled = SAMPLE_TEXTURE2D(_Texture2D, sampler_Texture2D, input.uv);
                // 검정 외곽선 머티리얼의 Color.a가 0인 기존 에셋도 유지한다.
                // 투명 영역은 텍스처 Alpha, 수명에 따른 페이드는 파티클 Alpha가 담당한다.
                return half4(sampled.rgb * _Color.rgb * input.color.rgb, sampled.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
