// Diseño de la cuenta en el launcher (F18: US 237, US 269 a US 273 y US 282). El launcher funciona como un cliente:
// sin una sesión iniciada, el panel de la cuenta tapa todo menos la barra de arriba (como Ajustes) y JUGAR no se puede
// tocar; con la cuenta, la barra muestra la cuenta y su menú. Cada estado tiene su ancla (#entrar, #crear, #listo…)
// para que la guía los muestre, y los botones llevan de un estado al siguiente. Todavía no está en el launcher.
(() => {
  const E = document.getElementById('escenario');
  const MAIL = 'ulises.fonseca@gmail.com', NOMBRE = 'Ulises', VERSION = '0.3.0';
  const ir = h => { location.hash = h; };
  let aviso = '', relojes = [];

  // ---------- La ventana del launcher (MainWindow.xaml) ----------
  const RIELES = `<svg viewBox="0 0 680 240" preserveAspectRatio="none" aria-hidden="true">
    <path d="M 0,240 L 365,75 L 680,240 M 80,240 L 365,75 L 600,240 M 180,240 L 365,75 L 500,240 M 0,216 L 680,216 M 85,182 L 608,182 M 180,147 L 541,147 M 260,116 L 457,116" stroke="#41464A" fill="none"/>
    <path d="M 295,102 L 295,18 L 315,18 L 315,92 M 407,98 L 407,18 L 428,18 L 428,111 M 210,142 L 210,35 L 288,35 L 288,103 M 220,142 L 220,47 L 277,47 L 277,108 M 461,127 L 461,30 L 590,30 L 590,197 M 473,137 L 473,43 L 578,43 L 578,191" stroke="#64717B" stroke-width="2" fill="none"/>
    <path d="M 160,240 L 300,150 L 330,152 L 520,60" stroke="#F29A38" stroke-width="2" fill="none" opacity=".55"/>
    <circle cx="213" cy="36" r="3" fill="#F29A38"/><circle cx="464" cy="31" r="3" fill="#4CC3FF"/></svg>`;
  const TRAZOS = `<svg class="trazos" viewBox="0 0 1120 700" aria-hidden="true">
    <defs><filter id="brillo" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="4" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter></defs>
    <polygon points="30.5,0.5 1107.5,0.5 1119.5,12.5 1119.5,669.5 1089.5,699.5 12.5,699.5 0.5,687.5 0.5,30.5" fill="none" stroke="#343B47"/>
    <g stroke="#F29A38" stroke-width="2" fill="none" filter="url(#brillo)"><path d="M 0.5,118 L 0.5,30.5 L 30.5,0.5 L 236,0.5"/><path d="M 884,699.5 L 1089.5,699.5 L 1119.5,669.5 L 1119.5,582"/></g>
    <g fill="#F29A38"><rect x="234" y="-2" width="5" height="5"/><rect x="-2" y="116" width="5" height="5"/><rect x="879" y="697" width="5" height="5"/><rect x="1117" y="577" width="5" height="5"/></g>
    <path d="M 1060,0.5 L 1107.5,0.5 L 1119.5,12.5 L 1119.5,60" stroke="#4CC3FF" stroke-width="1.5" fill="none" opacity=".8"/>
    <path d="M 60,699.5 L 12.5,699.5 L 0.5,687.5 L 0.5,640" stroke="#8E96A3" stroke-width="1.5" fill="none" opacity=".6"/></svg>`;
  const RAYO = '<svg width="30" height="36" viewBox="0 0 30 36" aria-hidden="true"><path d="M 16,0 L 3,19 L 13,19 L 7,35 L 29,12 L 18,12 L 24,0 Z" fill="#F29A38"/></svg>';

  // conCuenta: la barra muestra la cuenta. estado: el texto de la tarjeta de la derecha.
  function ventana({ conCuenta, menu, sinConexion, estado }) {
    E.innerHTML = `<div class="sombra"><div class="marco">
      <div class="barra">
        <div class="logo">${RAYO}<span class="d">RIFTWALKER</span><span class="canal">ESTABLE</span></div>
        <div class="derecha">
          ${conCuenta ? `<div class="chip${menu ? ' abierto' : ''}" id="chip" role="button" tabindex="0" aria-label="Tu cuenta">
            <div class="inicial d">${NOMBRE[0]}</div><div><div class="nombre d">${NOMBRE.toUpperCase()}</div><div class="mail">${MAIL}</div></div><span class="flecha">⌄</span></div>` : ''}
          <button class="btn plano">GUÍA DEL JUEGO ↗</button>
          <button class="btn">⚙&nbsp; AJUSTES</button>
          <div class="sep"></div>
          <div class="ventana" title="Minimizar"><svg width="12" height="2"><path d="M0,1 L12,1" stroke="currentColor" stroke-width="1.5"/></svg></div>
          <div class="ventana cerrar" title="Cerrar"><svg width="11" height="11"><path d="M0,0 L11,11 M11,0 L0,11" stroke="currentColor" stroke-width="1.5"/></svg></div>
        </div>
      </div>
      <div class="banner">${RIELES}<div class="tapa"></div>
        <div class="textos"><div class="ante">EL PRÓXIMO CRUCE ES TUYO</div><div class="p1 d">PROJECT</div><div class="p2 d">RIFTWALKER</div>
        <div class="lema">Elegí tu equipo. Prepará tu próxima partida.</div></div>
        <div class="punto">01 / PUNTO DE ENCUENTRO</div></div>
      <div class="notas-col"><h2>NOVEDADES DEL ANDÉN</h2><div class="sub">Lo nuevo, antes de entrar.</div><div class="combo">v${VERSION} · Estable</div>
        <div class="notas"><h3>Tu cuenta, en el launcher</h3><p>Entrás una vez y el juego abre con tu cuenta: tu progreso queda en la nube y lo seguís en cualquier PC.</p>
          <h4>Qué trae</h4><ul><li>Cuentas con mail y contraseña.</li><li>Buscar partida en Táctico y Deathmatch.</li><li>El ranking, solo con cuentas.</li></ul></div></div>
      <div class="partida"><div class="ante">TU PRÓXIMA PARTIDA</div><div class="titulo d">LISTO PARA ENTRAR</div>
        <div class="inst">Versión ${VERSION} · ${sinConexion ? 'Instalada' : 'Actualizado'}</div><hr>
        <div class="estado">${estado}</div>
        <div class="abajo"><div class="progreso"><i></i></div><div class="progreso-texto">${sinConexion ? 'Sin conexión' : 'Todo al día'}</div>
          <button class="jugar d" id="jugar"${conCuenta ? '' : ' disabled'}>JUGAR →</button>
          <div class="reintentar">${sinConexion ? '↻ Reintentar' : '&nbsp;'}</div></div></div>
      <div class="pie"><span class="pto" style="background:${sinConexion ? 'var(--bad)' : 'var(--ok)'}"></span>${sinConexion ? 'SIN CONEXIÓN' : 'CONECTADO'}<span class="ver">PROJECT RIFTWALKER / LAUNCHER 0.2.0</span></div>
      <div id="extra"></div>
    </div></div>${TRAZOS}`;
    const chip = document.getElementById('chip');
    if (chip) chip.addEventListener('click', () => ir(menu ? 'listo' : 'menu'));
    const jugar = document.getElementById('jugar');
    if (conCuenta) jugar.addEventListener('click', () => { aviso = 'Abriendo Project Riftwalker con tu cuenta…'; mostrar(); });
    if (menu) {
      const m = document.createElement('div'); m.className = 'menu';
      m.innerHTML = `<div class="cab"><b>${MAIL}</b><small>Cuenta confirmada · desde el 29/10/2026</small></div>
        <div class="item" data-ir="privacidad">Aviso de privacidad</div><div class="item" data-ir="cerrar">Cerrar sesión</div><hr>
        <div class="item rojo" data-ir="borrar">Borrar cuenta</div>`;
      m.style.left = (chip.offsetLeft + chip.offsetWidth - 300) + 'px';
      document.getElementById('extra').appendChild(m);
      m.querySelectorAll('[data-ir]').forEach(i => i.addEventListener('click', () => ir(i.dataset.ir)));
    }
  }
  // El panel de la cuenta sobre la ventana: una tarjeta como la de Ajustes.
  function capa(html, peligro) {
    const c = document.createElement('div'); c.className = 'capa';
    c.innerHTML = `<div class="tarjeta${peligro ? ' peligro' : ''}">${html}</div>`;
    document.getElementById('extra').appendChild(c);
    c.querySelectorAll('[data-ir]').forEach(b => b.addEventListener('click', () => ir(b.dataset.ir)));
    return c;
  }
  const campo = (rotulo, valor, o = {}) => `<div class="campo${o.error ? ' mal' : ''}"><div class="rotulo">${rotulo}${o.ayuda ? `<small>${o.ayuda}</small>` : ''}</div>
    <input type="${o.clave ? 'password' : 'text'}" value="${valor}" placeholder="${o.ejemplo || ''}" aria-label="${rotulo}">${o.clave ? '<span class="ver">VER</span>' : ''}${o.error ? `<div class="error">${o.error}</div>` : ''}</div>`;
  const cifras = (n, activo) => '<div class="cifras">' + Array.from({ length: 6 }, (_, i) => `<div class="${i === activo ? 'activo' : ''}">${n[i] || ''}</div>`).join('') + '</div>';
  const sinCuenta = () => ventana({ conCuenta: false, estado: 'Iniciá sesión para jugar.' });
  const conCuenta = (o = {}) => ventana({ conCuenta: true, estado: aviso || `Entrás como <b>${NOMBRE}</b>. Tu progreso está en la cuenta.`, ...o });
  function despues(texto, estado) { aviso = texto; ir(estado); }

  // ---------- Estados ----------
  function entrar(conError) {
    sinCuenta();
    capa(`<div class="ante">TU CUENTA</div><h1 class="d">INICIÁ SESIÓN</h1>
      <div class="ayuda">Para jugar hace falta una cuenta. Tu progreso queda en ella y lo seguís en cualquier PC.</div>
      ${conError ? '<div class="franja">El mail o la contraseña no coinciden.</div>' : ''}
      ${campo('MAIL', MAIL, { ejemplo: 'tu@mail.com' })}${campo('CONTRASEÑA', conError ? 'riftwalker' : 'riftwalkr', { clave: true })}
      <div class="fila" style="margin:-4px 0 14px"><span></span><span class="enlace" data-ir="recuperar">¿Te olvidaste la contraseña?</span></div>
      <button class="principal d" id="ok">ENTRAR →</button>
      <div class="nota">¿No tenés cuenta? <span class="enlace" data-ir="crear">Crear cuenta</span></div>`);
    document.getElementById('ok').addEventListener('click', () => conError ? despues('Entraste a tu cuenta.', 'listo') : ir('entrar-error'));
  }
  function crear(conError) {
    sinCuenta();
    capa(`<div class="ante">CUENTA NUEVA</div><h1 class="d">CREAR CUENTA</h1>
      <div class="ayuda">La cuenta arranca de cero: con ella guardás tu progreso en la nube y jugás en cualquier PC.</div>
      ${campo('MAIL', conError ? MAIL : '', { ejemplo: 'tu@mail.com', error: conError ? 'Ese mail ya tiene una cuenta. <span class="enlace" data-ir="entrar">Iniciar sesión</span>' : '' })}
      ${campo('CONTRASEÑA', 'riftwalker', { clave: true, ayuda: '8 caracteres o más' })}${campo('REPETIR LA CONTRASEÑA', 'riftwalker', { clave: true })}
      <div class="legal">Al crear la cuenta aceptás el <span class="enlace" data-ir="privacidad">aviso de privacidad</span>.</div>
      <div class="botones"><button class="btn" data-ir="entrar">Volver</button><button class="btn lleno" id="ok">CREAR CUENTA</button></div>`);
    document.getElementById('ok').addEventListener('click', () => ir(conError ? 'confirmar' : 'crear-error'));
  }
  function confirmar() {
    sinCuenta();
    capa(`<div class="ante">TU CUENTA · FALTA UN PASO</div><h1 class="d">CONFIRMÁ TU MAIL</h1>
      <div class="ayuda">Te mandamos un código de 6 números a <b>${MAIL}</b>. Si no lo ves, revisá el correo no deseado.</div>
      ${cifras(['4', '8', '1'], 3)}
      <div class="fila" style="margin:4px 0 2px"><span id="reenviar" style="color:var(--mute)"></span></div>
      <div class="fila" style="margin:0 0 16px;color:var(--mute)"><span>¿Te equivocaste de mail? <span class="enlace" data-ir="crear">Cambialo</span></span></div>
      <button class="principal d" id="ok">CONFIRMAR</button>`);
    const r = document.getElementById('reenviar'); let s = 42;
    const pintar = () => { r.innerHTML = s ? `¿No te llegó? Podés pedir otro en 0:${String(s).padStart(2, '0')}` : '¿No te llegó? <span class="enlace">Pedí otro código</span>'; };
    pintar(); relojes.push(setInterval(() => { s = Math.max(0, s - 1); pintar(); }, 1000));
    document.getElementById('ok').addEventListener('click', () => despues('Listo: tu cuenta está activa.', 'listo'));
  }
  function recuperar(paso) {
    sinCuenta();
    if (paso === 1) {
      capa(`<div class="ante">TU CUENTA</div><h1 class="d">RECUPERAR LA CONTRASEÑA</h1>
        <div class="ayuda">Escribí tu mail y te mandamos un código para elegir una contraseña nueva.</div>${campo('MAIL', MAIL)}
        <div class="botones"><button class="btn" data-ir="entrar">Volver</button><button class="btn lleno" data-ir="recuperar-codigo">MANDAR CÓDIGO</button></div>`);
      return;
    }
    capa(`<div class="ante">TU CUENTA</div><h1 class="d">CONTRASEÑA NUEVA</h1>
      <div class="ayuda">Si hay una cuenta con ese mail, te llegó un código de 6 números.</div>
      <div class="rotulo">CÓDIGO</div>${cifras(['2', '0', '7', '3', '9', '1'], -1)}
      ${campo('CONTRASEÑA NUEVA', 'nuevaclave', { clave: true, ayuda: '8 caracteres o más' })}${campo('REPETIR LA CONTRASEÑA', 'nuevaclave', { clave: true })}
      <div class="botones" style="margin-top:6px"><button class="btn" data-ir="entrar">Volver</button><button class="btn lleno" id="ok">CAMBIAR Y ENTRAR</button></div>`);
    document.getElementById('ok').addEventListener('click', () => despues('Contraseña cambiada. Entraste a tu cuenta.', 'listo'));
  }
  function cerrar(juegoAbierto) {
    conCuenta();
    if (juegoAbierto) {
      capa(`<div class="ante">TU CUENTA</div><h1 class="d">CERRÁ EL JUEGO PRIMERO</h1>
        <div class="ayuda">Project Riftwalker está abierto con tu cuenta. Cerralo y volvé a tocar «Cerrar sesión».</div>
        <div class="botones"><button class="btn lleno" data-ir="listo">ENTENDIDO</button></div>`);
      return;
    }
    capa(`<div class="ante">TU CUENTA</div><h1 class="d">¿CERRAR SESIÓN?</h1>
      <div class="ayuda">En esta PC se deja de usar tu cuenta, y para jugar hay que volver a iniciar sesión. Tu progreso queda en la cuenta y vuelve cuando entres de nuevo.</div>
      <div class="botones"><button class="btn" data-ir="listo">Volver</button><button class="btn lleno" id="ok">CERRAR SESIÓN</button></div>`);
    document.getElementById('ok').addEventListener('click', () => ir('entrar'));
  }
  function borrar() {
    conCuenta();
    capa(`<div class="ante">ESTO NO SE PUEDE DESHACER</div><h1 class="d">BORRAR CUENTA</h1>
      <div class="ayuda">Se borran tu cuenta, su progreso, sus monedas, sus cosméticos y tu lugar en el ranking. El mail queda libre para crear otra cuenta.</div>
      ${campo('TU CONTRASEÑA, PARA CONFIRMAR', 'riftwalker', { clave: true })}
      <div class="botones"><button class="btn" data-ir="listo">Volver</button><button class="btn rojo" id="ok">BORRAR CUENTA</button></div>`, true);
    document.getElementById('ok').addEventListener('click', () => ir('entrar'));
  }
  function privacidad() {
    conCuenta();
    capa(`<div class="ante">PRIVACIDAD</div><h1 class="d">AVISO DE PRIVACIDAD</h1><div class="priv">
      <h3>QUÉ GUARDAMOS</h3><p>Tu mail y tu contraseña (cifrada), tu nombre, tu progreso (logros, cosméticos y monedas), tus estadísticas de partida y las opiniones que mandes.</p>
      <h3>PARA QUÉ</h3><p>Para que recuperes tu progreso en cualquier PC, para el ranking global y para buscar partida. No lo usamos para publicidad ni lo compartimos con nadie.</p>
      <h3>DÓNDE</h3><p>En la base del juego (Supabase). Las partidas pasan por los servidores de Photon, que reciben tu nombre de jugador, tu id de cuenta y tu conexión (IP).</p>
      <h3>TUS DERECHOS</h3><p>Podés ver tus datos en el Perfil del juego y borrar la cuenta con todo lo que tiene desde este launcher (Ley 25.326 de Protección de Datos Personales).</p></div>
      <div class="botones"><button class="btn lleno" data-ir="listo">ENTENDIDO</button></div>`);
  }

  const ESTADOS = {
    'entrar': () => entrar(false),
    'entrar-error': () => entrar(true),
    'crear': () => crear(false),
    'crear-error': () => crear(true),
    'confirmar': confirmar,
    'recuperar': () => recuperar(1),
    'recuperar-codigo': () => recuperar(2),
    'listo': () => conCuenta(),
    'menu': () => conCuenta({ menu: true }),
    'cerrar': () => cerrar(false),
    'cerrar-juego': () => cerrar(true),
    'borrar': borrar,
    'privacidad': privacidad,
    'sin-conexion': () => ventana({ conCuenta: true, sinConexion: true, estado: 'No se pudo buscar actualizaciones. Podés jugar con tu cuenta: lo que juegues se sube cuando vuelva la conexión.' }),
  };
  function mostrar() {
    relojes.forEach(clearInterval); relojes = [];
    const h = location.hash.slice(1);
    (ESTADOS[h] || ESTADOS.entrar)();
    if (h !== 'listo') aviso = '';
  }
  addEventListener('hashchange', mostrar);
  addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    const h = location.hash.slice(1);
    if (['menu', 'cerrar', 'cerrar-juego', 'borrar', 'privacidad'].includes(h)) ir('listo');
    else if (['crear', 'crear-error', 'recuperar', 'recuperar-codigo', 'confirmar'].includes(h)) ir('entrar');
  });
  function escalar() {
    const k = Math.min(innerWidth / 1144, innerHeight / 724);
    E.style.transform = `scale(${k})`;
    E.style.left = (innerWidth - 1144 * k) / 2 + 'px';
    E.style.top = (innerHeight - 724 * k) / 2 + 'px';
  }
  addEventListener('resize', escalar);
  escalar();
  mostrar();
})();
