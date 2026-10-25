// Menú principal, Perfil y Estación del Rift de Project Riftwalker, como están en el juego (MenuRiftwalker, PantallaPerfil
// y PantallaEstacion). Las posiciones son las de Unity, en una pantalla de 1920 × 1080. En el Perfil todo está
// desbloqueado para probar combinaciones; la Estación arranca como un perfil nuevo para que las cápsulas traigan cosas nuevas.
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
  const CLAVE = 'riftwalker-menu-web';

  // Cápsulas: las reglas de ejemplo de PerfilDatos. Las monedas y lo que salió no se guardan: cada vez que se abre la
  // página arranca como el perfil nuevo del juego (2.450 créditos, 3 boletos y 120 cristales). F9 suma, como en el juego.
  const PROB = [0, 55, 30, 12, 3], PROB_TIPO = [35, 25, 25, 15], GARANTIA = 10; // por rareza; avatar, banner, título, marco
  const POR_REPETIDO = [0, 50, 120, 300, 800], CREDITOS_POR_BOLETO = 600, CRISTALES_POR_BOLETO = 100, CRISTALES_POR_DIEZ = 900;
  const TIPOS = ['avatar', 'banner', 'titulo', 'marco'], PREFIJO = { avatar: 'av:', banner: 'bn:', titulo: 'ti:', marco: 'mc:' };
  // El catálogo en el orden del juego: avatares, banners, marcos y títulos.
  const CAT = [['avatar', C.avatares], ['banner', C.banners], ['marco', C.marcos], ['titulo', C.titulos]].flatMap(([tipo, l]) => l.map(c => ({ ...c, tipo })));
  const rift = { creditos: 2450, boletos: 3, cristales: 120, obtenidos: [], sinEpico: 0, aperturas: 0, recientes: [] };
  const clave = c => PREFIJO[c.tipo] + c.id;
  const tiene = c => c.rareza === 0 || rift.obtenidos.includes(clave(c));
  const desdeClave = k => CAT.find(c => clave(c) === k);

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

    const tarjeta = clic(N(capa, 80, 900, 580, 132, 'panel borde-hover'), abrirEstacion, 'Abrir la Estación del Rift');
    const lt = N(tarjeta, 0, 0, 580, 3); lt.style.background = 'rgba(242,154,56,.8)';
    capsula(tarjeta, 30, 18);
    T(tarjeta, 128, 18, 420, 20, 'fr', 16, 'var(--accent)', '01 / Cápsulas', { mayus: true, esp: 12 });
    T(tarjeta, 126, 38, 430, 46, 'ft', 42, 'var(--ink)', 'Estación del Rift', { mayus: true, esp: 3 });
    T(tarjeta, 128, 86, 420, 26, 'fx', 19, 'var(--mute)', rift.boletos === 1 ? 'Tenés 1 boleto para abrir' : `Tenés ${rift.boletos} boletos para abrir`);

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
    pararFondos();
    if (est) est.vivo = false;
    capa.innerHTML = ''; capa.style.background = '#0b0d11';
    est = { vivo: true, estado: 'espera', t: 0, resultados: null, mostrados: false, amague: false, colorFinal: ACC, colorAmague: ACC,
      luzC: HOT, tapaC: ACC, brilloC: ACC, antes: null, canjes: [] };
    const volver = clic(N(capa, 60, 34, 150, 52, 'panel borde-hover'), volverAlMenu, 'Volver al menú');
    T(volver, 0, 0, 150, 52, 'ft', 26, 'var(--ink)', '« Menú', { mayus: true, esp: 6, al: 'centro' });
    T(capa, 240, 34, 400, 20, 'fr', 17, 'var(--accent)', 'Suministros del otro lado', { mayus: true, esp: 16 });
    T(capa, 238, 52, 600, 46, 'ft', 44, 'var(--ink)', 'Estación del Rift', { mayus: true, esp: 3 });
    est.monedas = N(capa, 1100, 0, 760, 120);
    escenaEstacion(est);
    accionesEstacion(est);
    columnaEstacion(est);
    leyenda('Un boleto, una cápsula. Cada una trae un avatar, un banner, un título o un marco.');
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
    T(canje, 24, 252, 540, 26, 'fr', 18, 'var(--cristales)', '¿Sin cristales? Ir a la tienda »', { mayus: true, esp: 8 });

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
  // Dentro de la rareza, primero el tipo (si no, casi todo serían títulos) y después el objeto.
  function sortearTipo(r) {
    const hay = t => CAT.some(c => c.rareza === r && c.tipo === TIPOS[t]);
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
    estado[c.tipo] = c.id; guardar();
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
    T(k, 20, 418, 340, 22, 'fr', 16, 'var(--mute)', TIPO[c.tipo] + (c.estilo ? ' · ' + c.estilo : ''), { al: 'centro', mayus: true, esp: 14 });
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
  function arte(k, c, x, y, lado) {
    if (c.tipo === 'avatar') { Img(k, srcAvatar(c.id), x, y, lado, lado); return; }
    const fondo = N(k, x, y, lado, lado); fondo.style.background = '#151922'; fondo.style.borderRadius = '6px';
    if (c.tipo === 'banner') Img(k, srcBanner(c.id), x, y + lado / 3, lado, lado / 3);
    else if (c.tipo === 'marco') {
      // Sobre el banner que tenés equipado, como queda en la cabecera del Perfil.
      const alto = lado / 3;
      bannerEnmarcado(N(k, x, y + (lado - alto) / 2, lado, alto), lado, alto, estado.banner, c.id, true);
    } else {
      const t = T(k, x + 12, y, lado - 24, lado, 'ft', lado / 6, COLOR[c.rareza], c.nombre, { al: 'centro', mayus: true, esp: 2 });
      t.style.whiteSpace = 'normal';
      ajustar(t, lado / 6, lado / 12);
    }
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
    s.equipar.style.display = s.estado === 'revelado' && r && r.length === 1 && estado[r[0].c.tipo] !== r[0].c.id ? '' : 'none';
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
    T(caja, 48, 118, 1064, 74, 'fx', 20, 'var(--mute)', `Primero sale la rareza (con estas probabilidades), después el tipo (${PROB_TIPO[0]} % avatar, ` +
      `${PROB_TIPO[1]} % banner, ${PROB_TIPO[2]} % título, ${PROB_TIPO[3]} % marco) y por último un objeto al azar. Cada ${GARANTIA} cápsulas ` +
      'hay uno épico o mejor asegurado. Los repetidos se convierten en créditos.', { arriba: true });
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

  // DEBUG (F9, en el menú y en sus pantallas, como en el juego): suma de las tres monedas.
  function sumarDebug() {
    rift.creditos += 1000; rift.boletos += 10; rift.cristales += 500;
    avisar('Debug: +1.000 créditos, +10 boletos y +500 cristales');
    if (location.hash === '#estacion') refrescarEstacion(); else mostrar();
  }

  // ---------- Navegación y escala ----------
  function mostrar() {
    if (est && location.hash !== '#estacion') est.vivo = false;
    if (location.hash === '#perfil') perfil(); else if (location.hash === '#estacion') estacion(); else menu();
  }
  addEventListener('hashchange', mostrar);
  addEventListener('keydown', e => {
    if (e.key === 'F9') { e.preventDefault(); sumarDebug(); return; }
    if (e.key !== 'Escape') return;
    const modal = E.querySelector('.modal');
    if (modal) { if (location.hash === '#estacion') modal.remove(); return; }
    if (location.hash === '#estacion' && est && est.estado === 'abriendo') { saltar(); return; }
    if (location.hash === '#perfil' || location.hash === '#estacion') volverAlMenu();
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
