// Diseño de la cuenta en el juego (F18: US 282 y US 273) y de buscar partida (F28: US 274 a US 277), armado con las
// piezas del menú del juego (menu.css, como MenuRiftwalker, PantallaPerfil y ModeSelectScreen) en una pantalla de
// 1920 × 1080. La cuenta se maneja en el launcher (launcher/cuenta.html): el juego abre con esa cuenta, el Perfil solo
// la muestra y, sin una sesión, el juego pide iniciarla en el launcher. Cada estado tiene su ancla (#perfil-cuenta,
// #sin-sesion, #buscando…) para que la guía los muestre. Todavía no está en el juego: es la propuesta para revisar.
(() => {
  const C = window.CATALOGO;
  const E = document.getElementById('escenario');
  const MAIL = 'ulises.fonseca@gmail.com', NOMBRE = 'Ulises';
  const SET = 'guerrero-synthwave';
  const TITULO = (C.titulos.find(t => t.nombre === 'Viajero del Rift') || C.titulos[1] || {}).nombre || 'Viajero del Rift';
  const MODO = {
    deathmatch: { c: '#FF4B4B', anden: 'Andén 2 · Online', nombre: 'Deathmatch', foto: '../img/deathmatch-vista.jpg',
      texto: 'Todos contra todos. Elegís tus armas gratis, cada baja te cura 50 y reaparecés a los 3 s lejos de los rivales.',
      reglas: ['Hasta 8', '25 bajas', '8 min', 'Armas gratis'], minimo: 2 },
    tactico: { c: '#F29A38', anden: 'Andén 1 · Online', nombre: 'Táctico', foto: '../img/tactico-vista.jpg',
      texto: 'Un equipo planta el dispositivo en A o B y el otro lo defiende. Sin reaparición: si morís, mirás a un compañero hasta la ronda siguiente.',
      reglas: ['4 vs 4', 'Primero a 7', 'Cambio de lado en la 6', 'Compra 20 s'], minimo: 4 },
  };

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

  // ---------- Andén y buscar partida (como ModeSelectScreen) ----------
  function anden(modo) {
    const m = MODO[modo];
    capa.innerHTML = ''; capa.style.background = '#07080a';
    capa.style.setProperty('--c', m.c);
    Img(capa, m.foto, 0, 0, 1920, 1080);
    degrade(N(capa, 0, 0, 1400, 1080), '90deg', 0.92, 0.1);
    degrade(N(capa, 0, 760, 1920, 320), '0deg', 0.7, 0);
    T(capa, 64, 50, 700, 22, 'fr', 20, 'rgba(255,255,255,.6)', 'Project Riftwalker', { mayus: true, esp: 30 });
    T(capa, 62, 76, 700, 58, 'ft', 54, 'var(--ink)', 'Elegí el modo', { mayus: true, esp: 2 });
    T(capa, 1500, 52, 356, 44, 'fr', 22, 'rgba(255,255,255,.7)', '<b style="color:#fff;font-family:JetBrains Mono,monospace;font-weight:400;border:1px solid rgba(255,255,255,.4);border-radius:4px;padding:2px 8px;margin-right:10px">Esc</b> Volver', { html: true, al: 'der', mayus: true, esp: 10 });
    T(capa, 150, 166, 900, 30, 'fr', 24, m.c, m.anden, { mayus: true, esp: 32 });
    T(capa, 144, 196, 1300, 130, 'ft', 128, 'var(--ink)', m.nombre, { mayus: true });
    T(capa, 150, 336, 1000, 80, 'fx', 28, 'rgba(255,255,255,.85)', m.texto, { arriba: true, alto: 1.35 });
    const r = N(capa, 150, 440, 1200, 40); r.innerHTML = m.reglas.map(x => `<span class="regla">${x}</span>`).join('');
    return m;
  }
  function andenConBotones(modo) {
    const m = anden(modo);
    Boton(capa, 150, 520, 380, 84, 'Buscar partida', 34, 'color', () => ir('buscando'));
    Boton(capa, 550, 520, 280, 84, 'Crear sala', 30, 'fantasma', null).disabled = false;
    Boton(capa, 850, 520, 340, 84, 'Unirse con código', 30, 'fantasma', null).disabled = false;
    T(capa, 152, 626, 1000, 30, 'fx', 22, 'rgba(255,255,255,.6)', `Buscás con tu cuenta (${NOMBRE}) · Región Sudamérica`);
    return m;
  }
  // Panel de búsqueda: reemplaza a los botones del andén mientras busca (US 275).
  function busqueda(modo, jugadores, fase) {
    const m = anden(modo);
    const p = N(capa, 150, 510, 860, 470, 'panel');
    p.style.background = 'rgba(0,0,0,.62)'; p.style.boxShadow = 'inset 0 0 0 1px rgba(255,255,255,.18)';
    const linea = N(p, 0, 0, 860, 4); linea.style.background = m.c; linea.style.borderRadius = '8px 8px 0 0';
    if (fase === 'cuenta') {
      T(p, 30, 22, 500, 26, 'fr', 22, m.c, 'Partida encontrada', { mayus: true, esp: 14 });
      const n = T(p, 30, 50, 600, 60, 'ft', 52, 'var(--ink)', '', { mayus: true, esp: 3 });
      reloj(n, 7, true, s => s ? `Empieza en ${s}` : 'Cargando el mapa…');
    } else {
      T(p, 30, 22, 500, 26, 'fr', 22, m.c, 'Buscando partida', { mayus: true, esp: 14 });
      T(p, 30, 50, 600, 60, 'ft', 52, 'var(--ink)', `${jugadores.length} / 8 jugadores`, { mayus: true, esp: 3 });
      const r = T(p, 560, 34, 270, 64, 'fm', 54, 'var(--ink)', '', { al: 'der' });
      reloj(r, fase === 'minimo' ? 52 : 23, false);
    }
    T(p, 30, 112, 800, 28, 'fx', 21, 'rgba(255,255,255,.6)', `${m.nombre} · Sudamérica · misma versión del juego`);
    for (let i = 0; i < 8; i++) {
      const col = i % 2, fila = Math.floor(i / 2), j = jugadores[i];
      const lugar = N(p, 30 + col * 410, 156 + fila * 52, 390, 46, 'lugar' + (j ? '' : ' libre'));
      lugar.innerHTML = `<span class="punto"></span><span>${j ? j : 'Buscando…'}</span>`;
      if (j && i === 0) lugar.lastChild.innerHTML = `${j} <span style="color:rgba(255,255,255,.5);font-weight:400">(vos)</span>`;
    }
    const pie = fase === 'minimo'
      ? `Hay ${jugadores.length}, el mínimo para ${m.nombre}. Si no entra nadie más, arranca en`
      : fase === 'cuenta' ? 'Si sale alguien y quedan menos que el mínimo, sigue buscando.'
      : `Arranca sola con 8 jugadores, o con ${m.minimo} si pasan 45 s sin que entre nadie.`;
    const t = T(p, 30, 380, 560, 60, 'fx', 20, 'rgba(255,255,255,.75)', pie, { arriba: true, alto: 1.35, html: true });
    if (fase === 'minimo') {
      const s = el('b', '', t.querySelector('span')); s.style.cssText = 'color:#fff;font-family:"JetBrains Mono",monospace;font-weight:400;margin-left:8px';
      reloj(s, 31, true, x => `0:${String(x).padStart(2, '0')}`);
      const b = N(p, 30, 440, 560, 6, 'barra'); const i = el('i', '', b); i.style.width = '31%';
    }
    Boton(p, 620, 380, 210, 64, 'Cancelar', 26, 'fantasma', () => { ir('anden'); avisar('Búsqueda cancelada'); });
  }
  function sinConexion() {
    const m = anden('deathmatch');
    const p = N(capa, 150, 510, 860, 300, 'panel');
    p.style.background = 'rgba(0,0,0,.62)'; p.style.boxShadow = 'inset 0 0 0 1px rgba(255,92,92,.45)';
    const linea = N(p, 0, 0, 860, 4); linea.style.background = 'var(--bad)'; linea.style.borderRadius = '8px 8px 0 0';
    T(p, 30, 26, 600, 26, 'fr', 22, 'var(--bad)', 'Buscar partida', { mayus: true, esp: 14 });
    T(p, 30, 54, 800, 60, 'ft', 52, 'var(--ink)', 'Se cortó la conexión', { mayus: true, esp: 3 });
    T(p, 30, 122, 800, 70, 'fx', 22, 'rgba(255,255,255,.8)', 'Revisá tu internet y volvé a buscar. No perdiste nada: tu progreso está guardado.', { arriba: true, alto: 1.4 });
    Boton(p, 30, 210, 300, 64, 'Volver al andén', 24, 'color', () => ir('anden'));
    return m;
  }

  // ---------- Estados ----------
  const ESTADOS = {
    'perfil-cuenta': perfil,
    'privacidad': privacidad,
    'sin-sesion': () => sinSesion(false),
    'sesion-cerrada': () => sinSesion(true),
    'anden': () => andenConBotones('deathmatch'),
    'buscando': () => busqueda('deathmatch', [NOMBRE, 'Nahue', 'Luka'], 'buscando'),
    'minimo': () => busqueda('tactico', [NOMBRE, 'Agus', 'Lucas', 'Nico'], 'minimo'),
    'cuenta-regresiva': () => busqueda('deathmatch', [NOMBRE, 'Nahue', 'Luka', 'David', 'Agus', 'Lucas'], 'cuenta'),
    'sin-conexion': sinConexion,
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
    if (['buscando', 'minimo', 'cuenta-regresiva'].includes(h)) ir('anden');
    else if (h === 'privacidad') ir('perfil-cuenta');
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
