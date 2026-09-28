// Brillo aditivo sin niebla para luciérnagas, polillas y estrellas fugaces.
// La niebla del mapa es densa, así que un shader de partículas normal las apagaría a lo lejos.
Shader "Uni/BrilloAditivo"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensidad ("Intensidad", Float) = 1
        _FadeInicio ("Empieza a desaparecer (m)", Float) = 100000
        _FadeFin ("Desaparece del todo (m)", Float) = 100001
        _FadeCerca ("Desaparece de muy cerca (m)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensidad;
                float _FadeInicio;
                float _FadeFin;
                float _FadeCerca;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                // Se apaga con la distancia a la cámara, y también si pasa pegado a ella.
                float dist = distance(posWS, _WorldSpaceCameraPos);
                float lejos = saturate((_FadeFin - dist) / max(_FadeFin - _FadeInicio, 0.001));
                float cerca = _FadeCerca > 0 ? saturate(dist / _FadeCerca - 0.5) : 1;
                o.color = i.color;
                o.color.a *= lejos * cerca;
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 c = t.rgb * t.a * i.color.rgb * i.color.a * _Color.rgb * _Intensidad;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
