Shader "Sonar/UnlitGlow"
{
    Properties
    {
        _GlowColor ("Color", Color) = (1,1,1,1)
        _GlowIntensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend One One
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
            };

            float4 _GlowColor;
            float  _GlowIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(_GlowColor.rgb * _GlowIntensity, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
