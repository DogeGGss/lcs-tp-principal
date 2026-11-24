// Menú principal, Perfil y Tienda de Project Riftwalker, como están en el juego (MenuRiftwalker, PantallaPerfil, la tienda
// directa, PantallaEstacion, PantallaPase y la tienda de cristales). Las posiciones son las de Unity, en una pantalla de
// 1920 × 1080. En el Perfil todo está desbloqueado para probar combinaciones; la Tienda arranca como un perfil nuevo para
// que las cápsulas, las ofertas y el pase traigan cosas nuevas.
(() => {
  const C = window.CATALOGO;
  const E = document.getElementById('escenario');
  const RAREZA = ['Inicial', 'Común', 'Raro', 'Épico', 'Legendario'];
  const COLOR = ['#8E96A3', '#9AA3B0', '#5C7CFF', '#B061FF', '#FFC93C'];
  const TIPO = { avatar: 'Avatar', banner: 'Banner', titulo: 'Título', marco: 'Marco', skin: 'Skin de arma', emote: 'Emote', cristales: 'Cristales' };
  const LISTA = { avatar: C.avatares, banner: C.banners, titulo: C.titulos, marco: C.marcos };
  // Skins de armas (F35) y emotes (US 392), de extras.js. El nombre de una skin es "Arma · Estilo", como en el juego.
  const X = window.EXTRAS;
  const ARMA = Object.fromEntries(X.armas.map(a => [a.id, a.nombre]));
  const SKINS = X.skins.map(s => ({ ...s, tipo: 'skin', estiloNombre: s.nombre, nombre: ARMA[s.arma] + ' · ' + s.nombre }));
  const EMOTES = X.emotes.map(e => ({ tipo: 'emote', id: e.estado, nombre: e.nombre, rareza: e.rareza, baile: e.baile }));
  const LUGARES = ['Arriba', 'Derecha', 'Abajo', 'Izquierda'];
  const FONDOS = ['fondo-01', 'fondo-02', 'fondo-03', 'fondo-04', 'fondo-05', 'fondo-06'];
  const CLAVE = 'riftwalker-menu-web-2'; // -2: desde que los emotes y las skins se guardan por nombre

  // Cápsulas: las reglas de ejemplo de PerfilDatos. Las monedas y lo que salió no se guardan: cada vez que se abre la
  // página arranca como el perfil nuevo del juego (2.450 créditos, 3 boletos y 120 cristales). F9 suma, como en el juego.
  // Por rareza; por tipo: avatar, banner, título, marco, skin de arma y emote (las épicas y legendarias son skins o emotes).
  const PROB = [0, 55, 30, 12, 3], PROB_TIPO = [20, 15, 20, 15, 20, 10], GARANTIA = 10;
  const POR_REPETIDO = [0, 50, 120, 300, 800], CREDITOS_POR_BOLETO = 600, CRISTALES_POR_BOLETO = 100, CRISTALES_POR_DIEZ = 900;
  const TIPOS = ['avatar', 'banner', 'titulo', 'marco', 'skin', 'emote'];
  const PREFIJO = { avatar: 'av:', banner: 'bn:', titulo: 'ti:', marco: 'mc:', skin: 'sk:', emote: 'em:' };
  const rift = { creditos: 2450, boletos: 3, cristales: 120, obtenidos: [], sinEpico: 0, aperturas: 0, recientes: [], compras: [], gastado: 0 };
  const clave = c => c.tipo === 'cristales' ? 'cr:' + c.cantidad : PREFIJO[c.tipo] + c.id;
  const tiene = c => c.rareza === 0 || rift.obtenidos.includes(clave(c));
  // Todo lo que se puede conseguir, en el orden del juego: avatares, banners, marcos, títulos, skins y emotes.
  const TODO = [['avatar', C.avatares], ['banner', C.banners], ['marco', C.marcos], ['titulo', C.titulos]]
    .flatMap(([tipo, l]) => l.map(c => ({ ...c, tipo }))).concat(SKINS, EMOTES);
  const desdeClave = k => k.startsWith('cr:') ? cristalesDe(+k.slice(3)) : TODO.find(c => clave(c) === k);
  const cristalesDe = n => ({ tipo: 'cristales', id: 'cr:' + n, cantidad: n, nombre: n + ' cristales', rareza: 1 });

  // ---------- De dónde sale cada cosa (OrigenDeCosmeticos) ----------
  const PASE = X.pase.map(k => k.startsWith('cr:') ? cristalesDe(+k.slice(3))
    : /^(av|bn|mc|sk|em):/.test(k) ? TODO.find(c => clave(c) === k) : TODO.find(c => c.tipo === 'titulo' && c.nombre === k));
  const NIVEL_PASE = {};
  PASE.forEach((p, i) => { if (p && p.tipo !== 'cristales') NIVEL_PASE[clave(p)] = i + 1; });
  function origen(c) {
    const o = X.origen;
    if (!c || c.rareza === 0) return 'inicial';
    if (NIVEL_PASE[clave(c)]) return 'pase';
    if (c.tipo === 'titulo') return o.titulosPase.includes(c.nombre) ? 'pase' : o.titulosTienda.includes(c.nombre) ? 'tienda' : 'capsulas';
    if (c.tipo === 'skin') return o.estilos[c.estilo] || 'capsulas';
    if (c.tipo === 'emote') return o.emotes[c.id] || 'capsulas';
    const conjunto = c.tipo === 'avatar' && o.bannerDelAvatar[c.id] ? o.bannerDelAvatar[c.id] : c.id;
    return o.conjuntos[conjunto] || 'capsulas';
  }
  const dondeSeConsigue = c => ({ capsulas: 'Se consigue en las cápsulas', tienda: 'Se consigue en la tienda',
    pase: `Se consigue en el pase (nivel ${NIVEL_PASE[clave(c)]})` })[origen(c)] || '';
  const CAT = TODO.filter(c => origen(c) === 'capsulas'); // lo que puede salir en una cápsula

  // ---------- Estado (se recuerda en este navegador) ----------
  const inicial = { nombre: 'Jugador', ...C.inicial, emotes: ['Greeting', '', '', ''], skins: {} };
  let estado = { ...inicial };
  try { const g = JSON.parse(localStorage.getItem(CLAVE) || 'null'); if (g) estado = { ...inicial, ...g }; } catch { }
  if (!Array.isArray(estado.emotes) || estado.emotes.length !== 4) estado.emotes = [...inicial.emotes];
  if (!estado.skins || typeof estado.skins !== 'object') estado.skins = {};
  const guardar = () => { try { localStorage.setItem(CLAVE, JSON.stringify(estado)); } catch { } };
  let pestana = 1, categoria = 'avatar', espacio = 0, emoteVisto = null, armaVista = 'mitre';
  const elegido = { avatar: null, banner: null, titulo: null, marco: null, skin: {} };
  const emoteDe = id => EMOTES.find(e => e.id === id);
  const estaEquipado = c => c.tipo === 'skin' ? estado.skins[c.arma] === c.id : c.tipo === 'emote' ? estado.emotes.includes(c.id)
    : c.tipo === 'cristales' ? true : estado[c.tipo] === c.id;
  function equipar(c) {
    if (c.tipo === 'skin') estado.skins[c.arma] = c.id;
    else if (c.tipo === 'emote') { if (!estado.emotes.includes(c.id)) { const libre = estado.emotes.indexOf(''); estado.emotes[libre >= 0 ? libre : 0] = c.id; } }
    else estado[c.tipo] = c.id;
    guardar();
  }

  const buscar = (tipo, id) => LISTA[tipo].find(c => c.id === id) || LISTA[tipo][0];
  const equipado = (tipo) => buscar(tipo, estado[tipo]);
  const personaje = (id) => (C.personajes.find(p => p.id === id) || {}).nombre;

  // ---------- Piezas (como Place y T del juego) ----------
  const el = (tag, cls, padre) => { const e = document.createElement(tag); if (cls) e.className = cls; if (padre) padre.appendChild(e); return e; };
  function N(padre, x, y, w, h, cls = '') {
    const d = el('div', 'n ' + cls, padre);
    d.style.left = x + 'px'; d.style.top = y + 'px'; d.style.width = w + 'px'; d.style.height = h + 'px';
    return d;
  }
  function T(padre, x, y, w, h, fuente, tam, color, texto, o = {}) {
    const cls = ['t', fuente, o.al === 'centro' ? 'centro' : o.al === 'der' ? 'derecha' : '', o.mayus ? 'mayus' : '', o.puntos ? 'puntos' : '', o.arriba ? 'arriba' : ''];
    const d = N(padre, x, y, w, h, cls.join(' '));
    d.style.fontSize = tam + 'px'; d.style.color = color;
    if (o.esp) d.style.letterSpacing = o.esp / 100 + 'em';
    if (o.puntos) d.style.lineHeight = h + 'px';
    if (o.html) d.innerHTML = '<span>' + texto + '</span>'; else d.textContent = texto; // un solo hijo: el flex no se come los espacios
    return d;
  }
  function Img(padre, src, x, y, w, h) {
    const caja = N(padre, x, y, w, h);
    caja.style.overflow = 'hidden';
    const i = el('img', 'cubrir', caja); i.src = src; i.alt = ''; i.draggable = false;
    return caja;
  }
  const srcAvatar = (id) => `img/avatares/${id}.jpg`;
  const srcBanner = (id) => `img/banners/${id}.jpg`;
  const srcSkin = s => `img/skins/${s.arma}-${s.estilo}.webp`, srcArma = a => `img/skins/${a}.webp`, srcEmote = id => `img/emotes/${id}.webp`;
  function Contener(padre, src, x, y, w, h) { const caja = Img(padre, src, x, y, w, h); caja.firstChild.className = 'contener'; return caja; }
  function clic(nodo, accion, etiqueta) {
    nodo.classList.add('clic'); nodo.tabIndex = 0; nodo.setAttribute('role', 'button');
    if (etiqueta) nodo.setAttribute('aria-label', etiqueta);
    nodo.addEventListener('click', accion);
    nodo.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); accion(); } });
    return nodo;
  }
  function Boton(padre, x, y, w, h, texto, tam, tipo, accion) {
    const b = el('button', 'boton ' + (tipo || ''), padre);
    b.style.left = x + 'px'; b.style.top = y + 'px'; b.style.width = w + 'px'; b.style.height = h + 'px'; b.style.fontSize = tam + 'px';
    b.textContent = texto;
    if (accion) b.addEventListener('click', accion); else b.disabled = true;
    return b;
  }
  const degrade = (nodo, dir, a0, a1) => { nodo.style.background = `linear-gradient(${dir},rgba(0,0,0,${a0}),rgba(0,0,0,${a1}))`; nodo.style.pointerEvents = 'none'; };

  // ---------- Marcos (marcos.js, compartido con la presentación) ----------
  const enmarcar = (padre, x, y, w, h, id, adentro) => padre.insertAdjacentHTML('beforeend', Marcos.html(id, x, y, w, h, adentro));
  const interior = (id, h, adentro) => Marcos.interior(id, h, adentro);
  // Banner con su marco en un recuadro: el banner llega hasta el borde interior del riel.
  function bannerEnmarcado(padre, w, h, banner, marco, adentro, sombra) {
    const d = interior(marco, h, adentro);
    const caja = Img(padre, srcBanner(banner), d[0], d[1], w - d[0] - d[2], h - d[1] - d[3]);
    caja.style.borderRadius = marco ? '0' : '8px';
    if (sombra) sombra(caja);
    enmarcar(padre, 0, 0, w, h, marco, adentro);
  }

  // ---------- Íconos ----------
  function iconoMoneda(padre, i, cx, cy, s) {
    if (i === 0) {
      const o = N(padre, cx - s / 2, cy - s / 2, s, s); o.style.borderRadius = '50%'; o.style.background = '#D6DAE0';
      const a = N(padre, cx - s * 0.3, cy - s * 0.3, s * 0.6, s * 0.6); a.style.borderRadius = '50%'; a.style.background = '#D6DAE0';
      a.style.boxShadow = `inset 0 0 0 ${s * 0.1}px #0A0C11`;
    } else if (i === 1) {
      const w = s * 1.15, h = s * 0.72, b = N(padre, cx - w / 2, cy - h / 2, w, h);
      b.style.background = 'var(--boletos)';
      b.style.clipPath = 'polygon(0 0,100% 0,100% 35%,88% 50%,100% 65%,100% 100%,0 100%,0 65%,12% 50%,0 35%)';
    } else {
      const w = s * 0.8, h = s, b = N(padre, cx - w / 2, cy - h / 2, w, h);
      b.style.background = 'var(--cristales)'; b.style.clipPath = 'polygon(50% 0,100% 45%,50% 100%,0 45%)';
    }
  }
  function monedas(padre, derecha, y) {
    const nombres = [['Se ganan jugando', rift.creditos, false], ['Abren cápsulas', rift.boletos, true], ['Moneda de pago', rift.cristales, true]];
    let x = derecha;
    for (let i = 2; i >= 0; i--) {
      const [ayuda, cantidad, sumar] = nombres[i], w = sumar ? 214 : 186;
      x -= w;
      const chip = N(padre, x, y, w, 56, 'panel');
      iconoMoneda(chip, i, 28, 28, 22);
      T(chip, 50, 6, 120, 30, 'ft', 28, 'var(--ink)', cantidad.toLocaleString('es-AR'));
      T(chip, 50, 33, 140, 16, 'fr', 13, 'var(--mute)', ayuda, { mayus: true, esp: 8 });
      if (sumar) {
        const mas = N(chip, w - 40, 12, 30, 30); mas.style.borderRadius = '6px'; mas.style.background = i === 1 ? 'var(--boletos)' : 'var(--cristales)';
        T(mas, 0, -1, 30, 30, 'ft', 26, 'var(--accent-ink)', '+', { al: 'centro' });
        if (i === 1 && location.hash !== '#estacion') clic(mas, abrirEstacion, 'Conseguir boletos');
        if (i === 2 && location.hash !== '#cristales') clic(mas, () => { location.hash = 'cristales'; }, 'Conseguir cristales');
      }
      x -= 10;
    }
  }
  function capsula(padre, x, y) {
    const c = N(padre, x, y, 68, 96); c.style.borderRadius = '10px';
    c.style.background = 'linear-gradient(var(--accent) 0 17px,#161a21 17px calc(100% - 17px),var(--accent) calc(100% - 17px))';
    const v = N(c, 22, 26, 24, 44); v.style.borderRadius = '6px'; v.style.background = '#0b0d11';
    const l = N(v, 7, 6, 10, 32); l.style.borderRadius = '3px'; l.style.background = '#F3F4F6';
  }

  // ---------- Fondo del menú ----------
  const capaFondo = N(E, 0, 0, 1920, 1080);
  const capa = N(E, 0, 0, 1920, 1080);
  const avisoNodo = el('div', 'aviso', E);
  let fondoActual = 0, relojFondo = null;
  function fondo() {
    const f = el('div', 'fondo', capaFondo);
    f.style.backgroundImage = `url(img/fondos/${FONDOS[fondoActual]}.jpg)`;
    void f.offsetWidth; // que el fundido arranque desde transparente
    f.classList.add('visible');
    const viejos = [...capaFondo.children].slice(0, -1);
    setTimeout(() => viejos.forEach(v => v.remove()), 2400);
  }
  function arrancarFondos() {
    capaFondo.style.display = '';
    if (relojFondo) return;
    fondo();
    relojFondo = setInterval(() => { fondoActual = (fondoActual + 1) % FONDOS.length; fondo(); }, 9000);
  }
  function pararFondos() { clearInterval(relojFondo); relojFondo = null; capaFondo.style.display = 'none'; capaFondo.innerHTML = ''; }
  FONDOS.forEach(f => { const i = new Image(); i.src = `img/fondos/${f}.jpg`; });

  let tiempoAviso;
  function avisar(html) {
    avisoNodo.innerHTML = html; avisoNodo.classList.add('visible');
    clearTimeout(tiempoAviso); tiempoAviso = setTimeout(() => avisoNodo.classList.remove('visible'), 2600);
  }

  // ================= Menú principal =================
  function menu() {
    arrancarFondos();
    capa.innerHTML = ''; capa.style.background = 'none';
    degrade(N(capa, 0, 0, 1150, 1080), '90deg', 0.9, 0);
    degrade(N(capa, 0, 690, 1920, 390), '0deg', 0.88, 0);
    degrade(N(capa, 0, 0, 1920, 190), '180deg', 0.65, 0);

    const icono = el('img', 'n', capa); icono.src = 'img/icono.png'; icono.alt = ''; Object.assign(icono.style, { left: '60px', top: '32px', width: '68px', height: '68px' });
    T(capa, 142, 38, 300, 20, 'fr', 17, 'var(--accent)', 'Project', { mayus: true, esp: 18 });
    T(capa, 140, 56, 400, 44, 'ft', 44, 'var(--ink)', 'Riftwalker', { mayus: true, esp: 4 });

    const opinion = N(capa, 470, 44, 230, 48, 'panel');
    const globo = N(opinion, 16, 13, 26, 18); globo.style.background = 'var(--accent)'; globo.style.borderRadius = '4px';
    const cola = N(opinion, 21, 30, 8, 7); cola.style.background = 'var(--accent)'; cola.style.clipPath = 'polygon(0 0,100% 0,0 100%)';
    T(opinion, 54, 0, 170, 48, 'fr', 19, 'var(--soft)', 'Danos tu opinión', { mayus: true, esp: 6 });

    monedas(capa, 1478, 44);
    const cred = clic(N(capa, 1490, 30, 370, 84, 'panel borde-hover'), abrirPerfil, 'Abrir el perfil');
    const av = N(cred, 10, 10, 64, 64); av.style.background = 'var(--ink)'; av.style.borderRadius = '6px';
    Img(cred, srcAvatar(estado.avatar), 12, 12, 60, 60).style.borderRadius = '4px';
    T(cred, 88, 6, 236, 44, 'ft', 30, 'var(--ink)', estado.nombre, { mayus: true, esp: 3, puntos: true });
    const ti = equipado('titulo');
    T(cred, 88, 46, 236, 24, 'fr', 18, ti.id ? 'var(--accent)' : 'var(--mute)', ti.id ? ti.nombre : 'Sin título', { esp: 4, puntos: true });
    T(cred, 330, 22, 28, 40, 'ft', 34, 'var(--mute)', '»', { al: 'centro' });

    T(capa, 120, 250, 700, 24, 'fr', 20, 'var(--accent)', 'Andén 01 · Punto de encuentro', { mayus: true, esp: 16 });
    const jugar = N(capa, 112, 270, 560, 168, 'jugar');
    const tj = T(jugar, 0, 0, 460, 168, 'ft', 176, 'var(--ink)', 'Jugar', { mayus: true, esp: 2 });
    tj.style.width = 'auto';
    const fl = N(jugar, 0, 64, 50, 50, 'flecha');
    requestAnimationFrame(() => { fl.style.left = tj.offsetWidth + 26 + 'px'; });
    T(capa, 122, 440, 600, 30, 'fx', 25, 'var(--mute)', 'Táctico  ·  Deathmatch  ·  Invasión').style.whiteSpace = 'pre';
    const linea = N(capa, 122, 486, 440, 3); linea.style.background = 'linear-gradient(90deg,var(--accent),transparent)';
    ['Opciones', 'Logros', 'Créditos', 'Salir'].forEach((o, i) => {
      const fila = N(capa, 120, 508 + i * 50, 340, 46, 'fila');
      el('div', 'marca', fila);
      T(fila, 0, 0, 340, 46, 'ft', 36, 'var(--soft)', o, { mayus: true, esp: 6 });
    });
    T(capa, 122, 718, 200, 24, 'fr', 17, 'var(--mute)', 'Campos de prueba:', { mayus: true, esp: 12 });
    T(capa, 300, 718, 240, 24, 'fr', 17, 'var(--soft)', 'Campo de práctica', { mayus: true, esp: 12 });

    // US 385: la tarjeta Tienda reúne la tienda directa, las cápsulas, el pase de batalla y los cristales.
    const tarjeta = clic(N(capa, 80, 900, 580, 132, 'panel borde-hover'), () => { location.hash = 'tienda'; }, 'Abrir la Tienda');
    const lt = N(tarjeta, 0, 0, 580, 3); lt.style.background = 'rgba(242,154,56,.8)';
    capsula(tarjeta, 30, 18);
    T(tarjeta, 128, 18, 420, 20, 'fr', 16, 'var(--accent)', '01 / Cápsulas · Pase · Cristales', { mayus: true, esp: 12 });
    T(tarjeta, 126, 38, 430, 46, 'ft', 42, 'var(--ink)', 'Tienda', { mayus: true, esp: 3 });
    T(tarjeta, 128, 86, 420, 26, 'fx', 19, 'var(--mute)', rift.boletos === 1 ? 'Tenés 1 boleto para abrir' : `Tenés ${rift.boletos} boletos para abrir`);
    if (paraReclamar() > 0) { const d = N(tarjeta, 550, -8, 20, 20); d.style.borderRadius = '10px'; d.style.background = 'var(--accent)'; }

    const nota = N(capa, 1572, 966, 300, 76, 'panel');
    const ci = N(nota, 16, 14, 48, 48); ci.style.borderRadius = '50%'; ci.style.background = 'var(--accent)';
    T(ci, 0, 0, 48, 48, 'ft', 34, 'var(--accent-ink)', 'i', { al: 'centro' });
    T(nota, 80, 12, 210, 18, 'fr', 14, 'var(--mute)', 'Antes de jugar', { mayus: true, esp: 14 });
    T(nota, 80, 30, 210, 36, 'ft', 30, 'var(--ink)', 'Nota para el PO', { mayus: true, esp: 3 });
  }

  // ================= Perfil =================
  function abrirPerfil() { location.hash = 'perfil'; }
  function volverAlMenu() { location.hash = ''; }

  function perfil() {
    pararFondos();
    capa.innerHTML = ''; capa.style.background = '#0b0d11';
    const volver = clic(N(capa, 60, 34, 150, 52, 'panel borde-hover'), volverAlMenu, 'Volver al menú');
    T(volver, 0, 0, 150, 52, 'ft', 26, 'var(--ink)', '« Menú', { mayus: true, esp: 6, al: 'centro' });
    T(capa, 240, 34, 400, 20, 'fr', 17, 'var(--accent)', 'Registro del jugador', { mayus: true, esp: 16 });
    T(capa, 238, 52, 400, 46, 'ft', 44, 'var(--ink)', 'Perfil', { mayus: true, esp: 3 });
    monedas(capa, 1860, 34);
    cabecera();
    pestanas();
    const panel = N(capa, 60, 520, 1800, 540);
    [estadisticas, personalizacion, emotes, historial, cuenta][pestana](panel);
  }

  function cabecera() {
    const cab = N(capa, 60, 120, 1800, 300);
    cab.style.background = 'var(--panel)'; cab.style.borderRadius = '10px';
    const marco = estado.marco;
    bannerEnmarcado(cab, 1800, 300, estado.banner, marco, false, caja => {
      const w = caja.offsetWidth || 1800;
      degrade(N(caja, 0, 0, 1150, 300), '90deg', 0.9, 0);
      degrade(N(caja, 0, 140, Math.max(w, 1800), 160), '0deg', 0.55, 0);
    });
    if (!marco) { const b = N(cab, 0, 0, 1800, 300); b.style.borderRadius = '10px'; b.style.boxShadow = 'inset 0 0 0 1px rgba(255,255,255,.08)'; b.style.pointerEvents = 'none'; }
    const ma = N(cab, 92, 46, 208, 208); ma.style.background = 'var(--ink)'; ma.style.borderRadius = '8px';
    Img(cab, srcAvatar(estado.avatar), 96, 50, 200, 200).style.borderRadius = '6px';
    T(cab, 336, 36, 900, 90, 'ft', 88, 'var(--ink)', estado.nombre, { mayus: true, esp: 3 });
    const ti = equipado('titulo');
    T(cab, 340, 124, 900, 38, 'fr', 30, ti.id ? 'var(--accent)' : 'var(--mute)', ti.id ? ti.nombre : 'Sin título', { esp: 6 });
    [['8', 'Partidas'], ['5', 'Victorias'], ['2,09', 'KDA*'], ['—', 'Oleada récord']].forEach(([v, r], i) => {
      const chip = N(cab, 340 + i * 170, 178, 156, 70); chip.style.background = 'rgba(0,0,0,.5)'; chip.style.borderRadius = '6px';
      T(chip, 16, 6, 130, 38, 'ft', 36, 'var(--ink)', v);
      T(chip, 16, 44, 140, 18, 'fr', 14, 'var(--mute)', r, { mayus: true, esp: 14 });
    });
    Boton(cab, 1800 - 150 - 220, 222, 220, 46, 'Cambiar nombre', 20, '', cambiarNombre);
    nivelJugador(cab);
  }

  // Nivel de jugador (el MMR de buscar partida) con su rango: el hexágono y el rango toman el color de la rareza.
  const NIVEL_JUGADOR = 1030;
  const RANGOS = [[1250, 'Jefe de estación', 4], [1100, 'Maquinista', 3], [950, 'Guarda', 2], [0, 'Pasajero', 1]];
  function nivelJugador(cab) {
    const [, rango, r] = RANGOS.find(([minimo]) => NIVEL_JUGADOR >= minimo), color = COLOR[r];
    const caja = N(cab, 1800 - 150 - 360, 34, 360, 172); caja.style.background = 'rgba(0,0,0,.55)'; caja.style.borderRadius = '8px';
    const hex = 'polygon(50% 0,93.3% 25%,93.3% 75%,50% 100%,6.7% 75%,6.7% 25%)';
    const a = N(caja, 70 - 52, 86 - 52, 104, 104); a.style.background = color; a.style.clipPath = hex;
    const b = N(caja, 70 - 44, 86 - 44, 88, 88); b.style.background = '#0E1117'; b.style.clipPath = hex;
    const c = N(caja, 70 - 24, 86 - 24, 48, 48); c.style.background = color; c.style.opacity = '.35'; c.style.clipPath = hex;
    T(caja, 140, 14, 210, 20, 'fr', 15, 'var(--mute)', 'Nivel de jugador', { mayus: true, esp: 14 });
    T(caja, 138, 30, 210, 78, 'ft', 78, 'var(--ink)', NIVEL_JUGADOR.toLocaleString('es-AR'));
    T(caja, 140, 106, 210, 28, 'ft', 24, color, rango, { mayus: true, esp: 6 });
    T(caja, 140, 136, 210, 22, 'fx', 14, 'var(--mute)', 'Sube y baja en buscar partida');
  }

  function pestanas() {
    const nombres = ['Estadísticas', 'Personalización', 'Emotes', 'Historial', 'Cuenta'];
    let x = 60;
    nombres.forEach((n, i) => {
      const t = T(capa, x, 438, 360, 50, 'ft pestana' + (i === pestana ? ' activa' : ''), 36, i === pestana ? 'var(--ink)' : 'var(--mute)', n, { mayus: true, esp: 6 });
      t.style.width = 'auto';
      clic(t, () => { pestana = i; perfil(); }, n);
      if (i === pestana) { const r = N(capa, x, 490, t.offsetWidth, 4); r.style.background = 'var(--accent)'; }
      x += t.offsetWidth + 56;
    });
    let info;
    if (pestana === 0) info = 'Partidas terminadas  <b>8</b>';
    else if (pestana === 1) { const tot = Object.values(LISTA).reduce((a, l) => a + l.length, 0) + SKINS.length; info = `Colección  <b>${tot} / ${tot}</b>`; }
    else if (pestana === 2) info = `En la rueda  <b>${estado.emotes.filter(e => e).length} / 4</b>`;
    else if (pestana === 3) info = `Partidas  <b>${HISTORIAL.length} de 20</b>`;
    else info = 'Cuenta  <b>Desde el launcher</b>';
    const d = T(capa, 1060, 452, 800, 26, 'fr', 19, 'var(--mute)', info, { mayus: true, esp: 14, al: 'der', html: true });
    d.style.whiteSpace = 'pre'; d.style.pointerEvents = 'none'; // con 5 pestañas, la última llega por debajo de este texto
    d.querySelector('b').style.cssText = 'color:var(--ink);font-family:"JetBrains Mono",monospace;font-weight:400;letter-spacing:0;margin-left:10px';
    const l = N(capa, 60, 494, 1800, 1); l.style.background = 'rgba(255,255,255,.08)';
  }

  // ---------- Estadísticas (datos de ejemplo) ----------
  function estadisticas(p) {
    const g = N(p, 0, 0, 1800, 168, 'panel');
    T(g, 28, 22, 600, 22, 'fr', 17, 'var(--accent)', 'General · Táctico y Deathmatch', { mayus: true, esp: 16 });
    [['8', 'Partidas'], ['5', 'Victorias'], ['62 %', '% de victorias'], ['112', 'Bajas'], ['58', 'Muertes'], ['9', 'Asistencias'], ['2,09', 'KDA*'], ['27 %', 'Tiros a la cabeza'], ['1,2 h', 'Horas jugadas']]
      .forEach(([v, r], i) => { T(g, 28 + i * 194, 50, 190, 60, 'ft', 52, 'var(--ink)', v); T(g, 30 + i * 194, 112, 190, 22, 'fr', 14, 'var(--mute)', r, { mayus: true, esp: 14 }); });
    const cartas = [
      ['Táctico', '#F29A38', [['Partidas', '3'], ['Victorias', '1'], ['Rondas ganadas', '10'], ['Plantados', '3'], ['Desactivados', '2'], ['MVP', '1']]],
      ['Deathmatch', '#FF5C5C', [['Partidas', '5'], ['Primer puesto', '4'], ['Bajas', '102'], ['Mejor racha', '11']]],
      ['Invasión', '#7DE05A', [['Partidas', '6'], ['Mundo salvado', '2'], ['Invasores eliminados', '1.284'], ['Récord F · N · D', '18 · 12 · 7'],
        ['Jefes derrotados', '3'], ['Puestos cargados', '41'], ['Carta favorita', 'Doble cartucho'], ['Arma favorita', 'Mitre'], ['En cooperativo', '4']]],
      ['Minijuego: cometas', '#4CC3FF', [['Destruidos', '171'], ['Precisión', '26 %'], ['Puntos', '12.600'], ['Reacción promedio', '5,9 s'], ['Récord en una fase', '16 · 1.250 pts'], ['Mejor reacción', '1,6 s']]],
    ];
    cartas.forEach(([titulo, color, filas], i) => {
      const c = N(p, i * 455, 188, 435, 300, 'panel');
      const l = N(c, 0, 0, 435, 4); l.style.background = color;
      T(c, 24, 18, 400, 44, 'ft', 34, color, titulo, { mayus: true, esp: 3 });
      const compacto = filas.length > 6, alto = compacto ? 25 : 37; // con más de 6 filas, más juntas (como la de Invasión)
      filas.forEach(([n, v], j) => {
        const y = (compacto ? 64 : 66) + j * alto;
        T(c, 24, y, 260, alto - 4, 'fx', compacto ? 18 : 21, 'var(--soft)', n);
        T(c, 180, y, 231, alto - 4, 'ft', compacto ? 21 : 26, 'var(--ink)', v, { al: 'der' });
        if (j < filas.length - 1) { const ln = N(c, 24, y + alto - 2, 387, 1); ln.style.background = 'rgba(255,255,255,.06)'; }
      });
    });
    T(p, 0, 500, 1800, 26, 'fx', 17, 'var(--mute)', '* KDA = (bajas + asistencias) ÷ muertes; sin muertes, ÷ 1. Asistencia: dañaste a un rival que otro eliminó en los 10 s siguientes. Cuentan las partidas terminadas.');
  }

  // ---------- Personalización ----------
  function personalizacion(p) {
    const tipos = ['avatar', 'banner', 'titulo', 'marco', 'skin'], nombres = ['Avatares', 'Banners', 'Títulos', 'Marcos', 'Armas'];
    tipos.forEach((t, i) => {
      const activo = t === categoria, chip = N(p, i * 202, 0, 190, 52), n = t === 'skin' ? SKINS.length : LISTA[t].length;
      chip.style.borderRadius = '8px'; chip.style.background = activo ? 'var(--accent)' : 'var(--panel)';
      T(chip, 16, 0, 120, 52, 'ft', 26, activo ? 'var(--accent-ink)' : 'var(--soft)', nombres[i], { mayus: true, esp: 5 });
      T(chip, 96, 0, 80, 52, 'fm', 16, activo ? 'var(--accent-ink)' : 'var(--mute)', `${n}/${n}`, { al: 'der' });
      if (!activo) clic(chip, () => { categoria = t; perfil(); }, nombres[i]);
    });
    if (categoria === 'skin') { armas(p); return; }
    const lista = LISTA[categoria];
    if (!elegido[categoria]) elegido[categoria] = estado[categoria];
    const sel = buscar(categoria, elegido[categoria]);
    const grilla = N(p, 0, 68, 1280, 472, 'grilla');
    const cols = categoria === 'avatar' ? 7 : categoria === 'marco' ? 2 : 3, gap = 18, AG = 1260;
    const w = (AG - (cols - 1) * gap) / cols;
    const h = categoria === 'avatar' ? w + 58 : categoria === 'banner' ? w / 3 + 60 : categoria === 'marco' ? w / 5.4 + 60 : 78;
    const interno = N(grilla, 0, 0, AG, Math.ceil(lista.length / cols) * (h + gap));
    lista.forEach((c, i) => carta(interno, c, (i % cols) * (w + gap), Math.floor(i / cols) * (h + gap), w, h, c.id === sel.id));
    vistaPrevia(N(p, 1300, 0, 500, 540, 'panel'), sel);
    const actual = grilla.querySelector('.elegida');
    if (actual && actual.offsetTop + actual.offsetHeight > 472) grilla.scrollTop = actual.offsetTop - 8;
  }

  function carta(padre, c, x, y, w, h, esElegida) {
    const t = categoria, color = COLOR[c.rareza], eq = estado[t] === c.id;
    const k = N(padre, x, y, w, h, 'carta' + (esElegida ? ' elegida' : ''));
    k.style.position = 'absolute';
    let textoY;
    if (t === 'titulo') {
      const r = N(k, 0, 0, 6, h); r.style.background = color;
      T(k, 22, 4, w - 40, 42, 'ft', 27, 'var(--ink)', c.nombre, { puntos: true });
      textoY = 44;
    } else {
      const ih = t === 'avatar' ? w : t === 'marco' ? w / 5.4 : w / 3;
      const caja = N(k, 0, 0, w, ih); caja.style.overflow = 'hidden';
      if (t === 'avatar') Img(caja, srcAvatar(c.id), 0, 0, w, ih);
      else if (t === 'banner') Img(caja, srcBanner(c.id), 0, 0, w, ih);
      else bannerEnmarcado(caja, w, ih, estado.banner, c.id, true);
      T(k, 12, ih + 5, w - 24, 30, 'fr', 20, 'var(--ink)', c.nombre, { esp: 2, puntos: true });
      textoY = ih + 32;
      const r = N(k, 0, h - 4, w, 4); r.style.background = color;
    }
    T(k, t === 'titulo' ? 22 : 12, textoY, w - 24, 20, 'fr', 15, eq ? 'var(--ok)' : color, eq ? 'Equipado' : RAREZA[c.rareza], { mayus: true, esp: 14 });
    el('div', 'linea-borde', k);
    clic(k, () => { elegido[t] = c.id; perfil(); }, c.nombre);
  }

  function vistaPrevia(v, c) {
    T(v, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Vista previa', { mayus: true, esp: 16 });
    const color = COLOR[c.rareza];
    if (categoria === 'avatar') {
      const m = N(v, 96, 54, 308, 308); m.style.background = color; m.style.borderRadius = '8px';
      Img(v, srcAvatar(c.id), 100, 58, 300, 300).style.borderRadius = '6px';
    } else {
      const mini = N(v, 24, 60, 452, 296); mini.style.background = '#11141B'; mini.style.borderRadius = '8px';
      const banner = categoria === 'banner' ? c.id : estado.banner, marco = categoria === 'marco' ? c.id : estado.marco;
      const arriba = N(mini, 0, 0, 452, 151);
      bannerEnmarcado(arriba, 452, 151, banner, marco, false, caja => degrade(N(caja, 0, 0, 300, 151), '90deg', 0.9, 0));
      const ma = N(mini, 34, 29.5, 92, 92); ma.style.background = 'var(--ink)'; ma.style.borderRadius = '6px';
      Img(mini, srcAvatar(estado.avatar), 36, 31.5, 88, 88).style.borderRadius = '4px';
      T(mini, 140, 40, 300, 40, 'ft', 38, 'var(--ink)', estado.nombre, { mayus: true, esp: 3, puntos: true });
      const ti = categoria === 'titulo' ? c : equipado('titulo');
      T(mini, 142, 82, 290, 26, 'fr', 20, ti.id ? 'var(--ink)' : 'var(--mute)', ti.id ? ti.nombre : 'Sin título', { puntos: true });
      if (categoria === 'titulo') {
        const g = T(mini, 20, 170, 412, 110, 'ft', c.nombre.length > 22 ? 34 : 44, color, c.nombre, { al: 'centro', mayus: true, esp: 2 });
        g.style.whiteSpace = 'normal';
      } else {
        T(mini, 20, 168, 412, 110, 'fx', 20, 'var(--mute)', categoria === 'marco'
          ? 'Así queda el marco en la cabecera de tu Perfil. Más adelante, también en la tabla con Tab.'
          : 'Así se ve tu cabecera en el Perfil. El lado izquierdo del banner queda para tu avatar y tu nombre.', { arriba: true });
      }
    }
    T(v, 24, 366, 452, 58, 'ft', 42, 'var(--ink)', c.nombre, { mayus: true, esp: 3, puntos: true });
    let detalle = `${TIPO[categoria]} · <span style="color:${color}">${RAREZA[c.rareza]}</span>`;
    if (c.estilo) detalle += ' · ' + c.estilo;
    if (personaje(c.personaje)) detalle += ' · ' + personaje(c.personaje);
    T(v, 24, 420, 452, 28, 'fx', 20, 'var(--mute)', detalle, { html: true });
    if (estado[categoria] === c.id) Boton(v, 24, 464, 452, 56, 'Equipado', 26, 'ok', null);
    else Boton(v, 24, 464, 452, 56, 'Equipar', 26, 'lleno', () => {
      estado[categoria] = c.id; guardar();
      avisar(`<span style="color:${color}">${c.nombre}</span> equipado`);
      perfil();
    });
  }

  // ---------- Armas (F35): la skin de cada arma ----------
  // Las skins del juego son materiales 3D; acá se ven sobre el ícono del arma de la tienda, pintado con los colores de
  // la skin. La barra de cada carta son los colores de cuerpo, paneles, detalles y acento (CatalogoDeSkins.ColoresDeBarra).
  function armas(p) {
    const aw = (1280 - 8 * 8) / 9;
    X.armas.forEach((a, i) => {
      const activo = a.id === armaVista, chip = N(p, i * (aw + 8), 66, aw, 48);
      chip.style.borderRadius = '8px'; chip.style.background = activo ? 'var(--sel)' : 'var(--panel)';
      chip.style.boxShadow = activo ? 'inset 0 0 0 2px var(--accent)' : 'inset 0 0 0 1px rgba(255,255,255,.08)';
      T(chip, 6, 0, aw - 12, 48, 'fr', 17, activo ? 'var(--ink)' : 'var(--soft)', a.nombre, { mayus: true, esp: 4, al: 'centro', puntos: true });
      if (!activo) clic(chip, () => { armaVista = a.id; perfil(); }, a.nombre);
    });
    const original = { tipo: 'skin', id: '', arma: armaVista, estilo: '', nombre: ARMA[armaVista] + ' · Sin skin', rareza: 0, barra: ['#5A616C', '#3A3F48', '#22262D', '#8E96A3'] };
    const lista = [original, ...SKINS.filter(s => s.arma === armaVista)];
    if (elegido.skin[armaVista] == null) elegido.skin[armaVista] = estado.skins[armaVista] || '';
    const sel = lista.find(s => s.id === elegido.skin[armaVista]) || original;
    const grilla = N(p, 0, 130, 1280, 410, 'grilla'), cols = 3, gap = 18, w = (1260 - (cols - 1) * gap) / cols, h = 128;
    const interno = N(grilla, 0, 0, 1260, Math.ceil(lista.length / cols) * (h + gap));
    lista.forEach((s, i) => {
      const x = (i % cols) * (w + gap), y = Math.floor(i / cols) * (h + gap), eq = (estado.skins[armaVista] || '') === s.id;
      const k = N(interno, x, y, w, h, 'carta' + (s.id === sel.id ? ' elegida' : ''));
      let bx = 0;
      [0.5, 0.3, 0.12, 0.08].forEach((parte, j) => { const b = N(k, bx, 0, w * parte, 40); b.style.background = s.barra[j]; bx += w * parte; });
      Contener(k, s.id ? srcSkin(s) : srcArma(s.arma), w - 150, 46, 136, 74);
      T(k, 16, 46, w - 170, 40, 'ft', 22, 'var(--ink)', s.nombre, { mayus: true, esp: 2, puntos: true });
      const color = COLOR[s.rareza];
      T(k, 16, 88, w - 176, 22, 'fr', 15, eq ? 'var(--ok)' : s.id ? color : 'var(--mute)', eq ? 'Equipada' : s.id ? RAREZA[s.rareza] : 'Original', { mayus: true, esp: 14 });
      el('div', 'linea-borde', k);
      clic(k, () => { elegido.skin[armaVista] = s.id; perfil(); }, s.nombre);
    });
    const actual = grilla.querySelector('.elegida');
    if (actual && actual.offsetTop + actual.offsetHeight > 410) grilla.scrollTop = actual.offsetTop - 8;

    const v = N(p, 1300, 0, 500, 540, 'panel'), color = COLOR[sel.rareza];
    T(v, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Vista previa', { mayus: true, esp: 16 });
    const caja = N(v, 24, 54, 452, 301); caja.style.borderRadius = '8px';
    caja.style.background = `radial-gradient(ellipse at 50% 45%,${sel.id ? color + '40' : '#232833'},#0d1015 70%)`;
    Contener(caja, sel.id ? srcSkin(sel) : srcArma(sel.arma), 26, 40, 400, 221);
    T(v, 24, 366, 452, 58, 'ft', 38, 'var(--ink)', sel.nombre, { mayus: true, esp: 3, puntos: true });
    T(v, 24, 420, 452, 28, 'fx', 20, 'var(--mute)', sel.id ? `Skin de arma · <span style="color:${color}">${RAREZA[sel.rareza]}</span> · ${dondeSeConsigue(sel)}`
      : 'El arma como viene, sin skin', { html: true, puntos: true });
    if ((estado.skins[armaVista] || '') === sel.id) Boton(v, 24, 464, 452, 56, 'Equipada', 26, 'ok', null);
    else Boton(v, 24, 464, 452, 56, 'Equipar', 26, 'lleno', () => {
      estado.skins[armaVista] = sel.id; guardar();
      avisar(sel.id ? `<span style="color:${color}">${sel.nombre}</span> equipada` : `${ARMA[armaVista]} sin skin`);
      perfil();
    });
  }

  // ---------- Emotes (US 392): la rueda de 4 espacios y los 15 bailes ----------
  function iconoEmote(padre, e, x, y, lado) {
    const f = N(padre, x, y, lado, lado); f.style.borderRadius = '50%'; f.style.background = 'var(--chip)';
    Contener(f, srcEmote(e.id), 0, 0, lado, lado);
    return f;
  }
  function emotes(p) {
    const w = (1260 - 3 * 18) / 4;
    LUGARES.forEach((lugar, i) => {
      const e = emoteDe(estado.emotes[i]), esElegido = espacio === i;
      const k = N(p, i * (w + 18), 0, w, 124, 'carta' + (esElegido ? ' elegida' : ''));
      k.style.background = esElegido ? 'var(--sel)' : 'var(--panel)';
      T(k, 18, 12, w - 36, 20, 'fr', 15, esElegido ? 'var(--accent)' : 'var(--mute)', `Espacio ${i + 1} · ${lugar}`, { mayus: true, esp: 14 });
      if (e) iconoEmote(k, e, 18, 42, 64);
      T(k, e ? 94 : 18, 44, w - (e ? 94 : 18) - 104, 60, 'ft', 28, e ? 'var(--ink)' : 'var(--mute)', e ? e.nombre : 'Vacío', { mayus: true, esp: 3, puntos: true });
      el('div', 'linea-borde', k);
      clic(k, () => { espacio = i; if (e) emoteVisto = e.id; perfil(); }, `Espacio ${i + 1}`);
      if (e) Boton(k, w - 100, 64, 86, 40, 'Quitar', 17, '', ev => {
        ev.stopPropagation(); estado.emotes[i] = ''; guardar(); avisar(`Espacio ${i + 1} vacío`); perfil();
      });
    });
    T(p, 0, 136, 1260, 26, 'fx', 19, 'var(--mute)', 'Elegí un espacio de la rueda y después el baile que va ahí. Mantené T en la partida para abrir la rueda.');
    const grilla = N(p, 0, 172, 1280, 368, 'grilla'), cw = (1260 - 3 * 18) / 4;
    const interno = N(grilla, 0, 0, 1260, Math.ceil(EMOTES.length / 4) * 122 - 18);
    EMOTES.forEach((e, i) => {
      const en = estado.emotes.indexOf(e.id), color = COLOR[e.rareza];
      const k = N(interno, (i % 4) * (cw + 18), Math.floor(i / 4) * 122, cw, 104, 'carta' + (emoteVisto === e.id ? ' elegida' : ''));
      const franja = N(k, 0, 0, 5, 104); franja.style.background = color;
      iconoEmote(k, e, 18, 16, 72);
      T(k, 104, 12, cw - 116, 36, 'ft', 30, 'var(--ink)', e.nombre, { mayus: true, esp: 3, puntos: true });
      T(k, 104, 48, cw - 116, 20, 'fr', 15, color, RAREZA[e.rareza], { mayus: true, esp: 12 });
      T(k, 104, 70, cw - 116, 20, 'fr', 14, en >= 0 ? 'var(--ok)' : 'var(--mute)', en >= 0 ? `En el espacio ${en + 1}` : 'Sin equipar', { mayus: true, esp: 12 });
      el('div', 'linea-borde', k);
      clic(k, () => {
        emoteVisto = e.id;
        const antes = estado.emotes.indexOf(e.id);
        if (antes >= 0) estado.emotes[antes] = '';
        estado.emotes[espacio] = e.id; guardar();
        avisar(`${e.nombre} en el espacio ${espacio + 1}`);
        perfil();
      }, e.nombre);
    });
    const v = N(p, 1300, 0, 500, 540, 'panel');
    T(v, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Vista previa', { mayus: true, esp: 16 });
    const caja = N(v, 62, 50, 376, 401); caja.style.borderRadius = '8px'; caja.style.background = 'radial-gradient(ellipse at 50% 40%,#232833,#0d1015)';
    const visto = emoteDe(emoteVisto || estado.emotes[espacio]);
    if (visto) {
      // Los cuatro bailes que la presentación tiene grabados se ven en movimiento; el resto, con su ícono.
      if (visto.baile) Contener(caja, `../presentacion/media/emotes/${visto.baile}.png`, 8, 8, 360, 385);
      else iconoEmote(caja, visto, 68, 80, 240);
      T(v, 24, 452, 452, 50, 'ft', 40, COLOR[visto.rareza], visto.nombre, { al: 'centro', mayus: true, esp: 3 });
      const en = estado.emotes.indexOf(visto.id);
      T(v, 24, 500, 452, 26, 'fx', 18, 'var(--mute)', en >= 0 ? `En el espacio ${en + 1} · ${LUGARES[en]}` : 'Sin equipar', { al: 'centro' });
    } else T(v, 24, 470, 452, 26, 'fx', 18, 'var(--mute)', 'Elegí un baile para verlo', { al: 'centro' });
  }

  // ---------- Historial (las últimas 20 partidas; acá, de ejemplo) ----------
  const HISTORIAL = [
    ['Deathmatch', '1.º puesto', '24 / 9 / 3 · KDA 3,00', 'Mapa 2', '23/11/2026', '8:12', true],
    ['Invasión', 'Mundo salvado', 'Normal · oleada 12 · cooperativo', 'Invasión', '23/11/2026', '21:40', true],
    ['Táctico', 'Victoria 7 - 5', '17 / 11 / 4 · KDA 1,91', 'Mapa 1', '22/11/2026', '24:05', true],
    ['Invasión', 'Derrota', 'Difícil · oleada 7 · solo', 'Invasión', '22/11/2026', '15:22', false],
    ['Deathmatch', '3.º puesto', '15 / 14 / 2 · KDA 1,21', 'Mapa 2', '21/11/2026', '8:00', false],
    ['Táctico', 'Derrota 4 - 7', '9 / 13 / 5 · KDA 1,08', 'Mapa 1', '20/11/2026', '19:47', false],
    ['Deathmatch', '2.º puesto', '21 / 12 / 1 · KDA 1,83', 'Mapa 2', '19/11/2026', '8:00', false],
    ['Invasión', 'Mundo salvado', 'Fácil · oleada 10 · solo', 'Invasión', '18/11/2026', '17:03', true],
  ];
  const COLOR_MODO = { 'Táctico': '#F29A38', 'Deathmatch': '#FF5C5C', 'Invasión': '#7DE05A' };
  function historial(p) {
    const t = N(p, 0, 0, 1800, 488, 'panel');
    const cols = [['Modo', 28], ['Resultado', 270], ['Desempeño', 560], ['Mapa', 1080], ['Fecha', 1340], ['Duración', 1590]];
    cols.forEach(([n, x]) => T(t, x, 14, 260, 24, 'fr', 16, 'var(--accent)', n, { mayus: true, esp: 14 }));
    HISTORIAL.forEach(([modo, res, des, mapa, fecha, dur, gano], i) => {
      const y = 48 + i * 54, f = N(t, 12, y, 1776, 48); f.style.borderRadius = '6px';
      f.style.background = i % 2 ? 'transparent' : 'rgba(255,255,255,.03)';
      const marca = N(t, 12, y, 4, 48); marca.style.background = COLOR_MODO[modo];
      T(t, 28, y, 230, 48, 'ft', 26, COLOR_MODO[modo], modo, { mayus: true, esp: 3 });
      T(t, 270, y, 280, 48, 'ft', 24, gano ? 'var(--ok)' : 'var(--soft)', res, { mayus: true, esp: 2 });
      T(t, 560, y, 500, 48, 'fx', 20, 'var(--ink)', des);
      T(t, 1080, y, 240, 48, 'fx', 20, 'var(--soft)', mapa);
      T(t, 1340, y, 230, 48, 'fm', 18, 'var(--soft)', fecha);
      T(t, 1590, y, 180, 48, 'fm', 18, 'var(--soft)', dur);
    });
    T(p, 0, 500, 1800, 26, 'fx', 17, 'var(--mute)', 'Las últimas 20 partidas terminadas de tu cuenta, de la más nueva a la más vieja. En la página son de ejemplo.');
  }

  // ---------- Cuenta (en el juego la maneja el launcher) ----------
  function cuenta(p) {
    const a = N(p, 0, 0, 1040, 488, 'panel');
    T(a, 32, 26, 600, 22, 'fr', 17, 'var(--accent)', 'Tu cuenta', { mayus: true, esp: 16 });
    T(a, 30, 52, 900, 54, 'ft', 48, 'var(--ok)', 'Con cuenta', { mayus: true, esp: 3 });
    T(a, 32, 112, 960, 60, 'fx', 21, 'var(--soft)', 'Tu progreso, tus monedas y tu lugar en el ranking están en la cuenta, y los seguís en cualquier PC.', { arriba: true });
    [['Nombre', estado.nombre], ['Mail', 'jugador@ejemplo.com'], ['Desde', 'Desde el launcher'], ['Progreso', 'Se guarda en la cuenta']].forEach(([n, v], i) => {
      const y = 190 + i * 66;
      T(a, 32, y, 220, 48, 'fr', 18, 'var(--mute)', n, { mayus: true, esp: 14 });
      T(a, 250, y, 760, 48, 'ft', 28, 'var(--ink)', v, { esp: 2, puntos: true });
      if (i < 3) { const l = N(a, 32, y + 56, 976, 1); l.style.background = 'rgba(255,255,255,.06)'; }
    });
    const b = N(p, 1060, 0, 740, 488, 'panel');
    T(b, 32, 26, 600, 22, 'fr', 17, 'var(--accent)', 'Cerrar sesión', { mayus: true, esp: 16 });
    T(b, 32, 62, 676, 120, 'fx', 21, 'var(--soft)', 'Para cambiar de cuenta o cerrar sesión, usá el <span style="color:var(--ink)">launcher</span>: el menú de la cuenta está arriba a la derecha.', { arriba: true, html: true });
    Boton(b, 32, 200, 300, 56, 'Cerrar sesión', 24, '', null);
    T(b, 32, 400, 676, 60, 'fx', 17, 'var(--mute)', 'En la página no hay cuenta de verdad: es una copia del menú para mostrarlo.', { arriba: true });
  }

  // ---------- Cambiar nombre ----------
  function cambiarNombre() {
    const m = el('div', 'modal', E), caja = el('div', 'caja', m);
    T(caja, 40, 30, 560, 22, 'fr', 17, 'var(--accent)', 'Tu nombre', { mayus: true, esp: 16 });
    T(caja, 40, 56, 560, 46, 'ft', 40, 'var(--ink)', 'Cambiar nombre', { mayus: true, esp: 3 });
    const inp = el('input', '', caja); inp.maxLength = 16; inp.value = estado.nombre; inp.setAttribute('aria-label', 'Nombre');
    const cerrar = () => m.remove();
    const aceptar = () => { const v = inp.value.trim(); if (v) { estado.nombre = v; guardar(); } cerrar(); perfil(); };
    Boton(caja, 40, 214, 270, 56, 'Cancelar', 24, '', cerrar);
    Boton(caja, 330, 214, 270, 56, 'Aceptar', 24, 'lleno', aceptar);
    inp.addEventListener('keydown', e => { if (e.key === 'Enter') aceptar(); if (e.key === 'Escape') { e.stopPropagation(); cerrar(); } });
    setTimeout(() => { inp.focus(); inp.select(); }, 0);
  }

  // ================= Estación del Rift =================
  // Con un boleto se abre una cápsula que trae un avatar, un banner, un título o un marco. La cápsula tiembla, toma el
  // color de la rareza (si es épica o legendaria primero amaga con azul), se abre la grieta, hay un destello y aparece
  // el premio. Lo que el juego superpone con transparencia pasa por sobre(): Unity mezcla en espacio lineal y así se
  // ve igual que en la captura del juego.
  const W = 1180, H = 690, CX = 590, CY = 320, REVELAR = 1.25, FIN = 1.7;
  const rgb = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
  const ACC = rgb('#F29A38'), HOT = rgb('#FFD2A1');
  const clamp01 = v => Math.min(1, Math.max(0, v));
  const mezclar = (a, b, k) => a.map((v, i) => v + (b[i] - v) * clamp01(k));
  const css = (c, a = 1) => `rgba(${c.map(Math.round).join(',')},${a.toFixed(3)})`;
  function sobre(a, fondo = 13) {
    a = clamp01(a);
    const l = Math.pow(fondo / 255, 2.2) * (1 - a) + a;
    return clamp01((Math.pow(l, 1 / 2.2) * 255 - fondo) / (255 - fondo));
  }
  // UIRing con glow: el color en el centro y transparente en el borde; glowMid es el alfa a mitad de radio.
  function resplandor(c, a, mid, radio, x, y) {
    const p = Math.log(mid) / Math.log(0.5), paradas = [];
    for (let k = 0; k <= 12; k++) paradas.push(`${css(c, sobre(a * Math.pow(1 - k / 12, p)))} ${(k / 12 * 100).toFixed(1)}%`);
    return `radial-gradient(circle ${radio}px at ${x}px ${y}px,${paradas.join(',')})`;
  }
  function lienzo(padre) {
    const c = el('canvas', 'n', padre); c.width = W; c.height = H;
    Object.assign(c.style, { left: '0', top: '0', width: W + 'px', height: H + 'px', pointerEvents: 'none' });
    return c.getContext('2d');
  }

  let est = null; // la pantalla abierta: nodos y estado de la animación
  function abrirEstacion() { location.hash = 'estacion'; }

  function estacion() {
    if (est) est.vivo = false;
    const nodoMonedas = barraTienda('Estación del Rift', 1);
    est = { vivo: true, estado: 'espera', t: 0, resultados: null, mostrados: false, amague: false, colorFinal: ACC, colorAmague: ACC,
      luzC: HOT, tapaC: ACC, brilloC: ACC, antes: null, canjes: [] };
    est.monedas = nodoMonedas;
    escenaEstacion(est);
    accionesEstacion(est);
    columnaEstacion(est);
    leyenda('Un boleto, una cápsula. Cada una trae un avatar, un banner, un título, un marco, una skin de arma o un emote.');
    refrescarEstacion();
    requestAnimationFrame(tick);
  }

  function escenaEstacion(s) {
    const e = N(capa, 60, 120, W, H); e.style.background = '#0D0D16'; e.style.borderRadius = '10px'; e.style.overflow = 'hidden';
    const cielo = N(e, 0, 0, W, 360);
    cielo.style.background = 'linear-gradient(rgba(0,0,0,.86),rgba(0,0,0,.68) 36%,rgba(0,0,0,.15) 78%,rgba(0,0,0,0))';
    // Piso en perspectiva, con la línea de seguridad del andén, y los marcos de cámara en las esquinas.
    let juntas = '';
    for (let i = 0; i <= 12; i++) { const x = i * W / 12; juntas += `<line x1="${CX + (x - CX) * 0.35}" y1="541" x2="${CX + (x - CX) * 1.6}" y2="${H}"/>`; }
    const esquinas = [[40, 40], [W - 40, 40], [40, H - 40], [W - 40, H - 40]].map(([x, y]) => {
      const sx = x < CX ? 1 : -1, sy = y < CY ? 1 : -1;
      return `<polyline points="${x},${y + 26 * sy} ${x},${y} ${x + 26 * sx},${y}"/>`;
    }).join('');
    e.insertAdjacentHTML('beforeend', `<svg class="n" style="left:0;top:0" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">` +
      `<rect x="0" y="540" width="${W}" height="${H - 540}" fill="#0D161C"/>` +
      `<rect x="0" y="538" width="${W}" height="3" fill="${css(ACC, sobre(0.7))}"/>` +
      `<g stroke="rgba(255,255,255,${sobre(0.05).toFixed(3)})" stroke-width="2">${juntas}</g>` +
      `<g stroke="rgba(255,255,255,${sobre(0.25, 2).toFixed(3)})" stroke-width="2" fill="none">${esquinas}</g></svg>`);
    s.numero = T(e, 70, 54, 500, 22, 'fm', 16, 'var(--mute)', '');
    s.estadoTexto = T(e, W - 570, 54, 500, 22, 'fm', 16, 'var(--ok)', '', { al: 'der' });
    s.brillo = N(e, 0, 0, W, H);
    s.sombra = N(e, CX - 150, 518, 300, 60); s.sombra.style.borderRadius = '30px'; s.sombra.style.background = 'rgba(0,0,0,.75)';
    s.grieta = lienzo(e);

    // Cápsula.
    const cap = s.capsula = N(e, CX - 115, CY - 170, 230, 340);
    const parte = (x, y, w, h, color, radio) => { const d = N(cap, x, y, w, h); d.style.background = color; d.style.borderRadius = radio + 'px'; return d; };
    parte(0, 0, 230, 340, '#1C1C2A', 16);
    parte(18, 50, 22, 240, `rgba(255,255,255,${sobre(0.08, 28).toFixed(3)})`, 8);
    s.luz = parte(95, 70, 40, 200, css(HOT), 10);
    parte(70, 60, 90, 220, 'rgba(0,0,0,.35)', 12);
    s.tapa = parte(-10, -6, 250, 58, css(ACC), 12);
    s.base = parte(-10, 288, 250, 58, css(ACC), 12);
    T(s.base, 0, 0, 250, 58, 'fm', 18, 'var(--accent-ink)', 'RW · RIFT', { al: 'centro', esp: 20 });
    const icono = el('img', 'n', cap); icono.src = 'img/icono.png'; icono.alt = '';
    Object.assign(icono.style, { left: '71px', top: '126px', width: '88px', height: '88px' });
    const borde = N(cap, 0, 0, 230, 340); borde.style.borderRadius = '12px'; borde.style.boxShadow = `inset 0 0 0 2px rgba(255,255,255,${sobre(0.15, 28).toFixed(3)})`;

    s.chispas = lienzo(e);
    s.datosChispas = Array.from({ length: 22 }, () => [Math.random() * 480 - 240, 30 + Math.random() * 60, Math.random()]); // x, velocidad, fase
    s.destello = N(e, 0, 0, W, H); s.destello.style.background = '#fff'; s.destello.style.opacity = '0';
    s.premio = N(e, 0, 0, W, H); // la carta grande crece desde el centro
    s.premios = N(e, 0, 0, W, H);
    [s.brillo, s.sombra, s.destello, s.premio, s.premios].forEach(n => { n.style.pointerEvents = 'none'; });
  }

  function accionesEstacion(s) {
    s.abrir1 = Boton(capa, 60, 870, 340, 74, 'Abrir 1 · 1 boleto', 30, 'lleno', () => abrir(1));
    s.abrir5 = Boton(capa, 416, 870, 340, 74, 'Abrir 5 · 5 boletos', 30, '', () => abrir(5));
    s.saltar = Boton(capa, 772, 870, 200, 74, 'Saltar', 28, '', saltar);
    s.equipar = Boton(capa, 772, 870, 260, 74, 'Equipar', 30, 'verde', equiparPremio);
    s.leyenda = T(capa, 60, 822, W, 36, 'fx', 24, 'var(--soft)', '', { html: true });
    T(capa, 60, 956, W, 50, 'fx', 18, 'var(--mute)', 'Simulación de negocio: no hay pagos reales. Los boletos se consiguen jugando (créditos) o con ' +
      'cristales de la tienda. Las probabilidades se muestran siempre.', { arriba: true });
  }
  const leyenda = html => { est.leyenda.innerHTML = '<span>' + html + '</span>'; };

  // ---------- Columna derecha ----------
  function columnaEstacion(s) {
    const x = 1270, w = 590;
    const prob = N(capa, x, 120, w, 262, 'panel');
    T(prob, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Probabilidades', { mayus: true, esp: 16 });
    for (let r = 4, fila = 0; r >= 1; r--, fila++) {
      const y = 52 + fila * 36;
      T(prob, 24, y, 140, 30, 'ft', 26, COLOR[r], RAREZA[r], { mayus: true, esp: 3 });
      const pista = N(prob, 170, y + 12, 300, 8); pista.style.background = `rgba(255,255,255,${sobre(0.08).toFixed(3)})`; pista.style.borderRadius = '4px';
      const barra = N(prob, 170, y + 12, 300 * PROB[r] / 55, 8); barra.style.background = COLOR[r]; barra.style.borderRadius = '4px';
      T(prob, 480, y, 86, 30, 'fm', 20, 'var(--ink)', PROB[r] + ' %', { al: 'der' });
    }
    s.garantia = T(prob, 24, 200, 540, 26, 'fx', 19, 'var(--soft)', '', { html: true });
    s.barraGarantia = N(prob, 24, 232, 542, 10);

    const canje = N(capa, x, 398, w, 290, 'panel');
    T(canje, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Conseguir boletos', { mayus: true, esp: 16 });
    filaCanje(canje, 52, 0, `${CREDITOS_POR_BOLETO} créditos`, '1 boleto', () => rift.creditos >= CREDITOS_POR_BOLETO,
      () => canjear(CREDITOS_POR_BOLETO, 0, 1) ? '+1 boleto' : 'Te faltan créditos: se ganan jugando partidas');
    filaCanje(canje, 120, 2, `${CRISTALES_POR_BOLETO} cristales`, '1 boleto', () => rift.cristales >= CRISTALES_POR_BOLETO,
      () => canjear(0, CRISTALES_POR_BOLETO, 1) ? '+1 boleto' : 'Te faltan cristales');
    filaCanje(canje, 188, 2, `${CRISTALES_POR_DIEZ} cristales`, '10 boletos · 10 % menos', () => rift.cristales >= CRISTALES_POR_DIEZ,
      () => canjear(0, CRISTALES_POR_DIEZ, 10) ? '+10 boletos' : 'Te faltan cristales');
    const ir = T(canje, 24, 252, 300, 26, 'fr', 18, 'var(--cristales)', '¿Sin cristales? Ir a la tienda »', { mayus: true, esp: 8 });
    ir.style.width = 'auto'; clic(ir, () => { location.hash = 'cristales'; }, 'Ir a la tienda de cristales');

    const rec = N(capa, x, 704, w, 232, 'panel');
    T(rec, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Últimos premios', { mayus: true, esp: 16 });
    s.recientes = N(rec, 24, 50, 542, 172);
    Boton(capa, x, 952, w, 56, 'Ver contenido de la cápsula', 24, '', verContenido);
  }

  function filaCanje(p, y, moneda, costo, recibe, puede, hacer) {
    const f = N(p, 16, y, 558, 58); f.style.background = `rgba(255,255,255,${sobre(0.03).toFixed(3)})`; f.style.borderRadius = '6px';
    iconoMoneda(p, moneda, 44, y + 29, 22);
    T(p, 66, y + 6, 300, 26, 'ft', 26, 'var(--ink)', costo);
    T(p, 66, y + 32, 300, 20, 'fr', 15, 'var(--mute)', 'por ' + recibe, { mayus: true, esp: 10 });
    const b = Boton(p, 574 - 160 - 10, y + 9, 160, 40, 'Canjear', 22, '', () => { avisar(hacer()); refrescarEstacion(); });
    est.canjes.push([b, puede]);
  }

  // ---------- Reglas (PerfilDatos) ----------
  function canjear(creditos, cristales, boletos) {
    if (rift.creditos < creditos || rift.cristales < cristales) return false;
    rift.creditos -= creditos; rift.cristales -= cristales; rift.boletos += boletos;
    return true;
  }
  // 0 a 99: los primeros números son legendarios, después épicos, raros y el resto comunes.
  function sortear() {
    const tiro = Math.floor(Math.random() * 100);
    let acumulado = 0;
    for (let r = 4; r >= 1; r--) { acumulado += PROB[r]; if (tiro < acumulado) return r; }
    return 1;
  }
  // Dentro de la rareza, primero el tipo (si no, casi todo serían títulos) y después el objeto. Las épicas y legendarias
  // son siempre skins de armas o emotes: los cosméticos del Perfil llegan hasta raro.
  function sortearTipo(r) {
    const hay = t => (r < 3 || t >= 4) && CAT.some(c => c.rareza === r && c.tipo === TIPOS[t]);
    let total = 0;
    TIPOS.forEach((_, t) => { if (hay(t)) total += PROB_TIPO[t]; });
    const tiro = Math.floor(Math.random() * total);
    let acumulado = 0;
    for (let t = 0; t < TIPOS.length; t++) { if (!hay(t)) continue; acumulado += PROB_TIPO[t]; if (tiro < acumulado) return TIPOS[t]; }
    return 'titulo';
  }
  function abrirCapsulas(cantidad) {
    const lista = [];
    for (let i = 0; i < cantidad; i++) {
      rift.boletos--; rift.aperturas++;
      const garantia = rift.sinEpico >= GARANTIA - 1;
      let r = sortear();
      if (garantia && r < 3) r = 3;
      rift.sinEpico = r >= 3 ? 0 : rift.sinEpico + 1;
      const tipo = sortearTipo(r), posibles = CAT.filter(c => c.rareza === r && c.tipo === tipo);
      const premio = posibles[Math.floor(Math.random() * posibles.length)];
      const res = { c: premio, garantia, nuevo: !tiene(premio), creditos: 0 };
      if (res.nuevo) rift.obtenidos.push(clave(premio));
      else { res.creditos = POR_REPETIDO[r]; rift.creditos += res.creditos; }
      rift.recientes.unshift(clave(premio));
      if (rift.recientes.length > 8) rift.recientes.pop();
      lista.push(res);
    }
    return lista;
  }

  // ---------- Acciones ----------
  function abrir(cantidad) {
    const s = est;
    if (s.estado === 'abriendo') return;
    if (rift.boletos < cantidad) { avisar(cantidad === 1 ? 'No tenés boletos: canjeá créditos o cristales' : `Te faltan boletos para abrir ${cantidad}`); return; }
    s.resultados = abrirCapsulas(cantidad);
    const mejor = Math.max(...s.resultados.map(r => r.c.rareza));
    s.colorFinal = rgb(COLOR[mejor]);
    s.amague = mejor >= 3;
    s.colorAmague = rgb(COLOR[2]);
    s.premio.innerHTML = ''; s.premios.innerHTML = ''; s.mostrados = false;
    s.estado = 'abriendo'; s.t = 0;
    s.capsula.style.display = '';
    leyenda('Recibiendo la carga…');
    refrescarEstacion();
  }
  function saltar() { if (est.estado === 'abriendo') est.t = FIN; }
  function equiparPremio() {
    const r = est.resultados;
    if (!r || r.length !== 1) return;
    const c = r[0].c;
    equipar(c);
    avisar(`<span style="color:${COLOR[c.rareza]}">${c.nombre}</span> equipado`);
    refrescarEstacion();
  }

  // ---------- Animación ----------
  function tick(ahora) {
    const s = est;
    if (!s || !s.vivo) return;
    const now = ahora / 1000, dt = s.antes == null ? 0 : Math.min(0.25, now - s.antes);
    s.antes = now;
    if (s.estado === 'abriendo') {
      s.t += dt;
      if (s.t >= REVELAR && !s.mostrados) mostrarPremios();
      if (s.t >= FIN) terminar();
    }
    const abriendo = s.estado === 'abriendo';
    const temblor = abriendo ? clamp01(s.t / REVELAR) : 0;
    const bob = Math.sin(now * 1.6) * 7, fuerza = 9 * temblor * temblor;
    s.capsula.style.transform = `translate(${Math.sin(now * 70) * fuerza}px,${-(bob + Math.cos(now * 53) * fuerza)}px) scale(${1 + 0.05 * temblor})`;
    s.sombra.style.transform = `scaleX(${1 - bob / 60})`;

    // Color: naranja en espera; al abrir, la rareza (si es épica o mejor, primero amaga con azul).
    let objetivo = ACC;
    if (abriendo) objetivo = s.amague && s.t < 0.75 ? s.colorAmague : s.colorFinal;
    else if (s.estado === 'revelado') objetivo = s.colorFinal;
    s.luzC = mezclar(s.luzC, objetivo, dt * 8);
    s.luz.style.background = css(s.luzC);
    s.tapaC = s.estado === 'espera' ? ACC : mezclar(s.tapaC, objetivo, dt * 6);
    s.tapa.style.background = s.base.style.background = css(s.tapaC);
    const pulso = s.estado === 'espera' ? 0.22 + 0.06 * Math.sin(now * 2) : 0.3 + 0.4 * temblor;
    s.brilloC = mezclar(s.brilloC, objetivo, dt * 6);
    s.brillo.style.background = resplandor(s.brilloC, pulso, 0.25, 300 + 60 * temblor, CX, CY);

    s.grieta.clearRect(0, 0, W, H);
    if (abriendo) grieta(s.grieta, Math.max(0, (s.t - 0.85) * 1.7));
    const flash = abriendo ? clamp01(1 - Math.abs(s.t - REVELAR) / 0.3) : 0;
    s.destello.style.opacity = sobre(0.85 * flash).toFixed(3);

    const ch = s.chispas, velocidad = 1 + 4 * temblor;
    ch.clearRect(0, 0, W, H);
    ch.fillStyle = css(mezclar(HOT, objetivo, 0.6));
    for (const [x, v, z] of s.datosChispas) {
      const fase = (z + now * v * velocidad / 400) % 1;
      ch.globalAlpha = sobre(Math.sin(fase * Math.PI) * 0.8);
      ch.fillRect(CX + x * (0.6 + fase * 0.5), CY + 170 - fase * 380, 4, 4);
    }
    ch.globalAlpha = 1;

    // Premio: entra creciendo.
    if (s.mostrados) {
      const k = s.estado === 'revelado' ? 1 : clamp01((s.t - REVELAR) / 0.35);
      s.premio.style.transform = `scale(${0.7 + 0.3 * (1 - Math.pow(1 - k, 3))})`;
      [...s.premios.children].forEach((carta, i) => {
        const ki = s.estado === 'revelado' ? 1 : clamp01((s.t - REVELAR - i * 0.08) / 0.3);
        carta.style.transform = `scale(${0.6 + 0.4 * (1 - Math.pow(1 - ki, 3))})`;
      });
    }
    requestAnimationFrame(tick);
  }

  // RiftwalkerRiftGraphic: dos grietas que se abren hacia los costados y chispas que salen del centro.
  function grieta(g, tiempo) {
    const suave = v => { v = clamp01(v); return v * v * (3 - 2 * v); }, entre = (a, b, v) => clamp01((v - a) / (b - a));
    const crece = suave(entre(0.3, 1.05, tiempo)), abre = suave(entre(1.05, 2.35, tiempo)), alfa = crece * (1 - entre(1.8, 2.65, tiempo));
    if (alfa <= 0) return;
    const tramo = (a, b, ancho, color, al) => {
      g.globalAlpha = sobre(al); g.strokeStyle = color; g.lineWidth = ancho;
      g.beginPath(); g.moveTo(CX + a[0], CY - a[1]); g.lineTo(CX + b[0], CY - b[1]); g.stroke();
    };
    const medio = 195 * crece;
    for (const lado of [-1, 1]) {
      let antes = [lado * 615 * abre, -medio];
      for (let i = 1; i <= 16; i++) {
        const jitter = Math.sin(i * 7.31) * 13 + Math.sin(i * 2.17) * 7;
        const sig = [lado * (615 * abre + jitter * Math.sin(i * Math.PI / 16)), -medio + 2 * medio * i / 16];
        tramo(antes, sig, 25, 'rgb(255,56,4)', alfa * 0.06);
        tramo(antes, sig, 10, 'rgb(255,99,13)', alfa * 0.18);
        tramo(antes, sig, 3, 'rgb(255,158,51)', alfa);
        tramo(antes, sig, 1, 'rgb(255,237,184)', alfa);
        antes = sig;
      }
    }
    const estallido = clamp01((tiempo - 0.85) / 1.4);
    for (let i = 0; i < 28; i++) {
      const ang = i * 2.39996, d = [Math.cos(ang), Math.sin(ang) * 0.5], r = 35 + estallido * (180 + (i % 5) * 43), l = 4 + 12 * estallido;
      tramo([d[0] * r, d[1] * r], [d[0] * (r + l), d[1] * (r + l)], 1.5, 'rgb(255,143,36)', alfa * Math.sin(estallido * Math.PI));
    }
    g.globalAlpha = 1;
  }

  function mostrarPremios() {
    const s = est;
    s.mostrados = true;
    s.capsula.style.display = 'none';
    if (s.resultados.length === 1) cartaGrande(s.premio, s.resultados[0]);
    else {
      const w = 196, gap = 18, n = s.resultados.length, x0 = (W - (n * w + (n - 1) * gap)) / 2;
      s.resultados.forEach((r, i) => cartaChica(s.premios, r, x0 + i * (w + gap), 170, w));
    }
  }

  function terminar() {
    const s = est;
    if (!s.mostrados) mostrarPremios();
    s.estado = 'revelado'; s.t = FIN;
    const nuevos = s.resultados.filter(r => r.nuevo).length, creditos = s.resultados.reduce((a, r) => a + r.creditos, 0);
    if (s.resultados.length === 1) {
      const r = s.resultados[0];
      leyenda(r.nuevo ? `¡Nuevo! <span style="color:${COLOR[r.c.rareza]}">${r.c.nombre}</span> ya está en tu colección.`
        : `Repetido: se convirtió en <span style="color:#D6DAE0">${r.creditos} créditos</span>.`);
    } else leyenda(`${nuevos} ${nuevos === 1 ? 'nuevo' : 'nuevos'}` + (creditos > 0 ? ` · repetidos convertidos en ${creditos} créditos` : ''));
    refrescarEstacion();
  }

  // ---------- Cartas del premio ----------
  // Achica la letra hasta que entra (como el auto size de TextMeshPro): antes que cortar una palabra larga.
  function ajustar(t, max, min) {
    for (let tam = max; tam >= min; tam--) {
      t.style.fontSize = tam + 'px';
      if (t.scrollWidth <= t.clientWidth + 1 && t.scrollHeight <= t.clientHeight + 1) return;
    }
  }
  function cartaGrande(padre, r) {
    const c = r.c, color = COLOR[c.rareza];
    N(padre, 0, 0, W, H).style.background = resplandor(rgb(color), 0.45, 0.3, 320, CX, CY);
    const k = N(padre, CX - 190, CY - 250, 380, 500); k.style.background = color; k.style.borderRadius = '12px';
    const fondo = N(k, 4, 4, 372, 492); fondo.style.background = '#0E1117'; fondo.style.borderRadius = '10px';
    T(k, 0, 18, 380, 26, 'fr', 22, color, RAREZA[c.rareza] + (r.garantia ? ' · garantía' : ''), { al: 'centro', mayus: true, esp: 16 });
    arte(k, c, 40, 56, 300);
    ajustar(T(k, 20, 372, 340, 46, 'ft', 40, 'var(--ink)', c.nombre, { al: 'centro', mayus: true, esp: 3 }), 40, 24);
    T(k, 20, 418, 340, 22, 'fr', 16, 'var(--mute)', TIPO[c.tipo] + (c.estilo && c.tipo !== 'skin' ? ' · ' + c.estilo : ''), { al: 'centro', mayus: true, esp: 14 });
    etiqueta(k, r, 190, 456);
  }
  function cartaChica(padre, r, x, y, w) {
    const c = r.c, color = COLOR[c.rareza];
    const k = N(padre, x, y, w, 320); k.style.background = color; k.style.borderRadius = '10px'; k.style.transform = 'scale(.6)';
    const fondo = N(k, 3, 3, w - 6, 314); fondo.style.background = '#0E1117'; fondo.style.borderRadius = '8px';
    T(k, 0, 12, w, 20, 'fr', 15, color, RAREZA[c.rareza], { al: 'centro', mayus: true, esp: 12 });
    arte(k, c, 14, 38, w - 28);
    T(k, 10, 216, w - 20, 52, 'fr', 20, 'var(--ink)', c.nombre, { al: 'centro' }).style.whiteSpace = 'normal';
    etiqueta(k, r, w / 2, 284);
  }
  const arte = (k, c, x, y, lado) => vitrina(k, c, x, y, lado, lado);
  // El dibujo de cualquier premio en un recuadro: avatares, banners, marcos, títulos, skins, emotes y cristales.
  function vitrina(k, c, x, y, w, h) {
    const f = N(k, x, y, w, h), l = Math.min(w, h); f.style.background = '#151922'; f.style.borderRadius = '6px'; f.style.overflow = 'hidden';
    if (c.tipo === 'avatar') Img(f, srcAvatar(c.id), (w - l) / 2, (h - l) / 2, l, l);
    else if (c.tipo === 'banner') Img(f, srcBanner(c.id), 0, (h - w / 3) / 2, w, w / 3);
    else if (c.tipo === 'marco') bannerEnmarcado(N(f, 0, (h - w / 3) / 2, w, w / 3), w, w / 3, estado.banner, c.id, true); // sobre tu banner
    else if (c.tipo === 'skin') {
      f.style.background = `radial-gradient(ellipse at 50% 50%,${COLOR[c.rareza]}38,#151922 72%)`;
      Contener(f, srcSkin(c), w * 0.07, h * 0.12, w * 0.86, h * 0.76);
    } else if (c.tipo === 'emote') { const d = l * 0.78; Contener(f, srcEmote(c.id), (w - d) / 2, (h - d) / 2, d, d); }
    else if (c.tipo === 'cristales') {
      iconoMoneda(f, 2, w / 2, h * 0.42, l * 0.36);
      T(f, 0, h * 0.64, w, h * 0.22, 'ft', l * 0.17, 'var(--cristales)', '+' + c.cantidad.toLocaleString('es-AR'), { al: 'centro' });
    } else {
      const t = T(f, 12, 0, w - 24, h, 'ft', l / 6, COLOR[c.rareza], c.nombre, { al: 'centro', mayus: true, esp: 2 });
      t.style.whiteSpace = 'normal';
      ajustar(t, l / 6, l / 12);
    }
    return f;
  }
  function etiqueta(k, r, cx, y) {
    const w = r.nuevo ? 110 : 170, chip = N(k, cx - w / 2, y, w, 30);
    chip.style.borderRadius = '6px'; chip.style.background = r.nuevo ? 'var(--accent)' : `rgba(255,255,255,${sobre(0.12, 14).toFixed(3)})`;
    T(chip, 0, 0, w, 30, 'fr', 17, r.nuevo ? 'var(--accent-ink)' : 'var(--soft)', r.nuevo ? 'Nuevo' : `+${r.creditos} créditos`, { al: 'centro', mayus: true, esp: 14 });
  }

  // ---------- Datos a la vista ----------
  function refrescarEstacion() {
    const s = est;
    if (!s || !s.vivo) return;
    const abriendo = s.estado === 'abriendo', r = s.resultados;
    s.abrir1.disabled = abriendo || rift.boletos < 1;
    s.abrir5.disabled = abriendo || rift.boletos < 5;
    s.abrir1.textContent = s.estado === 'revelado' ? 'Abrir otra · 1 boleto' : 'Abrir 1 · 1 boleto';
    s.saltar.style.display = abriendo ? '' : 'none';
    s.equipar.style.display = s.estado === 'revelado' && r && r.length === 1 && !estaEquipado(r[0].c) ? '' : 'none';
    s.canjes.forEach(([b, puede]) => { b.disabled = !puede(); });

    s.numero.textContent = 'CÁPSULA DEL RIFT · Nº ' + String(rift.aperturas + (abriendo ? 0 : 1)).padStart(3, '0');
    s.estadoTexto.textContent = abriendo ? '• ABRIENDO' : s.estado === 'revelado' ? '• ENTREGADO' : '• EN ESPERA';
    s.estadoTexto.style.color = abriendo ? 'var(--accent)' : 'var(--ok)';

    const faltan = GARANTIA - rift.sinEpico;
    s.garantia.innerHTML = '<span>' + (faltan <= 1 ? `¡La próxima trae <span style="color:${COLOR[3]}">épico o mejor</span> asegurado!`
      : `Épico o mejor asegurado en <span style="color:var(--ink)">${faltan} cápsulas</span>`) + '</span>';
    s.barraGarantia.innerHTML = '';
    const paso = 542 / GARANTIA;
    for (let i = 0; i < GARANTIA; i++) {
      const d = N(s.barraGarantia, i * paso, 0, paso - 4, 10); d.style.borderRadius = '3px';
      d.style.background = i < GARANTIA - faltan ? COLOR[3] : `rgba(255,255,255,${sobre(0.1).toFixed(3)})`;
    }

    s.recientes.innerHTML = '';
    let fila = 0;
    for (const k of rift.recientes) {
      if (fila >= 5) break;
      const c = desdeClave(k);
      if (!c) continue;
      const punto = N(s.recientes, 0, fila * 34 + 10, 10, 10); punto.style.borderRadius = '5px'; punto.style.background = COLOR[c.rareza];
      T(s.recientes, 22, fila * 34, 520, 30, 'fx', 20, 'var(--ink)', `${c.nombre}  <span style="color:var(--mute)">· ${TIPO[c.tipo]}</span>`, { html: true, puntos: true });
      fila++;
    }
    if (!fila) T(s.recientes, 0, 0, 520, 30, 'fx', 20, 'var(--mute)', 'Todavía no abriste ninguna cápsula.');
    s.monedas.innerHTML = '';
    monedas(s.monedas, 760, 34);
  }

  // ---------- Contenido de la cápsula ----------
  function verContenido() {
    const m = el('div', 'modal', E), caja = el('div', 'caja', m);
    caja.style.width = '1160px'; caja.style.height = '860px';
    T(caja, 48, 36, 900, 22, 'fr', 18, 'var(--accent)', 'Estación del Rift', { mayus: true, esp: 16 });
    T(caja, 46, 60, 900, 54, 'ft', 48, 'var(--ink)', 'Contenido de la cápsula', { mayus: true, esp: 3 });
    T(caja, 48, 118, 1064, 74, 'fx', 19, 'var(--mute)', `Primero sale la rareza (con estas probabilidades). Si es común o rara, después sale el tipo (${PROB_TIPO[0]} % avatar, ` +
      `${PROB_TIPO[1]} % banner, ${PROB_TIPO[2]} % título, ${PROB_TIPO[3]} % marco, ${PROB_TIPO[4]} % skin de arma, ${PROB_TIPO[5]} % emote); las épicas y ` +
      `legendarias son skins de armas o emotes. Por último, un objeto al azar de los de las cápsulas. Cada ${GARANTIA} cápsulas hay uno épico o ` +
      'mejor asegurado. Los repetidos se convierten en créditos.', { arriba: true });
    const lista = N(caja, 48, 200, 1064, 556, 'grilla'), interno = N(lista, 0, 0, 1064, 556);
    let y = 0;
    for (let r = 4; r >= 1; r--) {
      const items = CAT.filter(c => c.rareza === r);
      T(interno, 0, y, 1064, 36, 'ft', 30, COLOR[r], `${RAREZA[r]}  <span style="color:#8E96A3;font-size:20px">${PROB[r]} % · ${items.length} objetos</span>`,
        { mayus: true, esp: 3, html: true });
      y += 44;
      items.forEach((c, i) => {
        if (i > 0 && i % 3 === 0) y += 34;
        const suyo = tiene(c);
        T(interno, (i % 3) * 356, y, 346, 30, 'fx', 20, suyo ? 'var(--ink)' : 'var(--soft)',
          `${c.nombre}  <span style="color:#8E96A3">${TIPO[c.tipo]}</span>${suyo ? '  <span style="color:#3DDC97">· tenés</span>' : ''}`,
          { html: true, puntos: true });
      });
      y += 50;
    }
    interno.style.height = Math.max(556, y) + 'px';
    Boton(caja, 1160 - 48 - 200, 860 - 40 - 60 + 8, 200, 56, 'Cerrar', 24, '', () => m.remove());
  }

  // ================= Tienda (US 385): tienda directa, cápsulas, pase de batalla y cristales =================
  // La barra de arriba y las cuatro pestañas son las de PestanasDeTienda. Devuelve el lugar de las monedas.
  const PESTANAS_TIENDA = [['tienda', 'Tienda'], ['estacion', 'Cápsulas'], ['pase', 'Pase de batalla'], ['cristales', 'Cristales']];
  function barraTienda(rotulo, activa) {
    pararFondos();
    capa.innerHTML = ''; capa.style.background = '#07090D';
    const volver = clic(N(capa, 60, 34, 150, 52, 'panel borde-hover'), volverAlMenu, 'Volver al menú');
    T(volver, 0, 0, 150, 52, 'ft', 26, 'var(--ink)', '« Menú', { mayus: true, esp: 6, al: 'centro' });
    T(capa, 240, 34, 200, 20, 'fr', 17, 'var(--accent)', rotulo, { mayus: true, esp: 16 });
    T(capa, 238, 52, 200, 46, 'ft', 44, 'var(--ink)', 'Tienda', { mayus: true, esp: 3 });
    PESTANAS_TIENDA.forEach(([hash, nombre], i) => {
      const on = i === activa, t = N(capa, 450 + i * 186, 40, 180, 52, 'pestana-tienda' + (on ? ' activa' : ''));
      T(t, 0, 0, 180, 52, 'ft', 24, on ? 'var(--ink)' : 'var(--mute)', nombre, { mayus: true, esp: 4, al: 'centro' });
      if (on) { const l = N(t, 0, 49, 180, 3); l.style.background = 'var(--accent)'; }
      else clic(t, () => { location.hash = hash; }, nombre);
      if (hash === 'pase' && paraReclamar() > 0) { const d = N(t, 164, -6, 16, 16); d.style.borderRadius = '8px'; d.style.background = 'var(--accent)'; }
    });
    const nodo = N(capa, 1100, 0, 760, 120);
    monedas(nodo, 760, 34);
    return nodo;
  }
  const precioEnPesos = n => '$ ' + n.toLocaleString('es-AR');
  function precioCristales(padre, x, y, h, n, tam, color) {
    iconoMoneda(padre, 2, x + tam * 0.4, y + h / 2, tam * 0.75);
    return T(padre, x + tam * 0.95, y, 200, h, 'ft', tam, color || 'var(--ink)', n.toLocaleString('es-AR'));
  }
  let relojTienda = null; // la cuenta regresiva de las ofertas

  // ---------- Tienda directa: 8 ofertas del día, sin azar ----------
  const PRECIOS = [0, 400, 800, 1600, 3200]; // por rareza, en cristales
  const hoyEnArgentina = () => new Date(Date.now() - 3 * 3600e3);
  const diaDeHoy = () => { const d = hoyEnArgentina(); return d.getUTCFullYear() * 10000 + (d.getUTCMonth() + 1) * 100 + d.getUTCDate(); };
  function azar(semilla) { let a = semilla >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t ^= t + Math.imul(t ^ (t >>> 7), 61 | t); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
  // Como OfertasDe del juego: una épica o legendaria, que haya una skin y un emote, una de cada tipo del Perfil y el
  // resto cosméticos, de la más rara a la más común. Cambian todos los días a las 00:00 de Argentina.
  function ofertasDe(dia) {
    const todos = TODO.filter(c => origen(c) === 'tienda'), rng = azar(dia * 31 + 7), lista = [];
    const tomar = filtro => { const p = todos.filter(c => !lista.includes(c) && filtro(c)); if (p.length) lista.push(p[Math.floor(rng() * p.length)]); };
    tomar(c => c.rareza >= 3);
    if (!lista.some(c => c.tipo === 'skin')) tomar(c => c.tipo === 'skin');
    if (!lista.some(c => c.tipo === 'emote')) tomar(c => c.tipo === 'emote');
    ['avatar', 'banner', 'marco', 'titulo'].forEach(t => tomar(c => c.tipo === t));
    while (lista.length < 8 && lista.length < todos.length) tomar(c => LISTA[c.tipo]);
    return lista.sort((a, b) => b.rareza - a.rareza);
  }
  let ofertaElegida = null;
  function tienda() {
    barraTienda('Sin azar', 0);
    T(capa, 60, 112, 900, 48, 'ft', 40, 'var(--ink)', 'Ofertas del día', { mayus: true, esp: 3 });
    T(capa, 60, 156, 1000, 28, 'fx', 20, 'var(--mute)', 'El cosmético que elijas, sin azar. Compras simuladas: no se cobra dinero real.');
    const cuenta = T(capa, 960, 124, 400, 30, 'fm', 18, 'var(--accent)', '', { al: 'der' });
    const reloj = () => { const d = hoyEnArgentina(), resto = 86400 - (d.getUTCHours() * 3600 + d.getUTCMinutes() * 60 + d.getUTCSeconds());
      cuenta.textContent = `Nuevas ofertas en ${Math.floor(resto / 3600)} h ${String(Math.floor(resto / 60) % 60).padStart(2, '0')} min`; };
    reloj(); relojTienda = setInterval(reloj, 20000);
    const ofertas = ofertasDe(diaDeHoy());
    if (!ofertas.includes(ofertaElegida)) ofertaElegida = ofertas[0];
    const w = (1300 - 3 * 20) / 4, h = 360;
    ofertas.forEach((c, i) => {
      const x = 60 + (i % 4) * (w + 20), y = 196 + Math.floor(i / 4) * (h + 20), color = COLOR[c.rareza];
      const k = N(capa, x, y, w, h, 'carta' + (c === ofertaElegida ? ' elegida' : ''));
      vitrina(k, c, 0, 0, w, 200).style.borderRadius = '0';
      const r = N(k, 0, 200, w, 4); r.style.background = color;
      T(k, 18, 218, w - 36, 36, 'ft', 28, 'var(--ink)', c.nombre, { mayus: true, esp: 2, puntos: true });
      T(k, 18, 256, w - 36, 22, 'fr', 16, color, `${TIPO[c.tipo]} · ${RAREZA[c.rareza]}`, { mayus: true, esp: 12 });
      if (tiene(c)) T(k, 18, 300, w - 36, 40, 'ft', 26, 'var(--ok)', 'Obtenido', { mayus: true, esp: 6 });
      else precioCristales(k, 18, 300, 40, PRECIOS[c.rareza], 30);
      el('div', 'linea-borde', k);
      clic(k, () => { ofertaElegida = c; mostrar(); }, c.nombre);
    });
    detalleOferta(N(capa, 1390, 196, 470, 2 * h + 20, 'panel'), ofertaElegida);
  }
  function detalleOferta(v, c) {
    const color = COLOR[c.rareza], cuadrada = c.tipo === 'avatar' || c.tipo === 'emote', alto = cuadrada ? 422 : 230;
    T(v, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Oferta', { mayus: true, esp: 16 });
    vitrina(v, c, 24, 52, 422, alto);
    let y = 52 + alto + 18;
    T(v, 24, y, 422, 22, 'fr', 17, color, `${TIPO[c.tipo]} · ${RAREZA[c.rareza]}`, { mayus: true, esp: 12 });
    const n = T(v, 24, y + 26, 422, cuadrada ? 50 : 96, 'ft', 38, 'var(--ink)', c.nombre, { mayus: true, esp: 3, arriba: true });
    if (!cuadrada) ajustar(n, 38, 26);
    T(v, 24, 740 - 150, 422, 50, 'fx', 18, 'var(--mute)', c.tipo === 'skin' ? 'Se equipa en Perfil › Personalización › Armas.'
      : c.tipo === 'emote' ? 'Se pone en la rueda en Perfil › Emotes.' : 'Se equipa en Perfil › Personalización.', { arriba: true });
    const precio = PRECIOS[c.rareza], faltan = precio - rift.cristales;
    if (tiene(c)) Boton(v, 24, 740 - 84, 422, 60, 'Obtenido', 26, 'ok', null);
    else if (faltan > 0) Boton(v, 24, 740 - 84, 422, 60, `Te faltan ${faltan.toLocaleString('es-AR')} cristales`, 24, '', () => { location.hash = 'cristales'; });
    else Boton(v, 24, 740 - 84, 422, 60, `Comprar · ${precio.toLocaleString('es-AR')} cristales`, 26, 'lleno', () => {
      rift.cristales -= precio; rift.obtenidos.push(clave(c));
      avisar(`<span style="color:${color}">${c.nombre}</span> ya está en tu colección`);
      mostrar();
    });
  }

  // ---------- Pase de batalla (F32): 40 niveles, gratis cada 5 y el resto con el pase ----------
  const XP_POR_NIVEL = 1000, PRECIO_PASE = 950, INICIO_TEMPORADA = Date.UTC(2026, 10, 9, 3), DIAS_TEMPORADA = 56;
  const pase = { nivel: 12, xp: 450, comprado: false, reclamados: [1, 6], pagina: 1, vista: 0, elegido: 12, progreso: X.desafios.map((d, i) => (i * 7) % 5) };
  const gratis = n => (n - 1) % 10 === 0 || (n - 1) % 10 === 5;
  const estadoNivel = n => pase.reclamados.includes(n) ? 'reclamada' : n > pase.nivel ? 'bloqueada' : !gratis(n) && !pase.comprado ? 'sinPase' : 'lista';
  function paraReclamar() { let n = 0; for (let i = 1; i <= 40; i++) if (estadoNivel(i) === 'lista') n++; return n; }
  function reclamar(n) {
    const p = PASE[n - 1];
    pase.reclamados.push(n);
    if (p.tipo === 'cristales') rift.cristales += p.cantidad;
    else if (!rift.obtenidos.includes(clave(p))) rift.obtenidos.push(clave(p));
    return p;
  }
  function pantallaPase() {
    barraTienda('Temporada', 2);
    const cab = N(capa, 60, 120, 1800, 120, 'panel');
    const faltan = Math.max(0, Math.ceil((INICIO_TEMPORADA + DIAS_TEMPORADA * 86400e3 - Date.now()) / 86400e3));
    T(cab, 28, 16, 480, 50, 'ft', 44, 'var(--ink)', 'Temporada 1', { mayus: true, esp: 3 });
    T(cab, 28, 70, 520, 30, 'fx', 19, 'var(--mute)', `Faltan ${faltan} días · 40 niveles · 100 cristales gratis, 550 con el pase`);
    T(cab, 560, 14, 300, 46, 'ft', 40, 'var(--accent)', `Nivel ${pase.nivel}`, { mayus: true, esp: 3 });
    T(cab, 880, 22, 300, 30, 'fm', 18, 'var(--soft)', `${pase.xp.toLocaleString('es-AR')} / ${XP_POR_NIVEL.toLocaleString('es-AR')}`, { al: 'der' });
    const pista = N(cab, 560, 66, 620, 12); pista.style.background = 'rgba(255,255,255,.08)'; pista.style.borderRadius = '6px';
    const relleno = N(cab, 560, 66, 620 * pase.xp / XP_POR_NIVEL, 12); relleno.style.background = 'var(--accent)'; relleno.style.borderRadius = '6px';
    T(cab, 560, 84, 620, 24, 'fx', 16, 'var(--mute)', 'Se gana XP jugando partidas y cumpliendo desafíos');
    const n = paraReclamar();
    Boton(cab, 1210, 32, 250, 56, n ? `Reclamar todo (${n})` : 'Reclamar todo', 24, n ? 'verde' : '', n ? () => {
      const lista = []; for (let i = 1; i <= 40; i++) if (estadoNivel(i) === 'lista') lista.push(reclamar(i).nombre);
      avisar(`Reclamado: ${lista.join(', ')}`); mostrar();
    } : null);
    if (pase.comprado) Boton(cab, 1480, 32, 300, 56, 'Pase activo', 24, 'ok', null);
    else Boton(cab, 1480, 32, 300, 56, `Comprar pase · ${PRECIO_PASE}`, 24, 'lleno', () => {
      if (rift.cristales < PRECIO_PASE) { avisar(`Te faltan ${PRECIO_PASE - rift.cristales} cristales para el pase`); location.hash = 'cristales'; return; }
      rift.cristales -= PRECIO_PASE; pase.comprado = true; avisar('¡Pase de batalla activo! Ya podés reclamar los premios del pase'); mostrar();
    });

    ['Recompensas', 'Desafíos'].forEach((nombre, i) => {
      const on = pase.vista === i, t = T(capa, 60 + i * 230, 252, 220, 44, 'ft pestana' + (on ? ' activa' : ''), 30, on ? 'var(--ink)' : 'var(--mute)', nombre, { mayus: true, esp: 5 });
      t.style.width = 'auto';
      if (on) { const l = N(capa, 60 + i * 230, 296, t.offsetWidth, 3); l.style.background = 'var(--accent)'; }
      else clic(t, () => { pase.vista = i; mostrar(); }, nombre);
    });
    if (pase.vista === 1) { desafios(); return; }
    const p = pase.pagina, a = p * 10 + 1;
    T(capa, 700, 252, 530, 44, 'fr', 18, 'var(--mute)', `Página ${p + 1} / 4 · Niveles ${a}–${a + 9}`, { mayus: true, esp: 12, al: 'der' });
    Boton(capa, 1250, 252, 56, 44, '‹', 30, '', p > 0 ? () => { pase.pagina--; mostrar(); } : null);
    Boton(capa, 1316, 252, 56, 44, '›', 30, '', p < 3 ? () => { pase.pagina++; mostrar(); } : null);
    const w = (1310 - 4 * 16) / 5, h = 300;
    for (let i = 0; i < 10; i++) {
      const nivel = a + i, c = PASE[nivel - 1], ne = estadoNivel(nivel), color = COLOR[c.rareza];
      const k = N(capa, 60 + (i % 5) * (w + 16), 312 + Math.floor(i / 5) * (h + 16), w, h, 'carta' + (nivel === pase.elegido ? ' elegida' : '') + (ne === 'lista' ? ' lista' : ''));
      vitrina(k, c, 0, 0, w, 156).style.borderRadius = '0';
      if (ne === 'bloqueada' || ne === 'sinPase') { const o = N(k, 0, 0, w, 156); o.style.background = 'rgba(7,9,13,.55)'; candado(k, w - 34, 120); }
      const chipN = N(k, 10, 10, 46, 28); chipN.style.background = 'rgba(7,9,13,.85)'; chipN.style.borderRadius = '6px';
      T(chipN, 0, 0, 46, 28, 'fm', 16, 'var(--ink)', String(nivel), { al: 'centro' });
      const g = gratis(nivel), chipT = N(k, w - 76, 10, 66, 26); chipT.style.borderRadius = '6px';
      chipT.style.background = g ? 'rgba(61,220,151,.18)' : 'rgba(242,154,56,.18)';
      T(chipT, 0, 0, 66, 26, 'fr', 14, g ? 'var(--ok)' : 'var(--accent)', g ? 'Gratis' : 'Pase', { al: 'centro', mayus: true, esp: 10 });
      const r = N(k, 0, 156, w, 4); r.style.background = color;
      T(k, 14, 168, w - 28, 50, 'ft', 22, 'var(--ink)', c.nombre, { mayus: true, esp: 2, arriba: true }).style.lineHeight = '1.05';
      T(k, 14, 222, w - 28, 20, 'fr', 14, 'var(--mute)', TIPO[c.tipo], { mayus: true, esp: 12 });
      const textos = { reclamada: ['Reclamada', 'var(--ok)'], lista: ['Reclamar', 'var(--accent)'], sinPase: ['Con el pase', 'var(--accent)'], bloqueada: [`Nivel ${nivel}`, 'var(--mute)'] };
      const [txt, col] = textos[ne];
      if (ne === 'lista') { const b = N(k, 14, 252, w - 28, 36); b.style.background = 'var(--accent)'; b.style.borderRadius = '6px'; T(b, 0, 0, w - 28, 36, 'ft', 20, 'var(--accent-ink)', txt, { al: 'centro', mayus: true, esp: 6 }); }
      else T(k, 14, 252, w - 28, 36, 'ft', 20, col, txt, { mayus: true, esp: 6 });
      el('div', 'linea-borde', k);
      clic(k, () => {
        pase.elegido = nivel;
        if (estadoNivel(nivel) === 'lista') { const pr = reclamar(nivel); avisar(`<span style="color:${COLOR[pr.rareza]}">${pr.nombre}</span> reclamado`); }
        mostrar();
      }, `Nivel ${nivel}: ${c.nombre}`);
    }
    detallePase(N(capa, 1390, 252, 470, 676, 'panel'), pase.elegido);
  }
  function candado(padre, x, y) {
    const arco = N(padre, x + 4, y - 14, 16, 16); arco.style.border = '3px solid var(--soft)'; arco.style.borderBottom = '0'; arco.style.borderRadius = '8px 8px 0 0';
    const cuerpo = N(padre, x, y, 24, 18); cuerpo.style.background = 'var(--soft)'; cuerpo.style.borderRadius = '3px';
  }
  function detallePase(v, nivel) {
    const c = PASE[nivel - 1], color = COLOR[c.rareza], est = estadoNivel(nivel), cuadrada = c.tipo === 'avatar' || c.tipo === 'emote', alto = cuadrada ? 360 : 230;
    T(v, 24, 18, 422, 22, 'fr', 17, 'var(--accent)', `Nivel ${nivel} · ${gratis(nivel) ? 'Gratis' : 'Pase de batalla'}`, { mayus: true, esp: 14 });
    vitrina(v, c, 24, 52, 422, alto);
    const y = 52 + alto + 18;
    T(v, 24, y, 422, 22, 'fr', 17, color, `${TIPO[c.tipo]} · ${RAREZA[c.rareza]}`, { mayus: true, esp: 12 });
    ajustar(T(v, 24, y + 26, 422, 96, 'ft', 38, 'var(--ink)', c.nombre, { mayus: true, esp: 3, arriba: true }), 38, 26);
    const ayuda = { reclamada: c.tipo === 'cristales' ? 'Ya se sumaron a tus cristales.' : 'Ya está en tu colección del Perfil.', lista: 'Hacé clic en la carta para reclamarla.',
      sinPase: 'Se desbloquea con el pase de batalla.', bloqueada: `Se desbloquea al llegar al nivel ${nivel}.` }[est];
    T(v, 24, 676 - 70, 422, 50, 'fx', 19, est === 'lista' ? 'var(--accent)' : 'var(--mute)', ayuda, { arriba: true });
  }
  function desafios() {
    const w = (1800 - 20) / 2;
    X.desafios.forEach((d, i) => {
      const x = 60 + (i % 2) * (w + 20), y = 312 + Math.floor(i / 2) * 62;
      const etapas = d.metas.length, hecho = Math.min(pase.progreso[i], d.metas[etapas - 1]);
      let etapa = d.metas.findIndex(m => hecho < m); if (etapa < 0) etapa = etapas - 1;
      const meta = d.metas[etapa], completo = hecho >= d.metas[etapas - 1];
      const f = N(capa, x, y, w, 56, 'panel');
      T(f, 20, 4, w - 400, 30, 'ft', 24, completo ? 'var(--ok)' : 'var(--ink)', d.nombre.replace('{0}', meta), { esp: 1, puntos: true });
      T(f, 20, 32, w - 400, 20, 'fr', 14, 'var(--mute)', d.modo + (etapas > 1 ? ` · Etapa ${etapa + 1} / ${etapas}` : ''), { mayus: true, esp: 10 });
      const pista = N(f, w - 400, 24, 200, 8); pista.style.background = 'rgba(255,255,255,.08)'; pista.style.borderRadius = '4px';
      const rel = N(f, w - 400, 24, 200 * Math.min(1, hecho / meta), 8); rel.style.background = completo ? 'var(--ok)' : 'var(--accent)'; rel.style.borderRadius = '4px';
      T(f, w - 190, 0, 80, 56, 'fm', 16, 'var(--soft)', `${hecho}/${meta}`, { al: 'der' });
      T(f, w - 100, 0, 84, 56, 'ft', 22, completo ? 'var(--ok)' : 'var(--accent)', `+${d.xp[etapa]} XP`, { al: 'der' });
    });
  }

  // ---------- Cristales: compras simuladas ----------
  function pantallaCristales() {
    barraTienda('Simulación', 3);
    const aviso = N(capa, 60, 120, 1800, 64, 'panel');
    const i = N(aviso, 20, 16, 32, 32); i.style.borderRadius = '50%'; i.style.background = 'var(--cristales)';
    T(i, 0, 0, 32, 32, 'ft', 22, '#120d26', 'i', { al: 'centro' });
    T(aviso, 70, 0, 1700, 64, 'fx', 20, 'var(--soft)', 'Compras simuladas. Es un proyecto universitario: no se cobra dinero real. Se prueba cómo funcionaría la tienda de un juego gratuito.');
    const [bien, ...packs] = X.packs, usada = rift.compras.some(c => c.id === bien.id);
    const o = N(capa, 60, 204, 620, 560, 'panel'); o.style.overflow = 'hidden';
    Img(o, 'img/fondos/fondo-02.jpg', 0, 0, 620, 560);
    degrade(N(o, 0, 0, 620, 560), '0deg', 0.92, 0.25);
    const chip = N(o, 28, 28, 92, 36); chip.style.background = 'var(--accent)'; chip.style.borderRadius = '6px';
    T(chip, 0, 0, 92, 36, 'ft', 24, 'var(--accent-ink)', '-40 %', { al: 'centro' });
    T(o, 28, 300, 560, 22, 'fr', 17, 'var(--accent)', 'Una sola vez por cuenta', { mayus: true, esp: 14 });
    T(o, 26, 322, 560, 56, 'ft', 50, 'var(--ink)', bien.nombre, { mayus: true, esp: 3 });
    T(o, 28, 380, 560, 30, 'fx', 23, 'var(--soft)', bien.detalle);
    const tachado = T(o, 28, 430, 200, 30, 'ft', 24, 'var(--mute)', precioEnPesos(3300)); tachado.style.textDecoration = 'line-through';
    T(o, 28, 458, 300, 64, 'ft', 56, 'var(--ink)', precioEnPesos(bien.precio));
    if (usada) Boton(o, 620 - 28 - 220, 470, 220, 60, 'Comprada', 26, 'ok', null);
    else Boton(o, 620 - 28 - 220, 470, 220, 60, 'Comprar', 26, 'lleno', () => comprar(bien));
    packs.forEach((p, j) => {
      const k = N(capa, 700 + j * 295, 204, 275, 560, 'panel');
      if (p.id === 'c400' || p.id === 'c2500') {
        const s = N(k, 0, 0, 275, 34); s.style.background = p.id === 'c400' ? 'var(--accent)' : 'var(--cristales)'; s.style.borderRadius = '8px 8px 0 0';
        T(s, 0, 0, 275, 34, 'ft', 18, p.id === 'c400' ? 'var(--accent-ink)' : '#120d26', p.id === 'c400' ? 'Más elegido' : 'Mejor valor', { al: 'centro', mayus: true, esp: 12 });
      }
      const brillo = N(k, 0, 40, 275, 220); brillo.style.background = 'radial-gradient(circle at 50% 55%,rgba(155,123,255,.28),transparent 65%)';
      const pos = [[[137, 150]], [[112, 160], [165, 140]], [[95, 165], [140, 132], [182, 168]], [[90, 172], [137, 120], [184, 172], [137, 190]]][j];
      pos.forEach(([cx, cy], q) => iconoMoneda(k, 2, cx, cy, 64 - q * 4));
      T(k, 0, 262, 275, 70, 'ft', 64, 'var(--ink)', p.cristales.toLocaleString('es-AR'), { al: 'centro' });
      T(k, 0, 330, 275, 24, 'fr', 18, 'var(--mute)', 'cristales', { al: 'centro', mayus: true, esp: 14 });
      if (p.bonus) T(k, 0, 360, 275, 26, 'ft', 22, 'var(--ok)', `+${p.bonus.toLocaleString('es-AR')} de regalo`, { al: 'centro', mayus: true, esp: 4 });
      T(k, 0, 404, 275, 28, 'fx', 20, 'var(--soft)', p.nombre, { al: 'centro' });
      T(k, 0, 434, 275, 46, 'ft', 38, 'var(--ink)', precioEnPesos(p.precio), { al: 'centro' });
      Boton(k, 24, 488, 227, 52, 'Comprar', 24, 'lleno', () => comprar(p));
    });
    const h = N(capa, 60, 790, 1180, 250, 'panel');
    T(h, 28, 20, 600, 22, 'fr', 17, 'var(--accent)', 'Compras simuladas', { mayus: true, esp: 16 });
    if (!rift.compras.length) T(h, 28, 60, 1100, 30, 'fx', 20, 'var(--mute)', 'Todavía no compraste nada. Ninguna compra cobra dinero real.');
    rift.compras.slice(0, 5).forEach((c, j) => {
      const y = 56 + j * 36;
      T(h, 28, y, 500, 32, 'fx', 20, 'var(--ink)', c.nombre);
      T(h, 540, y, 400, 32, 'fx', 20, 'var(--cristales)', `+${c.cristales.toLocaleString('es-AR')} cristales` + (c.boletos ? ` · +${c.boletos} boletos` : ''));
      T(h, 940, y, 212, 32, 'fm', 18, 'var(--soft)', precioEnPesos(c.precio), { al: 'der' });
    });
    const t = N(capa, 1260, 790, 600, 250, 'panel');
    T(t, 28, 20, 540, 22, 'fr', 17, 'var(--accent)', 'Total gastado (simulado)', { mayus: true, esp: 16 });
    T(t, 26, 56, 540, 90, 'ft', 84, 'var(--ink)', precioEnPesos(rift.gastado));
    T(t, 28, 156, 540, 60, 'fx', 18, 'var(--mute)', `${rift.compras.length} ${rift.compras.length === 1 ? 'compra' : 'compras'} · Es una simulación: no se cobró nada.`, { arriba: true });
  }
  function comprar(p) {
    const total = p.cristales + (p.bonus || 0);
    rift.cristales += total; rift.boletos += p.boletos || 0; rift.gastado += p.precio;
    rift.compras.unshift({ id: p.id, nombre: p.nombre, cristales: total, boletos: p.boletos || 0, precio: p.precio });
    avisar(`Compra simulada: +${total.toLocaleString('es-AR')} cristales` + (p.boletos ? ` y +${p.boletos} boletos` : ''));
    mostrar();
  }

  // DEBUG (F9, en el menú y en sus pantallas, como en el juego): suma de las tres monedas.
  function sumarDebug() {
    rift.creditos += 1000; rift.boletos += 10; rift.cristales += 500;
    avisar('Debug: +1.000 créditos, +10 boletos y +500 cristales');
    if (location.hash === '#estacion') refrescarEstacion(); else mostrar();
  }

  // ---------- Navegación y escala ----------
  const PANTALLAS = { '#perfil': perfil, '#tienda': tienda, '#estacion': estacion, '#pase': pantallaPase, '#cristales': pantallaCristales };
  function mostrar() {
    if (est && location.hash !== '#estacion') est.vivo = false;
    clearInterval(relojTienda);
    const m = E.querySelector('.modal'); if (m) m.remove();
    (PANTALLAS[location.hash] || menu)();
  }
  document.fonts.ready.then(() => { if (location.hash === '#perfil' || location.hash === '#pase') mostrar(); }); // los anchos de las pestañas
  addEventListener('hashchange', mostrar);
  addEventListener('keydown', e => {
    if (e.key === 'F9') { e.preventDefault(); sumarDebug(); return; }
    if (e.key !== 'Escape') return;
    const modal = E.querySelector('.modal');
    if (modal) { if (location.hash === '#estacion') modal.remove(); return; }
    if (location.hash === '#estacion' && est && est.estado === 'abriendo') { saltar(); return; }
    if (PANTALLAS[location.hash]) volverAlMenu();
  });
  function escalar() {
    const k = Math.min(innerWidth / 1920, innerHeight / 1080);
    E.style.transform = `scale(${k})`;
    E.style.left = (innerWidth - 1920 * k) / 2 + 'px';
    E.style.top = (innerHeight - 1080 * k) / 2 + 'px';
  }
  addEventListener('resize', escalar);
  escalar();
  mostrar();
})();
