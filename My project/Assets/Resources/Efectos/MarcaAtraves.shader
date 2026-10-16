// US 022, CA8: la silueta del rival mordido por el perro del Callejero, que se ve a través de las paredes (ZTest Always)
// para el dueño del perro y sus compañeros. Un relleno tenue con el borde más fuerte, del color de rival.
// Está en Resources para que entre en el juego compilado (MarcaAtraves.cs lo carga por nombre).
Shader "Efectos/MarcaAtraves"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.361, 0.361, 1)
        _Alfa ("Alfa", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Back

        Pass
        {
            Name "MarcaAtraves"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Alfa;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half borde = pow(1.0 - saturate(abs(dot(normalize(i.normalWS), normalize(i.viewWS)))), 1.8);
                return half4(_BaseColor.rgb, (0.28 + 0.72 * borde) * _Alfa);
            }
            ENDHLSL
        }
    }
}
