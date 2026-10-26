// US 022, CA2: el material holográfico del perro del Callejero. Medio transparente y aditivo (brilla también en lugares
// oscuros), más claro en los bordes de la silueta, con líneas de escaneo que suben y un leve parpadeo. _Desvanecer (0 a 1)
// lo apaga al morir o al terminar su tiempo (CA4). El color lo pone Perro.cs: el de compañero o el de rival (CA3).
// Está en Resources para que entre en el juego compilado aunque ningún material lo use (Perro lo carga por nombre).
Shader "Efectos/Holograma"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.35, 0.65, 1, 1)
        _Desvanecer ("Desvanecer", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Holograma"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Desvanecer;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS), v = normalize(i.viewWS);
                half borde = pow(1.0 - saturate(abs(dot(n, v))), 2.2);
                half escaneo = 0.55 + 0.45 * step(0.45, frac(i.positionWS.y * 14.0 - _Time.y * 2.5));
                half parpadeo = 0.85 + 0.15 * sin(_Time.y * 37.0) * sin(_Time.y * 13.0);
                half alfa = (0.2 + 0.9 * borde) * escaneo * parpadeo * (1.0 - _Desvanecer) * _BaseColor.a;
                return half4(_BaseColor.rgb * (0.7 + 1.6 * borde), alfa);
            }
            ENDHLSL
        }
    }
}
