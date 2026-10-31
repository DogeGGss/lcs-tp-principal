// Diseño de buscar partida (F28: US 274 a US 277) en la pantalla de elegir modo que ya está en el juego
// (ModeSelectScreen, con un andén elegido). «Buscar partida» es lo principal: grande, del color del modo y animado; la
// sala privada (crear sala y unirse con código) queda abajo, más chica. Mientras busca, el panel de la búsqueda
// reemplaza a los botones y a la derecha aparece el radar. Cada estado tiene su ancla (#anden, #buscando…) para la
// guía, y los botones llevan de un estado al otro. Todavía no está en el juego: es la propuesta para revisar.
(() => {
  const E = document.getElementById('escenario');
  const YO = 'Ulises', VERSION = 'v0.4.0';
  const MODO = {
    deathmatch: { rgb: '255 75 75', base: '#0c0606', n: 'Andén 2 · Online', t: 'Deathmatch', minimo: 2, nombre: 'Deathmatch',
      d: 'Todos contra todos. Elegís tus armas gratis, cada baja te cura 50 y reaparecés a los 3 s lejos de los rivales.',
      reglas: ['Hasta 8', '25 bajas', '8 min', 'Armas gratis'] },
    tactico: { rgb: '242 154 56', base: '#0d0a07', n: 'Andén 1 · Online', t: 'Táctico', minimo: 4, nombre: 'Táctico',
      d: 'Un equipo planta el dispositivo en A o B y el otro lo defiende. Sin reaparición: si morís, mirás a un compañero hasta la ronda siguiente.',
      reglas: ['4 vs 4', 'Primero a 7', 'Cambio de lado en la 6', 'Compra 20 s'] },
  };
  const OTROS = ['Nahue', 'Luka', 'David', 'Agus', 'Lucas', 'Nico', 'Mati'];
  let relojes = [], tiempoAviso;
  const ir = h => { location.hash = h; };
  const pausa = f => relojes.push(setInterval(f, 1000));
  const mmss = s => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;

  // ---------- La pantalla con el andén elegido ----------
  function pantalla(modo, esc, escA) {
    const m = MODO[modo];
    E.style.setProperty('--rgb', m.rgb); E.style.setProperty('--base', m.base);
    const trazos = modo === 'deathmatch'
      ? Array.from({ length: 6 }, (_, i) => `<i class="trazo" style="left:${1100 + (i * 137) % 700}px;top:${170 + (i * 211) % 700}px;animation-delay:${-i * 0.45}s"></i>`).join('') : '';
    E.innerHTML = `<div class="fondo"><div class="mapa"></div>${modo === 'tactico' ? '<div class="rejilla"></div>' : ''}<div class="luz"></div>${trazos}</div>
      <div id="radar"></div><div class="sombra"></div>
      <div class="esc" id="esc"><b>Esc</b>${esc}</div>
      <div class="detalle"><div class="n">${m.n}</div><div class="t">${m.t}</div><div class="d">${m.d}</div>
        <div class="reglas">${m.reglas.map(r => `<span>${r}</span>`).join('')}</div><div id="acciones"></div></div>
      <div class="aviso" id="aviso"></div>`;
    document.getElementById('esc').addEventListener('click', () => ir(escA));
    return m;
  }
  const acciones = html => { const a = document.getElementById('acciones'); a.innerHTML = html; return a; };
  function avisar(texto) {
    const a = document.getElementById('aviso'); if (!a) return;
    a.textContent = texto; a.classList.add('on');
    clearTimeout(tiempoAviso); tiempoAviso = setTimeout(() => a.classList.remove('on'), 2600);
  }
  // El radar de la derecha: un punto por cada jugador que ya está (vos, en el centro).
  function radar(jugadores, listo) {
    const r = document.getElementById('radar');
    const lugares = [[150, 210], [470, 160], [520, 430], [200, 470], [330, 90], [90, 340], [560, 290]];
    r.innerHTML = `<div class="radar${listo ? ' listo' : ''}">
      ${[640, 440, 240].map(d => `<div class="guia" style="left:${320 - d / 2}px;top:${320 - d / 2}px;width:${d}px;height:${d}px"></div>`).join('')}
      <div class="barrido"></div><div class="aro"></div><div class="aro"></div><div class="aro"></div><div class="centro"></div>
      ${jugadores.slice(1).map((j, i) => `<div class="pj" style="left:${lugares[i][0]}px;top:${lugares[i][1]}px"><span>${j}</span></div>`).join('')}</div>`;
  }
  const lugares = (jugadores, nuevo, vacio = 'Buscando…') => '<ul class="lugares">' + Array.from({ length: 8 }, (_, i) => {
    const j = jugadores[i];
    if (!j) return `<li class="libre"><i></i>${vacio}</li>`;
    return `<li class="${i === nuevo ? 'nuevo' : ''}"><i></i>${j}${i === 0 ? ' <small>(vos)</small>' : ''}</li>`;
  }).join('') + '</ul>';

  // ---------- Estados ----------
  // El andén: «Buscar partida» grande y, abajo, la sala privada (US 274, CA1 y CA2).
  function anden(modo, conCodigo) {
    const m = pantalla(modo, 'Volver a los modos', 'anden');
    const a = acciones(`
      <button class="buscar" id="buscar"><span class="icono"></span><span class="txt"><b>Buscar partida</b><small>${m.nombre} · con jugadores de tu región</small></span><span class="flecha">→</span></button>
      <div class="cuenta"><span class="ok"></span>Con tu cuenta <b>${YO}</b> · Sudamérica · misma versión (${VERSION})</div>
      <div class="privada"><div class="r">Sala privada · con amigos</div>
        <div class="fila"><button class="chico" id="crear">Crear sala</button><button class="chico" id="unirse">Unirse con código</button></div>
        <div class="codigo${conCodigo ? ' on' : ''}" id="codigo"><input maxlength="5" placeholder="KX7QM" aria-label="Código de sala" spellcheck="false"><button class="chico">Entrar</button></div></div>`);
    a.querySelector('#buscar').addEventListener('click', () => ir(modo === 'tactico' ? 'minimo' : 'buscando'));
    a.querySelector('#crear').addEventListener('click', () => ir('sala'));
    a.querySelector('#unirse').addEventListener('click', () => ir(conCodigo ? 'anden' : 'codigo'));
  }

  // Buscando (US 275): cuántos hay, el tiempo, los 8 lugares, la regla para arrancar y «Cancelar». Van entrando jugadores.
  function buscando() {
    const m = pantalla('deathmatch', 'Cancelar búsqueda', 'anden');
    const jugadores = [YO, 'Nahue', 'Luka'];
    let s = 23;
    function pintar(nuevo) {
      const a = acciones(`<div class="panel"><div class="barra"></div><div class="in">
        <div class="cab"><span class="e">Buscando partida</span><span class="reloj" id="reloj">${mmss(s)}</span></div>
        <div class="grande"><em>${jugadores.length}</em> / 8 jugadores</div>
        <div class="datos">${m.nombre} · Sudamérica · misma versión (${VERSION})</div>
        ${lugares(jugadores, nuevo)}
        <div class="pie"><p>Arranca sola con 8 jugadores, o con ${m.minimo} si pasan 45 s sin que entre nadie.</p><button class="chico" id="cancelar">Cancelar</button></div></div></div>`);
      a.querySelector('#cancelar').addEventListener('click', () => { ir('anden'); setTimeout(() => avisar('Búsqueda cancelada'), 50); });
      radar(jugadores, false);
    }
    pintar(-1);
    pausa(() => {
      s++; const r = document.getElementById('reloj'); if (r) r.textContent = mmss(s);
      if (s % 5 === 0 && jugadores.length < 5) { jugadores.push(OTROS[jugadores.length - 1]); pintar(jugadores.length - 1); }
    });
  }

  // Con el mínimo (US 276, CA2): si en 45 s no entra nadie más, arranca la cuenta regresiva.
  function minimo() {
    const m = pantalla('tactico', 'Cancelar búsqueda', 'anden-tactico');
    const jugadores = [YO, 'Agus', 'Lucas', 'Nico'];
    let s = 52, falta = 31;
    const a = acciones(`<div class="panel"><div class="barra"></div><div class="in">
      <div class="cab"><span class="e">Buscando partida</span><span class="reloj" id="reloj">${mmss(s)}</span></div>
      <div class="grande"><em>4</em> / 8 jugadores</div>
      <div class="datos">${m.nombre} · 2 contra 2 · Sudamérica · misma versión (${VERSION})</div>
      ${lugares(jugadores, -1)}
      <div class="pie"><div><p>Ya está el mínimo para ${m.nombre}. Si no entra nadie más, arranca en <b id="falta">0:${falta}</b></p>
        <div class="progreso"><i id="barra" style="width:${(45 - falta) / 45 * 100}%"></i></div></div><button class="chico" id="cancelar">Cancelar</button></div></div></div>`);
    a.querySelector('#cancelar').addEventListener('click', () => ir('anden-tactico'));
    radar(jugadores, false);
    pausa(() => {
      s++; falta = Math.max(0, falta - 1);
      document.getElementById('reloj').textContent = mmss(s);
      document.getElementById('falta').textContent = mmss(falta);
      document.getElementById('barra').style.width = (45 - falta) / 45 * 100 + '%';
      if (falta === 0) ir('encontrada');
    });
  }

  // Partida encontrada (US 276, CA1 y CA3): la cuenta regresiva de 10 s; se puede cancelar hasta el final.
  function encontrada() {
    const m = pantalla('deathmatch', 'Cancelar', 'anden');
    const jugadores = [YO, 'Nahue', 'Luka', 'David', 'Agus', 'Lucas'];
    let s = 7;
    const a = acciones(`<div class="panel"><div class="barra"></div><div class="in">
      <div class="cab"><span class="e">Partida encontrada</span><span class="reloj">6 / 8</span></div>
      <div class="grande" id="cuenta">Empieza en <em>${s}</em></div>
      <div class="datos">${m.nombre} · Colonia ARA San Juan · Sudamérica</div>
      ${lugares(jugadores, -1)}
      <div class="pie"><p>Si entra alguien más, se suma. Si sale alguien y quedan menos que el mínimo, sigue buscando.</p><button class="chico" id="cancelar">Salir</button></div></div></div>`);
    a.querySelector('#cancelar').addEventListener('click', () => ir('anden'));
    radar(jugadores, true);
    pausa(() => { s = Math.max(0, s - 1); document.getElementById('cuenta').innerHTML = s ? `Empieza en <em>${s}</em>` : 'Cargando el mapa…'; });
  }

  // Sin conexión o con una versión vieja (US 275, CA3): el panel en rojo, sin perder nada.
  function error(version) {
    pantalla('deathmatch', 'Volver', 'anden');
    const a = acciones(`<div class="panel mal"><div class="barra"></div><div class="in">
      <div class="cab"><span class="e">Buscar partida</span></div>
      <div class="grande">${version ? 'Hay una versión nueva' : 'Se cortó la conexión'}</div>
      <div class="texto">${version
        ? 'Solo se juntan jugadores con la misma versión del juego. Actualizá desde el launcher y volvé a buscar.'
        : 'Revisá tu internet y volvé a buscar. No perdiste nada: tu progreso está guardado en tu cuenta.'}</div>
      <div class="botones"><button class="chico color" id="ok">${version ? 'Abrir el launcher' : 'Volver a buscar'}</button><button class="chico" id="volver">Volver</button></div></div></div>`);
    a.querySelector('#ok').addEventListener('click', () => version ? avisar('Se abre el launcher y se cierra el juego') : ir('buscando'));
    a.querySelector('#volver').addEventListener('click', () => ir('anden'));
  }

  // La sala privada abierta, como hoy (US 026 y US 027), en el lugar de los botones.
  function sala() {
    pantalla('deathmatch', 'Salir de la sala', 'anden');
    const a = acciones(`<div class="panel sala"><div class="in">
      <div class="cab"><span class="e">Sala privada</span><span class="rc">KX7QM</span><button class="chico" style="padding:10px 18px;font-size:20px">Copiar</button></div>
      <div class="datos" style="margin-top:12px">Pasales el código a tus amigos · 2 / 8 jugadores</div>
      ${lugares([YO, 'Nahue'], -1, 'Libre')}
      <div class="pie"><p>Mapa: Colonia ARA San Juan · lo cambia el anfitrión. Desde 2 jugadores podés iniciar.</p><button class="chico color">Iniciar partida</button></div></div></div>`);
  }

  const ESTADOS = {
    'anden': () => anden('deathmatch', false),
    'anden-tactico': () => anden('tactico', false),
    'codigo': () => anden('deathmatch', true),
    'buscando': buscando,
    'minimo': minimo,
    'encontrada': encontrada,
    'sin-conexion': () => error(false),
    'version': () => error(true),
    'sala': sala,
  };
  function mostrar() {
    relojes.forEach(clearInterval); relojes = [];
    (ESTADOS[location.hash.slice(1)] || ESTADOS.anden)();
  }
  addEventListener('hashchange', mostrar);
  addEventListener('keydown', e => { if (e.key === 'Escape') document.getElementById('esc')?.click(); });
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
