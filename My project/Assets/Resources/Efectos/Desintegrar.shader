// US 284: el cuerpo de un enemigo muerto que se desintegra de arriba hacia abajo. Reemplaza su material solo durante
// esos segundos (DesintegracionDeNpc.cs): su textura con una luz simple (la luz principal y la del ambiente, con niebla),
// y se va borrando por debajo de una altura que baja (_Corte), con el borde irregular (ruido) y encendido del color de
// sus marcas (pasa el umbral del bloom, como si se quemara desde adentro).
// Está en Resources para que entre en el juego compilado.
Shader "Efectos/Desintegrar"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [HDR] _ColorBorde ("Color del borde", Color) = (0.5, 2.5, 0.5, 1)
        _Corte ("Altura del corte (mundo)", Float) = 100
        _Ruido ("Qué tan desparejo es el borde (m)", Float) = 0.3
        _Ancho ("Ancho del borde encendido (m)", Float) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "Desintegrar"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float niebla : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ColorBorde;
                float _Corte, _Ruido, _Ancho;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.niebla = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Azar(float3 c) { return frac(sin(dot(c, float3(127.1, 311.7, 74.7))) * 43758.5453); }

            float Ruido(float3 p)
            {
                float3 c = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = lerp(Azar(c), Azar(c + float3(1, 0, 0)), f.x);
                float b = lerp(Azar(c + float3(0, 1, 0)), Azar(c + float3(1, 1, 0)), f.x);
                float e = lerp(Azar(c + float3(0, 0, 1)), Azar(c + float3(1, 0, 1)), f.x);
                float g = lerp(Azar(c + float3(0, 1, 1)), Azar(c + float3(1, 1, 1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(e, g, f.y), f.z);
            }

            half4 frag(Varyings i, bool frente : SV_IsFrontFace) : SV_Target
            {
                // Más arriba que el corte (con el borde desparejo): ya se desintegró.
                float n = Ruido(i.positionWS * 3.1) * 0.65 + Ruido(i.positionWS * 9.7) * 0.35;
                float queda = _Corte - (i.positionWS.y + (n - 0.5) * 2 * _Ruido);
                clip(queda);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 normal = normalize(i.normalWS) * (frente ? 1 : -1);
                Light luz = GetMainLight();
                half3 color = tex.rgb * (SampleSH(normal) + luz.color * saturate(dot(normal, luz.direction)));
                color = MixFog(color, i.niebla);

                // El borde que se está quemando.
                float borde = 1 - smoothstep(0, _Ancho, queda);
                color = lerp(color, _ColorBorde.rgb, saturate(borde * 1.2)) + _ColorBorde.rgb * borde * borde;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
