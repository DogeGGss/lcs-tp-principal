// F35 (Skins de armas), US 381: la skin legendaria "Dragón Abisal", animada. Un material por parte del arma (_Parte):
//   0 Cuerpo: escamas negras (la textura Texturas/EscamasDragon, de FreePBR, con su relieve), con el filo azul verdoso.
//   1 Paneles: escamas oscuras con grietas de fuego azul que fluye hacia el caño.
//   2 Franjas: fuego azul vivo que corre a lo largo del arma.
// El fuego "respira" (sube y baja despacio) y se enciende con cada disparo (_Calor, de 0 a 1, lo pone CalorDeSkin en
// cada arma). En los modelos de las armas el largo (el caño) es el eje X, con el caño hacia +X, y el alto el Z.
Shader "Riftwalker/Skins/Dragon"
{
    Properties
    {
        _Parte ("Parte (0 cuerpo, 1 paneles, 2 franjas)", Float) = 0
        _Escala ("Tamaño de las escamas (m)", Float) = 0.04
        _ColorEscama ("Escama", Color) = (0.165, 0.173, 0.212, 1)
        _ColorFilo ("Filo de la escama", Color) = (0.169, 0.722, 0.788, 1)
        _ColorFuego ("Fuego", Color) = (0.118, 0.42, 1, 1)
        _ColorNucleo ("Centro del fuego", Color) = (0.81, 0.98, 1, 1)
        _Brillo ("Brillo del fuego", Float) = 2.5
        _Calor ("Calor (al disparar)", Range(0, 1)) = 0
        [NoScaleOffset] _Escamas ("Escamas (gris)", 2D) = "gray" {}
        [NoScaleOffset] _EscamasNormal ("Relieve de las escamas", 2D) = "bump" {}
        _Tile ("Tamaño de la textura (m)", Float) = 0.3
        _Giro ("Giro de la textura (vueltas de 90°)", Float) = 3
        _Relieve ("Cuánto relieve", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Dragon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorEscama, _ColorFilo, _ColorFuego, _ColorNucleo;
                float _Parte, _Escala, _Brillo, _Calor, _Tile, _Giro, _Relieve;
            CBUFFER_END
            TEXTURE2D(_Escamas); SAMPLER(sampler_Escamas);
            TEXTURE2D(_EscamasNormal); SAMPLER(sampler_EscamasNormal);

            // Gira (x, y) de a 90°: así la punta de las escamas de la textura queda hacia la culata.
            float2 Girar(float2 v, int vueltas)
            {
                vueltas = ((vueltas % 4) + 4) % 4;
                if (vueltas == 1) return float2(-v.y, v.x);
                if (vueltas == 2) return -v;
                if (vueltas == 3) return float2(v.y, -v.x);
                return v;
            }

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posM : TEXCOORD0;
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

            float4 frag(Varyings i) : SV_Target
            {
                // El plano del patrón según la cara, con x siempre a lo largo del arma cuando se puede.
                float3 n = abs(normalize(i.normalOS));
                float2 p = n.x > max(n.y, n.z) ? i.posM.yz : n.y > n.z ? i.posM.xz : i.posM.xy;
                int parte = (int)round(_Parte);
                float respira = 0.75 + 0.25 * sin(_Time.y * 1.6);          // el fuego sube y baja despacio
                float fuerza = _Brillo * (respira + 1.2 * _Calor);          // y se enciende con los disparos

                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewWS);
                Light luzP = GetMainLight();
                float3 L = normalize(luzP.direction);
                float nl = saturate(dot(N, L));
                float fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);

                float3 color;
                if (parte == 2) // franjas: la llama corre hacia el caño
                {
                    float t = Fbm(float2(p.x / 0.03 - _Time.y * 2.0, p.y / 0.012));
                    float3 llama = lerp(_ColorFuego.rgb, _ColorNucleo.rgb, saturate(t * 1.4 - 0.2));
                    color = llama * fuerza * 0.6;
                }
                else
                {
                    // La textura de escamas, apoyada en la cara (con sus dos ejes en el modelo) y girada.
                    int giro = (int)round(_Giro);
                    float2 uv = Girar(p, giro) / _Tile;
                    float gris = SAMPLE_TEXTURE2D(_Escamas, sampler_Escamas, uv).r;
                    float3 tn = UnpackNormal(SAMPLE_TEXTURE2D(_EscamasNormal, sampler_EscamasNormal, uv));
                    tn.xy = Girar(tn.xy, -giro) * _Relieve; // (el relieve, de vuelta a los ejes de la cara)
                    // Los ejes de la cara en el modelo: los mismos con los que se armó p.
                    float3 no = normalize(i.normalOS);
                    float3 ejeU = n.x > max(n.y, n.z) ? float3(0, 1, 0) : float3(1, 0, 0);
                    float3 ejeV = n.x > max(n.y, n.z) || n.y > n.z ? float3(0, 0, 1) : float3(0, 1, 0);
                    float3 nRelieve = normalize(tn.x * ejeU + tn.y * ejeV + max(tn.z, 0.2) * no);
                    float3 Nr = normalize(TransformObjectToWorldNormal(nRelieve));
                    float nlr = saturate(dot(Nr, L));
                    float3 escama = _ColorEscama.rgb * (0.35 + 1.3 * gris) * (parte == 1 ? 0.8 : 1.0);
                    // Iluminadas con el relieve, con un brillo duro (escamas pulidas) y el filo azul verdoso de costado.
                    float3 H = normalize(L + V);
                    float especular = pow(saturate(dot(Nr, H)), 64.0) * 0.8 * gris;
                    color = escama * (luzP.color * nlr * 0.9 + SampleSH(Nr)) + especular * luzP.color;
                    float frs = pow(1.0 - saturate(dot(Nr, V)), 3.0);
                    color += _ColorFilo.rgb * frs * 0.25;
                    if (parte == 1) // paneles: grietas de fuego que fluye hacia el caño
                    {
                        float2 f = float2(p.x - _Time.y * 0.15, p.y) / 0.05;
                        float g = Fbm(f);
                        float nucleo = saturate(1.0 - abs(g - 0.5) / 0.02);
                        float halo = pow(saturate(1.0 - abs(g - 0.5) / 0.09), 2.0);
                        float3 fuego = lerp(_ColorFuego.rgb, _ColorNucleo.rgb, nucleo);
                        color = lerp(color, fuego * fuerza * 0.5, saturate(max(nucleo, halo * 0.85)));
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
