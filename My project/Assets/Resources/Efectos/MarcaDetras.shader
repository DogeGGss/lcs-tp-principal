// US 269, CA4: la silueta roja de un portal de enemigos, solo donde lo tapa el mapa (ZTest Greater). Es la marca del
// perro del Callejero (MarcaAtraves.shader), más llena, pero sin dibujarse donde el portal se ve directo.
// Se dibuja antes que los enemigos y que el propio portal (cola AlphaTest-50; ellos van en 2460, ColaDespuesDeLaMarca),
// así solo la tapan las paredes y el piso: un enemigo que pasa por delante se ve normal (se dibuja encima) y el portal
// no se tapa a sí mismo. Está en Resources para que entre en el juego compilado (MarcaAtraves.cs la carga).
Shader "Efectos/MarcaDetras"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.361, 0.361, 1)
        _Alfa ("Alfa", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "AlphaTest-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Greater
        Cull Back

        Pass
        {
            Name "MarcaDetras"
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
                return half4(_BaseColor.rgb, (0.55 + 0.45 * borde) * _Alfa);
            }
            ENDHLSL
        }
    }
}
