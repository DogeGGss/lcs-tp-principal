// Diseño de la cuenta en el juego (F18: US 282 y US 273), armado con las piezas del menú del juego (menu.css, como
// MenuRiftwalker y PantallaPerfil) en una pantalla de 1920 × 1080. La cuenta se maneja en el launcher
// (launcher/cuenta.html): el juego abre con esa cuenta, el Perfil solo la muestra y, sin una sesión, el juego pide
// iniciarla en el launcher. Buscar partida tiene su propia maqueta, sobre la pantalla de elegir modo (partida.html).
// Cada estado tiene su ancla (#perfil-cuenta, #sin-sesion…) para que la guía los muestre. Todavía no está en el juego.
(() => {
  const C = window.CATALOGO;
  const E = document.getElementById('escenario');
  const MAIL = 'ulises.fonseca@gmail.com', NOMBRE = 'Ulises';
  const SET = 'guerrero-synthwave';
  const TITULO = (C.titulos.find(t => t.nombre === 'Viajero del Rift') || C.titulos[1] || {}).nombre || 'Viajero del Rift';

  // ---------- Piezas (las mismas de menu.js) ----------
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
    if (o.alto) d.style.lineHeight = o.alto;
    if (o.html) d.innerHTML = '<span>' + texto + '</span>'; else d.textContent = texto;
    return d;
  }
  function Img(padre, src, x, y, w, h) {
    const caja = N(padre, x, y, w, h);
    caja.style.overflow = 'hidden';
    const i = el('img', 'cubrir', caja); i.src = src; i.alt = ''; i.draggable = false;
    return caja;
  }
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
  const ir = h => { location.hash = h; };

  const capa = N(E, 0, 0, 1920, 1080);
  const avisoNodo = el('div', 'aviso', E);
  let tiempoAviso, relojes = [];
  function avisar(texto) {
    avisoNodo.textContent = texto; avisoNodo.classList.add('visible');
    clearTimeout(tiempoAviso); tiempoAviso = setTimeout(() => avisoNodo.classList.remove('visible'), 3200);
  }
  // Reloj que cuenta desde "desde" segundos (hacia arriba) o hasta 0 (hacia abajo); se para al cambiar de estado.
  function reloj(nodo, desde, abajo, formato = s => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`) {
    let s = desde; nodo.textContent = formato(s);
    relojes.push(setInterval(() => { s = abajo ? Math.max(0, s - 1) : s + 1; nodo.textContent = formato(s); }, 1000));
  }

  // ---------- Monedas y cabecera del Perfil (como PantallaPerfil) ----------
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
  function monedas(derecha, y) {
    const datos = [['Se ganan jugando', 2450, false], ['Abren cápsulas', 3, true], ['Moneda de pago', 120, true]];
    let x = derecha;
    for (let i = 2; i >= 0; i--) {
      const [ayuda, cantidad, sumar] = datos[i], w = sumar ? 214 : 186;
      x -= w;
      const chip = N(capa, x, y, w, 56, 'panel');
      iconoMoneda(chip, i, 28, 28, 22);
      T(chip, 50, 6, 120, 30, 'ft', 28, 'var(--ink)', cantidad.toLocaleString('es-AR'));
      T(chip, 50, 33, 140, 16, 'fr', 13, 'var(--mute)', ayuda, { mayus: true, esp: 8 });
      if (sumar) {
        const mas = N(chip, w - 40, 12, 30, 30); mas.style.borderRadius = '6px'; mas.style.background = i === 1 ? 'var(--boletos)' : 'var(--cristales)';
        T(mas, 0, -1, 30, 30, 'ft', 26, 'var(--accent-ink)', '+', { al: 'centro' });
      }
      x -= 12;
    }
  }
  function cabecera() {
    const cab = N(capa, 60, 120, 1800, 300);
    cab.style.background = 'var(--panel)'; cab.style.borderRadius = '10px';
    const d = Marcos.interior(SET, 300, false);
    const caja = Img(cab, `img/banners/${SET}.jpg`, d[0], d[1], 1800 - d[0] - d[2], 300 - d[1] - d[3]);
    degrade(N(caja, 0, 0, 1150, 300), '90deg', 0.9, 0);
    degrade(N(caja, 0, 140, 1800, 160), '0deg', 0.55, 0);
    cab.insertAdjacentHTML('beforeend', Marcos.html(SET, 0, 0, 1800, 300, false));
    const ma = N(cab, 92, 46, 208, 208); ma.style.background = 'var(--ink)'; ma.style.borderRadius = '8px';
    Img(cab, `img/avatares/${SET}.jpg`, 96, 50, 200, 200).style.borderRadius = '6px';
    T(cab, 336, 36, 900, 90, 'ft', 88, 'var(--ink)', NOMBRE, { mayus: true, esp: 3 });
    T(cab, 340, 124, 900, 38, 'fr', 30, 'var(--accent)', TITULO, { esp: 6 });
    [['31', 'Partidas'], ['17', 'Victorias'], ['1,84', 'KDA'], ['—', 'Oleada récord']].forEach(([v, r], i) => {
      const chip = N(cab, 340 + i * 170, 178, 156, 70); chip.style.background = 'rgba(0,0,0,.5)'; chip.style.borderRadius = '6px';
      T(chip, 16, 6, 130, 38, 'ft', 36, 'var(--ink)', v);
      T(chip, 16, 44, 140, 18, 'fr', 14, 'var(--mute)', r, { mayus: true, esp: 14 });
    });
    Boton(cab, 1800 - 150 - 220, 222, 220, 46, 'Cambiar nombre', 20, '', null);
  }
  // El Perfil con la pestaña nueva «Cuenta» elegida.
  function perfil() {
    capa.innerHTML = ''; capa.style.background = '#0b0d11';
    const volver = N(capa, 60, 34, 150, 52, 'panel');
    T(volver, 0, 0, 150, 52, 'ft', 26, 'var(--ink)', '« Menú', { mayus: true, esp: 6, al: 'centro' });
    T(capa, 240, 34, 400, 20, 'fr', 17, 'var(--accent)', 'Registro del jugador', { mayus: true, esp: 16 });
    T(capa, 238, 52, 400, 46, 'ft', 44, 'var(--ink)', 'Perfil', { mayus: true, esp: 3 });
    monedas(1860, 34);
    cabecera();
    const pest = [['Estadísticas', 60], ['Personalización', 314], ['Cuenta', 635]];
    pest.forEach(([n, x], i) => {
      const t = T(capa, x, 438, 360, 50, 'ft', 36, i === 2 ? 'var(--ink)' : 'var(--mute)', n, { mayus: true, esp: 6 });
      t.style.width = 'auto';
      if (i === 2) { const r = N(capa, x, 488, 10, 4); r.style.background = 'var(--accent)'; requestAnimationFrame(() => { r.style.width = t.offsetWidth + 'px'; }); }
    });
    const info = T(capa, 1060, 452, 800, 26, 'fr', 19, 'var(--mute)', 'Cuenta  <b>Desde el launcher</b>', { mayus: true, esp: 14, al: 'der', html: true });
    info.style.whiteSpace = 'pre';
    info.querySelector('b').style.cssText = 'color:var(--ink);font-family:"JetBrains Mono",monospace;font-weight:400;letter-spacing:0;margin-left:10px';
    const l = N(capa, 60, 494, 1800, 1); l.style.background = 'rgba(255,255,255,.08)';
    tarjetaCuenta();
    tarjetaPrivacidad();
  }

  // ---------- Pestaña Cuenta: solo muestra la cuenta del launcher (US 282, CA2) ----------
  function tarjetaCuenta() {
    const t = N(capa, 60, 520, 1100, 500, 'panel');
    T(t, 44, 36, 600, 22, 'fr', 18, 'var(--accent)', 'Tu cuenta', { mayus: true, esp: 16 });
    T(t, 42, 64, 760, 56, 'ft', 48, 'var(--ink)', MAIL, { esp: 2, puntos: true });
    const chip = N(t, 830, 76, 0, 0, 'chip-estado'); chip.style.width = 'auto'; chip.style.height = '34px';
    chip.style.background = 'rgba(61,220,151,.14)'; chip.style.color = 'var(--ok)'; chip.textContent = 'Confirmada';
    T(t, 44, 136, 1000, 70, 'fx', 25, 'var(--soft)', 'Entraste desde el launcher. Tu progreso, tus monedas y tu lugar en el ranking están en la cuenta, y los seguís en cualquier PC.', { arriba: true, alto: 1.35 });
    [['Desde', '29/10/2026'], ['Nombre', NOMBRE], ['Progreso', 'En la nube']].forEach(([r, v], i) => {
      const c = N(t, 44 + i * 300, 236, 280, 84); c.style.background = 'rgba(0,0,0,.35)'; c.style.borderRadius = '6px';
      T(c, 18, 10, 260, 18, 'fr', 15, 'var(--mute)', r, { mayus: true, esp: 14 });
      T(c, 18, 32, 260, 40, 'ft', 32, 'var(--ink)', v, { mayus: true, esp: 3 });
    });
    const l = N(t, 44, 360, 1012, 1); l.style.background = 'rgba(255,255,255,.08)';
    T(t, 44, 390, 1000, 70, 'fx', 23, 'var(--mute)', 'Para cambiar de cuenta, cerrar sesión o borrar la cuenta, usá el <b style="color:var(--ink)">launcher</b>: el menú de la cuenta está arriba, al lado de Ajustes.', { arriba: true, alto: 1.4, html: true });
  }
  function tarjetaPrivacidad() {
    const t = N(capa, 1190, 520, 670, 500, 'panel');
    T(t, 40, 36, 560, 22, 'fr', 18, 'var(--accent)', 'Privacidad', { mayus: true, esp: 16 });
    T(t, 38, 62, 600, 64, 'ft', 48, 'var(--ink)', 'Qué guardamos', { mayus: true, esp: 3 });
    T(t, 40, 132, 590, 230, 'fx', 23, 'var(--soft)', 'Tu mail, tu nombre, tu progreso, tus estadísticas y tus opiniones. Los usamos para guardar tu progreso, armar el ranking y buscar partida. No los compartimos con nadie, y podés borrar la cuenta cuando quieras.', { arriba: true, alto: 1.4 });
    Boton(t, 40, 396, 330, 66, 'Ver el aviso completo', 24, '', () => ir('privacidad'));
  }

  // ---------- Ventanas ----------
  function ventana(w, h, cerrar) {
    const m = el('div', 'modal', capa), caja = el('div', 'caja', m);
    caja.style.width = w + 'px'; caja.style.height = h + 'px';
    m.addEventListener('click', e => { if (e.target === m && cerrar) cerrar(); });
    return caja;
  }
  function titulo(caja, antetitulo, texto, color) {
    T(caja, 48, 36, 700, 22, 'fr', 18, color || 'var(--accent)', antetitulo, { mayus: true, esp: 16 });
    T(caja, 46, 60, 800, 54, 'ft', 48, 'var(--ink)', texto, { mayus: true, esp: 3 });
  }
  function privacidad() {
    perfil();
    const volver = () => ir('perfil-cuenta');
    const c = ventana(980, 780, volver);
    titulo(c, 'Privacidad', 'Aviso de privacidad');
    const partes = [
      ['Qué guardamos', 'Tu mail y tu contraseña (cifrada), tu nombre, tu progreso (logros, cosméticos, monedas), tus estadísticas de partida y las opiniones que mandes.'],
      ['Para qué', 'Para que recuperes tu progreso en cualquier PC, para el ranking global y para buscar partida. No lo usamos para publicidad ni lo compartimos con nadie.'],
      ['Dónde', 'En la base del juego (Supabase). Las partidas pasan por los servidores de Photon, que reciben tu nombre de jugador, tu id de cuenta y tu conexión (IP), como cualquier juego en línea.'],
      ['Tus derechos', 'Podés ver tus datos en el Perfil y borrar la cuenta con todo lo que tiene desde el launcher, cuando quieras (Ley 25.326 de Protección de Datos Personales).'],
    ];
    partes.forEach(([t, d], i) => {
      T(c, 48, 136 + i * 136, 880, 26, 'fr', 20, 'var(--accent)', t, { mayus: true, esp: 12 });
      T(c, 48, 166 + i * 136, 880, 100, 'fx', 22, 'var(--soft)', d, { arriba: true, alto: 1.4 });
    });
    Boton(c, 660, 690, 272, 60, 'Entendido', 24, 'lleno', volver);
  }

  // ---------- Sin sesión: «Iniciá sesión en el launcher» (US 282, CA3) ----------
  function sinSesion(cerrada) {
    capa.innerHTML = ''; capa.style.background = '#07080a';
    Img(capa, '../img/tactico-vista.jpg', 0, 0, 1920, 1080);
    degrade(N(capa, 0, 0, 1920, 1080), '90deg', 0.96, 0.78);
    T(capa, 150, 250, 900, 26, 'fr', 22, cerrada ? 'var(--bad)' : 'var(--accent)', cerrada ? 'Tu sesión se cerró' : 'Project Riftwalker', { mayus: true, esp: 30 });
    T(capa, 144, 284, 1500, 110, 'ft', 104, 'var(--ink)', 'Iniciá sesión en el launcher', { mayus: true, esp: 1 });
    T(capa, 150, 410, 1100, 90, 'fx', 30, 'var(--soft)', cerrada
      ? 'Se cerró la sesión de tu cuenta, por ejemplo desde el launcher. Volvé a entrar ahí para seguir jugando: tu progreso está guardado en la cuenta.'
      : 'Para jugar hace falta una cuenta. Abrí el launcher, entrá con tu cuenta y tocá Jugar: el juego se abre con ella.', { arriba: true, alto: 1.4 });
    [['1', 'Abrí el launcher'], ['2', 'Entrá con tu cuenta'], ['3', 'Tocá Jugar']].forEach(([n, x], i) => {
      const p = N(capa, 150 + i * 360, 540, 340, 84, 'panel');
      T(p, 24, 0, 50, 84, 'ft', 48, 'var(--accent)', n);
      T(p, 74, 0, 260, 84, 'fx', 26, 'var(--ink)', x);
    });
    Boton(capa, 150, 680, 380, 76, 'Abrir el launcher', 30, 'lleno', () => avisar('Se abre el launcher y se cierra el juego'));
    Boton(capa, 550, 680, 220, 76, 'Salir', 30, '', () => avisar('Se cierra el juego'));
    T(capa, 152, 784, 1100, 30, 'fx', 21, 'var(--mute)', 'Si el launcher no está en esta PC, bajalo desde la guía del juego.');
  }

  // ---------- Estados ----------
  const ESTADOS = {
    'perfil-cuenta': perfil,
    'privacidad': privacidad,
    'sin-sesion': () => sinSesion(false),
    'sesion-cerrada': () => sinSesion(true),
  };
  function mostrar() {
    relojes.forEach(clearInterval); relojes = [];
    capa.style.removeProperty('--c');
    (ESTADOS[location.hash.slice(1)] || ESTADOS['perfil-cuenta'])();
  }
  addEventListener('hashchange', mostrar);
  addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    const h = location.hash.slice(1);
    if (h === 'privacidad') ir('perfil-cuenta');
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
