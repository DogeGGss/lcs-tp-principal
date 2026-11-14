// F35 (Skins de armas), US 381: la skin legendaria "Tormenta", animada. Un material por parte del arma (_Parte):
//   0 Cuerpo: nubes de tormenta negro azuladas que se mueven despacio.
//   1 Paneles: nubes más claras.
//   En las dos, lluvia fina que cae (en las caras de costado; arriba no se ve) y, cada _CadaRayo segundos (más o
//   menos), un relámpago amarillo que parpadea dos o tres veces y se apaga (_ConLluvia: 0 para sacarlos de esa parte).
//   2 Franjas: oscuras, con hilos de corriente amarilla que corren a lo largo del arma.
// El relámpago ilumina un instante todo el arma (las nubes del cuerpo también). Las nubes y los relámpagos se calculan
// en 3D (no apoyados en cada cara): pasan de una cara a otra sin cortarse. Con cada disparo la corriente se enciende
// (_Calor, de 0 a 1, lo pone CalorDeSkin). En los modelos de las armas el largo (el caño) es el eje X y el alto el Z.
Shader "Riftwalker/Skins/Tormenta"
{
    Properties
    {
        _Parte ("Parte (0 cuerpo, 1 paneles, 2 franjas)", Float) = 0
        _ColorOscuro ("Nube oscura", Color) = (0.047, 0.059, 0.082, 1)
        _ColorClaro ("Nube clara", Color) = (0.227, 0.263, 0.345, 1)
        _Nube ("Tamaño de las nubes (m)", Float) = 0.06
        _Viento ("Velocidad de las nubes", Float) = 0.15
        _ColorLluvia ("Lluvia", Color) = (0.725, 0.776, 0.878, 1)
        _Lluvia ("Cuánta lluvia", Range(0, 1)) = 0.65
        _Gota ("Separación de las gotas (m)", Float) = 0.006
        _LargoGota ("Largo de las gotas (m)", Float) = 0.02
        _VelocidadLluvia ("Velocidad de la lluvia", Float) = 3
        _ColorRayo ("Rayo", Color) = (1, 0.784, 0.118, 1)
        _ColorNucleo ("Centro del rayo", Color) = (1, 0.969, 0.784, 1)
        _Brillo ("Brillo del rayo y la corriente", Float) = 2.5
        _CadaRayo ("Cada cuánto cae un rayo (s)", Float) = 3.5
        _Rayo ("Tamaño de los rayos (m)", Float) = 0.05
        _Calor ("Calor (al disparar)", Range(0, 1)) = 0
        [Toggle] _ConLluvia ("Lluvia y rayos", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Tormenta"
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
                float4 _ColorOscuro, _ColorClaro, _ColorLluvia, _ColorRayo, _ColorNucleo;
                float _Parte, _Nube, _Viento, _Lluvia, _Gota, _LargoGota, _VelocidadLluvia, _Brillo, _CadaRayo, _Rayo, _Calor, _ConLluvia;
            CBUFFER_END

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
                o.posM = v.positionOS.xyz * escala;
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

            // Las nubes en p: de 0 (oscuro) a 1 (claro), moviéndose con el viento a lo largo del arma.
            float Nubes(float3 p, float tamano)
            {
                float t = _Time.y * _Viento;
                float3 q = p / tamano;
                float n = Fbm3(q + float3(t, 0.0, -t * 0.35));
                float n2 = Fbm3(q * 2.2 + float3(-t * 1.6, 3.0, t * 0.5));
                return pow(saturate((n * 0.75 + n2 * 0.35 - 0.25) * 1.6), 1.5);
            }

            // El relámpago de ahora: cada _CadaRayo segundos (con un corrimiento al azar) cae uno, que parpadea dos o
            // tres veces en medio segundo. Devuelve cuánto brilla (0 apagado) y en "cual" el número de rayo (para que
            // cada uno caiga en otro lado).
            float Relampago(out float cual)
            {
                float ciclo = max(_CadaRayo, 0.5);
                float n = floor(_Time.y / ciclo);
                cual = n;
                float empieza = n * ciclo + Azar(float2(n, 3.7)) * ciclo * 0.5;
                float t = _Time.y - empieza;
                if (t < 0.0 || t > 0.55) return 0.0;
                float parpadeo = pow(saturate(sin(t * 38.0 + 1.3)), 2.0);   // dos o tres destellos
                return parpadeo * exp(-t * 4.0) * (t < 0.04 ? 1.0 : 0.85);
            }

            // Un relámpago en p (cual: cuál de ellos): un hilo zigzagueante, más que nada de arriba hacia abajo, con
            // ramas. Devuelve el centro (nucleo) y el resplandor (halo).
            void Rayo(float3 p, float cual, out float nucleo, out float halo)
            {
                float3 o = float3(cual * 7.31, cual * 3.17, cual * 5.13);
                float3 q = float3(p.x / _Rayo, p.y / _Rayo, p.z / (_Rayo * 3.0)) + o; // estirado hacia abajo
                float g = Fbm3(q);
                float donde = Fbm3(p / (_Rayo * 4.0) + o * 1.3);                       // cae en una parte del panel
                float aca = saturate((donde - 0.52) * 10.0);
                float d = abs(g - 0.5);
                nucleo = saturate(1.0 - d / 0.014) * aca;
                halo = pow(saturate(1.0 - d / 0.09), 2.0) * aca;
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

            float4 frag(Varyings i) : SV_Target
            {
                float3 n = abs(normalize(i.normalOS));
                float2 p = n.x > max(n.y, n.z) ? i.posM.yz : n.y > n.z ? i.posM.xz : i.posM.xy;
                int parte = (int)round(_Parte);
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewWS);
                float cual;
                float destello = Relampago(cual);
                float fuerza = _Brillo * (0.8 + 0.2 * sin(_Time.y * 2.3) + 1.2 * _Calor);

                float3 color;
                if (parte == 2) // franjas: hilos de corriente que corren hacia el caño, con un pulso
                {
                    float t = _Time.y;
                    float hiloN = Fbm(float2(p.x / 0.03 - t * 4.0, p.y / 0.008));
                    float hilo = pow(saturate(1.0 - abs(hiloN - 0.5) / 0.06), 2.0);
                    float corre = 0.5 + 0.5 * pow(abs(sin((p.x / 0.1 - t * 6.0) * PI)), 6.0);
                    float3 base = Iluminar(float3(0.227, 0.165, 0.02), N, V, i.posWS, i.positionCS, 0.2);
                    color = base + _ColorRayo.rgb * hilo * fuerza * 0.5 * (0.4 + corre + destello)
                                 + _ColorNucleo.rgb * pow(hilo, 3.0) * corre * fuerza * 0.3;
                }
                else
                {
                    bool panel = parte == 1;
                    float k = Nubes(i.posM, _Nube * (panel ? 0.85 : 1.0));
                    float3 albedo = lerp(_ColorOscuro.rgb, _ColorClaro.rgb, k) * (panel ? 1.5 : 1.0);
                    color = Iluminar(albedo, N, V, i.posWS, i.positionCS, 0.05);
                    float nucleo = 0.0, halo = 0.0;
                    if (_ConLluvia > 0.5)
                    {
                        // La lluvia: rayitas que caen (hacia -Z), en columnas; solo en las caras de costado.
                        float2 celda = floor(i.posM.xy / _Gota);
                        float columna = n.y > n.x ? frac(i.posM.x / _Gota) : frac(i.posM.y / _Gota);
                        float vel = 1.0 + 0.6 * Azar(celda + 5.3);
                        float fase = frac(i.posM.z / _LargoGota * 0.35 + _Time.y * vel * _VelocidadLluvia + Azar(celda) * 10.0);
                        float gota = step(abs(columna - 0.5), 0.09) * step(fase, 0.32) * step(1.0 - _Lluvia, Azar(celda + 9.1));
                        float costado = saturate(1.0 - n.z * 1.5);
                        color += _ColorLluvia.rgb * gota * (1.0 - fase / 0.32) * 0.35 * costado;
                        if (destello > 0.0) Rayo(i.posM, cual, nucleo, halo);
                    }
                    // El relámpago ilumina las nubes (todo el arma un poco; alrededor del rayo, más).
                    color += albedo * destello * (panel ? 1.6 : 0.9) + _ColorRayo.rgb * halo * destello * 0.6 * _Brillo;
                    float3 rayo = lerp(_ColorRayo.rgb, _ColorNucleo.rgb, nucleo) * _Brillo * 1.4;
                    color = lerp(color, rayo, saturate(nucleo * destello * 1.5));
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
