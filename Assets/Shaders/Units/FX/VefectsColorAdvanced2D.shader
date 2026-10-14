// 원본 프로퍼티 이름을 유지하여 공유 캐시의 머티리얼 교체에 사용한다.
Shader "Units/FX/VefectsColorAdvanced2D"
{
    Properties
    {
		_Emissive("Emissive", Float) = 1
		_HueShift("Hue Shift", Range( 0 , 1)) = 0
		_FlipbookX("Flipbook X", Float) = 7
		_FlipbookY("Flipbook Y", Float) = 1
		[Space(13)][Header(Main Texture)][Space(13)]_MainTexture("Main Texture", 2D) = "white" {}
		_UVS("UV S", Vector) = (1,1,0,0)
		_UVP("UV P", Vector) = (0,0,0,0)
		[Space(13)][Header(DisolveMapping)][Space(13)]_disolveMap("disolveMap", 2D) = "white" {}
		_DissolveMapScale("Dissolve Map Scale", Float) = 1
		[Space(13)][Header(Distortion)][Space(13)]_DistortionTexture("Distortion Texture", 2D) = "white" {}
		_DistortionLerp("Distortion Lerp", Range( 0 , 0.1)) = 0
		_UVDS("UV D S", Vector) = (1,1,0,0)
		_UVDP("UV D P", Vector) = (0.1,-0.2,0,0)
		[Space(33)][Header(Pixelate)][Space(13)][Toggle] _Pixelate("Pixelate", Float) = 0
		_PixelsMultiplier("Pixels Multiplier", Float) = 1
		_PixelsX("Pixels X", Float) = 32
		_PixelsY("Pixels Y", Float) = 32
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
            #define VEFECTS_ADVANCED
            #include "VefectsParticle2D.hlsl"
            ENDHLSL
        }
    }
}
