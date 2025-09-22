Shader "Custom/Godray2D_Radial_SingleTex"
{
    Properties
    {
        _Texture   ("Mask Texture", 2D) = "white" {}
        _Alpha     ("Mask Alpha",   Float) = 1.0
        _Center    ("Sun Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _StartT    ("Sampling Start (0~1)", Range(0,1)) = 0.0
        _EndT      ("Sampling End (0~1)",   Range(0,1)) = 1.0
        _Intensity ("Intensity", Float) = 1.0
        _Tint      ("Tint", Color) = (1, 1, 0.85, 1)
        _Samples   ("Samples", Range(8,128)) = 32
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            ZWrite Off
            ZTest Always
            // 필요에 따라:
            // 가산합성(빛 느낌 강함):   Blend One One
            // 알파합성(부드럽게):      Blend SrcAlpha OneMinusSrcAlpha
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex   vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Texture;
            float     _Alpha;
            float4    _Center;
            float     _StartT, _EndT;
            float     _Intensity;
            float4    _Tint;
            float     _Samples;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // 샘플 수 가드 + 정수 루프
                int   count  = max(1, (int)_Samples);
                float stepT  = 1.0 / count;

                float startT = saturate(_StartT);
                float endT   = saturate(_EndT);

                float sum = 0.0;

                [loop]
                for (int s = 0; s < 128; s++)
                {
                    if (s >= count) break;

                    float t = s * stepT;                    // 0..1
                    float sampleT = lerp(startT, endT, t);  // 구간 제한
                    float2 sampleUV = lerp(_Center.xy, uv, sampleT);

                    // 마스크: 알파 사용
                    float m = tex2D(_Texture, sampleUV).a * _Alpha;
                    float falloff = 1.0 - t;                // 중심 가중치
                    sum += saturate(m) * falloff;
                }

                sum /= count;

                fixed4 col = _Tint * (sum * _Intensity);
                col.a = sum * _Intensity;
                return col;
            }
            ENDCG
        }
    }
}
