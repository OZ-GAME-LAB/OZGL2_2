// 원본 프로퍼티 이름을 유지하여 공유 캐시의 머티리얼 교체에 사용한다.
Shader "Units/FX/VefectsColor2D"
{
    Properties
    {
		_OverallTint("Overall Tint", Color) = (1,1,1,0)
		_HueShift("Hue Shift", Range( 0 , 1)) = 0
		_SaturationMultiply("Saturation Multiply", Float) = 1
		_Emissive("Emissive", Float) = 1
		[Space(13)][Header(Main Texture)][Space(13)]_MainTexture("Main Texture", 2D) = "white" {}
		[Space(13)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
	}
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_Src] [_Dst]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #define VEFECTS_COLOR
            #include "VefectsParticle2D.hlsl"
            ENDHLSL
        }
    }
}
