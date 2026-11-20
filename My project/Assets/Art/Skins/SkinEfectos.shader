// F35 (Skins de armas), US 381: los efectos de disparo de las skins legendarias, sin texturas, sumando luz (aditivos):
//   0 Alas: el fogonazo en forma de dos alas de fuego que se abren (_Abrir, de 0 a 1) hacia arriba y hacia abajo, en un
//     cuadrado (uv: x a lo largo del disparo, y de un ala a la otra).
//   1 Brasa: un puntito de fuego redondo (las brasas de la estela, las chispas), del color de la partícula.
//   2 Llamarada: el fogonazo, una lengua de fuego azul en forma de gota que sale de la boca (x: de la boca hacia
//     adelante), tiembla (_Abrir: cuánto creció, de 0 a 1) y se curva hacia un costado (_Enrosque).
//   3 Rayo: una línea (LineRenderer) eléctrica, con el centro casi blanco y el borde del color del rayo; el color y la
//     transparencia de la línea la tiñen y la apagan (RayoDeSkin).
Shader "Riftwalker/Skins/Efectos"
{
    Properties
    {
        _Modo ("Modo (0 alas, 1 brasa, 2 llamarada, 3 rayo)", Float) = 0
        _ColorFuego ("Fuego", Color) = (0.118, 0.42, 1, 1)
        _ColorNucleo ("Centro del fuego", Color) = (0.81, 0.98, 1, 1)
        _Brillo ("Brillo", Float) = 2
        _Abrir ("Abiertas (alas)", Range(0, 1)) = 1
        _Enrosque ("Enrosque de la llama (-1 a 1)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Efecto"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorFuego, _ColorNucleo;
                float _Modo, _Brillo, _Abrir, _Enrosque;
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

            float4 frag(Varyings i) : SV_Target
            {
                float3 color;
                if ((int)round(_Modo) == 3)
                {
                    float centro = saturate(1.0 - abs(i.uv.y * 2.0 - 1.0));
                    color = lerp(_ColorFuego.rgb, _ColorNucleo.rgb, pow(centro, 3.0)) * pow(centro, 1.5) * _Brillo;
                    return float4(color * i.color.rgb * i.color.a, 1);
                }
                if ((int)round(_Modo) == 2)
                {
                    // La lengua se curva hacia un costado (_Enrosque), más cuanto más lejos de la boca.
                    float x = i.uv.x, y = abs(i.uv.y * 2.0 - 1.0 - _Enrosque * 0.55 * x * x);
                    float temblor = 0.85 + 0.15 * sin(x * 23.0 + _Time.y * 60.0) * sin(y * 17.0 - _Time.y * 45.0);
                    float ancho = 0.95 * pow(saturate(x * 4.0), 0.5) * pow(saturate(1.0 - x), 0.8) * temblor;
                    float largo = step(x, _Abrir);                                   // crece desde la boca
                    float forma = saturate((ancho - y) / 0.25) * largo;
                    float centro = saturate(1.0 - y / max(ancho, 0.01)) * saturate(1.0 - x * 1.4);
                    color = lerp(_ColorFuego.rgb, _ColorNucleo.rgb, centro) * forma * _Brillo;
                    return float4(color, 1);
                }
                if ((int)round(_Modo) == 1)
                {
                    float d = length(i.uv - 0.5) * 2.0;
                    float b = saturate(1.0 - d);
                    // El color es el de la partícula (el fuego de cada skin), con el centro más claro.
                    color = lerp(i.color.rgb, i.color.rgb * 0.4 + 0.6, b * b) * b * b * _Brillo;
                    return float4(color * i.color.a, 1);
                }
                // Alas: x de 0 (la boca del arma) a 1 (hacia adelante); y de -1 a 1 (un ala arriba y otra abajo).
                float x = i.uv.x, y = abs(i.uv.y * 2.0 - 1.0);
                float alto = _Abrir * 0.95 * pow(saturate(sin(PI * saturate(x * 1.15))), 0.7);
                alto *= 0.82 + 0.18 * sin(x * 28.0);                       // el borde de atrás, festoneado
                float dentro = saturate((alto - y) / 0.08) * step(0.02, y);
                float nucleo = saturate(1.0 - length(float2(x * 1.6, y * 2.2)));  // el centro, junto a la boca, más claro
                float chorro = saturate(1.0 - y / 0.08) * saturate(1.0 - x);       // un chorro corto hacia adelante
                float fuerza = max(dentro * (0.35 + 0.65 * (1.0 - y)), chorro);
                color = lerp(_ColorFuego.rgb, _ColorNucleo.rgb, saturate(nucleo + chorro * 0.6)) * fuerza * _Brillo;
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}
