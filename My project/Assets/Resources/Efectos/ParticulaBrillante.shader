// Partícula redonda y suave, sin textura (el círculo sale de la UV del cuadradito de la partícula). El color de la
// partícula por el color del material, que puede pasar de 1 (HDR) para que resplandezca con el bloom.
// La mezcla se elige en el material: sumando luz (brillos, gotas que brillan) o normal (humo).
// La usa EfectoDeVeneno.cs (la bola de veneno del Jefe, US 267). Está en Resources para que entre en el juego compilado.
Shader "Efectos/ParticulaBrillante"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Suave ("Borde suave", Range(0.01, 1)) = 0.6
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Mezcla origen", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Mezcla destino", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ParticulaBrillante"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Suave;
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
                float d = length(i.uv - 0.5) * 2;                 // 0 en el medio, 1 en el borde
                half circulo = 1 - smoothstep(1 - _Suave, 1, d);
                half4 c = i.color * _Color;
                c.a *= circulo;
                c.rgb *= c.a; // premultiplicado: sirve igual sumando (One One) que mezclando (One OneMinusSrcAlpha)
                return c;
            }
            ENDHLSL
        }
    }
}
