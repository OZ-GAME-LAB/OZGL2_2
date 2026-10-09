// 원본 프로퍼티 이름을 유지하여 공유 캐시의 머티리얼 교체에 사용한다.
Shader "Units/FX/VefectsPalette2D"
{
    Properties
    {
		[Space(13)][Header(Main Texture)][Space(13)]_MainTexture("Main Texture", 2D) = "white" {}
		[HDR]_R("R", Color) = (1,0.9719134,0.5896226,0)
		[HDR]_G("G", Color) = (1,0.7230805,0.25,0)
		[HDR]_B("B", Color) = (0.5943396,0.259371,0.09812209,0)
		[HDR]_Outline("Outline", Color) = (0.2169811,0.03320287,0.02354041,0)
		_FlatColor("Flat Color", Range( 0 , 1)) = 0
		_Emissive("Emissive", Float) = 1
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
            #include "VefectsParticle2D.hlsl"
            ENDHLSL
        }
    }
}
