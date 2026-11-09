// Una línea que brilla (LineRenderer), más fuerte en el medio y suave en los bordes, sumando luz. El color del material
// puede pasar de 1 (HDR) para que resplandezca con el bloom. La usa DistanciaDeNpc.cs: la mira y el rayo del ojo del
// Watcher francotirador (US 285). Está en Resources para que entre en el juego compilado.
Shader "Efectos/LineaBrillante"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2, 0.2, 0.2, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LineaBrillante"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = abs(i.uv.y - 0.5) * 2;           // 0 en el medio de la línea, 1 en el borde
                half nucleo = 1 - smoothstep(0, 0.35, d);  // el centro, blanco y fuerte
                half halo = 1 - smoothstep(0.2, 1, d);
                half a = i.color.a;
                half3 c = _Color.rgb * halo + half3(1, 1, 1) * nucleo * 0.6 * saturate(_Color.a);
                return half4(c * i.color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
