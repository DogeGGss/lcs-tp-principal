// Contorno rojo de los rivales (US 031, CA5), como en Valorant: se dibuja una copia del personaje un poco más grande y
// solo por dentro (Cull Front), así lo único que se ve es el borde. El ancho se mide en píxeles de una pantalla de
// 1080 de alto, así queda igual de fino de cerca y de lejos. Usa el buffer de profundidad: no se ve a través de las paredes.
// Para que solo quede la silueta (y no rayas por dentro del cuerpo), antes se marca en el stencil lo que se ve del
// personaje con una máscara del mismo shader (ContornoRival.cs la arma: sin ancho, sin color y una cola antes), y el
// contorno se dibuja solo fuera de esa marca.
Shader "Efectos/ContornoRival"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.361, 0.361, 1)
        _Ancho ("Ancho (px a 1080)", Range(0, 6)) = 2
        [HideInInspector] _Cull ("Cull", Float) = 1               // Front
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _Offset ("Offset", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 6 // NotEqual
        [HideInInspector] _StencilPass ("Stencil Pass", Float) = 0 // Keep
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+2" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull [_Cull]
        ColorMask [_ColorMask]
        ZWrite [_ZWrite]
        Offset [_Offset], [_Offset]
        Stencil
        {
            Ref 64
            ReadMask 64
            WriteMask 64
            Comp [_StencilComp]
            Pass [_StencilPass]
        }

        Pass
        {
            Name "ContornoRival"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Ancho;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                float4 clip = TransformObjectToHClip(i.positionOS.xyz);
                // La normal en la pantalla dice hacia dónde agrandar cada vértice.
                float2 normal = TransformWorldToHClipDir(TransformObjectToWorldNormal(i.normalOS)).xy;
                normal /= max(length(normal), 1e-5);
                float2 borde = normal * (_Ancho / 540.0) * clip.w;
                borde.x *= _ScreenParams.y / _ScreenParams.x;
                clip.xy += borde;
                o.positionCS = clip;
                o.fog = ComputeFogFactor(clip.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
