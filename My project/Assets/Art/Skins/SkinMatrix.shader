// Skin de arma "Matrix" (prueba de skins con material animado, F33): código binario verde que cae por el arma.
// Todo se calcula acá, sin texturas: el arma se divide en columnas de celdas, cada celda dibuja un 0 o un 1 que cambia
// cada tanto, y por cada columna baja una "gota" con la cabeza clara y una cola que se apaga. Los dígitos van pegados al
// arma (se usa la posición del modelo), así que acompañan al arma cuando se mueve; caen hacia abajo del modelo.
// Cada modelo tiene su "arriba" (_Arriba): en el Mitre (AR_1.fbx) es el eje Z del modelo, en otros puede ser el Y.
Shader "Riftwalker/Skins/Matrix"
{
    Properties
    {
        _ColorFondo ("Fondo", Color) = (0.01, 0.035, 0.015, 1)
        _ColorCodigo ("Código", Color) = (0.15, 1, 0.35, 1)
        _ColorCabeza ("Cabeza de la gota", Color) = (0.85, 1, 0.88, 1)
        _Celda ("Tamaño de cada dígito (m)", Float) = 0.012
        _Velocidad ("Velocidad de caída", Float) = 1.6
        _Cola ("Largo de la cola", Range(0.05, 1)) = 0.45
        _Cambio ("Cambios de dígito por segundo", Float) = 6
        _Brillo ("Brillo", Float) = 2.2
        _Borde ("Brillo del borde", Float) = 0.6
        _Arriba ("Arriba del modelo", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Matrix"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorFondo, _ColorCodigo, _ColorCabeza;
                float4 _Arriba;
                float _Celda, _Velocidad, _Cola, _Cambio, _Brillo, _Borde;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posM : TEXCOORD0;   // posición en el modelo, en metros (con la escala del arma)
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                // Escala del modelo: los modelos de armas vienen en otras unidades; así la celda se mide en metros.
                float3 escala = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                o.posM = v.positionOS.xyz * escala;
                o.normalOS = v.normalOS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Azar(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // Un dígito dentro de su celda (uv de 0 a 1): el 1 es una barra vertical, el 0 un óvalo hueco.
            float Digito(float2 uv, float uno)
            {
                float2 c = uv - 0.5;
                float barra = step(abs(c.x), 0.09) * step(abs(c.y), 0.34);
                float2 e = c / float2(0.26, 0.36);
                float r = length(e);
                float ovalo = step(r, 1.0) * step(0.62, r);
                return lerp(ovalo, barra, uno);
            }

            float4 frag(Varyings i) : SV_Target
            {
                // Tres ejes del modelo: arriba (por donde cae el código) y dos de costado.
                float3 arriba = normalize(_Arriba.xyz);
                float3 lado = normalize(cross(arriba, abs(arriba.x) < 0.9 ? float3(1, 0, 0) : float3(0, 1, 0)));
                float3 otro = cross(arriba, lado);
                float3 n = normalize(i.normalOS);
                float nl = abs(dot(n, lado)), no = abs(dot(n, otro)), na = abs(dot(n, arriba));
                float pl = dot(i.posM, lado), po = dot(i.posM, otro), pa = dot(i.posM, arriba);
                // De qué lado mira la cara: los dígitos se apoyan en el plano que mejor le queda y caen hacia abajo
                // (en las caras de arriba y abajo, a lo largo del otro eje).
                float2 plano = nl > no ? float2(po, pa) : float2(pl, pa);
                if (na > max(nl, no)) plano = float2(pl, po);

                float2 celda = plano / _Celda;
                float2 id = floor(celda);
                float2 uv = frac(celda);

                // Cada columna tiene su velocidad y su desfase; la gota baja y deja una cola que se apaga.
                float semilla = Azar(float2(id.x, 7.3));
                float velocidad = _Velocidad * (0.6 + 0.8 * semilla);
                float largo = 24.0 + 18.0 * Azar(float2(id.x, 3.1)); // cada cuántas celdas se repite la gota
                float fase = frac((id.y / largo) + _Time.y * velocidad / largo * 6.0 + semilla * 13.0);
                float cola = saturate(1.0 - fase / _Cola);
                float cabeza = step(1.0 - 1.0 / largo, 1.0 - fase) * step(fase, 1.0 / largo);

                // El dígito de cada celda cambia de vez en cuando (unas celdas más rápido que otras).
                float tic = floor(_Time.y * _Cambio * (0.3 + Azar(id)));
                float uno = step(0.5, Azar(id + tic * 0.137));
                float forma = Digito(uv, uno);

                float3 codigo = _ColorCodigo.rgb * forma * (0.08 + cola * cola) * _Brillo;
                codigo = lerp(codigo, _ColorCabeza.rgb * forma * _Brillo * 1.3, cabeza);

                // Un borde verde suave, para que el arma se siga leyendo como un objeto.
                float borde = pow(1.0 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), 3.0) * _Borde;

                float3 color = _ColorFondo.rgb + codigo + _ColorCodigo.rgb * borde;
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
