Shader "Game/Consumables/AreaIndicator"
{
    Properties
    {
        _FillColor ("Fill Color", Color) = (0.65882355, 0.27450982, 0.19607843, 0.12)
        _BorderColor ("Border Color", Color) = (0.8509804, 0.68235296, 0.40784314, 0.9)
        _Radius ("World Radius", Float) = 3
        _BorderWidth ("Border Width (World Units)", Float) = 0.06
        _Softness ("Edge Softness (World Units)", Float) = 0.025
        _DashCount ("Dash Count (0 = Solid)", Float) = 0
        _DashRatio ("Dash Fill Ratio", Range(0.05, 1)) = 0.7
        _RotationSpeed ("Rotation (Turns / Second)", Float) = 0.03
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.12
        _PulseSpeed ("Pulse (Cycles / Second)", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "AreaIndicator"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor, _BorderColor;
                float _Radius, _BorderWidth, _Softness, _DashCount, _DashRatio;
                float _RotationSpeed, _PulseStrength, _PulseSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = max(_Radius, 0.0001);
                float distanceFromCenter = length(p) * radius;
                float aa = max(fwidth(distanceFromCenter), max(_Softness, 0.0001));
                // Fade inward: the outer edge remains at the actual gameplay radius.
                float disk = 1.0 - smoothstep(radius - aa, radius, distanceFromCenter);
                float border = smoothstep(radius - _BorderWidth - aa,
                    radius - _BorderWidth, distanceFromCenter);
                if (_DashCount > 0.5)
                {
                    float angle = atan2(p.y, p.x) / 6.2831853 + 0.5;
                    float phase = frac((angle - _Time.y * _RotationSpeed) * _DashCount);
                    float dashAA = min(0.05, max(fwidth(phase), 0.001));
                    border *= smoothstep(0.0, dashAA, phase)
                        * (1.0 - smoothstep(_DashRatio - dashAA, _DashRatio, phase));
                }
                float pulse = 1.0 - _PulseStrength * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed * 6.2831853));
                // Compose the border over the fill without darkening translucent edges.
                float borderAlpha = border * _BorderColor.a;
                float fillAlpha = _FillColor.a * (1.0 - borderAlpha);
                float alpha = borderAlpha + fillAlpha;
                float3 color = (_BorderColor.rgb * borderAlpha + _FillColor.rgb * fillAlpha) / max(alpha, 0.0001);
                return half4(color, alpha * disk * pulse);
            }
            ENDHLSL
        }
    }
}
