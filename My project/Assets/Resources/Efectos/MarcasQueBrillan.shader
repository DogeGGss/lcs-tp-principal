// US 283: las marcas bioluminiscentes de los enemigos (manchas, aros o rayas que brillan en la oscuridad), para que se
// vean de noche. Va encima del material del bicho (una pasada más, sumando luz), así no se toca su material Lit.
// El patrón sale de la posición de cada punto del modelo en su pose quieta (TEXCOORD3, la guarda MarcasQueBrillan.cs),
// así queda pegado al cuerpo cuando se mueve. En TEXCOORD3.w va cuánto lleva marcas ese punto: 0 en lo que no es
// piel (la lanza del Catfish, por ejemplo). Brilla más que el umbral del bloom, así resplandece.
// Está en Resources para que entre en el juego compilado (MarcasQueBrillan.cs la carga por nombre).
Shader "Efectos/MarcasQueBrillan"
{
    Properties
    {
        [HDR] _ColorDeMarcas ("Color", Color) = (0, 1, 1, 1)
        [MainTexture] _BaseMap ("Textura del bicho (para los ojos)", 2D) = "black" {}
        _BrilloOjos ("Brillo de los ojos", Float) = 4
        _Delineado ("Delineado de los ojos", Range(0, 1)) = 0
        _Ojo1 ("Ojo 1 (pose quieta, w: radio)", Vector) = (0, 0, 0, 0)
        _Ojo2 ("Ojo 2 (pose quieta, w: radio)", Vector) = (0, 0, 0, 0)
        _Patron ("Patrón (0 ninguno, 1 manchas, 2 aros, 3 rayas en el lomo)", Float) = 2
        _OjosOscuros ("Ojos: brilla lo negro (huecos)", Float) = 0
        _Cantidad ("Cantidad (a lo largo del cuerpo)", Float) = 9
        _Tamanio ("Tamaño", Range(0.1, 1)) = 0.6
        _Huecos ("Huecos", Range(0, 0.95)) = 0.35
        _Brillo ("Brillo", Float) = 2.2
        _Opacidad ("Opacidad", Range(0, 1)) = 0.5
        _Pulso ("Pulso", Range(0, 1)) = 0.35
        _Irregular ("Manchas y aros: irregularidad (0 redondas, 1 como de vaca)", Range(0, 1)) = 0
        _Separacion ("Rayas: separación (largo del cuerpo = 1)", Float) = 0.05
        _AnchoRayas ("Rayas: ancho", Float) = 0.015
        _LargoRayas ("Rayas: largo hacia cada costado", Float) = 0.08
        _Destello ("Destello (al recibir un golpe)", Range(0, 1)) = 0
        _Apagado ("Apagado (al morir)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Cull Back

        Pass
        {
            Name "MarcasQueBrillan"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 quieto : TEXCOORD3;       // posición en la pose quieta (largo del cuerpo = 1); w: lleva marcas
                float4 normalQuieto : TEXCOORD4; // normal en la pose quieta (y = arriba); w: es ojo
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 quieto : TEXCOORD0;
                float4 normalQuieto : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorDeMarcas;
                float4 _BaseMap_ST;
                float _BrilloOjos, _Delineado, _OjosOscuros;
                float4 _Ojo1, _Ojo2;
                float _Patron, _Cantidad, _Tamanio, _Huecos, _Brillo, _Opacidad, _Pulso, _Destello, _Apagado, _Separacion, _AnchoRayas, _LargoRayas, _Irregular;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.quieto = i.quieto;
                o.normalQuieto = i.normalQuieto;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            float3 Azar3(float3 c)
            {
                c = float3(dot(c, float3(127.1, 311.7, 74.7)), dot(c, float3(269.5, 183.3, 246.1)), dot(c, float3(113.5, 271.9, 124.6)));
                return frac(sin(c) * 43758.5453);
            }

            // Ruido suave (de valor) entre 0 y 1.
            float Ruido(float3 p)
            {
                float3 c = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = lerp(Azar3(c).x, Azar3(c + float3(1, 0, 0)).x, f.x);
                float b = lerp(Azar3(c + float3(0, 1, 0)).x, Azar3(c + float3(1, 1, 0)).x, f.x);
                float e = lerp(Azar3(c + float3(0, 0, 1)).x, Azar3(c + float3(1, 0, 1)).x, f.x);
                float g = lerp(Azar3(c + float3(0, 1, 1)).x, Azar3(c + float3(1, 1, 1)).x, f.x);
                return lerp(lerp(a, b, f.y), lerp(e, g, f.y), f.z);
            }

            // Celdas (Worley): la distancia al centro de la marca más cercana y un azar de esa marca.
            void Celdas(float3 p, out float d, out float3 azar)
            {
                float3 celda = floor(p), f = frac(p);
                d = 10; azar = 0;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    float3 o = float3(x, y, z);
                    float3 a = Azar3(celda + o);
                    float3 centro = o + 0.15 + 0.7 * a;
                    float dd = length(centro - f);
                    if (dd < d) { d = dd; azar = Azar3(celda + o + 17.31); }
                }
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 p = i.quieto.xyz * _Cantidad;
                float marca = 0, aura = 0;

                if (_Patron > 0.5 && _Patron < 2.5)
                {
                    float d; float3 azar;
                    // Irregularidad: se deforma el espacio antes de buscar las marcas (así salen manchas de vaca, con
                    // formas largas y torcidas) y se ondula el borde.
                    float3 q = p * 0.8;
                    p += (float3(Ruido(q), Ruido(q + 31.4), Ruido(q + 57.2)) - 0.5) * (_Irregular * 1.8);
                    Celdas(p, d, azar);
                    d += (Ruido(p * 3.3 + 11.7) - 0.5) * _Irregular * 0.22;
                    float hay = step(_Huecos, azar.x);                // algunas celdas sin marca
                    float radio = _Tamanio * lerp(0.28, 0.42, azar.y);
                    float aa = max(fwidth(d), _Tamanio * 0.06); // bordes suaves, como bajo la piel
                    if (_Patron < 1.5)
                    {
                        // Manchas llenas.
                        marca = 1 - smoothstep(radio - aa, radio + aa, d);
                        aura = 1 - smoothstep(radio, radio * 1.8, d);
                    }
                    else
                    {
                        // Aros: un anillo alrededor de cada marca.
                        float ancho = radio * 0.28;
                        float e = abs(d - radio);
                        marca = 1 - smoothstep(ancho - aa, ancho + aa, e);
                        aura = 1 - smoothstep(ancho, ancho * 3.5, e);
                    }
                    marca *= hay; aura *= hay;
                }
                else if (_Patron > 2.5)
                {
                    // Rayas cruzadas sobre el lomo: una cada _Separacion a lo largo del cuerpo (z), onduladas, solo
                    // arriba. Todo medido en el largo del cuerpo (1).
                    float sep = max(_Separacion, 0.004);
                    float onda = sin(i.quieto.x * 9 + i.quieto.z * 3) * 0.12 * sep;
                    float e = abs(frac((i.quieto.z + onda) / sep) - 0.5) * sep; // distancia al medio de la raya
                    float aa = max(fwidth(e), 0.0015);
                    float ancho = max(_AnchoRayas, 0.0005) * 0.5;
                    // Se afinan hacia los costados hasta terminar en punta a _LargoRayas del medio del lomo.
                    float costado = saturate(1 - abs(i.quieto.x) / max(_LargoRayas, 0.005));
                    float lomo = smoothstep(0.25, 0.7, normalize(i.normalQuieto.xyz).y);
                    ancho *= costado;
                    marca = (1 - smoothstep(ancho - aa, ancho + aa, e)) * lomo;
                    aura = (1 - smoothstep(ancho, ancho * 2.5 + 0.004, e)) * lomo * costado;
                }

                // Respira despacio, como una ola que recorre el cuerpo.
                float pulso = 1 + _Pulso * sin(_Time.y * 2.2 + i.quieto.z * 6);
                float brillo = _Brillo * pulso * (1 + _Destello * 2.5) * (1 - _Apagado);
                // Opacidad: cuánto se notan sobre la piel (bajas, se ven como de adentro del bicho).
                float ojo = smoothstep(0.5, 1.0, i.normalQuieto.w);
                float luz = (marca + aura * 0.1) * _Opacidad * saturate(i.quieto.w) * (1 - ojo);

                // Los ojos brillan del color de las marcas, con el dibujo de su textura (lo de color fuerte, el iris; la pupila oscura y la piel no).
                half3 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float iris = smoothstep(0.2, 0.5, max(t.r, max(t.g, t.b)) - min(t.r, min(t.g, t.b))); // lo de color fuerte
                // O lo negro: los huecos de la cabeza del Jefe (US 146), su punto débil.
                iris = lerp(iris, 1 - smoothstep(0.03, 0.1, max(t.r, max(t.g, t.b))), _OjosOscuros);
                float luzOjo = ojo * iris * _BrilloOjos * (1 + _Pulso * 0.3 * sin(_Time.y * 2.2)) * (1 + _Destello) * (1 - _Apagado);

                // Delineado: un anillo de manchitas alrededor de cada ojo.
                float delineado = 0;
                if (_Delineado > 0)
                {
                    float4 o = distance(i.quieto.xyz, _Ojo1.xyz) / max(_Ojo1.w, 1e-4) < distance(i.quieto.xyz, _Ojo2.xyz) / max(_Ojo2.w, 1e-4) ? _Ojo1 : _Ojo2;
                    float r = max(o.w, 1e-4);
                    float dist = distance(i.quieto.xyz, o.xyz) / r;   // 1: el borde del ojo
                    float banda = smoothstep(0.9, 1.2, dist) * (1 - smoothstep(1.7, 2.6, dist));
                    float dm; float3 am;
                    Celdas(i.quieto.xyz / (r * 0.32), dm, am);
                    float radioPunto = lerp(0.16, 0.3, am.y);
                    float aaP = max(fwidth(dm), 0.02);
                    float punto = (1 - smoothstep(radioPunto - aaP, radioPunto + aaP, dm)) * step(0.25, am.x);
                    delineado = banda * punto * _Delineado * _BrilloOjos * 0.6 * (1 + _Destello) * (1 - _Apagado);
                }

                return half4(_ColorDeMarcas.rgb * (brillo * luz + luzOjo + delineado), 0);
            }
            ENDHLSL
        }
    }
}
