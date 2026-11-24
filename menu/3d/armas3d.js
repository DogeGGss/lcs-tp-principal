// Las armas en 3D con su skin, como la vista previa del juego (VistaPreviaDeSkin): el modelo del juego (el mismo FBX que
// usa ModelosDeArmas), de costado, con los materiales de la skin. Las comunes cambian los colores de los cuatro
// materiales del arma (Black, White, Grey y Main); las demás ponen sus shaders (sombreado.js) en las partes que dice
// CatalogoDeSkins. Hay un solo lienzo: se muda a la vista previa que está a la vista.
// Uso (desde menu.js): Armas3D.mostrar(nodo, { arma, estilo, barra }, alListo).
import * as THREE from 'three';
import { FBXLoader } from './vendor/jsm/loaders/FBXLoader.js';
import { vertice, fragmento } from './sombreado.js';

const BASE = new URL('./', import.meta.url).href;
// Lo que mide cada arma en la mano, en metros: los patrones se miden en metros sobre el modelo (posM), como en el juego.
const LARGO = { mitre: 0.85, urquiza: 0.7, 'belgrano-sur': 0.85, roca: 0.95, 'ultimo-tren': 1.15, 'gran-capitan': 0.9,
  'la-portena': 0.22, 'la-trochita': 0.26, cuchillo: 0.3 };
const ORIGINALES = { Black: '#070707', White: '#757575', Grey: '#1A1A1A', Main: '#9D3607' };
const PARTES = ['Black', 'White', 'Grey', 'Main']; // el orden de la barra: cuerpo, paneles, detalles y acento

// Colores de Unity (los de los .mat y los Hex() del catálogo están en sRGB; los shaders trabajan en lineal).
const c = (r, g, b) => new THREE.Color().setRGB(r, g, b, THREE.SRGBColorSpace);
const h = x => new THREE.Color(x);
const v3 = (x, y, z) => new THREE.Vector3(x, y, z);

// ---------- Los materiales de Art/Skins (sus valores, copiados de cada .mat) ----------
const cargador = new THREE.TextureLoader();
const texturas = {};
function textura(nombre, color, puntos, repetir = true) {
  if (texturas[nombre]) return texturas[nombre];
  const t = cargador.load(BASE + 'texturas/' + nombre);
  if (color) t.colorSpace = THREE.SRGBColorSpace;
  if (repetir) t.wrapS = t.wrapT = THREE.RepeatWrapping;
  if (puntos) { t.magFilter = t.minFilter = THREE.NearestFilter; t.generateMipmaps = false; }
  return (texturas[nombre] = t);
}
const MAT = {
  'marmol-de-retiro': [1, { uPatron: 3, uEscala: 0.03, uLisura: 0.85, uMetal: 0, uC1: c(0.9333, 0.9294, 0.9098), uC2: c(0.5412, 0.549, 0.5647), uC3: c(0.2431, 0.251, 0.2706), uC4: c(0, 0, 0) }],
  'marmol-negro': [1, { uPatron: 3, uEscala: 0.03, uLisura: 0.85, uMetal: 0, uC1: c(0.0863, 0.0902, 0.102), uC2: c(0.7882, 0.6431, 0.298), uC3: c(0.4196, 0.4353, 0.4627), uC4: c(0, 0, 0) }],
  'oro': [1, { uPatron: 0, uEscala: 0.01, uLisura: 0.85, uMetal: 1, uC1: c(0.9098, 0.7216, 0.2902), uC2: c(0, 0, 0), uC3: c(0, 0, 0), uC4: c(0, 0, 0) }],
  'azulejos-del-subte': [1, { uPatron: 4, uEscala: 0.01, uLisura: 0.9, uMetal: 0, uC1: c(0.498, 0.7137, 0.851), uC2: c(0.9451, 0.9451, 0.9255), uC3: c(0.2392, 0.4314, 0.6118), uC4: c(0.1647, 0.1804, 0.2) }],
  'cristal-del-rift': [1, { uPatron: 5, uEscala: 0.02, uLisura: 0.9, uMetal: 0.2, uC1: c(0.4824, 0.2471, 0.8941), uC2: c(0.298, 0.7647, 1), uC3: c(0.902, 0.8588, 1), uC4: c(0, 0, 0) }],
  'cristal-oscuro': [1, { uPatron: 5, uEscala: 0.032, uLisura: 0.9, uMetal: 0.2, uC1: c(0.0784, 0.0627, 0.1412), uC2: c(0.1647, 0.1216, 0.2902), uC3: c(0.6275, 0.4196, 1), uC4: c(0, 0, 0) }],
  'codigo-binario': [2, { uFondo: c(0.01, 0.035, 0.015), uCodigo: c(0.15, 1, 0.35), uCabeza: c(0.85, 1, 0.88), uArriba: v3(0, 0, 1),
    uCelda: 0.006, uVelocidad: 1.6, uCola: 0.45, uCambio: 6, uBrillo: 2.2, uBorde: 0.6 }],
  'proxima-estacion': [3, { uParte: 0, uEspejo: -1, ...cartel() }],
  'proxima-estacion-franjas': [3, { uParte: 2, uEspejo: 1, ...cartel() }],
  'mapa-del-subte': [4, { uParte: 0, ...mapa() }],
  'mapa-franjas': [4, { uParte: 2, ...mapa() }],
  'dragon-escamas': [5, { ...dragon(), uParte: 0, uBrillo: 2.5, uCiclo: 3.5, uPausa: 1.2, uRelieve: 2, uTile: 0.7 }],
  'dragon-abisal': [5, { ...dragon(), uParte: 1, uBrillo: 2.5, uCiclo: 10, uPausa: 2, uRelieve: 1, uTile: 0.3 }],
  'dragon-llama': [5, { ...dragon(), uParte: 2, uBrillo: 2.5 }],
  'dragon-cuchillo': [5, { ...dragon(), uParte: 3, uBrillo: 2.8, uCiclo: 4, uPausa: 0.5, uGrieta: 0.03, uFilo: c(0.169, 0.722, 0.788) }],
  'tormenta-nubes': [6, { ...tormenta(), uParte: 0, uConLluvia: 1 }],
  'tormenta': [6, { ...tormenta(), uParte: 1, uConLluvia: 1 }],
  'tormenta-corriente': [6, { ...tormenta(), uParte: 2, uConLluvia: 0 }],
  'cuchillo-hoja-de-plasma': [7, { uModo: 0, uBrillo: 2.5, uTamano: 0.02, uColor: c(0.62, 0.3, 1), uNucleo: c(0.95, 0.88, 1) }],
  'cuchillo-cristal-de-hielo': [7, { uModo: 2, uBrillo: 1.5, uTamano: 0.015, uColor: c(0.55, 0.8, 1), uNucleo: c(0.85, 0.95, 1) }],
  'cuchillo-sangre-de-invasor': [7, { uModo: 3, uBrillo: 2.5, uTamano: 0.018, uColor: c(0.9, 0.05, 0.05), uNucleo: c(1, 0.45, 0.35) }],
  'cuchillo-holograma': [7, { uModo: 4, uBrillo: 2, uTamano: 0.02, uColor: c(0.15, 0.75, 1), uNucleo: c(0.8, 1, 1) }, true],
};
function cartel() {
  return { uLed: c(1, 0.541, 0.118), uApagado: c(0.102, 0.055, 0.02), uCarcasa: c(0.07, 0.07, 0.07), uCelda: 0.006, uVelocidad: 22,
    uLargoTexto: 6818, uRenglon: 15, uBrillo: 2.2, uTexto: textura('cartel-texto.png', false, true), uTamTexto: new THREE.Vector2(2048, 64) };
}
function mapa() {
  return { uPapel: c(0.96, 0.95, 0.91), uTinta: c(0.12, 0.12, 0.14), uSeparacion: 0.016, uGrosor: 0.0045, uTramo: 0.07, uVelocidad: 0.22,
    uDesfase: 1.1, uInicio: -0.7, uCiclo: 14, uEspejo: 1 };
}
function dragon() {
  return { uEscama: c(0.0549, 0.0549, 0.0667), uFilo: c(0.1647, 0.1647, 0.1882), uFuego: c(0.1176, 0.4196, 1), uNucleo: c(0.8118, 0.9804, 1),
    uPiedraColor: c(1, 1, 1), uTile: 0.3, uGiro: 3, uRelieve: 1, uCiclo: 10, uPausa: 2, uDosTandas: 1, uGrieta: 0.05, uTilePiedra: 0.25,
    uRelievePiedra: 1, uOscuro: 0.35, uRed: 0.018, uCorre: 0.6, uHoja: 0.34, uTileMango: 0.08, uEje: new THREE.Vector4(0, 1, 0, 1),
    uEscamas: textura('escamas.jpg', true), uEscamasNormal: textura('escamas-normal.jpg', false), uPiedra: textura('roca.jpg', true),
    uPiedraNormal: textura('roca-normal.jpg', false), uBase: textura('cuchillo.jpg', true, false, false) };
}
function tormenta() {
  return { uOscuro: c(0.047, 0.059, 0.082), uClaro: c(0.227, 0.263, 0.345), uLluvia: c(0.725, 0.776, 0.878), uRayo: c(1, 0.784, 0.118),
    uNucleo: c(1, 0.969, 0.784), uNube: 0.06, uViento: 0.15, uCantLluvia: 0.65, uGota: 0.006, uLargoGota: 0.02, uVelLluvia: 3, uBrillo: 2.5,
    uCadaRayo: 3.5, uTamRayo: 0.05 };
}

// Qué material va en cada parte del arma, por diseño (CatalogoDeSkins.Disenos): un id de MAT o un color.
const DISENOS = {
  'marmol-de-retiro': { White: 'marmol-de-retiro', Black: 'marmol-negro', Grey: 'marmol-negro', Main: 'oro' },
  'azulejos-del-subte': { White: 'azulejos-del-subte', Black: '#ECEBE6', Grey: '#ECEBE6', Main: '#8A8D92' },
  'cristal-del-rift': { White: 'cristal-del-rift', Black: 'cristal-oscuro', Grey: 'cristal-oscuro', Main: '#3B1F7A' },
  'codigo-binario': { White: 'codigo-binario', Main: 'codigo-binario' },
  'proxima-estacion': { White: 'proxima-estacion', Black: 'proxima-estacion', Grey: 'proxima-estacion', Main: 'proxima-estacion-franjas' },
  'mapa-del-subte': { White: 'mapa-del-subte', Black: 'mapa-del-subte', Grey: 'mapa-del-subte', Main: 'mapa-franjas' },
  'dragon-abisal': { White: 'dragon-abisal', Black: 'dragon-escamas', Grey: 'dragon-escamas', Main: 'dragon-llama' },
  'tormenta': { White: 'tormenta', Black: 'tormenta-nubes', Grey: 'tormenta-nubes', Main: 'tormenta-corriente' },
};
// El cuchillo tiene un solo material (M_Knife), que la skin cambia entero.
const DEL_CUCHILLO = { 'dragon-abisal': 'dragon-cuchillo', 'hoja-de-plasma': 'cuchillo-hoja-de-plasma', 'cristal-de-hielo': 'cuchillo-cristal-de-hielo',
  'sangre-de-invasor': 'cuchillo-sangre-de-invasor', 'holograma': 'cuchillo-holograma' };

// ---------- three.js ----------
const tiempo = { value: 0 };
const materiales = {};
function material(id, k, eje) {
  const clave = id + '|' + k + (eje ? '|' + eje.toArray().join(',') : '');
  if (materiales[clave]) return materiales[clave];
  let familia, valores, suma = false;
  if (id.startsWith('#')) { familia = 0; valores = { uColor: h(id) }; }
  else [familia, valores, suma] = MAT[id];
  const uniforms = { uTiempo: tiempo, uK: { value: k } };
  for (const [n, v] of Object.entries(valores)) uniforms[n] = { value: v };
  if (eje) uniforms.uEje = { value: eje };
  const m = new THREE.ShaderMaterial({ uniforms, vertexShader: vertice, fragmentShader: fragmento(familia), side: THREE.DoubleSide });
  if (suma) Object.assign(m, { blending: THREE.AdditiveBlending, transparent: true, depthWrite: false }); // el holograma suma luz
  return (materiales[clave] = m);
}

// Los modelos (una vez cada uno). Sin las texturas del FBX: los materiales son todos de la skin.
const gestor = new THREE.LoadingManager();
gestor.setURLModifier(u => /\.fbx$/i.test(u) ? u : 'data:image/gif;base64,R0lGODlhAQABAAAAACw=');
const fbx = new FBXLoader(gestor);
const modelos = {};
const modelo = arma => modelos[arma] || (modelos[arma] = fbx.loadAsync(BASE + 'modelos/' + arma + '.fbx'));

let render = null, escena, camara, vista = null, giro = 0, arrastre = null, quieto = 0;
function preparar() {
  if (render) return true;
  try { render = new THREE.WebGLRenderer({ antialias: true, alpha: true }); } catch { return false; }
  render.outputColorSpace = THREE.SRGBColorSpace;
  render.setPixelRatio(Math.min(2, devicePixelRatio || 1));
  escena = new THREE.Scene();
  camara = new THREE.PerspectiveCamera(20, 1.5, 0.05, 50);
  const lienzo = render.domElement;
  lienzo.className = 'lienzo-3d';
  lienzo.addEventListener('pointerdown', e => { arrastre = e.clientX; lienzo.setPointerCapture(e.pointerId); });
  lienzo.addEventListener('pointermove', e => { if (arrastre == null) return; giro += (e.clientX - arrastre) * 0.012; arrastre = e.clientX; quieto = performance.now(); });
  lienzo.addEventListener('pointerup', () => { arrastre = null; quieto = performance.now(); });
  const reloj = new THREE.Clock();
  render.setAnimationLoop(() => {
    tiempo.value = reloj.getElapsedTime();
    if (!vista || !lienzo.isConnected) return;
    const r = vista.nodo.getBoundingClientRect();
    if (r.width < 2) return;
    const w = Math.round(r.width), hh = Math.round(r.height);
    if (lienzo.width !== Math.round(w * render.getPixelRatio()) || lienzo.height !== Math.round(hh * render.getPixelRatio())) {
      render.setSize(w, hh, false); camara.aspect = w / hh; encuadrar();
    }
    // Se mece de un lado al otro (como en el juego, que gira despacio); arrastrando se gira a mano.
    const solo = arrastre == null && performance.now() - quieto > 2500;
    if (solo) giro *= 0.96;
    if (vista.grupo) vista.grupo.rotation.y = giro + (solo ? Math.sin(tiempo.value * 0.6) * 0.5 : 0);
    render.render(escena, camara);
  });
  return true;
}
// De costado, centrada y que entre a lo largo (VistaPreviaDeSkin: Largo 2,7 y AltoMaximo 1,4 de una vista de 3,1 × 2,1).
function encuadrar() {
  if (!vista || !vista.medida) return;
  const { largo, alto } = vista.medida, mitad = Math.tan(THREE.MathUtils.degToRad(camara.fov / 2));
  const d = Math.max(largo / 0.82 / (2 * mitad * camara.aspect), alto / 0.62 / (2 * mitad)) + 0.5;
  camara.position.set(0, d * 0.12, d);
  camara.lookAt(0, 0, 0);
}

async function mostrar(nodo, skin, alListo) {
  if (!preparar()) return;
  const pedido = vista = { nodo, grupo: null, medida: null };
  const lienzo = render.domElement;
  lienzo.style.opacity = '0';
  nodo.appendChild(lienzo);
  let base;
  try { base = await modelo(skin.arma); } catch { return; }
  if (vista !== pedido) return; // ya se pidió otra
  escena.clear();
  const arma = base.clone(true), k = LARGO[skin.arma] || 0.8;
  const cuchillo = skin.arma === 'cuchillo';
  arma.traverse(m => {
    if (!m.isMesh) return;
    m.geometry.computeBoundingBox();
    const b = m.geometry.boundingBox, tam = b.getSize(new THREE.Vector3());
    // uK: de unidades del modelo a metros, por lo que mide el arma a lo largo (X en las armas de fuego, Y en el cuchillo).
    const kMalla = k / (cuchillo ? tam.y : medidaX(base));
    let eje = null;
    if (cuchillo) { // CuchilloDeSkin.Medir: el eje más largo, hacia la punta, y cuánto mide del origen a la punta
      const cy = (b.min.y + b.max.y) / 2;
      eje = new THREE.Vector4(0, cy < 0 ? -1 : 1, 0, Math.abs(cy) + tam.y / 2);
    }
    const lista = [].concat(m.material).map(mm => {
      const parte = (mm.name || '').split(' ')[0];
      return material(elegir(skin, parte, cuchillo), kMalla, eje);
    });
    m.material = Array.isArray(m.material) ? lista : lista[0];
  });
  // De costado, con el caño hacia la derecha. El cuchillo viene parado (a lo largo de Y): se acuesta.
  const grupo = new THREE.Group(), girado = new THREE.Group();
  girado.add(arma);
  if (cuchillo) girado.rotation.z = -Math.PI / 2;
  grupo.add(girado);
  const caja = new THREE.Box3().setFromObject(grupo), centro = caja.getCenter(new THREE.Vector3()), t = caja.getSize(new THREE.Vector3());
  const escala = 1 / Math.max(t.x, 0.0001);
  girado.position.sub(centro);
  grupo.scale.setScalar(escala);
  vista.grupo = grupo;
  vista.medida = { largo: 1, alto: t.y * escala };
  escena.add(grupo);
  encuadrar();
  giro = 0;
  requestAnimationFrame(() => { if (vista === pedido) { lienzo.style.opacity = '1'; if (alListo) alListo(); } });
}
const medidas = new WeakMap();
function medidaX(base) {
  if (!medidas.has(base)) {
    let max = 0;
    base.traverse(m => { if (m.isMesh) { m.geometry.computeBoundingBox(); max = Math.max(max, m.geometry.boundingBox.getSize(new THREE.Vector3()).x); } });
    medidas.set(base, max || 1);
  }
  return medidas.get(base);
}
// El material de esa parte del arma con esa skin: un color (comunes y lo que el diseño deja como viene) o un shader.
function elegir(skin, parte, cuchillo) {
  if (cuchillo) return DEL_CUCHILLO[skin.estilo] || '#8E96A3';
  if (skin.barra && !DISENOS[skin.estilo] && skin.estilo) return skin.barra[PARTES.indexOf(parte)] || ORIGINALES[parte] || '#555555';
  const d = DISENOS[skin.estilo];
  return (d && d[parte]) || ORIGINALES[parte] || '#555555';
}

window.Armas3D = { mostrar };
if (window.pedido3D) { const [n, s, f] = window.pedido3D; window.pedido3D = null; if (n.isConnected) mostrar(n, s, f); }
