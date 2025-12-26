Shader "Stone/ChunkLightOverlay"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)

        _LightTex ("Light Texture (18x18)", 2D) = "black" {}
        _AlphaMul ("Alpha Multiplier", Range(0,2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Sprite"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            sampler2D _LightTex;
            float4 _LightTex_TexelSize; // x=1/w, y=1/h, z=w, w=h
            float _AlphaMul;

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // keep SpriteRenderer compatibility
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                // Map overlay UV(0..1) -> LightTex(18x18) central 16x16 region.
                // tc = (uv * 16 + 1) / 18
                float2 tc = (i.uv * 16.0 + 1.0) / 18.0;

                // 3x3 box filter in texel space (Point texture recommended)
                float2 ts = _LightTex_TexelSize.xy; // (1/18, 1/18)
                float aSum = 0.0;

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float2 uv2 = tc + float2(ox, oy) * ts;
                        aSum += tex2D(_LightTex, uv2).a;
                    }
                }

                float a = (aSum / 9.0) * _AlphaMul;
                fixed outA = saturate(a);

                return fixed4(0, 0, 0, outA);
            }
            ENDCG
        }
    }
}
