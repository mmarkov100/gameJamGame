Shader "Custom/SonarUnlit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0,0,0,1)      // базовый цвет (обычно почти чёрный)
        _SonarColor ("Sonar Color", Color) = (1,1,1,1)    // цвет проявления от волны
        _AlwaysVisible ("Always Visible", Float) = 0      // 1 — объект всегда светится (для игрока)
        _AlwaysEmit ("Always Emission", Range(0,5)) = 1.2 // сила постоянного свечения (игрок)
        _MaxReveal ("Max Reveal", Range(0,5)) = 1.0       // яркость от волны
        _Smooth ("Smoothness of band", Range(0.001,0.5)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 100
        Pass
        {
            Name "FORWARD_UNLIT"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SonarColor;
                float _AlwaysVisible;
                float _AlwaysEmit;
                float _MaxReveal;
                float _Smooth;
            CBUFFER_END

            // ---- Глобали от скрипта сонар-контроллера ----
            // Максимум 8 одновременных волн (можно увеличить при надобности)
            #define MAX_WAVES 8
            int    _SonarWaveCount;
            float4 _SonarOrigins[MAX_WAVES];   // xyz = позиция, w не используется
            float  _SonarStartTimes[MAX_WAVES];// время старта волны
            float  _SonarSpeed;
            float  _SonarBandWidth;            // ширина «кольца»
            float  _SonarMaxDistance;          // отсечка по дальности
            float  _TimeNow;                   // реальное Time.time (для вычислений в шейдере)

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.worldPos = world;
                o.positionHCS = TransformWorldToHClip(world);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // База — тёмная сцена
                half3 col = _BaseColor.rgb;
                half  alpha = 1;

                // Суммируем вклад от всех волн (берём максимум)
                half sonarReveal = 0;
                [unroll]
                for (int idx = 0; idx < _SonarWaveCount; idx++)
                {
                    float3 o = _SonarOrigins[idx].xyz;
                    float  t = _TimeNow - _SonarStartTimes[idx];
                    if (t <= 0) continue;

                    float r = _SonarSpeed * t; // текущий радиус волны
                    float d = distance(i.worldPos, o);

                    if (d > _SonarMaxDistance) continue;

                    // Гладкая полоса вокруг радиуса r шириной _SonarBandWidth
                    float a = saturate(1.0 - abs(d - r) / max(_SonarBandWidth, 1e-4));
                    // Доп. смягчение краёв
                    a = smoothstep(0.0, _Smooth, a);

                    sonarReveal = max(sonarReveal, a);
                }

                // Всегда видимый объект (игрок)
                if (_AlwaysVisible > 0.5)
                {
                    col += _SonarColor.rgb * _AlwaysEmit;
                }

                // Проявление от волны
                col += _SonarColor.rgb * _MaxReveal * sonarReveal;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
