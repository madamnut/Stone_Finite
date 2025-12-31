Shader "Stone/FX/FireGlow2D"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)

        _GlowColor ("Glow Color", Color) = (1,0.6,0.2,1)
        _GlowStrength ("Glow Strength", Range(0,5)) = 1.5
        _CoreStrength ("Core Strength", Range(0,5)) = 1.0

        _FlickerSpeed ("Flicker Speed", Range(0,20)) = 8
        _FlickerAmount ("Flicker Amount", Range(0,2)) = 0.35

        _EdgeSoftness ("Edge Softness", Range(0,2)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        // 더 “번쩍이는” 불을 원하면 아래로 교체:
        // Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Tint;
            fixed4 _GlowColor;
            float _GlowStrength;
            float _CoreStrength;

            float _FlickerSpeed;
            float _FlickerAmount;
            float _EdgeSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // 매우 가벼운 해시 노이즈(텍스처 없이 flicker 만들기)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // 스프라이트 알파(불 모양)
                float a = tex.a * i.color.a * _Tint.a;

                // 알파가 거의 없으면 조기 리턴(오버드로우 감소)
                if (a <= 0.001) return 0;

                // 기본 색
                fixed3 baseCol = tex.rgb * i.color.rgb * _Tint.rgb;

                // UV 기반 “중심/가장자리” 마스크 (스프라이트 중앙이 더 뜨겁게)
                float2 uv = i.uv;
                float2 centered = (uv - 0.5) * 2.0;         // -1..1
                float r = length(centered);                 // 0..~1.4
                float core = saturate(1.0 - r);             // 중심 1, 가장자리 0
                core = pow(core, 1.5);                      // 중심 강조

                // 가장자리 부드러움(알파가 낮은 영역을 더 빛나게)
                float edge = saturate((1.0 - a) * (1.0 / max(0.001, _EdgeSoftness)));
                edge = pow(edge, 1.2);

                // 깜빡임(시간 + uv) : 0.7~1.3 정도 변동
                float t = _Time.y * _FlickerSpeed;
                float n = hash21(float2(floor(uv.x * 24.0), floor(uv.y * 24.0)) + t);
                float flicker = 1.0 + (n - 0.5) * 2.0 * _FlickerAmount;

                // 발광 성분
                float glowMask = saturate(core * _CoreStrength + edge * _GlowStrength);
                fixed3 glow = _GlowColor.rgb * glowMask * flicker;

                // 최종: 기본색 + 발광 (불은 과밝게 보이도록)
                fixed3 outCol = baseCol + glow;

                // 알파는 원본 기반 유지(필요하면 flicker를 알파에도 살짝 반영 가능)
                return fixed4(outCol, a);
            }
            ENDCG
        }
    }
}
