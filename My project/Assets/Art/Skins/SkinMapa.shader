// F35 (Skins de armas), US 384: la skin épica "Mapa del subte", animada: sobre un papel blanco se va dibujando un mapa
// de la red de subte, con las líneas de sus colores (A celeste, B roja, C azul, D verde, E violeta y H amarilla): cada
// línea arranca un rato después de la anterior y avanza como trazada con una lapicera (la punta, más oscura), con
// tramos rectos y diagonales a 45° como en los planos, y estaciones (círculos blancos con borde) que aparecen al pasar.
// Cuando están todas, el mapa se borra y se dibuja otro distinto (_Ciclo). Un material por parte (_Parte):
//   0 Papel: el cuerpo, los paneles y los detalles (el mapa recorre toda el arma; da la vuelta por arriba como el
//     texto de "Próxima estación"). En las puntas del arma, solo el papel.
//   2 Franjas: los colores de las líneas, en tramos, a lo largo del arma.
// Va pegado al arma (la posición del modelo, en metros). En los modelos el largo es el eje X y el alto el Z.
Shader "Riftwalker/Skins/Mapa"
{
    Properties
    {
        [HideInInspector] _AjusteEscala ("Ajuste del tamaño del patrón", Float) = 1
        _Parte ("Parte (0 papel, 2 franjas)", Float) = 0
        _ColorPapel ("Papel", Color) = (0.96, 0.95, 0.91, 1)
        _ColorTinta ("Borde de las estaciones", Color) = (0.12, 0.12, 0.14, 1)
        _Separacion ("Separación entre líneas (m)", Float) = 0.016
        _Grosor ("Grosor de las líneas (m)", Float) = 0.0045
        _Tramo ("Largo de cada tramo (m)", Float) = 0.07
        _Velocidad ("Velocidad del trazo (m/s)", Float) = 0.22
        _Desfase ("Cada cuánto arranca la línea siguiente (s)", Float) = 1.1
        _Inicio ("Desde dónde se dibuja (x del modelo, m)", Float) = -0.7
        _Ciclo ("Duración de cada mapa (s)", Float) = 14
        _Espejo ("Dar vuelta el sentido (1 o -1)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Mapa"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LucesDeSkin.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _AjusteEscala;
                float4 _ColorPapel, _ColorTinta;
                float _Parte, _Separacion, _Grosor, _Tramo, _Velocidad, _Desfase, _Inicio, _Ciclo, _Espejo;
            CBUFFER_END

            #define LINEAS 6
            static const float3 COLORES[LINEAS] = {
                float3(0.0, 0.682, 0.937),   // A
                float3(0.933, 0.239, 0.239), // B
                float3(0.0, 0.443, 0.737),   // C
                float3(0.0, 0.502, 0.396),   // D
                float3(0.427, 0.133, 0.506), // E
                float3(1.0, 0.839, 0.0)      // H
            };

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posM : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float fog : TEXCOORD4;
                float3 posWS : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                float3 escala = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                o.posM = v.positionOS.xyz * escala * _AjusteEscala; // (la vista previa del perfil la ajusta)
                o.normalOS = v.normalOS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.posWS = p.positionWS;
                return o;
            }

            float Azar(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Azar(i), Azar(i + float2(1, 0)), u.x), lerp(Azar(i + float2(0, 1)), Azar(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += a * Ruido(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            float Azar3(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }

            float Ruido3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                float a = lerp(lerp(Azar3(i), Azar3(i + float3(1, 0, 0)), u.x), lerp(Azar3(i + float3(0, 1, 0)), Azar3(i + float3(1, 1, 0)), u.x), u.y);
                float b = lerp(lerp(Azar3(i + float3(0, 0, 1)), Azar3(i + float3(1, 0, 1)), u.x), lerp(Azar3(i + float3(0, 1, 1)), Azar3(i + float3(1, 1, 1)), u.x), u.y);
                return lerp(a, b, u.z);
            }

            float Fbm3(float3 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += a * Ruido3(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            float3 Iluminar(float3 albedo, float3 N, float3 V, float3 posWS, float4 positionCS, float brillo)
            {
                Light luzP = GetMainLight();
                float3 L = normalize(luzP.direction);
                float ambiente = dot(SampleSH(N), float3(0.299, 0.587, 0.114));
                float3 color = albedo * (luzP.color * saturate(dot(N, L)) * 0.9 + ambiente);
                color += pow(saturate(dot(N, normalize(L + V))), 32.0) * brillo;
                float3 brilloExtra;
                float3 extra = LucesExtra(posWS, N, V, positionCS, 32.0, brilloExtra);
                return color + albedo * extra + brilloExtra * brillo;
            }

            // En qué renglón (de 0 a 7) va la línea k en el tramo c de este mapa: cerca del suyo (k + 1), a veces uno más
            // arriba o más abajo.
            float Nivel(float k, float c, float mapa)
            {
                float salto = floor(Azar(float2(k * 13.1 + mapa * 3.7, c * 7.7 + 1.3)) * 3.0) - 1.0;
                return clamp(k + 1.0 + salto, 0.0, 7.0);
            }

            // La altura del centro de la línea k en x (y si va en diagonal), dentro de una vuelta de 9 renglones.
            float Centro(float k, float x, float mapa, out float diagonal, out float c, out float xl)
            {
                c = floor(x / _Tramo);
                xl = x - c * _Tramo;
                float a = Nivel(k, c, mapa), b = Nivel(k, c + 1.0, mapa);
                float d = abs(b - a) * _Separacion;                 // a 45°: lo que sube es lo que avanza
                float f = d > 0.0 ? saturate((xl - (_Tramo - d)) / d) : 0.0;
                diagonal = (f > 0.0 && f < 1.0) ? 1.0 : 0.0;
                return (a + (b - a) * f) * _Separacion;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 n = abs(normalize(i.normalOS));
                int parte = (int)round(_Parte);
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewWS);
                float x = i.posM.x * _Espejo;
                float3 color;
                if (parte == 2) // franjas: los colores de las líneas, en tramos
                {
                    int k = (int)fmod(abs(floor(x / 0.035)), (float)LINEAS);
                    color = Iluminar(COLORES[k], N, V, i.posWS, i.positionCS, 0.25);
                }
                else
                {
                    // El papel, con un grano apenas visible.
                    float grano = 0.96 + 0.04 * Ruido3(i.posM / 0.002);
                    float3 papel = _ColorPapel.rgb * grano;
                    color = Iluminar(papel, N, V, i.posWS, i.positionCS, 0.08);
                    bool punta = n.x > max(n.y, n.z);
                    if (!punta)
                    {
                        // El mapa: a lo largo (x) y hacia arriba dando la vuelta por encima (z - |y|).
                        float periodo = 9.0 * _Separacion;
                        float v = i.posM.z - abs(i.posM.y);
                        v = v - floor(v / periodo) * periodo;
                        float ciclo = max(_Ciclo, 1.0);
                        float mapa = floor(_Time.y / ciclo);
                        float tt = _Time.y - mapa * ciclo;
                        float borra = saturate((ciclo - tt) / 1.2);        // al final del ciclo se borra
                        for (int k = 0; k < LINEAS; k++)
                        {
                            float frente = _Inicio + (tt - k * _Desfase) * _Velocidad; // hasta dónde llegó el trazo
                            if (x > frente) continue;
                            float diagonal, c, xl;
                            float centro = Centro(k, x, mapa, diagonal, c, xl);
                            float d = abs(v - centro) * (diagonal > 0.0 ? 0.7071 : 1.0);
                            float linea = saturate((_Grosor * 0.5 - d) / (_Grosor * 0.18) + 0.5);
                            // La punta de la lapicera, más oscura, un poco antes del frente.
                            float tinta = 1.0 - 0.35 * saturate(1.0 - (frente - x) / (_Grosor * 3.0));
                            float3 lin = Iluminar(COLORES[k] * tinta, N, V, i.posWS, i.positionCS, 0.2);
                            color = lerp(color, lin, linea * borra);
                            // Las estaciones: en el medio de la parte recta de cada tramo, un círculo blanco con borde.
                            float xe = _Tramo * 0.3;
                            float2 de = float2(xl - xe, v - Nivel(k, c, mapa) * _Separacion);
                            float r = length(de);
                            if (x <= frente - 0.002 && Azar(float2(k * 5.3 + mapa, c * 3.1)) > 0.35)
                            {
                                float radio = _Grosor * 0.95;
                                float borde = saturate((radio - r) / (_Grosor * 0.15) + 0.5);
                                float blanco = saturate((radio * 0.62 - r) / (_Grosor * 0.15) + 0.5);
                                float3 est = lerp(Iluminar(_ColorTinta.rgb, N, V, i.posWS, i.positionCS, 0.1), papel * 1.05, blanco);
                                color = lerp(color, est, borde * borra);
                            }
                        }
                    }
                }
                color = MixFog(color, i.fog);
                return float4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
