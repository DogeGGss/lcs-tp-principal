// F35 (Skins de armas), US 384: la skin épica "Próxima estación", animada: el arma es un cartel de LEDs naranjas, como
// los de los trenes, con el texto que pasa (Texturas/CartelTexto.png: "ÚLTIMO TREN A RETIRO · PRÓXIMA ESTACIÓN" y
// estaciones del San Martín hasta Retiro). Un material por parte (_Parte):
//   0 Cartel: los LEDs, en el cuerpo, los paneles y los detalles (el texto recorre toda el arma: no lo tapa una pieza).
//     El texto va a lo largo del arma, en renglones que se repiten hacia arriba y siguen por encima del arma (pasa del
//     costado a la parte de arriba sin cortarse); en las puntas, solo los LEDs apagados.
//   2 Franjas: LEDs ámbar que se prenden en secuencia, de a grupos, corriendo a lo largo del arma.
// Los LEDs van pegados al arma (la posición del modelo, en metros). En los modelos el largo es el eje X y el alto el Z.
Shader "Riftwalker/Skins/Cartel"
{
    Properties
    {
        [HideInInspector] _AjusteEscala ("Ajuste del tamaño del patrón", Float) = 1
        _Parte ("Parte (0 cartel, 2 franjas)", Float) = 0
        [NoScaleOffset] _Texto ("Texto (blanco sobre negro)", 2D) = "black" {}
        _ColorLed ("LED prendido", Color) = (1, 0.541, 0.118, 1)
        _ColorApagado ("LED apagado", Color) = (0.102, 0.055, 0.02, 1)
        _ColorCarcasa ("Carcasa", Color) = (0.07, 0.07, 0.07, 1)
        _Celda ("Tamaño de cada LED (m)", Float) = 0.006
        _Velocidad ("LEDs por segundo que corre el texto", Float) = 22
        _Renglon ("Renglones de alto (texto + espacio)", Float) = 15
        _Espejo ("Dar vuelta el texto (1 o -1)", Float) = 1
        _Brillo ("Brillo", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Cartel"
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
                float4 _ColorLed, _ColorApagado, _ColorCarcasa;
                float _Parte, _Celda, _Velocidad, _Renglon, _Espejo, _Brillo;
                float4 _Texto_TexelSize;
            CBUFFER_END
            TEXTURE2D(_Texto); SAMPLER(sampler_point_repeat);

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
                float3 n = abs(normalize(i.normalOS));
                int parte = (int)round(_Parte);
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewWS);
                float x = i.posM.x * _Espejo, z = i.posM.z;
                float3 color;
                if (parte == 2) // franjas: grupos de LEDs que se prenden en secuencia
                {
                    float c = x / (_Celda * 3.0);
                    float grupo = fmod(floor(c) - floor(_Time.y * 10.0), 12.0);
                    grupo = grupo < 0.0 ? grupo + 12.0 : grupo;
                    float punto = saturate(1.0 - abs(frac(c) - 0.5) / 0.4);
                    float prendido = step(grupo, 2.5);
                    color = Iluminar(_ColorApagado.rgb * 1.6, N, V, i.posWS, i.positionCS, 0.1) +
                            float3(1.0, 0.70, 0.28) * prendido * punto * _Brillo * 0.8;
                }
                else
                {
                    // La grilla de LEDs: a lo largo del arma (x) y "hacia arriba" dando la vuelta por encima: en los
                    // costados es la altura y arriba sigue hacia el centro (z - |y|), así el texto pasa del costado a
                    // la parte de arriba sin cortarse. En las puntas del arma (caras que miran en x), solo la grilla.
                    bool punta = n.x > max(n.y, n.z);
                    float2 p = punta ? i.posM.yz : float2(x, z - abs(i.posM.y));
                    float2 celda = p / _Celda;
                    float2 id = floor(celda);
                    float2 f = frac(celda) - 0.5;
                    float punto = saturate(1.0 - length(f) / 0.42);
                    // El texto: los renglones hacia arriba y el texto que corre a lo largo (x). Cada renglón es una
                    // vuelta del texto, corrida para que no queden alineados.
                    float prendido = 0.0;
                    if (!punta)
                    {
                        float renglon = floor(id.y / _Renglon);
                        float fila = id.y - renglon * _Renglon;       // 0 abajo
                        float alto = _Texto_TexelSize.w;              // filas de la textura
                        if (fila < alto)
                        {
                            // (Girado media vuelta respecto del modelo: así se lee al derecho en el arma.)
                            float col = -id.x + floor(_Time.y * _Velocidad) + renglon * 37.0;
                            float2 uv = float2((col + 0.5) * _Texto_TexelSize.x, (fila + 0.5) / alto);
                            prendido = SAMPLE_TEXTURE2D(_Texto, sampler_point_repeat, uv).r;
                        }
                    }
                    float3 carcasa = Iluminar(_ColorCarcasa.rgb, N, V, i.posWS, i.positionCS, 0.15);
                    float3 apagado = Iluminar(_ColorApagado.rgb * 1.5, N, V, i.posWS, i.positionCS, 0.25);
                    color = lerp(carcasa, apagado, punto) + _ColorLed.rgb * prendido * pow(punto, 0.6) * _Brillo;
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
