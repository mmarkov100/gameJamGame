Shader "Sonar/UnlitRing"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend One One // аддитив
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
            };

            float4 _BaseColor;
            float _Alpha;

            // Глобали от SonarController
            float4 _SonarOrigin;   // xyz
            float  _SonarRadius;
            float  _SonarWidth;
            float  _SonarTail;
            float4 _SonarColor;    // rgba
            float  _SonarIntensity;
            float  _WorldDarkness; // 0..1
            float  _SonarPaused;

            v2f vert(appdata v)
            {
                v2f o;
                o.wpos = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(o.wpos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // базовая «тьма» → немного видимости, если хотите не абсолютный 0
                half baseVis = _WorldDarkness;

                // горизонтальная дистанция от центра
                float2 originXZ = _SonarOrigin.xz;
                float2 posXZ = i.wpos.xz;
                float dist = distance(posXZ, originXZ);

                // профиль кольца: пик на фронте (_SonarRadius), ширина _SonarWidth
                float band = abs(dist - _SonarRadius);
                float ring = saturate(1.0 - smoothstep(0.0, _SonarWidth, band));

                // хвост — плавное затухание позади фронта (опционально)
                float tail = 0.0;
                if (_SonarTail > 0.0001)
                {
                    float behind = saturate((_SonarRadius - dist) / max(0.0001, _SonarTail));
                    tail = behind;
                }

                // если пауза — не рисуем кольцо (но можно оставить минимальную видимость)
                float waveMask = (1.0 - step(0.5, _SonarPaused)); // 1 когда НЕ на паузе
                float vis = baseVis + waveMask * (ring + tail);

                float3 col = _SonarColor.rgb * _SonarIntensity * vis * _BaseColor.rgb;
                return half4(col, vis * _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
