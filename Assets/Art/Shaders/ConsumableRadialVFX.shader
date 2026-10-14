Shader "Game/Consumables/PixelRadialVFX"
{
    Properties
    {
        // 0 Explosion, 1 Shockwave, 2 Heal, 3 Mist, 4 Shield, 5 Rune,
        // 6 Haste, 7 War pulse, 8 Reticle, 9 Pierce, 10 Wind,
        // 11 Weakness, 12 Shatter, 13 Tar, 14 Seal, 15 Poison,
        // 16 Net, 17 Lightning, 18 Frost, 19 Dread, 20 Stun.
        // 21 Floating healing emblem.
        // 22 Shield, 23 Sword, 24 Haste, 25 Reticle, 26 Pierce, 27 Wing,
        // 28 Broken sword, 29 Broken shield, 30 Slow, 31 Seal, 32 Poison,
        // 33 Chains, 34 Frost, 35 Dread, 36 Stun, 37 Physical, 38 Magic, 39 Lightning.
        [IntRange] _Style ("Effect Style (0-39)", Range(0,39)) = 0
        _Progress ("Animation Progress", Range(0,1)) = 0.35
        _Pixels ("Pixels Across Diameter", Range(24,192)) = 96
        _HotColor ("Flash Color", Color) = (1,0.95,0.65,1)
        _MainColor ("Main Color", Color) = (1,0.45,0.08,1)
        _TailColor ("Ember / Dust Color", Color) = (0.7,0.12,0.03,1)
        _Opacity ("Opacity", Range(0,1)) = 0.9
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
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
                float4 _HotColor, _MainColor, _TailColor;
                float _Style, _Progress, _Pixels, _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Cross(float2 p, float arm, float reach)
            {
                float2 q = abs(p);
                return max(step(q.x,arm)*step(q.y,reach),step(q.y,arm)*step(q.x,reach));
            }
            float Line(float2 p, float2 a, float2 b, float width)
            {
                // Keep thin strokes visible after UV pixel quantization.
                width = max(width, 1.5 / max(24.0, _Pixels));
                float2 ab = b-a;
                float2 q = p-a-ab*saturate(dot(p-a,ab)/max(dot(ab,ab),0.0001));
                return 1.0-step(width,length(q));
            }
            float Emblem(float2 q, int style)
            {
                float2 a=abs(q);
                float shape=0;
                if (style==22 || style==29)
                {
                    // Solid shield with a pointed lower edge.
                    shape=step(a.x,0.36)*step(q.y,0.42)*step(-0.46,q.y)*step(a.x,-q.y*-0.8+0.4);
                    if (style==29) shape*=step(0.075,abs(q.x-0.10*sin(q.y*13.0)));
                }
                else if (style==23 || style==26 || style==28)
                {
                    float2 v=float2(q.x+q.y,q.y-q.x)*0.7071;
                    shape=step(abs(v.x),0.095)*step(-0.15,v.y)*step(v.y,0.48-abs(v.x));
                    shape=max(shape,step(abs(v.y+0.17),0.05)*step(abs(v.x),0.24));
                    shape=max(shape,step(abs(v.x),0.055)*step(abs(v.y+0.31),0.13));
                    if (style==28) shape*=step(0.075,abs(v.y-0.1));
                    if (style==26) shape=max(shape,Line(q,float2(-0.3,-0.38),float2(0.35,0.32),0.05));
                }
                else if (style==24 || style==35)
                {
                    if (style==35) q.y=-q.y;
                    shape=max(step(abs(q.y-(0.34-abs(q.x))),0.065),
                        step(abs(q.y-(-0.03-abs(q.x))),0.065))*step(abs(q.x),0.35);
                }
                else if (style==25 || style==31)
                {
                    shape=step(abs(length(q)-0.36),0.06);
                    if (style==25) shape=max(shape,Cross(q,0.045,0.52)*step(0.22,length(q)));
                    else shape=max(shape,max(Line(q,float2(-0.2,-0.2),float2(0.2,0.2),0.05),
                        Line(q,float2(-0.2,0.2),float2(0.2,-0.2),0.05)));
                }
                else if (style==27)
                {
                    // Three filled feathers on each side.
                    [unroll] for(int k=0;k<3;k++)
                        shape=max(shape,Line(float2(abs(q.x),q.y),float2(0.03,-0.22+k*0.12),float2(0.44-k*0.06,0.1+k*0.16),0.065));
                }
                else if (style==30)
                {
                    // Boot with a weight underneath: movement restriction.
                    shape=max(step(abs(q.x+0.09),0.14)*step(abs(q.y-0.07),0.29),
                        step(abs(q.x-0.08),0.31)*step(abs(q.y+0.2),0.09));
                    shape=max(shape,step(abs(q.y+0.4),0.045)*step(abs(q.x),0.36));
                }
                else if (style==32)
                {
                    shape=max(step(length(q-float2(0,-0.14)),0.3),
                        step(a.x,(0.47-q.y)*0.5)*step(-0.05,q.y)*step(q.y,0.47));
                    // Two eye cutouts make the droplet read as poison.
                    shape*=1.0-max(step(length(q-float2(-0.11,-0.12)),0.065),step(length(q-float2(0.11,-0.12)),0.065));
                }
                else if (style==33)
                {
                    float2 v=float2(q.x+q.y,q.y-q.x)*0.7071;
                    shape=max(step(abs(length(float2(v.x*0.8,(v.y-0.18)*1.4))-0.21),0.065),
                        step(abs(length(float2(v.x*0.8,(v.y+0.18)*1.4))-0.21),0.065));
                }
                else if (style==34)
                {
                    [unroll] for(int k=0;k<3;k++)
                    {
                        float angle=k*1.047198;
                        float2 d=float2(cos(angle),sin(angle));
                        shape=max(shape,Line(q,-d*0.45,d*0.45,0.05));
                    }
                    shape=max(shape,step(abs(a.x+a.y-0.28),0.055));
                }
                else if (style==36)
                {
                    float angle=atan2(q.y,q.x);
                    shape=step(length(q),0.27+0.15*cos(angle*5.0-1.5708));
                }
                return shape;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float pixels = max(24.0, _Pixels);
                float2 p = ((floor(input.uv * pixels) + 0.5) / pixels - 0.5) * 2.0;
                float r = length(p);
                float t = saturate(_Progress);
                if (_Style > 21.5)
                {
                    int style=(int)round(_Style);
                    float pop=lerp(0.65,1.0,smoothstep(0.0,0.12,t));
                    float2 q=p/pop;
                    float body=0, outline=0;
                    float fade=smoothstep(0.0,0.06,t)*(1.0-smoothstep(0.72,1.0,t));
                    float3 color;
                    if (style>=37)
                    {
                        // Short body impacts rather than status emblems.
                        float angle=atan2(p.y,p.x);
                        float rr=lerp(0.2,0.75,t);
                        float jag=style==37 ? 0.08*cos(angle*9.0):0.22*cos(angle*4.0);
                        body=step(r,rr+jag)*step(rr*max(0.0,t-0.2),r);
                        if (style==39)
                        {
                            body=max(Line(p,float2(0.2,0.9),float2(-0.14,0.1),0.07),
                                max(Line(p,float2(-0.14,0.1),float2(0.15,0.17),0.07),
                                Line(p,float2(0.15,0.17),float2(-0.12,-0.5),0.07)));
                        }
                        fade=1.0-smoothstep(0.25,1.0,t);
                        color=lerp(_HotColor.rgb,_MainColor.rgb,saturate(r*1.2+t*0.5));
                        return half4(color,body*fade*_Opacity*step(r,0.96));
                    }
                    body=Emblem(q,style);
                    float edge=2.0/pixels;
                    outline=max(body,max(max(Emblem(q+float2(edge,0),style),Emblem(q-float2(edge,0),style)),
                        max(Emblem(q+float2(0,edge),style),Emblem(q-float2(0,edge),style))));
                    float highlight=body*Emblem(q+float2(0.045,-0.045),style);
                    color=lerp(float3(0.10,0.08,0.10),_MainColor.rgb,body);
                    color=lerp(color,_HotColor.rgb,highlight*0.8);
                    float specks=0;
                    [unroll] for(int k=0;k<4;k++)
                    {
                        float side=(k%2==0) ? -1.0:1.0;
                        float2 center=float2(side*0.65,-0.5+k*0.26+t*0.3);
                        specks=max(specks,step(length(p-center),0.045));
                    }
                    if(specks>outline) color=_HotColor.rgb;
                    return half4(color,max(outline,specks)*fade*_Opacity*step(r,1.0));
                }
                if (_Style > 20.5)
                {
                    // Opaque pixel silhouette reads clearly against both bright and dark battlefields.
                    float pop = lerp(0.65,1.0,smoothstep(0.0,0.12,t));
                    float2 q = p/pop;
                    float outline = Cross(q,0.23,0.51);
                    float body = Cross(q,0.16,0.44);
                    float inset = Cross(q+float2(0.025,-0.025),0.10,0.36);
                    float fade = smoothstep(0.0,0.06,t)*(1.0-smoothstep(0.72,1.0,t));
                    float3 color = lerp(float3(0.09,0.16,0.11),_MainColor.rgb,body);
                    color = lerp(color,_HotColor.rgb,inset);
                    float specks = 0;
                    [unroll] for (int j=0;j<4;j++)
                    {
                        float side = (j % 2 == 0) ? -1.0:1.0;
                        float2 center = float2(side*(0.62+0.04*sin(t*6.0+j)),
                            -0.55+j*0.27+t*0.35);
                        float2 delta = p-center;
                        specks = max(specks,Cross(delta,0.025,0.075));
                    }
                    if (specks > outline) color = _HotColor.rgb;
                    return half4(color,max(outline,specks)*fade*_Opacity*step(length(p),1.0));
                }
                float angle = atan2(p.y,p.x);
                float expansion = 1.0 - pow(1.0-t, 2.5);
                float radius = lerp(0.035,0.91,expansion);
                float flash = step(r,0.16*(1.0-t)) * (1.0-smoothstep(0.04,0.22,t));
                float alpha = 0;
                float heat = 0;
                if (_Style < 0.5)
                {
                    // Flame clusters expand around the circumference, leaving the center clear.
                    float lobes = sin(angle*11.0 + 0.7*sin(angle*5.0));
                    float noise = Hash(floor(p*38.0));
                    float rim = radius + lobes*0.07*sin(t*3.14159);
                    float width = lerp(0.17,0.015,t);
                    float band = saturate(1.0-abs(r-rim)/width);
                    band *= step(0.23 + t*0.52, noise*0.5 + lobes*0.25+0.5);
                    alpha = band * (1.0-smoothstep(0.42,1.0,t));
                    heat = floor(saturate(band*1.2-t*0.7)*4.0)/4.0;
                }
                else if (_Style < 1.5)
                {
                    float jagged = sin(angle*23.0)*0.009 + sin(angle*41.0)*0.006;
                    float width = lerp(0.035,0.012,t);
                    float ring = step(abs(r-radius-jagged),width);
                    float gaps = step(t*0.65,Hash(float2(floor(angle*30.0),3.0)));
                    alpha = ring*gaps*(1.0-smoothstep(0.45,1.0,t));
                    heat = 0.65;
                }
                else
                {
                    float stroke = max(0.025, 1.5 / pixels);
                    float fade = smoothstep(0.0,0.08,t)*(1.0-smoothstep(0.4,1.0,t));
                    float ring = step(abs(r-radius),stroke);
                    float motif = 0;
                    float detail = 0;
                    int style = (int)round(_Style);
                    flash = 0;
                    if (style == 2) // Healing: expanding halo and rising crosses.
                    {
                        motif = ring*0.55;
                        [unroll] for (int j=0;j<6;j++)
                        {
                            float h=Hash(float2(j,12));
                            float2 q=p-float2((h-0.5)*1.25,-0.5+t*0.9+Hash(float2(j,9))*0.55);
                            detail=max(detail,max(step(abs(q.x),stroke)*step(abs(q.y),0.1),
                                step(abs(q.y),stroke)*step(abs(q.x),0.1)));
                        }
                    }
                    else if (style == 3 || style == 11 || style == 15) // Incense, weakness and poison.
                    {
                        [unroll] for (int j=0;j<9;j++)
                        {
                            float h=Hash(float2(j,31));
                            float a=j*2.39996;
                            float2 c=float2(cos(a),sin(a))*(0.15+expansion*0.5);
                            c.y+=t*0.15*(style==11 ? -1.0:1.0);
                            float d=length(p-c);
                            float size=0.08+0.12*h+0.09*sin(t*3.14159);
                            motif=max(motif,step(d,size)*0.45);
                            if (style==15) detail=max(detail,step(abs(d-size*0.6),0.018));
                        }
                        if (style==11)
                            detail=max(Line(p,float2(-0.14,0.15),float2(0,-0.05),0.025),
                                Line(p,float2(0.14,0.15),float2(0,-0.05),0.025));
                    }
                    else if (style == 4 || style == 12) // Shield outline, or cracked armor.
                    {
                        float2 q=p/lerp(0.65,1.0,expansion);
                        if (style==12) q.x-=sign(q.x)*t*0.2;
                        motif=max(Line(q,float2(-0.38,0.38),float2(0.38,0.38),0.035),
                            max(Line(q,float2(-0.38,0.38),float2(-0.3,-0.15),0.035),
                            Line(q,float2(0.38,0.38),float2(0.3,-0.15),0.035)));
                        motif=max(motif,max(Line(q,float2(-0.3,-0.15),float2(0,-0.45),0.035),
                            Line(q,float2(0.3,-0.15),float2(0,-0.45),0.035)));
                        if (style==4) detail=Line(q,float2(0,-0.15),float2(0,0.2),0.025);
                        else detail=max(Line(q,float2(0.05,0.35),float2(-0.09,0),0.025),
                            Line(q,float2(-0.09,0),float2(0.12,-0.32),0.025));
                        motif=max(motif,ring*0.35);
                    }
                    else if (style == 5 || style == 14) // Ward and silence seal.
                    {
                        float rr=lerp(0.2,0.7,expansion);
                        motif=step(abs(r-rr),stroke);
                        motif=max(motif,step(abs(r-rr*0.75),stroke)*step(0.3,sin(angle*8.0)));
                        if (style==14)
                            detail=max(Line(p,float2(-0.3,-0.3),float2(0.3,0.3),0.035),
                                Line(p,float2(-0.3,0.3),float2(0.3,-0.3),0.035));
                        else detail=step(abs(abs(p.x)+abs(p.y)-rr*0.8),stroke);
                    }
                    else if (style == 6 || style == 9 || style == 19) // Speed, piercing rays, descending dread.
                    {
                        [unroll] for (int j=0;j<5;j++)
                        {
                            float h=Hash(float2(j,4));
                            float x=(j-2)*0.23;
                            float y=lerp(-0.65,0.5,t)+(h-0.5)*0.25;
                            if (style==19) y=-y;
                            float2 a=float2(x,y), b=a+float2(style==9 ? 0.25:0,0.23);
                            motif=max(motif,Line(p,a,b,0.018+0.012*h));
                        }
                        detail=ring*0.35;
                    }
                    else if (style == 7) // Two drum beats.
                    {
                        float beat=frac(t*2.0);
                        motif=step(abs(r-lerp(0.12,0.88,beat)),0.04)*(1.0-beat);
                        detail=step(abs(abs(p.x)+abs(p.y)-0.27),0.03)*(1.0-t);
                    }
                    else if (style == 8) // Hunter reticle.
                    {
                        float rr=lerp(0.7,0.35,expansion);
                        motif=step(abs(r-rr),stroke);
                        detail=max(step(abs(p.x),stroke),step(abs(p.y),stroke))*step(rr-0.1,r)*step(r,rr+0.15);
                    }
                    else if (style == 10) // Broken wind spirals.
                    {
                        float spiral=frac(angle/6.28318*3.0-r*0.8+t*1.5);
                        motif=step(spiral,0.12)*step(0.18,r)*step(r,0.8)*(1.0-r*0.6);
                        detail=ring*0.2;
                    }
                    else if (style == 13) // Tar splash, immediately fades (not a persistent ground zone).
                    {
                        float edge=radius*(0.72+0.14*sin(angle*9.0)+0.08*cos(angle*13.0));
                        motif=step(r,edge)*0.55;
                        detail=step(abs(r-edge),0.025)*0.6;
                    }
                    else if (style == 16) // Net contracts around the target.
                    {
                        float limit=lerp(0.9,0.48,expansion);
                        float2 grid=abs(frac(float2(p.x+p.y,p.x-p.y)*4.0)-0.5);
                        motif=step(min(grid.x,grid.y),0.055)*step(r,limit)*0.8;
                        detail=step(abs(r-limit),0.03);
                    }
                    else if (style == 17) // Lightning bolt and impact ring.
                    {
                        float bolt=max(Line(p,float2(0.16,0.9),float2(-0.14,0.27),0.035),
                            max(Line(p,float2(-0.14,0.27),float2(0.12,0.3),0.035),
                            Line(p,float2(0.12,0.3),float2(-0.06,-0.4),0.035)));
                        detail=bolt*(1.0-smoothstep(0.25,0.55,t));
                        motif=step(abs(length(p-float2(-0.06,-0.4))-expansion*0.38),0.025)*0.6;
                    }
                    else if (style == 18) // Six-sided frost crystal.
                    {
                        [unroll] for (int j=0;j<6;j++)
                        {
                            float a=j*1.047198;
                            float2 d=float2(cos(a),sin(a));
                            float2 side=float2(-d.y,d.x);
                            motif=max(motif,Line(p,float2(0,0),d*expansion*0.7,0.022));
                            motif=max(motif,max(Line(p,d*0.36,(d*0.53+side*0.13)*expansion,0.018),
                                Line(p,d*0.36,(d*0.53-side*0.13)*expansion,0.018)));
                        }
                        detail=ring*0.25;
                    }
                    else if (style == 20) // Small orbiting stun stars.
                    {
                        [unroll] for (int j=0;j<3;j++)
                        {
                            float a=j*2.0944+t*4.0;
                            float2 q=p-float2(cos(a)*0.5,0.35+sin(a)*0.18);
                            detail=max(detail,step(abs(q.x)+abs(q.y),0.12));
                        }
                    }
                    alpha=max(motif,detail)*fade;
                    heat=detail>motif ? 0.95:0.4;
                }
                // Stable particle directions avoid random flicker between frames.
                float particles = 0;
                [unroll] for (int i=0;i<20;i++)
                {
                    float h = Hash(float2(i,7));
                    float a = i*2.399963 + h*0.2;
                    float pr = min(0.96, expansion*(0.65+h*0.3));
                    float2 center = float2(cos(a),sin(a))*pr;
                    float2 delta = abs(p-center);
                    float size = (1.0 + step(0.75,h)) / pixels;
                    particles = max(particles,step(max(delta.x,delta.y),size));
                }
                particles *= (1.0-step(1.5,_Style))*smoothstep(0.08,0.2,t)*(1.0-smoothstep(0.45,1.0,t));
                float3 color = heat < 0.5 ? lerp(_TailColor.rgb,_MainColor.rgb,heat*2.0)
                    : lerp(_MainColor.rgb,_HotColor.rgb,(heat-0.5)*2.0);
                if (particles > alpha) { color = lerp(_MainColor.rgb,_TailColor.rgb,t); alpha=particles; }
                if (flash > alpha) { color=_HotColor.rgb; alpha=flash; }
                return half4(color,alpha*_Opacity*step(r,1.0)*(1.0-step(1.0,t)));
            }
            ENDHLSL
        }
    }
}
