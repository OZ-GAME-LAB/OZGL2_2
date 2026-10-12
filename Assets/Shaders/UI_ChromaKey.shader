Shader "UI/ChromaKeyPrecise"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}

        _KeyColor ("Key Color", Color) = (0,1,0,1)

        _Threshold ("Key Threshold", Range(0.001, 0.5)) = 0.10
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.08

        _LumaWeight ("Brightness Protection", Range(0, 2)) = 0.6

        _Despill ("Edge Despill", Range(0, 1)) = 0.6
        _DespillRange ("Despill Range", Range(0.01, 1)) = 0.3

        _Color ("Tint", Color) = (1,1,1,1)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;

            float4 _KeyColor;
            float4 _Color;

            float _Threshold;
            float _Softness;
            float _LumaWeight;

            float _Despill;
            float _DespillRange;

            v2f vert(appdata_t v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;

                return o;
            }

            float3 RGBToYCbCr(float3 c)
            {
                float y = dot(c, float3(0.299, 0.587, 0.114));

                float cb = (c.b - y) * 0.564;
                float cr = (c.r - y) * 0.713;

                return float3(y, cb, cr);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                float3 src = RGBToYCbCr(col.rgb);
                float3 key = RGBToYCbCr(_KeyColor.rgb);

                // 색상 차이
                float chromaDist =
                    distance(src.yz, key.yz);

                // 밝기 차이
                float lumaDist =
                    abs(src.x - key.x);

                // 크로마키 색상 + 밝기 둘 다 비슷해야 제거
                float dist =
                    chromaDist +
                    lumaDist * _LumaWeight;

                float alpha =
                    smoothstep(
                        _Threshold,
                        _Threshold + _Softness,
                        dist
                    );

// -------------------------
// Chroma-distance based Despill
// -------------------------

// 키 색상에 가까운 영역 + 그 주변까지 despill 적용
float despillMask =
    1.0 - smoothstep(
        _Threshold,
        _Threshold + _Softness + _DespillRange,
        dist
    );

float maxRB = max(col.r, col.b);

// 주변 R/B보다 튀어나온 녹색 양
float greenSpill =
    max(0.0, col.g - maxRB);

// 실제 화면에 남아 있는 부분에만 의미 있게 적용
float visibleMask = smoothstep(0.02, 0.5, alpha);

float amount =
    saturate(despillMask * visibleMask * _Despill);

col.g = lerp(
    col.g,
    maxRB,
    amount
);

                col.a *= alpha;

                return col;
            }

            ENDCG
        }
    }
}