Shader "Custom/Liquid_WaterSprite_WithDistort_Builtin"
{
    Properties
    {
        _MainTex ("Water Sprite Texture", 2D) = "white" {}
        _TypeTex ("Type Texture (16x16)", 2D) = "black" {}
        _AmountTex ("Amount Texture (16x16)", 2D) = "black" {}

        _ChunkOriginWS ("Chunk Origin WS (x,y)", Vector) = (0,0,0,0)
        _ChunkSize ("Chunk Size", Float) = 16

        _Alpha ("Water Alpha", Range(0,1)) = 0.55

        _WaveScale ("Wave Scale", Float) = 6.0
        _WaveSpeed ("Wave Speed", Float) = 1.5
        _DistortStrength ("Distortion Strength (pixels-ish)", Float) = 2.0

        _BgMix ("Background Distort Mix", Range(0,1)) = 0.75
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
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        GrabPass { "_GrabTex" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _TypeTex;
            sampler2D _AmountTex;
            sampler2D _GrabTex;
            float4 _GrabTex_TexelSize;

            float4 _MainTex_ST;
            float4 _ChunkOriginWS;
            float  _ChunkSize;

            float  _Alpha;
            float  _WaveScale;
            float  _WaveSpeed;
            float  _DistortStrength;
            float  _BgMix;

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
                float2 worldXY  : TEXCOORD1;
                float4 grabPos  : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;

                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldXY = wp.xy;

                o.grabPos = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 물 스프라이트(그대로 사용)
                fixed4 water = tex2D(_MainTex, i.uv) * i.color;
                if (water.a <= 0.001) return 0;

                // 청크 로컬 셀 계산
                float2 local = i.worldXY - _ChunkOriginWS.xy;
                float2 cellF = floor(local);

                // 청크 밖이면 그냥 스프라이트 반투명(레벨 영향 없음)
                if (cellF.x < 0 || cellF.y < 0 || cellF.x >= _ChunkSize || cellF.y >= _ChunkSize)
                {
                    fixed4 o0;
                    o0.a = _Alpha * water.a;
                    o0.rgb = water.rgb * o0.a;
                    return o0;
                }

                float2 uvMask = (cellF + 0.5) / _ChunkSize;

                int type = (int)round(tex2D(_TypeTex, uvMask).r * 255.0);
                float amount = tex2D(_AmountTex, uvMask).r * 255.0; // (실제 0..128)
                float amt01 = saturate(amount / 128.0);

                // 유체 없으면 그냥 스프라이트 반투명(레벨 영향 없음)
                if (type == 0 || amt01 <= 0.0001)
                {
                    fixed4 o1;
                    o1.a = _Alpha * water.a;
                    o1.rgb = water.rgb * o1.a;
                    return o1;
                }

                // 현재는 물(type==1)만 왜곡 적용
                if (type != 1)
                {
                    fixed4 o2;
                    o2.a = _Alpha * water.a;
                    o2.rgb = water.rgb * o2.a;
                    return o2;
                }

                // Grab UV (0..1)
                float2 grabUV = i.grabPos.xy / i.grabPos.w;

                // GrabTex Y 플립 보정(플랫폼별 미러링 방지)
                #if UNITY_UV_STARTS_AT_TOP
                if (_GrabTex_TexelSize.y < 0)
                    grabUV.y = 1.0 - grabUV.y;
                #endif

                // 일렁임(왜곡) 오프셋
                float time = _Time.y * _WaveSpeed;

                float w1 = sin((i.worldXY.x + time) * _WaveScale);
                float w2 = sin((i.worldXY.y - time * 1.23) * (_WaveScale * 0.9));
                float w3 = sin((i.worldXY.x + i.worldXY.y + time * 0.7) * (_WaveScale * 0.6));

                float2 wave = float2(w1 + w3, w2 - w3) * 0.5;

                // amount는 "얼마나 차있나" 표현용이므로
                // ✅ 투명도에는 절대 관여하지 않음
                // (왜곡 강도에만 약하게 반영해도 되고, 완전 고정해도 됨)
                float strength = _DistortStrength * (0.25 + 0.75 * amt01);

                float2 screenUVScale = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                float2 uvOffset = wave * strength * screenUVScale;

                fixed3 bg = tex2D(_GrabTex, saturate(grabUV + uvOffset)).rgb;

                // 배경(왜곡된 것) + 물 스프라이트(그대로) 합성
                // ✅ 레벨 영향 없는 고정 투명도
                float outA = _Alpha * water.a;

                fixed3 rgb = lerp(water.rgb, bg, _BgMix);

                fixed4 outCol;
                outCol.a = outA;
                outCol.rgb = rgb * outCol.a; // premultiply
                return outCol;
            }
            ENDCG
        }
    }
}
