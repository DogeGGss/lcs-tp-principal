// F35 (Skins de armas), US 381: el humo que larga la boca de un arma con skin legendaria después de disparar (HumoDeSkin):
// cada partícula es una bocanada redonda, suave y despareja, que tapa un poco lo de atrás (no suma luz como el fuego).
// El color y la transparencia vienen de la partícula (el sistema las aclara y las agranda al subir). Las brasas que
// suben con el humo usan el mismo material, con el color del fuego: Brillo las hace notar.
Shader "Riftwalker/Skins/Humo"
{
    Properties
    {
        _Color ("Humo", Color) = (0.1, 0.12, 0.2, 1)
        _Brillo ("Brillo de las brasas", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Humo"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Brillo;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Azar(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Azar(i), Azar(i + float2(1, 0)), u.x), lerp(Azar(i + float2(0, 1)), Azar(i + float2(1, 1)), u.x), u.y);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float d = length(c) * 2.0;
                // Una partícula clara (las brasas, con el color del fuego) es un puntito que brilla.
                float claro = max(i.color.r, max(i.color.g, i.color.b));
                if (claro > 0.5)
                {
                    float b = saturate(1.0 - d);
                    return float4(i.color.rgb * _Brillo, b * b * i.color.a);
                }
                // El humo: redondo y suave, con el borde despedazado (ruido que gira con el tiempo de cada bocanada).
                float n = Ruido(i.uv * 4.0 + i.color.a * 7.0) * 0.6 + Ruido(i.uv * 9.0 - i.color.a * 3.0) * 0.4;
                float a = saturate(1.0 - d) ;
                a = saturate(a * a * 1.6 * (0.55 + 0.9 * n));
                return float4(_Color.rgb + i.color.rgb, a * i.color.a);
            }
            ENDHLSL
        }
    }
}
