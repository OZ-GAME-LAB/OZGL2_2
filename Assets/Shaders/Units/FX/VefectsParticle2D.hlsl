// Vefects 머티리얼의 프로퍼티와 파티클 UV 배치를 유지하는 2D 공통 렌더링.
#ifndef UNITS_VEFECTS_PARTICLE_2D_INCLUDED
#define UNITS_VEFECTS_PARTICLE_2D_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_DistortionTexture);
SAMPLER(sampler_DistortionTexture);
TEXTURE2D(_disolveMap);
SAMPLER(sampler_disolveMap);

CBUFFER_START(UnityPerMaterial)
    float4 _R, _G, _B, _Outline, _OverallTint;
    float4 _UVS, _UVP, _UVDS, _UVDP;
    float _FlatColor, _Emissive, _HueShift, _SaturationMultiply;
    float _FlipbookX, _FlipbookY, _DissolveMapScale, _DistortionLerp;
    float _Pixelate, _PixelsMultiplier, _PixelsX, _PixelsY;
    float _Cull, _Src, _Dst, _ZWrite, _ZTest;
CBUFFER_END

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color : COLOR;
    // UV0.zw에는 디졸브 진행도와 경계 폭, UV1.x에는 무작위 오프셋이 전달된다.
    float4 uv : TEXCOORD0;
    float4 custom : TEXCOORD1;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
    float4 uv : TEXCOORD0;
    float randomOffset : TEXCOORD1;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.color = input.color;
    output.uv = input.uv;
    output.randomOffset = input.custom.x;
    return output;
}

float2 ResolveTextureUV(float2 uv)
{
#if defined(VEFECTS_ADVANCED)
    float2 noiseUV = uv * _UVDS.xy + _Time.y * _UVDP.xy;
    float2 noise = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture, noiseUV).rg;
    uv = uv * _UVS.xy + _Time.y * _UVP.xy + (noise * 2.0 - 1.0) * _DistortionLerp;
    // 머티리얼 값으로 분기하여 런타임 셰이더 교체와 빌드의 키워드 제거에 영향을 받지 않는다.
    if (_Pixelate > 0.5)
    {
        float2 resolution = max(abs(float2(_PixelsX, _PixelsY) * _PixelsMultiplier), 0.0001);
        uv = trunc(uv * resolution) / resolution;
    }
#endif
    return uv;
}

float ResolveDissolve(Varyings input)
{
#if defined(VEFECTS_ADVANCED)
    float offset = _FlipbookX;
    #if defined(VEFECTS_COLOR)
        offset = input.randomOffset;
    #endif
    float2 uv = input.uv.xy * float2(_FlipbookX, _FlipbookY) * _DissolveMapScale + offset;
    float mask = SAMPLE_TEXTURE2D(_disolveMap, sampler_disolveMap, uv).g;
    float width = input.uv.w;
    float edge = lerp(width - 1.0, 1.0, input.uv.z);
    // 폭이 0인 입력도 NaN 없이 단단한 경계로 처리한다.
    if (abs(width) < 0.00001)
        return step(edge, mask);
    return smoothstep(edge, edge + width, mask);
#else
    return 1.0;
#endif
}

float4 Frag(Varyings input) : SV_Target
{
    float4 sampled = SAMPLE_TEXTURE2D(_MainTexture, sampler_MainTexture, ResolveTextureUV(input.uv.xy));
    float3 color;
#if defined(VEFECTS_COLOR)
    float3 hsv = RgbToHsv(sampled.rgb);
    hsv.x += _HueShift;
    #if !defined(VEFECTS_ADVANCED)
        hsv.y *= _SaturationMultiply;
    #endif
    color = HsvToRgb(hsv) * input.color.rgb;
    #if !defined(VEFECTS_ADVANCED)
        color *= _OverallTint.rgb;
    #endif
#else
    float3 palette = lerp(_Outline.rgb, _B.rgb, sampled.b);
    palette = lerp(palette, _G.rgb, sampled.g);
    palette = lerp(palette, _R.rgb, sampled.r);
    color = lerp(palette * input.color.rgb, input.color.rgb, _FlatColor);
#endif
    // 색상 프로퍼티의 Alpha 대신 텍스처·파티클·디졸브로 투명도를 계산한다.
    return float4(color * _Emissive, sampled.a * input.color.a * ResolveDissolve(input));
}
#endif
