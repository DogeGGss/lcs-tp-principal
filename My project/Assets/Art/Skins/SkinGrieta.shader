// F35 (Skins de armas), US 384: la skin épica "Grieta del Rift", animada. Un material por parte (_Parte):
//   0 Cuerpo: negro violáceo, con una grieta violeta (el centro casi blanco) que lo cruza y un resplandor alrededor que
//     late; la grieta se abre y se cierra apenas.
//   1 Paneles: un remolino de energía violeta que gira despacio y late, como un portal.
//   2 Franjas: violeta oscuro con chispas claras que corren hacia el caño.
// Se calcula en 3D (no apoyado en cada cara): no se corta entre caras. En los modelos el largo es el eje X y el alto el Z.
Shader "Riftwalker/Skins/Grieta"
{
    Properties
    {
        [HideInInspector] _AjusteEscala ("Ajuste del tamaño del patrón", Float) = 1
        _Parte ("Parte (0 cuerpo, 1 paneles, 2 franjas)", Float) = 0
        _ColorFondo ("Fondo", Color) = (0.043, 0.031, 0.071, 1)
        _ColorGrieta ("Grieta", Color) = (0.482, 0.247, 0.894, 1)
        _ColorCentro ("Centro de la grieta", Color) = (0.847, 0.722, 1, 1)
        _ColorPortalOscuro ("Portal oscuro", Color) = (0.11, 0.043, 0.227, 1)
        _ColorPortalClaro ("Portal claro", Color) = (0.29, 0.122, 0.627, 1)
        _Grieta ("Tamaño de la grieta (m)", Float) = 0.12
        _Remolino ("Tamaño del remolino (m)", Float) = 0.05
        _Giro ("Velocidad del remolino", Float) = 0.6
        _Brillo ("Brillo", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Grieta"
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
                float4 _ColorFondo, _ColorGrieta, _ColorCentro, _ColorPortalOscuro, _ColorPortalClaro;
                float _Parte, _Grieta, _Remolino, _Giro, _Brillo;
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

            float4 frag(Varyings i) : SV_Target
            {
                int parte = (int)round(_Parte);
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewWS);
                float t = _Time.y;
                float late = 0.75 + 0.25 * sin(t * 3.0);
                float3 color;
                if (parte == 2) // franjas: chispas que corren
                {
                    float c = frac(i.posM.x / 0.03 - t * 5.0);
                    float chispa = step(c, 0.12) * step(0.5, Ruido3(i.posM / 0.01));
                    color = Iluminar(float3(0.165, 0.063, 0.314), N, V, i.posWS, i.positionCS, 0.2) +
                            _ColorCentro.rgb * chispa * _Brillo * 0.7;
                }
                else if (parte == 1) // paneles: el remolino (ruido que se retuerce sobre sí mismo y gira)
                {
                    float3 q = i.posM / _Remolino;
                    float a = t * _Giro;
                    float3 giro = float3(sin(a), cos(a * 0.7), cos(a)) * 0.6;
                    float w = Fbm3(q * 0.5 + giro);
                    float n = Fbm3(q + float3(w * 3.0, w * 2.0, -w * 3.0) + giro * 2.0);
                    float brazo = pow(saturate(1.0 - abs(frac(n * 3.0 - t * _Giro * 0.5) - 0.5) / 0.2), 2.0);
                    float3 base = lerp(_ColorPortalOscuro.rgb, _ColorPortalClaro.rgb, n);
                    color = Iluminar(base * 0.6, N, V, i.posWS, i.positionCS, 0.2) + base * late * 0.6 +
                            _ColorCentro.rgb * brazo * late * 0.45 * _Brillo * 0.5;
                }
                else // cuerpo: la grieta
                {
                    float g = Fbm3(i.posM / _Grieta * float3(0.5, 1.0, 1.0));
                    float zona = saturate((Fbm3(i.posM / (_Grieta * 3.0) + 3.0) - 0.45) * 6.0);
                    float d = abs(g - 0.5);
                    float ancho = 0.018 * (0.85 + 0.15 * sin(t * 1.7));   // se abre y se cierra apenas
                    float nucleo = saturate(1.0 - d / ancho) * zona;
                    float halo = pow(saturate(1.0 - d / 0.1), 2.0) * zona;
                    color = Iluminar(_ColorFondo.rgb, N, V, i.posWS, i.positionCS, 0.15);
                    color += _ColorGrieta.rgb * halo * 0.35 * late * _Brillo * 0.5;
                    float3 grieta = lerp(_ColorGrieta.rgb, _ColorCentro.rgb, nucleo) * _Brillo;
                    color = lerp(color, grieta, saturate(nucleo * 1.2));
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
