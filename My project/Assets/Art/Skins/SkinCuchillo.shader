// F35 (Skins de armas), 19/11/2026: las legendarias del cuchillo (salvo el Dragón Abisal, que va con SkinDragon), todas
// animadas. El cuchillo tiene un solo material, con textura (_BaseMap); la hoja se distingue del mango por el largo: en
// el espacio de la malla, _Eje.xyz es el eje del cuchillo hacia la punta y _Eje.w lo que mide de la empuñadura a la
// punta (lo pone CuchilloDeSkin según la malla). La hoja empieza en _Hoja (la parte del largo).
//   0 Hoja de plasma: la hoja es energía que corre hacia la punta y late; el mango, metal oscuro con vetas encendidas.
//   1 Rift abierto: la hoja es una ventana al Rift (estrellas y nebulosa que se ven según desde dónde se mire, como si
//     adentro hubiera espacio), con el borde encendido; el mango, metal oscuro.
//   2 Cristal de hielo: hielo celeste con grietas adentro y escarcha que crece y se derrite; el mango, con escarcha.
//   3 Sangre de invasor: metal negro con venas rojas que laten como un corazón (más rápido al cortar).
//   4 Holograma: el cuchillo como un holograma celeste (suma luz: va con Blend One One), con líneas de escaneo, una
//     barra que lo recorre, parpadeos y cortes ("glitch") por franjas.
// Todos brillan más al cortar (_Calor, de 0 a 1: CalorDeSkin).
Shader "Riftwalker/Skins/Cuchillo"
{
    Properties
    {
        [HideInInspector] _AjusteEscala ("Ajuste del tamaño del patrón", Float) = 1
        _Modo ("Modo (0 plasma, 1 rift, 2 hielo, 3 sangre, 4 holograma)", Float) = 0
        [NoScaleOffset] _BaseMap ("Textura del cuchillo", 2D) = "white" {}
        _Color ("Color", Color) = (0.6, 0.3, 1, 1)
        _ColorNucleo ("Color del centro", Color) = (0.95, 0.85, 1, 1)
        _Brillo ("Brillo", Float) = 2.5
        _Calor ("Calor (al cortar)", Range(0, 1)) = 0
        _Eje ("Eje hacia la punta (xyz) y largo (w), en la malla", Vector) = (0, 0, -1, 0.3)
        _Hoja ("Dónde empieza la hoja (parte del largo)", Range(0, 1)) = 0.34
        _Tamano ("Tamaño del patrón (m)", Float) = 0.02
        _Velocidad ("Velocidad", Float) = 1
        [HideInInspector] _SrcBlend ("", Float) = 1
        [HideInInspector] _DstBlend ("", Float) = 0
        [HideInInspector] _ZWrite ("", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Cuchillo"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _AjusteEscala, _Modo, _Brillo, _Calor, _Hoja, _Tamano, _Velocidad, _SrcBlend, _DstBlend, _ZWrite;
                float4 _Color, _ColorNucleo, _Eje;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posM : TEXCOORD0;   // en metros, a la escala del arma (para los patrones)
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
                float3 posWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float2 largo : TEXCOORD5;  // x: de 0 (empuñadura) a 1 (punta); y: 1 en la hoja, 0 en el mango
                float fog : TEXCOORD6;
            };

            float Campana(float x) { return exp(-x * x); } // (pow con base negativa no anda)
            float Azar(float n) { return frac(sin(n * 91.3458) * 47453.5453); }
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
                for (int k = 0; k < 4; k++) { v += a * Ruido3(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 eje = normalize(_Eje.xyz);
                float s = dot(v.positionOS.xyz, eje) / max(_Eje.w, 0.0001);
                float3 posOS = v.positionOS.xyz;
                if ((int)round(_Modo) == 4)
                {
                    // Holograma: cada tanto, algunas franjas se corren de lado (glitch); al cortar, más.
                    float t = _Time.y;
                    float hay = step(0.82 - 0.3 * _Calor, Azar(floor(t * 3.0)));
                    float franja = floor(s * 30.0);
                    float corre = (Azar(franja + floor(t * 14.0) * 7.0) - 0.5) * step(0.75, Azar(franja * 3.1 + floor(t * 14.0)));
                    float3 lado = normalize(cross(eje, abs(eje.y) > 0.9 ? float3(1, 0, 0) : float3(0, 1, 0)));
                    posOS += lado * corre * hay * 0.12 * _Eje.w;
                }
                VertexPositionInputs p = GetVertexPositionInputs(posOS);
                o.positionCS = p.positionCS;
                float3 escala = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                o.posM = v.positionOS.xyz * escala * _AjusteEscala;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                o.posWS = p.positionWS;
                o.uv = v.uv;
                o.largo = float2(s, smoothstep(_Hoja - 0.03, _Hoja + 0.03, s));
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Metal oscuro (la textura del cuchillo, apagada), con la luz principal y la del ambiente.
            float3 Metal(Varyings i, float3 N, float3 V, float cuanto, float3 tinte)
            {
                Light luz = GetMainLight();
                float3 L = normalize(luz.direction);
                float3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * cuanto * tinte;
                float ambiente = dot(SampleSH(N), float3(0.299, 0.587, 0.114));
                float especular = pow(saturate(dot(N, normalize(L + V))), 48.0) * 0.5;
                return base * (luz.color * saturate(dot(N, L)) * 0.9 + ambiente) + especular * luz.color * 0.4;
            }

            // Una red de líneas finas (venas, grietas): 1 sobre la línea, que se apaga hacia los costados.
            float Red(float3 p, float semilla, float ancho, out float halo)
            {
                float d = abs(Fbm3(p + semilla) - 0.5);
                halo = pow(saturate(1.0 - d / (ancho * 4.0)), 2.0);
                return saturate(1.0 - d / ancho);
            }

            float4 frag(Varyings i) : SV_Target
            {
                int modo = (int)round(_Modo);
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewWS);
                float borde = pow(1.0 - saturate(dot(N, V)), 3.0);
                float hoja = i.largo.y, u = i.largo.x;
                float t = _Time.y * _Velocidad;
                float fuerza = _Brillo * (1.0 + 1.2 * _Calor);
                float3 p = i.posM / _Tamano;
                float3 color;

                if (modo == 0) // plasma
                {
                    float3 mango = Metal(i, N, V, 0.25, float3(0.9, 0.85, 1.0));
                    float halo;
                    float veta = Red(p * 0.8, 3.0, 0.03, halo);
                    mango += _Color.rgb * (veta + halo * 0.3) * fuerza * 0.25 * (0.6 + 0.4 * sin(t * 3.0));
                    // La energía corre hacia la punta (por el eje del cuchillo) y late.
                    float3 corre = p - normalize(_Eje.xyz) * t * 2.5;
                    float n = Fbm3(corre * 0.7);
                    float onda = 0.75 + 0.25 * sin(u * 30.0 - t * 9.0);
                    float energia = saturate(n * 1.4 - 0.15) * onda;
                    float3 plasma = lerp(_Color.rgb, _ColorNucleo.rgb, pow(energia, 2.0)) * (0.5 + 1.2 * energia + borde) * fuerza;
                    color = lerp(mango, plasma, hoja);
                }
                else if (modo == 1) // rift abierto
                {
                    float3 mango = Metal(i, N, V, 0.2, float3(0.85, 0.8, 1.0)) + _Color.rgb * borde * 0.4;
                    // Lo que se ve "adentro": según la dirección desde la cámara (no pegado a la superficie).
                    float3 dir = normalize(-V);
                    float3 neb = dir * 3.0 + float3(t * 0.05, 0.0, t * 0.03);
                    float nube = Fbm3(neb);
                    float3 nebulosa = lerp(float3(0.02, 0.0, 0.06), _Color.rgb * 0.6, saturate(nube * 1.6 - 0.45));
                    nebulosa += float3(0.05, 0.15, 0.5) * saturate(Fbm3(neb * 2.0 + 9.0) * 1.8 - 0.9);
                    float3 celda = floor(dir * 70.0);
                    float azar = Azar3(celda);
                    float3 centro = (celda + 0.5) / 70.0;
                    float estrella = step(0.97, azar) * pow(saturate(1.0 - length(dir - normalize(centro)) * 70.0), 3.0);
                    estrella *= 0.6 + 0.4 * sin(t * 3.0 + azar * 40.0);
                    float3 espacio = nebulosa * 1.5 + _ColorNucleo.rgb * estrella * 4.0;
                    espacio += _Color.rgb * borde * fuerza * 0.8; // el borde de la hoja, encendido
                    color = lerp(mango, espacio * (1.0 + 0.6 * _Calor), hoja);
                }
                else if (modo == 2) // hielo
                {
                    Light luz = GetMainLight();
                    float3 L = normalize(luz.direction);
                    float ambiente = dot(SampleSH(N), float3(0.299, 0.587, 0.114));
                    float3 hielo = _Color.rgb * (0.35 + 0.65 * (luz.color * saturate(dot(N, L)) * 0.7 + ambiente));
                    hielo += pow(saturate(dot(N, normalize(L + V))), 96.0) * 1.5 * luz.color; // brillo duro
                    hielo += _ColorNucleo.rgb * borde * 0.9;                                   // canto claro
                    float halo;
                    float grieta = Red(p * 0.9, 7.0, 0.025, halo);
                    hielo = lerp(hielo, _ColorNucleo.rgb * 1.4, grieta * 0.7) + _ColorNucleo.rgb * halo * 0.15;
                    float3 mango = Metal(i, N, V, 0.3, float3(0.8, 0.9, 1.1));
                    float3 base = lerp(mango, hielo, hoja);
                    // Escarcha que crece y se derrite despacio (en el mango, menos).
                    float cuanta = 0.45 + 0.25 * sin(t * 0.6) + 0.15 * _Calor - (1.0 - hoja) * 0.15;
                    float escarcha = smoothstep(cuanta, cuanta - 0.08, Fbm3(p * 2.2 + 21.0));
                    color = lerp(base, float3(0.88, 0.95, 1.0) * (0.6 + ambiente), escarcha * 0.85);
                    color *= 1.0 + 0.5 * _Calor;
                }
                else if (modo == 3) // sangre
                {
                    float3 metal = Metal(i, N, V, 0.22, float3(1.0, 0.85, 0.85));
                    // El latido: dos golpes y una pausa, que viajan hacia la punta; al cortar, más rápido.
                    float f = frac(t * (1.1 + 1.4 * _Calor) - u * 0.35);
                    float latido = Campana(f / 0.06) + 0.6 * Campana((f - 0.2) / 0.06);
                    float halo;
                    float vena = Red(p, 5.0, 0.035, halo);
                    float vivas = 0.35 + 1.1 * latido;
                    color = metal + _Color.rgb * halo * 0.35 * vivas * fuerza * 0.4;
                    color = lerp(color, lerp(_Color.rgb, _ColorNucleo.rgb, latido * vena) * vivas * fuerza * 0.5, vena);
                }
                else // holograma (suma luz)
                {
                    float gris = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb, float3(0.299, 0.587, 0.114));
                    float lineas = 0.55 + 0.45 * step(0.5, frac(i.posWS.y * 160.0 - t * 2.0));
                    float barra = Campana((frac(t * 0.45) * 1.4 - 0.2 - u) / 0.05);
                    float parpadeo = 0.8 + 0.2 * Ruido3(float3(t * 18.0, 0.0, 0.0));
                    parpadeo *= 1.0 - 0.6 * step(0.96, Azar(floor(t * 10.0)));
                    float luz = (0.18 + 0.4 * gris) * lineas + borde * 1.3 + barra * 0.9;
                    color = lerp(_Color.rgb, _ColorNucleo.rgb, saturate(borde + barra)) * luz * parpadeo * fuerza * 0.6;
                    return float4(color, 1);
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
            ZWrite [_ZWrite]
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
