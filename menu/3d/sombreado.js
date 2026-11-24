// Los shaders de las skins de armas del juego (Assets/Art/Skins/*.shader), pasados de HLSL (Unity, URP) a GLSL (three.js).
// Los patrones, los colores y las animaciones son los mismos; la luz es más simple: una luz principal, una de relleno y el
// ambiente (en el juego, la de la escena y sus reflejos). Lo que el juego calcula con "posM" (la posición en el modelo, en
// metros) acá también: así el patrón queda pegado al arma y del mismo tamaño.
// Cada material es una "familia" (#define FAMILIA): 0 color liso, 1 patrón (SkinPatron), 2 código binario (SkinMatrix),
// 3 cartel de LEDs (SkinCartel), 4 mapa del subte (SkinMapa), 5 dragón (SkinDragon), 6 tormenta (SkinTormenta) y
// 7 cuchillo (SkinCuchillo).

export const vertice = /* glsl */`
uniform float uK;          // cuánto mide en metros una unidad del modelo (posM = position * uK)
varying vec3 vPosM, vPosOS, vNormalOS, vNormalW, vVistaW, vMx, vMy, vMz;
varying vec2 vUv;
void main() {
  // Unity da vuelta el eje X al importar el FBX (de mano derecha a mano izquierda): así los patrones quedan como en el juego.
  vec3 espejo = vec3(-1.0, 1.0, 1.0);
  vPosOS = position * espejo;
  vPosM = vPosOS * uK;
  vNormalOS = normal * espejo;
  vec4 w = modelMatrix * vec4(position, 1.0);
  vMx = normalize(mat3(modelMatrix) * vec3(-1.0, 0.0, 0.0)); // (el eje X dado vuelta, como vPosOS)
  vMy = normalize(mat3(modelMatrix) * vec3(0.0, 1.0, 0.0));
  vMz = normalize(mat3(modelMatrix) * vec3(0.0, 0.0, 1.0));
  vNormalW = normalize(mat3(modelMatrix) * normal);
  vVistaW = cameraPosition - w.xyz;
  vUv = uv;
  gl_Position = projectionMatrix * viewMatrix * w;
}`;

const comun = /* glsl */`
uniform float uTiempo;
varying vec3 vPosM, vPosOS, vNormalOS, vNormalW, vVistaW, vMx, vMy, vMz;
varying vec2 vUv;
#define PI 3.14159265
float sat(float v) { return clamp(v, 0.0, 1.0); }
vec3 sat(vec3 v) { return clamp(v, 0.0, 1.0); }
float azar(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
vec2 azar2(vec2 p) { return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453); }
float azar3(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float ruido(vec2 p) {
  vec2 i = floor(p), f = fract(p), u = f * f * (3.0 - 2.0 * f);
  return mix(mix(azar(i), azar(i + vec2(1, 0)), u.x), mix(azar(i + vec2(0, 1)), azar(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int k = 0; k < 5; k++) { v += a * ruido(p); p = p * 2.03 + 17.1; a *= 0.5; } return v; }
float ruido3(vec3 p) {
  vec3 i = floor(p), f = fract(p), u = f * f * (3.0 - 2.0 * f);
  float a = mix(mix(azar3(i), azar3(i + vec3(1, 0, 0)), u.x), mix(azar3(i + vec3(0, 1, 0)), azar3(i + vec3(1, 1, 0)), u.x), u.y);
  float b = mix(mix(azar3(i + vec3(0, 0, 1)), azar3(i + vec3(1, 0, 1)), u.x), mix(azar3(i + vec3(0, 1, 1)), azar3(i + vec3(1, 1, 1)), u.x), u.y);
  return mix(a, b, u.z);
}
float fbm3(vec3 p) { float v = 0.0, a = 0.5; for (int k = 0; k < 5; k++) { v += a * ruido3(p); p = p * 2.03 + 17.1; a *= 0.5; } return v; }
// La luz de la vista previa: la principal de arriba adelante, una de relleno de atrás y el ambiente.
const vec3 LUZ = vec3(0.3624, 0.7247, 0.5859);
const vec3 RELLENO = vec3(-0.6, 0.25, -0.75);
const float AMBIENTE = 0.30;
vec3 aMundo(vec3 n) { return normalize(n.x * vMx + n.y * vMy + n.z * vMz); }
float luzDifusa(vec3 N) { return sat(dot(N, LUZ)) * 0.9 + sat(dot(N, normalize(RELLENO))) * 0.25 + AMBIENTE; }
vec3 iluminar(vec3 albedo, vec3 N, vec3 V, float brillo) {
  vec3 c = albedo * luzDifusa(N);
  return c + pow(sat(dot(N, normalize(LUZ + V))), 32.0) * brillo;
}
// Lo que se refleja (en el juego, el cielo de la escena): un degradé de abajo oscuro a arriba claro.
vec3 entorno(vec3 r) { return mix(vec3(0.03, 0.035, 0.045), vec3(0.32, 0.35, 0.42), sat(r.y * 0.5 + 0.5)); }
`;

const familias = {
  // ---------- 0: color liso (las comunes y las partes que quedan como vienen) ----------
  0: `
uniform vec3 uColor;
vec3 colorDe(vec3 N, vec3 V) {
  vec3 c = iluminar(uColor, N, V, 0.18);
  float fr = pow(1.0 - sat(dot(N, V)), 5.0);
  return c + entorno(reflect(-V, N)) * mix(0.04, 1.0, fr) * 0.6;
}`,
  // ---------- 1: SkinPatron (mármol, azulejos, cristal y oro) ----------
  1: `
uniform float uPatron, uEscala, uMetal, uLisura;
uniform vec3 uC1, uC2, uC3, uC4;
void patron(vec2 uv, out vec3 color, out float lisura, out float metal) {
  lisura = uLisura; metal = uMetal;
  int p = int(uPatron + 0.5);
  if (p == 3) {
    vec2 q = uv / uEscala;
    float t = q.x * 0.6 + q.y * 0.25 + fbm(q * 0.7) * 6.0;
    float veta = 1.0 - smoothstep(0.0, 0.12, abs(sin(t)));
    float fina = 1.0 - smoothstep(0.0, 0.05, abs(sin(t * 2.7 + 1.3)));
    float nube = fbm(q * 1.7 + 3.0);
    color = uC1 * (0.92 + 0.08 * nube);
    color = mix(color, uC2, veta * 0.85);
    color = mix(color, uC3, fina * 0.35);
  } else if (p == 4) {
    vec2 c = uv / uEscala, id = floor(c), f = fract(c);
    float junta = step(0.07, f.x) * step(f.x, 0.93) * step(0.07, f.y) * step(f.y, 0.93);
    float r = azar(id);
    vec3 az = r < 0.45 ? uC1 : r < 0.8 ? uC2 : uC3;
    az *= 0.92 + 0.12 * azar(id + 7.7);
    color = mix(uC4, az, junta);
    lisura = mix(0.25, uLisura, junta);
  } else if (p == 5) {
    vec2 q = uv / uEscala, id = floor(q), f = fract(q);
    float d1 = 8.0, d2 = 8.0; vec2 mejor = vec2(0.0);
    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
      vec2 g = vec2(float(x), float(y));
      float d = length(g + azar2(id + g) - f);
      if (d < d1) { d2 = d1; d1 = d; mejor = id + g; } else if (d < d2) d2 = d;
    }
    float borde = 1.0 - smoothstep(0.0, 0.06, d2 - d1);
    color = mix(uC1, uC2, azar(mejor)) * (0.7 + 0.5 * azar(mejor + 3.1));
    color = mix(color, uC3, borde);
  } else {
    float cep = ruido(vec2(uv.x / uEscala * 0.05, uv.y / uEscala * 4.0));
    color = uC1 * (0.9 + 0.15 * cep);
  }
}
vec3 colorDe(vec3 N, vec3 V) {
  vec3 n = abs(normalize(vNormalOS));
  vec2 uv = n.x > max(n.y, n.z) ? vPosM.yz : n.y > n.z ? vPosM.xz : vPosM.xy;
  vec3 albedo; float lisura, metal;
  patron(uv, albedo, lisura, metal);
  vec3 H = normalize(LUZ + V);
  float potencia = exp2(2.0 + 9.0 * lisura);
  vec3 reflejo = mix(vec3(0.04), albedo, metal);
  vec3 especular = reflejo * pow(sat(dot(N, H)), potencia) * (potencia + 2.0) / 8.0;
  vec3 difuso = albedo * (1.0 - metal);
  vec3 color = (difuso + especular) * sat(dot(N, LUZ)) + difuso * (AMBIENTE + sat(dot(N, normalize(RELLENO))) * 0.25);
  float fresnel = pow(1.0 - sat(dot(N, V)), 5.0);
  color += entorno(reflect(-V, N)) * mix(reflejo, vec3(1.0), fresnel * lisura) * (0.4 + 0.6 * lisura);
  return color;
}`,
  // ---------- 2: SkinMatrix (código binario) ----------
  2: `
uniform vec3 uFondo, uCodigo, uCabeza, uArriba;
uniform float uCelda, uVelocidad, uCola, uCambio, uBrillo, uBorde;
float digito(vec2 uv, float uno) {
  vec2 c = uv - 0.5;
  float barra = step(abs(c.x), 0.09) * step(abs(c.y), 0.34);
  float r = length(c / vec2(0.26, 0.36));
  return mix(step(r, 1.0) * step(0.62, r), barra, uno);
}
vec3 colorDe(vec3 N, vec3 V) {
  vec3 arriba = normalize(uArriba);
  vec3 lado = normalize(cross(arriba, abs(arriba.x) < 0.9 ? vec3(1, 0, 0) : vec3(0, 1, 0)));
  vec3 otro = cross(arriba, lado);
  vec3 n = normalize(vNormalOS);
  float nl = abs(dot(n, lado)), no = abs(dot(n, otro)), na = abs(dot(n, arriba));
  float pl = dot(vPosM, lado), po = dot(vPosM, otro), pa = dot(vPosM, arriba);
  vec2 plano = nl > no ? vec2(po, pa) : vec2(pl, pa);
  if (na > max(nl, no)) plano = vec2(pl, po);
  vec2 celda = plano / uCelda, id = floor(celda), uv = fract(celda);
  float semilla = azar(vec2(id.x, 7.3));
  float velocidad = uVelocidad * (0.6 + 0.8 * semilla);
  float largo = 24.0 + 18.0 * azar(vec2(id.x, 3.1));
  float fase = fract(id.y / largo + uTiempo * velocidad / largo * 6.0 + semilla * 13.0);
  float cola = sat(1.0 - fase / uCola);
  float cabeza = step(1.0 - 1.0 / largo, 1.0 - fase) * step(fase, 1.0 / largo);
  float tic = floor(uTiempo * uCambio * (0.3 + azar(id)));
  float forma = digito(uv, step(0.5, azar(id + tic * 0.137)));
  vec3 codigo = uCodigo * forma * (0.08 + cola * cola) * uBrillo;
  codigo = mix(codigo, uCabeza * forma * uBrillo * 1.3, cabeza);
  float borde = pow(1.0 - sat(dot(N, V)), 3.0) * uBorde;
  return uFondo + codigo + uCodigo * borde + (uFondo + 0.06) * sat(dot(N, LUZ)) * 0.5;
}`,
  // ---------- 3: SkinCartel (Próxima estación) ----------
  3: `
uniform vec3 uLed, uApagado, uCarcasa;
uniform float uParte, uCelda, uVelocidad, uLargoTexto, uRenglon, uEspejo, uBrillo;
uniform sampler2D uTexto;
uniform vec2 uTamTexto;
vec3 colorDe(vec3 N, vec3 V) {
  vec3 n = abs(normalize(vNormalOS));
  float x = vPosM.x * uEspejo, z = vPosM.z;
  if (int(uParte + 0.5) == 2) {
    float c = x / (uCelda * 3.0);
    float grupo = mod(floor(c) - floor(uTiempo * 10.0), 12.0);
    float punto = sat(1.0 - abs(fract(c) - 0.5) / 0.4);
    float prendido = step(grupo, 2.5);
    return iluminar(uApagado * 1.6, N, V, 0.1) + vec3(1.0, 0.70, 0.28) * prendido * punto * uBrillo * 0.8;
  }
  bool punta = n.x > max(n.y, n.z);
  vec2 p = punta ? vPosM.yz : vec2(x, z - abs(vPosM.y));
  vec2 celda = p / uCelda, id = floor(celda), f = fract(celda) - 0.5;
  float punto = sat(1.0 - length(f) / 0.42);
  float prendido = 0.0;
  if (!punta) {
    float renglon = floor(id.y / uRenglon);
    float fila = id.y - renglon * uRenglon;
    if (fila < 16.0) {
      float col = -id.x + floor(uTiempo * uVelocidad) + renglon * 37.0;
      float largo = max(uLargoTexto, 1.0);
      col = col - floor(col / largo) * largo;
      float ancho = uTamTexto.x, alto = uTamTexto.y;
      float tira = floor(col / ancho);
      float cx = col - tira * ancho;
      vec2 uv = vec2((cx + 0.5) / ancho, (alto - (tira + 1.0) * 16.0 + fila + 0.5) / alto);
      prendido = texture2D(uTexto, uv).r;
    }
  }
  vec3 carcasa = iluminar(uCarcasa, N, V, 0.15);
  vec3 apagado = iluminar(uApagado * 1.5, N, V, 0.25);
  return mix(carcasa, apagado, punto) + uLed * prendido * pow(punto, 0.6) * uBrillo;
}`,
  // ---------- 4: SkinMapa (Mapa del subte) ----------
  4: `
uniform vec3 uPapel, uTinta;
uniform float uParte, uSeparacion, uGrosor, uTramo, uVelocidad, uDesfase, uInicio, uCiclo, uEspejo;
const vec3 COLORES[6] = vec3[6](vec3(0.0, 0.682, 0.937), vec3(0.933, 0.239, 0.239), vec3(0.0, 0.443, 0.737),
                                vec3(0.0, 0.502, 0.396), vec3(0.427, 0.133, 0.506), vec3(1.0, 0.839, 0.0));
float nivel(float k, float c, float mapa) {
  float salto = floor(azar(vec2(k * 13.1 + mapa * 3.7, c * 7.7 + 1.3)) * 3.0) - 1.0;
  return clamp(k + 1.0 + salto, 0.0, 7.0);
}
float centro(float k, float x, float mapa, out float diagonal, out float c, out float xl) {
  c = floor(x / uTramo);
  xl = x - c * uTramo;
  float a = nivel(k, c, mapa), b = nivel(k, c + 1.0, mapa);
  float d = abs(b - a) * uSeparacion;
  float f = d > 0.0 ? sat((xl - (uTramo - d)) / d) : 0.0;
  diagonal = (f > 0.0 && f < 1.0) ? 1.0 : 0.0;
  return (a + (b - a) * f) * uSeparacion;
}
vec3 colorDe(vec3 N, vec3 V) {
  vec3 n = abs(normalize(vNormalOS));
  float x = vPosM.x * uEspejo;
  if (int(uParte + 0.5) == 2) return iluminar(COLORES[int(mod(abs(floor(x / 0.035)), 6.0))], N, V, 0.25);
  vec3 papel = uPapel * (0.96 + 0.04 * ruido3(vPosM / 0.002));
  vec3 color = iluminar(papel, N, V, 0.08);
  if (n.x > max(n.y, n.z)) return color;
  float periodo = 9.0 * uSeparacion;
  float v = vPosM.z - abs(vPosM.y);
  v = v - floor(v / periodo) * periodo;
  float ciclo = max(uCiclo, 1.0);
  float mapa = floor(uTiempo / ciclo);
  float tt = uTiempo - mapa * ciclo;
  float borra = sat((ciclo - tt) / 1.2);
  for (int i = 0; i < 6; i++) {
    float k = float(i);
    float frente = uInicio + (tt - k * uDesfase) * uVelocidad;
    if (x > frente) continue;
    float diagonal, c, xl;
    float cen = centro(k, x, mapa, diagonal, c, xl);
    float d = abs(v - cen) * (diagonal > 0.0 ? 0.7071 : 1.0);
    float linea = sat((uGrosor * 0.5 - d) / (uGrosor * 0.18) + 0.5);
    float tinta = 1.0 - 0.35 * sat(1.0 - (frente - x) / (uGrosor * 3.0));
    color = mix(color, iluminar(COLORES[i] * tinta, N, V, 0.2), linea * borra);
    float xe = uTramo * 0.3;
    float r = length(vec2(xl - xe, v - nivel(k, c, mapa) * uSeparacion));
    if (x <= frente - 0.002 && azar(vec2(k * 5.3 + mapa, c * 3.1)) > 0.35) {
      float radio = uGrosor * 0.95;
      float borde = sat((radio - r) / (uGrosor * 0.15) + 0.5);
      float blanco = sat((radio * 0.62 - r) / (uGrosor * 0.15) + 0.5);
      color = mix(color, mix(iluminar(uTinta, N, V, 0.1), papel * 1.05, blanco), borde * borra);
    }
  }
  return color;
}`,
  // ---------- 5: SkinDragon (Dragón Abisal: escamas, grietas de fuego, llamas y el cuchillo) ----------
  5: `
uniform vec3 uEscama, uFilo, uFuego, uNucleo, uPiedraColor;
uniform float uParte, uBrillo, uTile, uGiro, uRelieve, uCiclo, uPausa, uDosTandas, uGrieta, uTilePiedra, uRelievePiedra;
uniform float uOscuro, uRed, uCorre, uHoja, uTileMango;
uniform vec4 uEje;
uniform sampler2D uEscamas, uEscamasNormal, uPiedra, uPiedraNormal, uBase;
vec2 girar(vec2 v, int vueltas) {
  vueltas = ((vueltas % 4) + 4) % 4;
  if (vueltas == 1) return vec2(-v.y, v.x);
  if (vueltas == 2) return -v;
  if (vueltas == 3) return vec2(v.y, -v.x);
  return v;
}
vec3 normalDe(sampler2D t, vec2 uv, float escala) {
  vec3 n = texture2D(t, uv).xyz * 2.0 - 1.0;
  n.xy *= escala;
  return normalize(n);
}
void tanda(vec3 p, float fase, float semilla, out float nucleo, out float halo) {
  vec3 o = vec3(semilla * 7.31, semilla * 3.17, semilla * 5.13);
  float g = fbm3(p / uGrieta + o);
  float llega = fbm3(p / (uGrieta * 2.4) + o * 1.7);
  float frente = fase * 1.5 - 0.25;
  float abierta = sat((frente - llega) * 7.0);
  float cierra = sat((1.0 - fase) * 3.5);
  float ancho = 0.022 * abierta * cierra;
  float d = abs(g - 0.5);
  nucleo = ancho > 0.0005 ? sat(1.0 - d / ancho) : 0.0;
  float punta = sat(1.0 - abs(frente - llega) * 12.0) * step(llega, frente);
  halo = pow(sat(1.0 - d / (ancho * 4.0 + 0.0005)), 2.0) * abierta * cierra * (0.7 + 0.8 * punta);
}
void grietas(vec3 p, out float nucleo, out float halo) {
  float ciclo = max(uCiclo, 0.1), periodo = ciclo + max(uPausa, 0.0), t = uTiempo / periodo;
  float n2 = 0.0, h2 = 0.0;
  nucleo = 0.0; halo = 0.0;
  float fase = fract(t) * periodo / ciclo;
  if (fase < 1.0) tanda(p, fase, floor(t), nucleo, halo);
  if (uDosTandas > 0.5) {
    float fase2 = fract(t + 0.5) * periodo / ciclo;
    if (fase2 < 1.0) tanda(p, fase2, floor(t + 0.5) + 100.0, n2, h2);
    nucleo = max(nucleo, n2); halo = max(halo, h2);
  }
}
vec3 cuchillo(vec3 N, vec3 V, float fuerza) {
  vec3 textura = texture2D(uBase, vUv).rgb;
  vec3 base = mix(uEscama, textura * uEscama * 6.0, uOscuro);
  vec3 H = normalize(LUZ + V);
  vec3 color = base * luzDifusa(N) + pow(sat(dot(N, H)), 48.0) * 0.6 * 0.35;
  color += uFuego * pow(1.0 - sat(dot(N, V)), 3.0) * 0.35 * fuerza * 0.4;
  vec3 p = vPosM;
  float d = abs(fbm3(p / uRed + 3.7) - 0.5);
  float red = sat(1.0 - d / 0.02), resplandor = pow(sat(1.0 - d / 0.08), 2.0);
  float corre = fbm3(p / (uRed * 2.5) - uTiempo * uCorre * vec3(1.0, 0.35, 0.6));
  float pulso = 0.3 + 0.9 * smoothstep(0.45, 0.72, corre);
  float nucleoT, haloT;
  grietas(p, nucleoT, haloT);
  float nucleo = max(red * pulso, nucleoT), halo = max(resplandor * pulso * 0.8, haloT);
  color += uFuego * halo * 0.2 * fuerza;
  vec3 fuego = mix(uFuego, uNucleo, nucleo * nucleo);
  vec3 hoja = mix(color, fuego * fuerza * 0.5, sat(max(nucleo, halo * 0.6)));
  vec3 w = pow(abs(normalize(vNormalOS)), vec3(4.0)); w /= w.x + w.y + w.z;
  vec3 P = vPosM / uTileMango;
  float gris = texture2D(uEscamas, P.zy).r * w.x + texture2D(uEscamas, P.xz).r * w.y + texture2D(uEscamas, P.xy).r * w.z;
  vec3 negro = vec3(0.012, 0.012, 0.014) * (0.6 + 0.6 * gris);
  vec3 mango = negro * luzDifusa(N) + pow(sat(dot(N, H)), 64.0) * 0.25 * gris;
  float largo = dot(vPosOS, normalize(uEje.xyz)) / max(uEje.w, 0.0001);
  return mix(mango, hoja, smoothstep(uHoja - 0.02, uHoja + 0.02, largo));
}
vec3 colorDe(vec3 N, vec3 V) {
  vec3 n = abs(normalize(vNormalOS));
  vec2 p = n.x > max(n.y, n.z) ? vPosM.yz : n.y > n.z ? vPosM.xz : vPosM.xy;
  int parte = int(uParte + 0.5);
  float fuerza = uBrillo * (0.75 + 0.25 * sin(uTiempo * 1.6));
  if (parte == 3) return cuchillo(N, V, fuerza);
  if (parte == 2) {
    float t = fbm(vec2(p.x / 0.03 - uTiempo * 2.0, p.y / 0.012));
    return mix(uFuego, uNucleo, sat(t * 1.4 - 0.2)) * fuerza * 0.6;
  }
  int giro = int(uGiro + 0.5);
  vec2 uv = girar(p, giro) / uTile;
  float gris = texture2D(uEscamas, uv).r;
  vec3 tn = normalDe(uEscamasNormal, uv, 1.0);
  tn.xy = girar(tn.xy, -giro) * uRelieve;
  vec3 no = normalize(vNormalOS);
  vec3 ejeU = n.x > max(n.y, n.z) ? vec3(0, 1, 0) : vec3(1, 0, 0);
  vec3 ejeV = (n.x > max(n.y, n.z) || n.y > n.z) ? vec3(0, 0, 1) : vec3(0, 1, 0);
  vec3 Nr = aMundo(normalize(tn.x * ejeU + tn.y * ejeV + max(tn.z, 0.2) * no));
  vec3 escama = uEscama * (0.35 + 1.3 * gris) * (parte == 1 ? 0.8 : 1.0);
  vec3 H = normalize(LUZ + V);
  vec3 color = escama * luzDifusa(Nr) + pow(sat(dot(Nr, H)), 64.0) * 0.8 * gris * 0.5;
  color += uFilo * pow(1.0 - sat(dot(Nr, V)), 3.0) * 0.25;
  if (parte == 1) {
    vec3 w = pow(n, vec3(4.0)); w /= w.x + w.y + w.z;
    vec3 P = vPosM / uTilePiedra;
    vec2 uvX = P.zy, uvY = P.xz, uvZ = P.xy;
    vec3 roca = texture2D(uPiedra, uvX).rgb * w.x + texture2D(uPiedra, uvY).rgb * w.y + texture2D(uPiedra, uvZ).rgb * w.z;
    vec3 tnX = normalDe(uPiedraNormal, uvX, uRelievePiedra), tnY = normalDe(uPiedraNormal, uvY, uRelievePiedra), tnZ = normalDe(uPiedraNormal, uvZ, uRelievePiedra);
    tnX = vec3(tnX.xy + no.zy, abs(tnX.z) * no.x);
    tnY = vec3(tnY.xy + no.xz, abs(tnY.z) * no.y);
    tnZ = vec3(tnZ.xy + no.xy, abs(tnZ.z) * no.z);
    vec3 Np = aMundo(normalize(tnX.zyx * w.x + tnY.xzy * w.y + tnZ.xyz * w.z));
    vec3 piedra = roca * uPiedraColor;
    float grano = sat(dot(roca, vec3(0.299, 0.587, 0.114)) * 4.0);
    color = piedra * luzDifusa(Np) + pow(sat(dot(Np, H)), 24.0) * 0.15 * grano;
    float nucleo, halo;
    grietas(vPosM, nucleo, halo);
    color += uFuego * halo * 0.15 * fuerza * (0.5 + grano);
    color = mix(color, mix(uFuego, uNucleo, nucleo) * fuerza * 0.5, sat(max(nucleo, halo * 0.7)));
  }
  return color;
}`,
  // ---------- 6: SkinTormenta ----------
  6: `
uniform vec3 uOscuro, uClaro, uLluvia, uRayo, uNucleo;
uniform float uParte, uNube, uViento, uCantLluvia, uGota, uLargoGota, uVelLluvia, uBrillo, uCadaRayo, uTamRayo, uConLluvia;
float nubes(vec3 p, float tamano) {
  float t = uTiempo * uViento;
  vec3 q = p / tamano;
  float n = fbm3(q + vec3(t, 0.0, -t * 0.35));
  float n2 = fbm3(q * 2.2 + vec3(-t * 1.6, 3.0, t * 0.5));
  return pow(sat((n * 0.75 + n2 * 0.35 - 0.25) * 1.6), 1.5);
}
float relampago(out float cual) {
  float ciclo = max(uCadaRayo, 0.5), n = floor(uTiempo / ciclo);
  cual = n;
  float t = uTiempo - (n * ciclo + azar(vec2(n, 3.7)) * ciclo * 0.5);
  if (t < 0.0 || t > 0.55) return 0.0;
  return pow(sat(sin(t * 38.0 + 1.3)), 2.0) * exp(-t * 4.0) * (t < 0.04 ? 1.0 : 0.85);
}
void rayo(vec3 p, float cual, out float nucleo, out float halo) {
  vec3 o = vec3(cual * 7.31, cual * 3.17, cual * 5.13);
  float g = fbm3(vec3(p.x / uTamRayo, p.y / uTamRayo, p.z / (uTamRayo * 3.0)) + o);
  float aca = sat((fbm3(p / (uTamRayo * 4.0) + o * 1.3) - 0.52) * 10.0);
  float d = abs(g - 0.5);
  nucleo = sat(1.0 - d / 0.014) * aca;
  halo = pow(sat(1.0 - d / 0.09), 2.0) * aca;
}
vec3 colorDe(vec3 N, vec3 V) {
  vec3 n = abs(normalize(vNormalOS));
  vec2 p = n.x > max(n.y, n.z) ? vPosM.yz : n.y > n.z ? vPosM.xz : vPosM.xy;
  int parte = int(uParte + 0.5);
  float cual, destello = relampago(cual);
  float fuerza = uBrillo * (0.8 + 0.2 * sin(uTiempo * 2.3));
  if (parte == 2) {
    float t = uTiempo;
    float hiloN = fbm(vec2(p.x / 0.03 - t * 4.0, p.y / 0.008));
    float hilo = pow(sat(1.0 - abs(hiloN - 0.5) / 0.06), 2.0);
    float corre = 0.5 + 0.5 * pow(abs(sin((p.x / 0.1 - t * 6.0) * PI)), 6.0);
    return iluminar(vec3(0.227, 0.165, 0.02), N, V, 0.2) + uRayo * hilo * fuerza * 0.5 * (0.4 + corre + destello)
         + uNucleo * pow(hilo, 3.0) * corre * fuerza * 0.3;
  }
  bool panel = parte == 1;
  float k = nubes(vPosM, uNube * (panel ? 0.85 : 1.0));
  vec3 albedo = mix(uOscuro, uClaro, k) * (panel ? 1.5 : 1.0);
  vec3 color = iluminar(albedo, N, V, 0.05);
  float nucleo = 0.0, halo = 0.0;
  if (uConLluvia > 0.5) {
    vec2 celda = floor(vPosM.xy / uGota);
    float columna = n.y > n.x ? fract(vPosM.x / uGota) : fract(vPosM.y / uGota);
    float vel = 1.0 + 0.6 * azar(celda + 5.3);
    float fase = fract(vPosM.z / uLargoGota * 0.35 + uTiempo * vel * uVelLluvia + azar(celda) * 10.0);
    float gota = step(abs(columna - 0.5), 0.09) * step(fase, 0.32) * step(1.0 - uCantLluvia, azar(celda + 9.1));
    color += uLluvia * gota * (1.0 - fase / 0.32) * 0.35 * sat(1.0 - n.z * 1.5);
    if (destello > 0.0) rayo(vPosM, cual, nucleo, halo);
  }
  color += albedo * destello * (panel ? 1.6 : 0.9) + uRayo * halo * destello * 0.6 * uBrillo;
  return mix(color, mix(uRayo, uNucleo, nucleo) * uBrillo * 1.4, sat(nucleo * destello * 1.5));
}`,
  // ---------- 7: SkinCuchillo (plasma, hielo, sangre y holograma) ----------
  7: `
uniform vec3 uColor, uNucleo;
uniform float uModo, uBrillo, uHoja, uTamano, uVelocidad;
uniform vec4 uEje;
uniform sampler2D uBase;
float campana(float x) { return exp(-x * x); }
float azar1(float n) { return fract(sin(n * 91.3458) * 47453.5453); }
vec3 metal(vec3 N, vec3 V, float cuanto, vec3 tinte) {
  vec3 base = texture2D(uBase, vUv).rgb * cuanto * tinte;
  return base * luzDifusa(N) + pow(sat(dot(N, normalize(LUZ + V))), 48.0) * 0.5 * 0.4;
}
float red(vec3 p, float semilla, float ancho, out float halo) {
  float d = abs(fbm3(p + semilla) - 0.5);
  halo = pow(sat(1.0 - d / (ancho * 4.0)), 2.0);
  return sat(1.0 - d / ancho);
}
vec3 colorDe(vec3 N, vec3 V) {
  int modo = int(uModo + 0.5);
  float borde = pow(1.0 - sat(dot(N, V)), 3.0);
  vec3 eje = normalize(uEje.xyz);
  float u = dot(vPosOS, eje) / max(uEje.w, 0.0001);
  float hoja = smoothstep(uHoja - 0.03, uHoja + 0.03, u);
  float t = uTiempo * uVelocidad, fuerza = uBrillo;
  vec3 p = vPosM / uTamano;
  if (modo == 0) {
    vec3 mango = metal(N, V, 0.25, vec3(0.9, 0.85, 1.0));
    float halo, veta = red(p * 0.8, 3.0, 0.03, halo);
    mango += uColor * (veta + halo * 0.3) * fuerza * 0.25 * (0.6 + 0.4 * sin(t * 3.0));
    float n = fbm3((p - eje * t * 2.5) * 0.7);
    float energia = sat(n * 1.4 - 0.15) * (0.75 + 0.25 * sin(u * 30.0 - t * 9.0));
    vec3 plasma = mix(uColor, uNucleo, pow(energia, 2.0)) * (0.5 + 1.2 * energia + borde) * fuerza;
    return mix(mango, plasma, hoja);
  }
  if (modo == 2) {
    vec3 hielo = uColor * (0.35 + 0.65 * (sat(dot(N, LUZ)) * 0.7 + AMBIENTE));
    hielo += pow(sat(dot(N, normalize(LUZ + V))), 96.0) * 1.5;
    hielo += uNucleo * borde * 0.9;
    float halo, grieta = red(p * 0.9, 7.0, 0.025, halo);
    hielo = mix(hielo, uNucleo * 1.4, grieta * 0.7) + uNucleo * halo * 0.15;
    vec3 base = mix(metal(N, V, 0.3, vec3(0.8, 0.9, 1.1)), hielo, hoja);
    float cuanta = 0.45 + 0.25 * sin(t * 0.6) - (1.0 - hoja) * 0.15;
    float escarcha = smoothstep(cuanta, cuanta - 0.08, fbm3(p * 2.2 + 21.0));
    return mix(base, vec3(0.88, 0.95, 1.0) * (0.6 + AMBIENTE), escarcha * 0.85);
  }
  if (modo == 3) {
    vec3 m = metal(N, V, 0.2, vec3(1.0, 0.8, 0.8));
    float a = dot(vPosM, eje);
    vec3 q = (vPosM - eje * a) / uTamano + eje * (a / (uTamano * 5.0));
    float d = abs(fbm3(q + 5.0) - 0.5);
    float ancho = mix(0.07, 0.025, sat(u));
    float vena = sat(1.0 - d / ancho), rodea = sat(1.0 - d / (ancho * 2.2));
    float f = fract(t * 1.1 - u * 1.6);
    float tanda = campana(f / 0.07) + 0.6 * campana((f - 0.22) / 0.07);
    vec3 sangre = mix(vec3(0.18, 0.0, 0.01), uColor, 0.35 + 0.65 * tanda);
    sangre = mix(sangre, uNucleo, tanda * vena * vena * 0.6);
    float brillo = pow(sat(dot(N, normalize(LUZ + V))), 64.0);
    vec3 color = m + uColor * (rodea - vena) * 0.12 * (0.5 + tanda);
    return mix(color, sangre * fuerza * (0.45 + 0.35 * tanda) + brillo * 0.6, vena);
  }
  float gris = dot(texture2D(uBase, vUv).rgb, vec3(0.299, 0.587, 0.114));
  float lineas = 0.55 + 0.45 * step(0.5, fract(vPosM.y * 160.0 - t * 2.0));
  float barra = campana((fract(t * 0.45) * 1.4 - 0.2 - u) / 0.05);
  float parpadeo = (0.8 + 0.2 * ruido3(vec3(t * 18.0, 0.0, 0.0))) * (1.0 - 0.6 * step(0.96, azar1(floor(t * 10.0))));
  float luz = (0.18 + 0.4 * gris) * lineas + borde * 1.3 + barra * 0.9;
  return mix(uColor, uNucleo, sat(borde + barra)) * luz * parpadeo * fuerza * 0.6;
}`,
};

export function fragmento(familia) {
  return comun + familias[familia] + `
void main() {
  vec3 N = normalize(vNormalW);
  if (!gl_FrontFacing) N = -N;
  vec3 V = normalize(vVistaW);
  gl_FragColor = vec4(colorDe(N, V), 1.0);
  #include <colorspace_fragment>
}`;
}
