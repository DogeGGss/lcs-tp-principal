// Menú principal y Perfil de Project Riftwalker, como están en el juego (MenuRiftwalker y PantallaPerfil).
// Las posiciones son las de Unity, en una pantalla de 1920 × 1080. Acá todo está desbloqueado para probar combinaciones.
(() => {
  const C = window.CATALOGO;
  const E = document.getElementById('escenario');
  const RAREZA = ['Inicial', 'Común', 'Raro', 'Épico', 'Legendario'];
  const COLOR = ['#8E96A3', '#9AA3B0', '#5C7CFF', '#B061FF', '#FFC93C'];
  const TIPO = { avatar: 'Avatar', banner: 'Banner', titulo: 'Título', marco: 'Marco' };
  const LISTA = { avatar: C.avatares, banner: C.banners, titulo: C.titulos, marco: C.marcos };
  const EMOTES = ['Shuffle', 'Twerk', 'Breakdance', 'Flair', 'Rumba', 'Silly'];
  const LUGARES = ['Arriba', 'Derecha', 'Abajo', 'Izquierda'];
  const FONDOS = ['fondo-01', 'fondo-02', 'fondo-03', 'fondo-04', 'fondo-05', 'fondo-06'];
  const MONEDAS = { creditos: 2450, boletos: 3, cristales: 120 }; // las del perfil nuevo del juego
  const CLAVE = 'riftwalker-menu-web';

  // ---------- Estado (se recuerda en este navegador) ----------
  const inicial = { nombre: 'Jugador', ...C.inicial, emotes: [-1, -1, -1, -1] };
  let estado = { ...inicial };
  try { const g = JSON.parse(localStorage.getItem(CLAVE) || 'null'); if (g) estado = { ...inicial, ...g }; } catch { }
  const guardar = () => { try { localStorage.setItem(CLAVE, JSON.stringify(estado)); } catch { } };
  let pestana = 1, categoria = 'avatar', espacio = 0, emoteVisto = -1;
  const elegido = { avatar: null, banner: null, titulo: null, marco: null };

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
    const nombres = [['Se ganan jugando', MONEDAS.creditos, false], ['Abren cápsulas', MONEDAS.boletos, true], ['Moneda de pago', MONEDAS.cristales, true]];
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
    T(capa, 122, 440, 600, 30, 'fx', 25, 'var(--mute)', 'Táctico  ·  Deathmatch  ·  Zombie').style.whiteSpace = 'pre';
    const linea = N(capa, 122, 486, 440, 3); linea.style.background = 'linear-gradient(90deg,var(--accent),transparent)';
    ['Opciones', 'Logros', 'Créditos', 'Salir'].forEach((o, i) => {
      const fila = N(capa, 120, 508 + i * 50, 340, 46, 'fila');
      el('div', 'marca', fila);
      T(fila, 0, 0, 340, 46, 'ft', 36, 'var(--soft)', o, { mayus: true, esp: 6 });
    });
    T(capa, 122, 718, 200, 24, 'fr', 17, 'var(--mute)', 'Campos de prueba:', { mayus: true, esp: 12 });
    T(capa, 300, 718, 240, 24, 'fr', 17, 'var(--soft)', 'Campo de práctica', { mayus: true, esp: 12 });

    const tarjeta = N(capa, 80, 900, 580, 132, 'panel');
    const lt = N(tarjeta, 0, 0, 580, 3); lt.style.background = 'rgba(242,154,56,.8)';
    capsula(tarjeta, 30, 18);
    T(tarjeta, 128, 18, 420, 20, 'fr', 16, 'var(--accent)', '01 / Cápsulas', { mayus: true, esp: 12 });
    T(tarjeta, 126, 38, 430, 46, 'ft', 42, 'var(--ink)', 'Estación del Rift', { mayus: true, esp: 3 });
    T(tarjeta, 128, 86, 420, 26, 'fx', 19, 'var(--mute)', `Tenés ${MONEDAS.boletos} boletos para abrir`);

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
    if (pestana === 0) estadisticas(panel); else if (pestana === 1) personalizacion(panel); else emotes(panel);
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
  }

  function pestanas() {
    const nombres = ['Estadísticas', 'Personalización', 'Emotes'];
    let x = 60;
    nombres.forEach((n, i) => {
      const t = T(capa, x, 438, 360, 50, 'ft pestana' + (i === pestana ? ' activa' : ''), 36, i === pestana ? 'var(--ink)' : 'var(--mute)', n, { mayus: true, esp: 6 });
      t.style.width = 'auto';
      clic(t, () => { pestana = i; perfil(); }, n);
      if (i === pestana) { const r = N(capa, x, 488, 10, 4); r.style.background = 'var(--accent)'; requestAnimationFrame(() => { r.style.width = t.offsetWidth + 'px'; }); }
      x += { 0: 254, 1: 321, 2: 0 }[i];
    });
    let info;
    if (pestana === 0) info = 'Partidas terminadas  <b>8</b>';
    else if (pestana === 1) { const tot = Object.values(LISTA).reduce((a, l) => a + l.length, 0); info = `Colección  <b>${tot} / ${tot}</b>`; }
    else info = `En la rueda  <b>${estado.emotes.filter(e => e >= 0).length} / 4</b>`;
    const d = T(capa, 1060, 452, 800, 26, 'fr', 19, 'var(--mute)', info, { mayus: true, esp: 14, al: 'der', html: true });
    d.style.whiteSpace = 'pre';
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
      ['Zombie', '#3DDC97', null],
      ['Minijuego: cometas', '#4CC3FF', [['Destruidos', '171'], ['Precisión', '26 %'], ['Puntos', '12.600'], ['Reacción promedio', '5,9 s'], ['Récord en una fase', '16 · 1.250 pts'], ['Mejor reacción', '1,6 s']]],
    ];
    cartas.forEach(([titulo, color, filas], i) => {
      const c = N(p, i * 455, 188, 435, 300, 'panel');
      const l = N(c, 0, 0, 435, 4); l.style.background = color;
      T(c, 24, 18, 400, 44, 'ft', 34, color, titulo, { mayus: true, esp: 3 });
      if (!filas) { T(c, 24, 74, 390, 70, 'fx', 21, 'var(--mute)', 'Sin datos. Las partidas Zombie se van a registrar cuando el modo tenga final.', { arriba: true }); return; }
      filas.forEach(([n, v], j) => {
        const y = 66 + j * 37;
        T(c, 24, y, 260, 32, 'fx', 21, 'var(--soft)', n);
        T(c, 200, y, 211, 32, 'ft', 26, 'var(--ink)', v, { al: 'der' });
        if (j < filas.length - 1) { const ln = N(c, 24, y + 34, 387, 1); ln.style.background = 'rgba(255,255,255,.06)'; }
      });
    });
    T(p, 0, 500, 1800, 26, 'fx', 17, 'var(--mute)', '* KDA = (bajas + asistencias) ÷ muertes; sin muertes, ÷ 1. Asistencia: dañaste a un rival que otro eliminó en los 10 s siguientes. Cuentan las partidas terminadas.');
  }

  // ---------- Personalización ----------
  function personalizacion(p) {
    const tipos = ['avatar', 'banner', 'titulo', 'marco'], nombres = ['Avatares', 'Banners', 'Títulos', 'Marcos'];
    tipos.forEach((t, i) => {
      const activo = t === categoria, chip = N(p, i * 242, 0, 230, 52);
      chip.style.borderRadius = '8px'; chip.style.background = activo ? 'var(--accent)' : 'var(--panel)';
      T(chip, 20, 0, 150, 52, 'ft', 28, activo ? 'var(--accent-ink)' : 'var(--soft)', nombres[i], { mayus: true, esp: 6 });
      T(chip, 118, 0, 96, 52, 'fm', 18, activo ? 'var(--accent-ink)' : 'var(--mute)', `${LISTA[t].length}/${LISTA[t].length}`, { al: 'der' });
      if (!activo) clic(chip, () => { categoria = t; perfil(); }, nombres[i]);
    });
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

  // ---------- Emotes ----------
  function emotes(p) {
    const w = (1260 - 3 * 18) / 4;
    LUGARES.forEach((lugar, i) => {
      const e = estado.emotes[i], esElegido = espacio === i;
      const k = N(p, i * (w + 18), 0, w, 124, 'carta' + (esElegido ? ' elegida' : ''));
      k.style.background = esElegido ? 'var(--sel)' : 'var(--panel)';
      T(k, 18, 12, w - 36, 20, 'fr', 15, esElegido ? 'var(--accent)' : 'var(--mute)', `Espacio ${i + 1} · ${lugar}`, { mayus: true, esp: 14 });
      T(k, 18, 44, w - 148, 60, 'ft', 32, e >= 0 ? 'var(--ink)' : 'var(--mute)', e >= 0 ? EMOTES[e] : 'Vacío', { mayus: true, esp: 3, puntos: true });
      el('div', 'linea-borde', k);
      clic(k, () => { espacio = i; if (e >= 0) emoteVisto = e; perfil(); }, `Espacio ${i + 1}`);
      if (e >= 0) Boton(k, w - 118, 64, 100, 40, 'Quitar', 18, '', ev => {
        ev.stopPropagation(); estado.emotes[i] = -1; guardar(); avisar(`Espacio ${i + 1} vacío`); perfil();
      });
    });
    T(p, 0, 136, 1260, 26, 'fx', 19, 'var(--mute)', 'Elegí un espacio de la rueda y después el baile que va ahí. Mantené T en la partida para abrir la rueda.');
    const cw = (1260 - 3 * 18) / 4;
    EMOTES.forEach((n, i) => {
      const en = estado.emotes.indexOf(i);
      const k = N(p, (i % 4) * (cw + 18), 172 + Math.floor(i / 4) * 122, cw, 104, 'carta' + (emoteVisto === i ? ' elegida' : ''));
      const ini = N(k, 16, 16, 72, 72); ini.style.background = 'var(--chip)'; ini.style.borderRadius = '6px';
      T(ini, 0, 0, 72, 72, 'ft', 40, 'var(--soft)', n[0], { al: 'centro' });
      T(k, 104, 18, cw - 120, 40, 'ft', 30, 'var(--ink)', n, { mayus: true, esp: 3, puntos: true });
      T(k, 104, 60, cw - 120, 24, 'fr', 16, en >= 0 ? 'var(--ok)' : 'var(--mute)', en >= 0 ? `En el espacio ${en + 1}` : 'Sin equipar', { mayus: true, esp: 12 });
      el('div', 'linea-borde', k);
      clic(k, () => {
        emoteVisto = i;
        const antes = estado.emotes.indexOf(i);
        if (antes >= 0) estado.emotes[antes] = -1;
        estado.emotes[espacio] = i; guardar();
        avisar(`${n} en el espacio ${espacio + 1}`);
        perfil();
      }, n);
    });
    const v = N(p, 1300, 0, 500, 540, 'panel');
    T(v, 24, 18, 400, 22, 'fr', 17, 'var(--accent)', 'Vista previa', { mayus: true, esp: 16 });
    const caja = N(v, 62, 50, 376, 401); caja.style.borderRadius = '8px'; caja.style.background = 'radial-gradient(ellipse at 50% 40%,#232833,#0d1015)';
    const visto = emoteVisto >= 0 ? emoteVisto : estado.emotes[espacio];
    if (visto >= 0) {
      T(caja, 0, 0, 376, 401, 'ft', 180, 'rgba(242,154,56,.85)', EMOTES[visto][0], { al: 'centro' });
      T(v, 24, 452, 452, 50, 'ft', 40, 'var(--ink)', EMOTES[visto], { al: 'centro', mayus: true, esp: 3 });
      const en = estado.emotes.indexOf(visto);
      T(v, 24, 500, 452, 26, 'fx', 18, 'var(--mute)', en >= 0 ? `En el espacio ${en + 1} · ${LUGARES[en]}` : 'Sin equipar', { al: 'centro' });
    } else T(v, 24, 470, 452, 26, 'fx', 18, 'var(--mute)', 'Elegí un baile para verlo', { al: 'centro' });
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

  // ---------- Navegación y escala ----------
  function mostrar() { if (location.hash === '#perfil') perfil(); else menu(); }
  addEventListener('hashchange', mostrar);
  addEventListener('keydown', e => { if (e.key === 'Escape' && location.hash === '#perfil' && !E.querySelector('.modal')) volverAlMenu(); });
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
