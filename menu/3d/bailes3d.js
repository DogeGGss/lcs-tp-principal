// Los emotes en 3D: uno de los personajes haciendo el baile, como VistaPreviaDeEmote del juego (Scripts/Emotes).
// Los modelos son los de CharacterData.modelo pasados a glTF y achicados (personajes/), y los bailes son los clips de
// Mixamo de Personajes/Emotes pasados a JSON (bailes/): por hueso, sus cuaterniones, y de la cadera también la posición.
// Todos los personajes son esqueletos de Mixamo, así que un baile sirve para cualquiera (como el Humanoid de Unity).
import * as THREE from 'three';
import { GLTFLoader } from './vendor/jsm/loaders/GLTFLoader.js';
import { MeshoptDecoder } from './vendor/jsm/libs/meshopt_decoder.module.js';

const BASE = new URL('./', import.meta.url).href;
// Los bailes que tienen clip pasado a la web (El Clásico y Floss son clips Humanoid de Unity: quedan con su ícono).
const BAILES = ['Shuffle', 'Twerk', 'Flair', 'Rumba', 'Silly', 'Dancing', 'Greeting', 'HipHop', 'HipHop2', 'HipHop3', 'NorthernSoul', 'PunchingBag', 'Twist'];
// Los materiales de cada personaje (los .mat de Personajes/*): la textura de color y, si tiene, la de brillo.
const MATERIALES = {
  alfa: { VanguardBodyMat: ['alfa.jpg'], Vanguard_VisorMat: ['alfa.jpg'] },
  bravo: { Ch45_Body: ['bravo.jpg', 'bravo-luz.jpg'] },
  charlie: { ely_vanguardsoldier_kerwinatienza_M2: ['charlie.jpg', 'charlie-luz.jpg'] },
  astra: { Ch48_body: ['astra-1.jpg', 'astra-1-luz.jpg'], Ch48_body1: ['astra-2.jpg', 'astra-2-luz.jpg'], Ch48_hair: ['astra-pelo.webp', null, 1, true] },
};
export const PERSONAJES = Object.keys(MATERIALES);

const cargador = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
const texturas = new THREE.TextureLoader();
// Las UV de los modelos vienen del FBX (origen abajo, como en Unity): la textura se da vuelta como en el FBXLoader.
const textura = (n, color) => { const t = texturas.load(BASE + 'personajes/' + n); t.flipY = true; if (color) t.colorSpace = THREE.SRGBColorSpace; return t; };
const modelos = {}, bailes = {}, clips = {};

function modelo(id) {
  return modelos[id] || (modelos[id] = cargador.loadAsync(BASE + 'personajes/' + id + '.glb').then(g => {
    const raiz = g.scene;
    raiz.traverse(m => {
      if (!m.isMesh) return;
      m.frustumCulled = false; // al bailar se sale de la caja que tiene quieto
      const lista = [].concat(m.material).map(x => {
        const [color, luz, tinte = 1, pelo] = MATERIALES[id][x.name] || [];
        const mat = new THREE.MeshStandardMaterial({ name: x.name, roughness: 0.55, metalness: 0, color: new THREE.Color(tinte, tinte, tinte) });
        if (color) mat.map = textura(color, true);
        if (luz) { mat.emissiveMap = textura(luz, true); mat.emissive = new THREE.Color(1, 1, 1); }
        // El pelo tiene las UV al revés que el cuerpo, y en el juego es transparente (Ch48_hair.mat): se mezcla en vez de cortarse.
        if (pelo) { mat.map.flipY = false; mat.alphaTest = 0.1; mat.transparent = true; mat.depthWrite = false; mat.side = THREE.DoubleSide; }
        return mat;
      });
      m.material = Array.isArray(m.material) ? lista : lista[0];
    });
    // Los modelos vienen en centímetros (de Mixamo): a metros.
    raiz.scale.setScalar(0.01);
    const huesos = {};
    raiz.traverse(o => { if (o.isBone) huesos[o.name.replace(/^mixamorig\d*:?/, '')] = o.name; });
    return { raiz, huesos, mezclador: new THREE.AnimationMixer(raiz) };
  }));
}
const baile = id => bailes[id] || (bailes[id] = fetch(BASE + 'bailes/' + id + '.json').then(r => r.json()));
function clip(pj, m, id, datos) {
  const k = pj + '|' + id;
  if (!clips[k]) {
    const pistas = [];
    for (const p of datos.pistas) {
      const n = m.huesos[p.h];
      if (!n) continue;
      pistas.push(p.p === 'q' ? new THREE.QuaternionKeyframeTrack(n + '.quaternion', p.t, p.v) : new THREE.VectorKeyframeTrack(n + '.position', p.t, p.v));
    }
    clips[k] = new THREE.AnimationClip(id, datos.duracion, pistas);
  }
  return clips[k];
}

let render = null, escena, camara, vista = null;
function preparar() {
  if (render) return true;
  try { render = new THREE.WebGLRenderer({ antialias: true, alpha: true }); } catch { return false; }
  render.outputColorSpace = THREE.SRGBColorSpace;
  render.setPixelRatio(Math.min(2, devicePixelRatio || 1));
  escena = new THREE.Scene();
  // La cámara de VistaPreviaDeEmote: en (0; 1,05; 4,2), mirando un poco hacia abajo, 34° de campo vertical.
  camara = new THREE.PerspectiveCamera(34, 600 / 640, 0.1, 12);
  camara.position.set(0, 1.05, 4.2);
  camara.lookAt(0, 1.05 - 0.05 * 4.2, 0);
  // Su luz: de frente, desde arriba a la derecha. Más una luz pareja para que no quede negro lo que no le da.
  const luz = new THREE.DirectionalLight(0xffffff, 3.4);
  luz.position.set(1.2, 3, 3);
  escena.add(luz, new THREE.HemisphereLight(0xe8edff, 0x3a3f48, 1.8));
  render.domElement.className = 'lienzo-3d baile';
  const reloj = new THREE.Clock();
  render.setAnimationLoop(() => {
    const dt = Math.min(reloj.getDelta(), 0.1);
    const lienzo = render.domElement;
    if (!vista || !vista.listo || !lienzo.isConnected) return;
    const r = vista.nodo.getBoundingClientRect();
    if (r.width < 2) return;
    const w = Math.round(r.width), h = Math.round(r.height);
    if (lienzo.width !== Math.round(w * render.getPixelRatio()) || lienzo.height !== Math.round(h * render.getPixelRatio())) {
      render.setSize(w, h, false);
      camara.aspect = w / h;
      // Si la caja es más ancha que la del juego, se ve igual de alto; si es más angosta, se aleja para que entre.
      camara.fov = w / h >= 600 / 640 ? 34 : THREE.MathUtils.radToDeg(2 * Math.atan(Math.tan(THREE.MathUtils.degToRad(17)) * (600 / 640) / (w / h)));
      camara.updateProjectionMatrix();
    }
    vista.modelo.mezclador.update(dt);
    render.render(escena, camara);
  });
  return true;
}

/** Muestra en ese nodo al personaje (alfa, bravo, charlie o astra) haciendo el baile (el estado del emote). */
async function mostrar(nodo, { baile: id, personaje }, alListo) {
  if (!BAILES.includes(id) || !preparar()) return false;
  const pj = MATERIALES[personaje] ? personaje : PERSONAJES[0];
  const pedido = vista = { nodo, listo: false, modelo: null };
  const lienzo = render.domElement;
  lienzo.style.opacity = '0';
  nodo.appendChild(lienzo);
  let m, datos;
  try { [m, datos] = await Promise.all([modelo(pj), baile(id)]); } catch { return false; }
  if (vista !== pedido) return false;
  escena.children.filter(o => o.type === 'Group').forEach(o => escena.remove(o));
  m.mezclador.stopAllAction();
  m.mezclador.clipAction(clip(pj, m, id, datos)).reset().play();
  escena.add(m.raiz);
  pedido.modelo = m;
  pedido.listo = true;
  requestAnimationFrame(() => requestAnimationFrame(() => { if (vista === pedido) { lienzo.style.opacity = '1'; if (alListo) alListo(); } }));
  return true;
}
const tiene = id => BAILES.includes(id);

window.Bailes3D = { mostrar, tiene, PERSONAJES };
if (window.pedidoBaile) { const [n, s, f] = window.pedidoBaile; window.pedidoBaile = null; if (n.isConnected) mostrar(n, s, f); }
