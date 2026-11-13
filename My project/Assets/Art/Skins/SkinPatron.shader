// F35 (Skins de armas): las skins raras, materiales sin animar dibujados acá, sin texturas (como el código binario,
// SkinMatrix.shader). Un mismo shader con varios patrones (_Patron); cada skin es un material con sus colores:
//   0 Oro pulido      1 Fibra de carbono   2 Cobre oxidado
//   3 Mármol de Retiro   4 Azulejos del subte   5 Cristal del Rift   6 Madera
// El oro (0) quedó para las franjas del mármol; la fibra de carbono (1) quedó sin usar (se descartó el 13/11/2026).
// El patrón va pegado al arma (se usa la posición del modelo, en metros) y se apoya en la cara del modelo que mejor le
// queda (como un "triplanar" simple). Luz: la principal de la escena, la del ambiente y los reflejos (para los metales).
Shader "Riftwalker/Skins/Patron"
{
    Properties
    {
        _Patron ("Patrón (0 oro, 1 carbono, 2 cobre, 3 mármol, 4 azulejos, 5 cristal, 6 madera)", Float) = 0
        _Color1 ("Color 1", Color) = (1, 1, 1, 1)
        _Color2 ("Color 2", Color) = (0.5, 0.5, 0.5, 1)
        _Color3 ("Color 3", Color) = (0, 0, 0, 1)
        _Color4 ("Color 4", Color) = (0, 0, 0, 1)
        _Escala ("Tamaño del patrón (m)", Float) = 0.02
        _Metal ("Metálico", Range(0, 1)) = 0
        _Lisura ("Lisura (brillo)", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Patron"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color1, _Color2, _Color3, _Color4;
                float _Patron, _Escala, _Metal, _Lisura;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posM : TEXCOORD0;   // posición en el modelo, en metros (con la escala del arma)
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float fog : TEXCOORD4;
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
                return o;
            }

            float Azar(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 Azar2(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }

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

            // Cada patrón: color y cuánto brilla (lisura), y cuánto es metal.
            void Patron(float2 uv, float3 pos, out float3 color, out float lisura, out float metal)
            {
                lisura = _Lisura;
                metal = _Metal;
                int patron = (int)round(_Patron);
                if (patron == 6) // madera: anillos alrededor del largo del arma (como un tronco), con vetas y fibra
                {
                    // En los modelos de las armas, el largo (el caño) es el eje X; el alto, el Z.
                    float3 q = pos / _Escala;
                    float r = length(q.yz);
                    float t = r * 0.35 + Fbm(float2(q.x * 0.15, r * 0.6)) * 2.5;
                    float anillo = pow(0.5 + 0.5 * sin(t * 6.2831), 3.0);
                    float fibra = Ruido(float2(q.x * 0.05, r * 12.0));
                    color = lerp(_Color1.rgb, _Color2.rgb, anillo * 0.8 + 0.2 * fibra);
                    color *= 0.9 + 0.15 * Fbm(float2(q.x * 0.5 + 9.0, r * 2.0));
                }
                else if (patron == 1) // fibra de carbono: tejido en diagonal, hebras que alternan de dirección
                {
                    float2 c = uv / _Escala;
                    float2 id = floor(c);
                    float2 f = frac(c);
                    bool horizontal = fmod(id.x + id.y, 2.0) < 1.0;
                    float hebra = sin((horizontal ? f.y : f.x) * PI);
                    float corte = smoothstep(0.0, 0.08, horizontal ? f.x : f.y) * smoothstep(1.0, 0.92, horizontal ? f.x : f.y);
                    color = lerp(_Color1.rgb, _Color2.rgb, hebra * (0.6 + 0.4 * corte));
                }
                else if (patron == 2) // cobre oxidado: cobre con manchas de óxido verde, mate donde hay óxido
                {
                    float n = Fbm(uv / _Escala);
                    float oxido = smoothstep(0.48, 0.62, n);
                    float vetas = Fbm(uv / _Escala * 3.0 + 5.0);
                    color = lerp(_Color1.rgb * (0.85 + 0.3 * vetas), lerp(_Color2.rgb, _Color3.rgb, vetas), oxido);
                    metal = lerp(_Metal, 0.0, oxido);
                    lisura = lerp(_Lisura, 0.15, oxido);
                }
                else if (patron == 3) // mármol: blanco con vetas grises que serpentean
                {
                    float2 p = uv / _Escala;
                    float t = p.x * 0.6 + p.y * 0.25 + Fbm(p * 0.7) * 6.0;
                    float veta = 1.0 - smoothstep(0.0, 0.12, abs(sin(t)));
                    float fina = 1.0 - smoothstep(0.0, 0.05, abs(sin(t * 2.7 + 1.3)));
                    float nube = Fbm(p * 1.7 + 3.0);
                    color = _Color1.rgb * (0.92 + 0.08 * nube);
                    color = lerp(color, _Color2.rgb, veta * 0.85);
                    color = lerp(color, _Color3.rgb, fina * 0.35);
                }
                else if (patron == 4) // azulejos: cuadrícula con junta, cada azulejo de uno de tres colores
                {
                    float2 c = uv / _Escala;
                    float2 id = floor(c);
                    float2 f = frac(c);
                    float junta = step(0.07, f.x) * step(f.x, 0.93) * step(0.07, f.y) * step(f.y, 0.93);
                    float r = Azar(id);
                    float3 azulejo = r < 0.45 ? _Color1.rgb : r < 0.8 ? _Color2.rgb : _Color3.rgb;
                    azulejo *= 0.92 + 0.12 * Azar(id + 7.7);
                    color = lerp(_Color4.rgb, azulejo, junta);
                    lisura = lerp(0.25, _Lisura, junta);
                }
                else if (patron == 5) // cristal: facetas (Voronoi), cada una entre violeta y celeste, con los bordes claros
                {
                    float2 p = uv / _Escala;
                    float2 id = floor(p), f = frac(p);
                    float d1 = 8.0, d2 = 8.0;
                    float2 mejor = 0;
                    for (int y = -1; y <= 1; y++)
                        for (int x = -1; x <= 1; x++)
                        {
                            float2 g = float2(x, y);
                            float2 o = Azar2(id + g);
                            float d = length(g + o - f);
                            if (d < d1) { d2 = d1; d1 = d; mejor = id + g; }
                            else if (d < d2) d2 = d;
                        }
                    float borde = 1.0 - smoothstep(0.0, 0.06, d2 - d1);
                    float tono = Azar(mejor);
                    color = lerp(_Color1.rgb, _Color2.rgb, tono) * (0.7 + 0.5 * Azar(mejor + 3.1));
                    color = lerp(color, _Color3.rgb, borde);
                }
                else // oro pulido: un cepillado muy suave
                {
                    float cepillado = Ruido(float2(uv.x / _Escala * 0.05, uv.y / _Escala * 4.0));
                    color = _Color1.rgb * (0.9 + 0.15 * cepillado);
                }
            }

            float4 frag(Varyings i) : SV_Target
            {
                // El plano donde se apoya el patrón: el de la cara del modelo que mejor le queda.
                float3 n = abs(normalize(i.normalOS));
                float2 uv = n.x > max(n.y, n.z) ? i.posM.yz : n.y > n.z ? i.posM.xz : i.posM.xy;

                float3 albedo; float lisura, metal;
                Patron(uv, i.posM, albedo, lisura, metal);

                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewWS);
                Light luz = GetMainLight();
                float3 L = normalize(luz.direction);
                float3 H = normalize(L + V);
                float nl = saturate(dot(N, L));
                float potencia = exp2(2.0 + 9.0 * lisura);
                float3 reflejo = lerp(float3(0.04, 0.04, 0.04), albedo, metal); // los metales reflejan con su color
                float3 especular = reflejo * pow(saturate(dot(N, H)), potencia) * (potencia + 2.0) / 8.0;

                float3 difuso = albedo * (1.0 - metal);
                float3 color = (difuso + especular) * luz.color * nl;
                color += difuso * SampleSH(N);
                // Reflejos del ambiente: los metales y lo pulido (mármol, azulejos) devuelven lo que tienen alrededor.
                float fresnel = pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 entorno = GlossyEnvironmentReflection(reflect(-V, N), 1.0 - lisura, 1.0);
                color += entorno * lerp(reflejo, float3(1, 1, 1), fresnel * lisura);

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
