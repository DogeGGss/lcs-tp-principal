// El camino al puesto de suministros (US 335, CA8 y CA9): flechas (chevrones) que avanzan a lo largo de un LineRenderer
// acostado en el piso, como el camino de Overwatch. Se dibuja dos veces: normal, y a través de las paredes más tenue
// (se ve "un poco"). La usa CaminoAlPuesto.cs. Está en Resources para que entre en el juego compilado.
// El LineRenderer va con textureMode Tile: uv.x son los metros a lo largo del camino y uv.y va de un borde al otro. El
// camino arranca en el puesto (uv.x = 0 ahí) y las flechas apuntan y avanzan hacia uv.x = 0: así quedan quietas en el
// piso aunque el jugador camine y el camino se vuelva a calcular.
Shader "Efectos/Camino"
{
    Properties
    {
        _Color ("Color", Color) = (0.49, 0.88, 0.35, 1)
        _Periodo ("Metros entre flechas", Float) = 1.1
        _Velocidad ("Metros por segundo", Float) = 1.6
        _Atraves ("Opacidad a través de las paredes", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Periodo;
            float _Velocidad;
            half _Atraves;
        CBUFFER_END

        Varyings vert(Attributes i)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
            o.color = i.color;
            o.uv = i.uv;
            return o;
        }

        // Las flechas: una "V" que apunta hacia el puesto (uv.x decreciente), y una franja suave abajo de todo.
        half Flechas(float2 uv)
        {
            float lado = abs(uv.y - 0.5) * 2;                             // 0 en el medio, 1 en el borde
            float s = frac((-uv.x - _Time.y * _Velocidad) / _Periodo);    // 0..1 en cada tramo
            float x = s + lado * 0.32;                                    // corre los bordes hacia atrás: la V
            half flecha = smoothstep(0.30, 0.36, x) * (1 - smoothstep(0.58, 0.64, x));
            flecha *= 1 - smoothstep(0.85, 1.0, lado);
            half franja = 0.18 * (1 - smoothstep(0.55, 0.9, lado));
            return max(flecha, franja);
        }

        half4 Pintar(Varyings i, half opacidad)
        {
            half a = Flechas(i.uv) * i.color.a * _Color.a * opacidad;
            return half4(_Color.rgb * i.color.rgb, a);
        }
        ENDHLSL

        Pass
        {
            Name "Camino"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target { return Pintar(i, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "CaminoAtraves"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Greater
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target { return Pintar(i, _Atraves); }
            ENDHLSL
        }
    }
}
