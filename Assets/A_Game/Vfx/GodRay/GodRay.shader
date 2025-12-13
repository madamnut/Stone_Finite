Shader "Custom/Godray2D_Radial_Position_StripesOnRayOnly"
{
    Properties
    {
        _TextureBG ("BG Mask Texture", 2D) = "white" {}
        _TextureFG ("FG Mask Texture", 2D) = "white" {}

        _Alpha     ("Mask Alpha", Float) = 1.0
        _Center    ("Sun Center (UV)", Vector) = (0.5, 0.5, 0, 0)

        _StartT    ("Sampling Start", Range(0,1)) = 0.0
        _EndT      ("Sampling End",   Range(0,1)) = 1.0

        _Intensity ("Intensity", Float) = 1.0
        _Tint      ("Tint", Color) = (1,1,0.85,1)
        _Samples   ("Samples", Range(8,128)) = 32
        _Mix       ("FG Mix", Range(0,1)) = 0.5

        // Stripes (ray-only modulation)
        _StripeStrength ("Stripe Strength", Range(0,2)) = 0.6
        _StripeFreq     ("Stripe Frequency", Range(1,200)) = 60
        _StripeSpeed    ("Stripe Speed", Range(-10,10)) = 1.5
        _StripePower    ("Stripe Sharpness", Range(0.5,8)) = 2.5

        _NoiseStrength ("Stripe Noise Strength", Range(0,2)) = 0.25
        _NoiseFreq     ("Stripe Noise Frequency", Range(0,200)) = 35
        _NoiseSpeed    ("Stripe Noise Speed", Range(-10,10)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex   vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TextureBG;
            sampler2D _TextureFG;

            float     _Alpha;
            float4    _Center;
            float     _StartT, _EndT;
            float     _Intensity;
            float4    _Tint;
            float     _Samples;
            float     _Mix;

            float _StripeStrength;
            float _StripeFreq;
            float _StripeSpeed;
            float _StripePower;

            float _NoiseStrength;
            float _NoiseFreq;
            float _NoiseSpeed;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                int count = max(1, (int)_Samples);
                float stepT = 1.0 / count;

                float startT = saturate(_StartT);
                float endT   = saturate(_EndT);

                float sum = 0.0;

                float time = _Time.y;
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);

                [loop]
                for (int s = 0; s < 128; s++)
                {
                    if (s >= count) break;

                    float t = s * stepT;
                    float sampleT = lerp(startT, endT, t);

                    // 태양 중심 → 현재 픽셀 방향으로 샘플
                    float2 sampleUV = lerp(_Center.xy, uv, sampleT);

                    // 마스크 샘플
                    float bg = tex2D(_TextureBG, sampleUV).a;
                    float fg = tex2D(_TextureFG, sampleUV).a;
                    float m  = lerp(bg, fg, saturate(_Mix)) * _Alpha;

                    float falloff = 1.0 - t;

                    // ===== "기존 갓레이 샘플 기여도" =====
                    float base = saturate(m) * falloff;

                    // base가 0이면, 어떤 효과도 추가되지 않음 (요구사항)
                    // ===== stripes (각도 기반, 직선 유지) =====
                    float2 d = sampleUV - _Center.xy;
                    d.x *= aspect;

                    float ang = atan2(d.y, d.x);

                    float n = noise2(float2(ang * _NoiseFreq, time * _NoiseSpeed));
                    float wobble = (n - 0.5) * _NoiseStrength * 6.2831853;

                    float phase = ang * _StripeFreq + time * _StripeSpeed + wobble;

                    float stripe01 = 0.5 + 0.5 * sin(phase);                 // 0..1
                    stripe01 = pow(saturate(stripe01), _StripePower);          // sharpen

                    // 0..1 -> -1..1 진동값 (추가/감쇠용)
                    float osc = (stripe01 * 2.0) - 1.0;

                    // ===== 누적: base에만 변조가 걸림 =====
                    // strength=0이면 원래 base 그대로
                    float contrib = base + base * osc * _StripeStrength;

                    sum += contrib;
                }

                sum /= count;

                // 과도/음수 방지 (특히 osc가 음수일 때)
                sum = saturate(sum);

                float v = sum * _Intensity;

                fixed4 col = _Tint * v;
                col.a = v;
                return col;
            }
            ENDCG
        }
    }
}
