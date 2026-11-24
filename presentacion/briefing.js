// Briefing de los modos (diapositiva «Modos de juego» de la presentación). En la demo de la selección de modo
// (?demo=modos&briefing=1), «Buscar partida» no abre la búsqueda: encuentra partida enseguida y muestra cómo se juega
// el modo, paso a paso sobre su mapa, para hablar por encima. Los números son los del juego (Sprint 5):
// RondasTacticas, DispositivoTactico, PartidaDeathmatch, ConfigInvasion, CooperativoDeInvasion y CaidaEnInvasion.
// Pasos: se pasan a mano, con ← y → (o con las flechas de abajo, Espacio, Enter o Av Pág / Re Pág); Esc vuelve a los andenes.
// En pantalla va solo el título de cada paso; `txt` es lo que se dice (está también en la nota de la diapositiva, content.js).
// Cada escena termina quieta o en un ciclo, así el paso puede quedar en pantalla lo que dure la explicación.
// Un modo con `img` se dibuja sobre su mapa (SVG); Invasión usa en cambio un video del juego por paso (`video`, en bucle).
(() => {
  const stage = document.getElementById('msStage'), inner = document.getElementById('msi');
  if (!stage || !inner) return;
  const NS = 'http://www.w3.org/2000/svg';

  const css = `
  .brf{position:absolute;inset:0;z-index:50;background:#07080a;color:#fff;opacity:0;transition:opacity .35s ease;font-family:var(--body)}
  .brf.on{opacity:1}
  .brf-busca{position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:22px;transition:opacity .4s ease}
  .brf-busca.fuera{opacity:0;pointer-events:none}
  .brf-radar{position:relative;width:360px;height:360px}
  .brf-radar i{position:absolute;inset:0;border-radius:50%;border:3px solid var(--c);opacity:0}
  .brf-radar b{position:absolute;left:50%;top:50%;width:28px;height:28px;margin:-14px;border-radius:50%;background:var(--c);box-shadow:0 0 40px var(--c)}
  .brf-busca .e{font:600 26px/1 var(--display);letter-spacing:.32em;text-transform:uppercase;color:var(--c)}
  .brf-busca .g{font:700 96px/1 var(--display);text-transform:uppercase}
  .brf-busca .s{font:500 30px/1 var(--body);color:rgba(255,255,255,.75)}
  .brf-main{position:absolute;inset:0;opacity:0;transition:opacity .5s ease}
  .brf-main.on{opacity:1}
  .brf-izq{position:absolute;left:90px;top:120px;width:560px;display:flex;flex-direction:column;gap:18px}
  .brf-eb{font:600 24px/1 var(--display);letter-spacing:.32em;text-transform:uppercase;color:var(--c)}
  .brf-modo{font:700 104px/.86 var(--display);text-transform:uppercase}
  .brf-sub{font:500 26px/1.2 var(--body);color:rgba(255,255,255,.7)}
  .brf-paso{margin-top:34px;font:600 22px/1 var(--mono);color:rgba(255,255,255,.55)}
  .brf-tit{font:700 80px/.95 var(--display);text-transform:uppercase;margin:0;min-height:152px;text-wrap:balance}
  .brf-dots{display:flex;gap:12px;margin-top:6px}
  .brf-dots i{width:46px;height:8px;border-radius:4px;background:rgba(255,255,255,.18)}
  .brf-dots i.on{background:var(--c)}
  .brf-der{position:absolute;left:700px;top:90px;width:1150px;height:820px;border-radius:14px;overflow:hidden;background:#0b0e13;box-shadow:0 0 0 2px rgba(255,255,255,.08)}
  .brf-der svg{position:absolute;inset:0;width:100%;height:100%}
  .brf-hud{position:absolute;inset:0;pointer-events:none;font-family:var(--display)}
  .brf-chip{position:absolute;padding:10px 18px;border-radius:8px;background:rgba(7,8,10,.82);box-shadow:0 0 0 2px rgba(255,255,255,.12);font:700 34px/1 var(--display);letter-spacing:.06em;text-transform:uppercase;white-space:nowrap}
  .brf-chip small{display:block;font:600 18px/1 var(--display);letter-spacing:.22em;color:var(--c);margin-bottom:6px}
  .brf-feed{position:absolute;right:22px;top:22px;display:flex;flex-direction:column;gap:8px;align-items:flex-end}
  .brf-feed div{padding:8px 14px;border-radius:6px;background:rgba(7,8,10,.82);font:600 26px/1 var(--display);letter-spacing:.04em;text-transform:uppercase}
  .brf-feed b{color:var(--c)}
  .brf-video{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:0;transition:opacity .3s ease}
  .brf-video.on{opacity:1}
  .brf-lista{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:30px}
  .brf-lista h4{grid-column:1/-1;margin:0;font:700 30px/1 var(--display);letter-spacing:.12em;text-transform:uppercase}
  .brf-fila{display:flex;flex-direction:column;gap:7px;padding:14px;border-radius:10px;background:#121722;border-top:7px solid var(--r)}
  .brf-fila small{font:600 14px/1 var(--display);letter-spacing:.18em;text-transform:uppercase;color:var(--r)}
  .brf-fila b{font:700 26px/1 var(--display);text-transform:uppercase}
  .brf-fila span{font:500 17px/1.3 var(--body);color:rgba(255,255,255,.8)}
  .brf-lista>*{opacity:0;transform:translateX(-14px);transition:opacity .4s ease,transform .4s ease}
  .brf-lista>.on{opacity:1;transform:none}
  .brf-armas{position:absolute;left:24px;right:24px;bottom:22px;display:grid;grid-template-columns:repeat(7,1fr);gap:10px}
  .brf-arma{display:flex;flex-direction:column;align-items:center;gap:5px;opacity:0;transform:translateY(14px);transition:opacity .4s ease,transform .4s ease}
  .brf-arma.on{opacity:1;transform:none}
  .brf-arma img{width:100%;height:62px;object-fit:contain}
  .brf-arma b{font:700 22px/1 var(--display);text-transform:uppercase;letter-spacing:.04em;white-space:nowrap}
  .brf-arma small{font:600 14px/1 var(--display);letter-spacing:.12em;text-transform:uppercase;color:rgba(255,255,255,.55);white-space:nowrap}
  .brf-arma span{font:600 18px/1 var(--mono);color:var(--c)}
  .brf-ayuda{position:absolute;left:90px;bottom:70px;font:600 22px/1 var(--display);letter-spacing:.14em;text-transform:uppercase;color:rgba(255,255,255,.5)}
  .brf-ayuda b,.brf-ayuda button{display:inline-block;padding:6px 10px;margin-right:8px;border:0;border-radius:5px;background:rgba(255,255,255,.12);color:#fff;font:inherit;letter-spacing:.04em}
  .brf-ayuda button{cursor:pointer;padding:6px 14px}
  .brf-ayuda button:hover{background:rgba(255,255,255,.24)}
  .brf-ayuda button:disabled{opacity:.35;cursor:default}
  .brf-volver{position:absolute;right:70px;top:30px;padding:12px 22px;border:0;border-radius:8px;background:rgba(255,255,255,.1);color:#fff;font:600 24px/1 var(--display);letter-spacing:.14em;text-transform:uppercase;cursor:pointer}
  .brf-volver:hover{background:rgba(255,255,255,.18)}`;
  const style = document.createElement('style');
  style.textContent = css;
  document.head.appendChild(style);

  // ---------- Ayudas de dibujo y de tiempo ----------
  const lim = (v, a = 0, b = 1) => Math.max(a, Math.min(b, v));
  const suave = k => { k = lim(k); return k * k * (3 - 2 * k); };
  const tramo = (t, a, b) => lim((t - a) / (b - a)); // 0 antes de a, 1 después de b
  function por(pts, k) { // punto a una fracción k del largo de la polilínea
    const segs = [];
    let total = 0;
    for (let i = 1; i < pts.length; i++) { const l = Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]); segs.push(l); total += l; }
    let d = lim(k) * total;
    for (let i = 0; i < segs.length; i++) {
      if (d <= segs[i] || i === segs.length - 1) { const f = segs[i] ? Math.min(1, d / segs[i]) : 0; return [pts[i][0] + (pts[i + 1][0] - pts[i][0]) * f, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * f]; }
      d -= segs[i];
    }
    return pts[pts.length - 1];
  }
  function el(padre, tag, attrs = {}, texto) {
    const e = document.createElementNS(NS, tag);
    for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
    if (texto !== undefined) e.textContent = texto;
    padre.appendChild(e);
    return e;
  }
  const set = (e, attrs) => { for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v); };
  // Un jugador o enemigo: un punto con aro, opcionalmente con nombre.
  function punto(capa, color, r, nombre) {
    const g = el(capa, 'g');
    el(g, 'circle', { r: r * 1.9, fill: color, opacity: .22 });
    el(g, 'circle', { r, fill: color, stroke: '#07080a', 'stroke-width': r * .35 });
    if (nombre) el(g, 'text', { x: r * 1.7, y: r * .5, fill: '#fff', 'font-size': r * 2.1, 'font-family': 'Barlow Condensed, Arial Narrow, sans-serif', 'font-weight': 700, style: 'paint-order:stroke', stroke: '#07080a', 'stroke-width': r * .5 }, nombre);
    return { g, mover(x, y, o = 1) { set(g, { transform: `translate(${x.toFixed(1)} ${y.toFixed(1)})`, opacity: o }); } };
  }
  const html = (padre, clase, contenido) => { const d = document.createElement('div'); d.className = clase; d.innerHTML = contenido; padre.appendChild(d); return d; };
  const mmss = s => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;

  // ---------- Los tres modos ----------
  const NARANJA = '#F29A38', AZUL = '#3AA0FF', ROJO = '#FF4B4B', VERDE = '#7DE05A', VIOLETA = '#B061FF';

  // Táctico: el plano de MALVINAS-01 (1250 × 729): A arriba, B abajo, la base naranja a la izquierda y la azul a la derecha.
  // Cada paso arranca donde terminó el anterior: los 4 naranjas llevan el dispositivo a B, 2 azules cuidan A y 2 cuidan B.
  // Sin reaparición: el que cae queda marcado con una cruz.
  const T = {
    A: [606, 150], B: [632, 543],
    rojos: [[150, 335], [190, 362], [222, 330], [172, 398]],
    azules: [[1062, 338], [1122, 330], [1098, 366], [1052, 398]], // 0 y 1 van a A; 2 y 3, a B
    finRojos: [[622, 548], [574, 584], [582, 506], [530, 556]],
    finAzules: [[636, 160], [662, 136], [702, 560], [714, 506]]
  };
  const ROJOS = ['Vos', 'Luka', 'David', 'Agus'], AZULES = ['Lucas', 'Ulises', 'Nico', 'Mati'];
  // Las 7 armas de la tienda del Táctico (Data/Tienda/CatalogoTactico.asset), en su orden: [imagen, nombre, tipo, precio].
  const ARMAS_T = [['pistola', 'La Porteña', 'Pistola', 0], ['pistolaPesada', 'La Trochita', 'Pistola pesada', 800], ['subfusil', 'Urquiza', 'Subfusil', 1600],
    ['escopeta', 'Roca', 'Escopeta', 900], ['rafaga', 'Belgrano Sur', 'Fusil de ráfaga', 2100], ['fusil', 'Mitre', 'Fusil', 2900], ['francotirador', 'Último Tren', 'Francotirador', 4700]];
  const caminoRojo = i => [T.rojos[i], [255, 470], [330, 540], [470, 520], T.finRojos[i]];
  const POR_AZUL = [[[960, 250], [820, 205], [700, 175]], [[990, 230], [840, 190], [720, 160]], [[980, 500], [860, 555], [760, 560]], [[950, 470], [850, 520], [740, 545]]];
  const caminoAzul = i => [T.azules[i], ...POR_AZUL[i], T.finAzules[i]];
  // Paso 3: los 2 de A bajan por el medio a B. Bajas: [equipo que tira, quién, equipo que cae, quién, segundo].
  const ROTA = [[656, 514], [668, 592]];
  const rotacion = i => [T.finAzules[i], [612, 215], [622, 330], [626, 420], [630, 480], ROTA[i]];
  const BAJAS_T = [['a', 2, 'r', 3, 3.2], ['r', 1, 'a', 3, 4.2], ['a', 0, 'r', 1, 6.4], ['a', 1, 'r', 2, 6.9], ['a', 2, 'r', 0, 7.5]];
  const TACTICO = {
    nombre: 'Táctico', color: NARANJA, sub: '4 vs 4 · MALVINAS-01', mapa: 'MALVINAS-01', busca: 8,
    img: { src: 'presentacion/media/modos/tactico.jpg', w: 1250, h: 729, vista: [0, 20, 1250, 700], ajuste: 'meet' },
    pasos: [
      { tit: 'Primero, la compra', txt: 'Cada ronda arranca con 20 s para comprar armas, granadas y escudo. Se empieza con $ 800: cada baja da $ 200, ganar la ronda $ 3.000, perderla $ 1.900 o más, y plantar, $ 300 a los que atacan.',
        escena(c) {
          const rojos = T.rojos.map((p, i) => punto(c.capa, NARANJA, 11, i === 0 ? 'Vos' : null));
          const azules = T.azules.map(() => punto(c.capa, AZUL, 11));
          const chip = html(c.hud, 'brf-chip', '');
          chip.style.cssText = 'left:24px;top:24px';
          // Las 7 armas, en fila sobre el fondo negro de abajo del plano. La pistola la tienen todos, sin pagar.
          const fila = html(c.hud, 'brf-armas', ARMAS_T.map(([img, n, tipo, p]) => `<div class="brf-arma"><img src="presentacion/media/armas/${img}.png" alt=""><b>${n}</b><small>${tipo}</small><span>${p ? '$ ' + p.toLocaleString('es-AR') : 'Gratis'}</span></div>`).join(''));
          const armas = [...fila.children];
          return t => {
            rojos.forEach((r, i) => r.mover(T.rojos[i][0] + Math.sin(t * 2 + i) * 4, T.rojos[i][1]));
            azules.forEach((a, i) => a.mover(T.azules[i][0] + Math.sin(t * 2 + i) * 4, T.azules[i][1]));
            const queda = 20 - t * 2.6;
            chip.innerHTML = queda > 0 ? `<small>Ronda 1 · compra</small>${mmss(queda)}` : '<small>Ronda 1</small>¡Arranca!';
            armas.forEach((d, i) => d.classList.toggle('on', t > .5 + i * .25));
          };
        } },
      { tit: 'Atacar o defender', txt: 'Un equipo lleva el dispositivo a A o a B; el otro se reparte para cuidar los dos. Sin reaparición: si morís, mirás a un compañero hasta la ronda siguiente.',
        escena(c) {
          const marcas = [T.A, T.B].map(p => el(c.capa, 'circle', { cx: p[0], cy: p[1], r: 40, fill: 'none', stroke: '#fff', 'stroke-width': 3 }));
          const disp = el(c.capa, 'circle', { r: 20, fill: 'none', stroke: NARANJA, 'stroke-width': 4 });
          const rojos = T.rojos.map((_, i) => punto(c.capa, NARANJA, 11, i === 0 ? 'Vos' : null));
          const azules = T.azules.map(() => punto(c.capa, AZUL, 11));
          const chip = html(c.hud, 'brf-chip', '<small>Atacan los naranjas</small>El dispositivo va a B');
          chip.style.cssText = 'left:24px;top:24px';
          return t => {
            marcas.forEach((m, i) => set(m, { r: 40 + 8 * Math.sin(t * 3 + i), opacity: .35 + .25 * Math.sin(t * 3 + i) }));
            rojos.forEach((d, i) => d.mover(...por(caminoRojo(i), suave(tramo(t, .4 + i * .35, 6 + i * .35)))));
            const [dx, dy] = por(caminoRojo(0), suave(tramo(t, .4, 6)));
            set(disp, { cx: dx, cy: dy, r: 20 + 4 * Math.sin(t * 6) });
            azules.forEach((d, i) => d.mover(...por(caminoAzul(i), suave(tramo(t, .3 + i * .1, 5.5)))));
          };
        } },
      { tit: 'Plantar o desactivar', vista: [268, 118, 728, 520], txt: 'Plantar el dispositivo tarda 4 s. Plantado, explota a los 45 s, salvo que los que defienden lo desactiven antes: eso tarda 7 s.',
        escena(c) {
          const L = 289; // 2π × 46
          const aro = el(c.capa, 'circle', { cx: T.B[0], cy: T.B[1], r: 46, fill: 'none', stroke: '#fff', 'stroke-width': 7, transform: `rotate(-90 ${T.B[0]} ${T.B[1]})`, 'stroke-dasharray': L, 'stroke-dashoffset': L });
          // Las cruces y los disparos van debajo de los jugadores.
          const marcas = BAJAS_T.map(() => ({
            linea: el(c.capa, 'line', { 'stroke-width': 3, opacity: 0 }),
            cruz: el(c.capa, 'text', { 'font-size': 30, 'font-weight': 700, 'text-anchor': 'middle', 'dominant-baseline': 'central', 'font-family': 'Barlow Condensed, Arial Narrow, sans-serif', stroke: '#07080a', 'stroke-width': 5, style: 'paint-order:stroke', opacity: 0 }, '✕')
          }));
          const rojos = ROJOS.map(n => punto(c.capa, NARANJA, 9, n));
          const azules = AZULES.map(n => punto(c.capa, AZUL, 9, n));
          const chip = html(c.hud, 'brf-chip', '');
          chip.style.cssText = 'left:24px;top:24px';
          const feed = html(c.hud, 'brf-feed', '');
          const pos = (eq, i, t) => eq === 'r' ? T.finRojos[i] : i < 2 ? por(rotacion(i), suave(tramo(t, 2.6 + i * .2, 6 + i * .2))) : T.finAzules[i];
          const muere = (eq, i) => { const b = BAJAS_T.find(b => b[2] === eq && b[3] === i); return b ? b[4] : Infinity; };
          const col = eq => eq === 'r' ? NARANJA : AZUL, nom = (eq, i) => (eq === 'r' ? ROJOS : AZULES)[i];
          return t => {
            [[rojos, 'r'], [azules, 'a']].forEach(([ds, eq]) => ds.forEach((d, i) => { const td = muere(eq, i); d.mover(...pos(eq, i, t), t < td ? 1 : Math.max(0, 1 - (t - td) * 3)); }));
            BAJAS_T.forEach(([e1, i1, e2, i2, tb], k) => {
              const [x1, y1] = pos(e1, i1, tb), [x2, y2] = pos(e2, i2, tb);
              set(marcas[k].linea, { x1, y1, x2, y2, stroke: col(e1), opacity: t > tb - .25 && t < tb + .2 ? 1 : 0 });
              set(marcas[k].cruz, { x: x2, y: y2, fill: col(e2), opacity: t >= tb ? Math.min(.9, (t - tb) * 3) : 0 });
            });
            feed.innerHTML = BAJAS_T.filter(b => t >= b[4]).map(([e1, i1, e2, i2]) => `<div><span style="color:${col(e1)}">${nom(e1, i1)}</span> ${e2 === 'r' && i2 === 0 ? 'te eliminó' : `eliminó a <span style="color:${col(e2)}">${nom(e2, i2)}</span>`}</div>`).join('');
            const explota = 45 - (t - 2.5) * 3;
            if (t < 2.5) { set(aro, { stroke: '#fff', 'stroke-dashoffset': L * (1 - t / 2.5) }); chip.innerHTML = `<small>Vos plantás el dispositivo</small>${Math.ceil(4 - t / 2.5 * 4)} s`; }
            else if (t < 8) { set(aro, { stroke: NARANJA, 'stroke-dashoffset': L * (1 - explota / 45) }); chip.innerHTML = `<small>Dispositivo plantado · explota en</small>${mmss(explota)}`; }
            else if (t < 11.5) { const k = (t - 8) / 3.5; set(aro, { stroke: AZUL, 'stroke-dashoffset': L * (1 - k) }); chip.innerHTML = `<small>Lucas desactiva · explota en ${mmss(explota)}</small>${Math.ceil(7 - k * 7)} s`; }
            else { set(aro, { stroke: AZUL, 'stroke-dashoffset': 0 }); chip.innerHTML = '<small>Dispositivo desactivado</small>Ronda para el azul'; }
          };
        } },
      { tit: 'Gana el primero a 7', txt: 'Cada ronda dura 100 s de combate. Después de la ronda 6 los equipos cambian de lado, y gana el primero que llega a 7 rondas.',
        escena(c) {
          const chip = html(c.hud, 'brf-chip', '');
          chip.style.cssText = 'left:50%;top:40%;transform:translate(-50%,-50%);font-size:110px;padding:24px 44px;text-align:center';
          const flechas = [el(c.capa, 'path', { d: 'M260 300 C450 160 800 160 990 300', fill: 'none', stroke: NARANJA, 'stroke-width': 8, 'stroke-dasharray': 900, 'stroke-dashoffset': 900, 'marker-end': '' }),
            el(c.capa, 'path', { d: 'M990 450 C800 590 450 590 260 450', fill: 'none', stroke: AZUL, 'stroke-width': 8, 'stroke-dasharray': 900, 'stroke-dashoffset': 900 })];
          const marcador = [[0, 0], [1, 0], [2, 1], [3, 1], [3, 2], [4, 2], [4, 3], [5, 3], [5, 4], [6, 4], [7, 4]];
          return t => {
            const i = Math.min(marcador.length - 1, Math.floor(t * 1.4));
            const [a, b] = marcador[i];
            chip.innerHTML = `<small>${a === 7 ? '¡Ganó el naranja!' : `Ronda ${i + 1}`}</small><span style="color:${NARANJA}">${a}</span> · <span style="color:${AZUL}">${b}</span>`;
            const cambio = suave(tramo(t, 4.2, 5.4));
            flechas.forEach(f => set(f, { 'stroke-dashoffset': 900 * (1 - cambio), opacity: t > 6.2 ? Math.max(0, 1 - (t - 6.2) * 2) : 1 }));
          };
        } }
    ]
  };

  // Deathmatch: la vista aérea de Colonia ARA San Juan (1280 × 720).
  const NOMBRES = ['Vos', 'Luka', 'David', 'Agus', 'Lucas', 'Ulises', 'Nico', 'Mati'];
  const COLORES = ['#FF4B4B', '#F29A38', '#FFC93C', '#7DE05A', '#3AA0FF', '#B061FF', '#FF7FD1', '#5EE1E6'];
  function paseo(i) { // un recorrido fijo por jugador, de punto a punto
    const pts = [];
    let s = (i + 1) * 9301;
    const azar = () => { s = (s * 49297 + 233280) % 233280; return s / 233280; };
    for (let k = 0; k < 8; k++) pts.push([170 + azar() * 940, 270 + azar() * 380]);
    return pts;
  }
  const DM = {
    nombre: 'Deathmatch', color: ROJO, sub: 'Hasta 8 · Colonia ARA San Juan', mapa: 'Colonia ARA San Juan', busca: 8,
    img: { src: 'presentacion/media/historia/colonia.jpg', w: 1280, h: 720, vista: [0, 0, 1280, 720], ajuste: 'meet' },
    pasos: [
      { tit: 'Todos contra todos', txt: 'Hasta 8 jugadores, cada uno por su cuenta. Las armas se eligen gratis en la tienda, antes de salir y cada vez que reaparecés.',
        escena(c) {
          const js = NOMBRES.map((n, i) => ({ d: punto(c.capa, COLORES[i], 10, n), cm: paseo(i) }));
          return t => js.forEach(({ d, cm }, i) => d.mover(...por(cm, (t / 26 + i / 8) % 1)));
        } },
      { tit: 'Cada baja te cura 50', txt: 'Cada baja suma al marcador y te devuelve 50 de vida, así el que va ganando puede seguir peleando.',
        escena(c) {
          const js = NOMBRES.map((n, i) => ({ d: punto(c.capa, COLORES[i], 10, n), cm: paseo(i) }));
          const feed = html(c.hud, 'brf-feed', '');
          // Las bajas se repiten cada 9 s y el que cae vuelve a los 3 s, así nadie desaparece aunque el paso quede en pantalla.
          const bajas = [[1, 6, 1], [3, 5, 2.6], [0, 7, 4.2], [1, 2, 5.8], [4, 3, 7.2]], CICLO = 9;
          const desde = (b, t) => t < b[2] ? Infinity : (t - b[2]) % CICLO; // segundos desde la última vez que pasó
          const cura = js.map(() => el(c.capa, 'text', { fill: VERDE, 'font-size': 26, 'font-weight': 700, 'font-family': 'Barlow Condensed, sans-serif', opacity: 0 }, '+50'));
          return t => {
            js.forEach(({ d, cm }, i) => {
              const caida = bajas.map(b => b[1] === i ? desde(b, t) : Infinity).find(s => s < 3);
              const [x, y] = por(cm, (t / 26 + i / 8) % 1);
              d.mover(x, y, caida === undefined ? 1 : caida < 2.7 ? Math.max(0, 1 - caida * 3) : (caida - 2.7) / .3);
              const gano = bajas.map(b => b[0] === i ? desde(b, t) : Infinity).find(s => s < 1.2);
              set(cura[i], { x: x + 14, y: y - 18 - (gano !== undefined ? gano * 20 : 0), opacity: gano !== undefined ? 1 - gano / 1.2 : 0 });
            });
            feed.innerHTML = bajas.filter(b => t >= b[2]).sort((p, q) => desde(q, t) - desde(p, t)).slice(-4)
              .map(([a, b]) => `<div><b>${NOMBRES[a]}</b> ${a === 0 ? 'eliminaste' : 'eliminó'} a ${b === 0 ? 'vos' : NOMBRES[b]}</div>`).join('');
          };
        } },
      { tit: 'Reaparecés a los 3 s', txt: 'Si te eliminan, a los 3 s volvés lejos de los rivales, con tus armas elegidas.',
        escena(c) {
          const js = NOMBRES.map((n, i) => ({ d: punto(c.capa, COLORES[i], 10, n), cm: paseo(i) }));
          const chip = html(c.hud, 'brf-chip', '');
          chip.style.cssText = 'left:50%;bottom:30px;transform:translateX(-50%)';
          const cruz = el(c.capa, 'text', { fill: ROJO, 'font-size': 54, 'font-weight': 700, 'text-anchor': 'middle', 'font-family': 'Barlow Condensed, sans-serif', opacity: 0 }, '✕');
          const aro = el(c.capa, 'circle', { r: 20, fill: 'none', stroke: ROJO, 'stroke-width': 4, opacity: 0 });
          let donde = null;
          const lejos = [1080, 300];
          return t => {
            js.forEach(({ d, cm }, i) => {
              let [x, y] = por(cm, (t / 26 + i / 8) % 1);
              let o = 1;
              if (i === 0) {
                if (t > 1.5 && !donde) donde = [x, y];
                if (t > 1.5 && t < 5) { [x, y] = donde; o = Math.max(0, 1 - (t - 1.5) * 3); }
                if (t >= 5) { const k = t - 5, m = Math.min(k, 5); [x, y] = [lejos[0] - m * 18 + Math.sin(k * 1.3) * 6, lejos[1] + m * 12 + Math.cos(k) * 4]; o = Math.min(1, k * 2); }
              }
              d.mover(x, y, o);
            });
            if (donde) set(cruz, { x: donde[0], y: donde[1] + 18, opacity: t < 5 ? Math.min(1, (t - 1.5) * 3) : Math.max(0, 1 - (t - 5) * 2) });
            set(aro, { cx: lejos[0], cy: lejos[1], r: Math.max(0, 20 + (t - 5) * 40), opacity: t > 5 && t < 6.2 ? 1 - (t - 5) / 1.2 : 0 });
            chip.innerHTML = t < 1.5 ? '<small>Vos</small>En combate' : t < 5 ? `<small>Te eliminó Luka</small>Reaparecés en ${Math.max(1, Math.ceil(3 - (t - 2) * .9))}` : '<small>Vos</small>De vuelta, lejos de los rivales';
          };
        } },
      { tit: '25 bajas u 8 minutos', txt: 'Gana el primero que llega a 25 bajas. Si a los 8 minutos nadie llegó, gana el que tiene más.',
        escena(c) {
          const js = NOMBRES.map((n, i) => ({ d: punto(c.capa, COLORES[i], 10, n), cm: paseo(i) }));
          const tabla = html(c.hud, 'brf-chip', '');
          tabla.style.cssText = 'right:24px;top:24px;font-size:28px;min-width:300px';
          const ritmo = [3.4, 2.9, 2.2, 1.6];
          return t => {
            js.forEach(({ d, cm }, i) => d.mover(...por(cm, (t / 26 + i / 8) % 1)));
            const filas = [1, 0, 4, 3].map((j, k) => [NOMBRES[j], Math.min([25, 22, 19, 16][k], Math.floor(t * ritmo[k] + 4 - k))]);
            const fin = filas[0][1] >= 25;
            tabla.innerHTML = `<small>${fin ? '¡Ganó Luka!' : mmss(Math.max(0, 480 - t * 50))}</small>` + filas.map(([n, b], k) => `<div style="display:flex;justify-content:space-between;gap:30px;margin-top:6px;color:${k === 0 ? '#FFC93C' : '#fff'}"><span>${n}</span><span>${b}</span></div>`).join('');
          };
        } }
    ]
  };

  // Invasión: tomas grabadas en el juego (Unity, 24/11/2026), con cámara isométrica y los 4 personajes manejados por bots:
  // 1, entre el TUM y los módulos del medio; 2, en la entrada de la UNGS; 3, el puesto en la Biblioteca; 4, el Jefe en el Auditorio.
  // El video ocupa todo el cuadro (el HUD del juego ya está apagado); lo que se agrega va sobre el fondo negro, a la izquierda.
  const CARTAS_I = [['Legendaria', '#FFC93C', 'Escolta', 'Un dron te acompaña: su rayo derrite al invasor más cercano.'],
    ['Épica', '#B061FF', 'Campo minado', 'Las granadas que tocan el piso quedan como minas.'],
    ['Arma', ROJO, 'Katana', 'Reemplaza a tu cuchillo. Corta a todos los que tenés delante.']];
  const INVASION = {
    nombre: 'Invasión', color: VERDE, sub: '1 a 4 jugadores · la universidad', mapa: 'la universidad', busca: 4,
    pasos: [
      { tit: 'Juntos contra la horda', video: 'presentacion/media/modos/invasion-1.mp4', txt: 'De 1 a 4 jugadores, contra oleadas cada vez más grandes: la oleada N trae 10 + 5 × (N − 1) invasores, y cada jugador de más suma la mitad.' },
      { tit: 'Salen de los portales', video: 'presentacion/media/modos/invasion-2.mp4', txt: 'Los invasores salen de portales que se abren alrededor del puesto de suministros, entre 30 y 55 m, y van hacia el jugador más cercano.' },
      { tit: 'Cargar el puesto', video: 'presentacion/media/modos/invasion-3.mp4', txt: 'Las bajas dentro del anillo cargan el puesto. Al 100 %, cada uno elige 1 de 3: un arma, la Katana o una de las 27 cartas.',
        escena(c) {
          const lista = html(c.info, 'brf-lista', '<h4>Elegí 1 de 3</h4>'
            + CARTAS_I.map(([r, col, n, x]) => `<div class="brf-fila" style="--r:${col}"><small>${r}</small><b>${n}</b><span>${x}</span></div>`).join(''));
          const filas = lista.querySelectorAll('.brf-fila, h4');
          return t => filas.forEach((f, i) => f.classList.toggle('on', t > 1.2 + i * .35));
        } },
      { tit: 'El Jefe y revivir', video: 'presentacion/media/modos/invasion-4.mp4', txt: 'Cada 10 oleadas llega el Jefe. Si te quedás sin vida, quedás caído 30 s: un compañero te revive en 5 s manteniendo E.' }
    ]
  };
  const MODOS = [TACTICO, DM, INVASION];

  // ---------- El briefing ----------
  let b = null; // { modo, paso, t0, raf, raiz, ... }
  function abrir(i) {
    if (b) return;
    const M = MODOS[i];
    const raiz = document.createElement('div');
    raiz.className = 'brf';
    raiz.style.setProperty('--c', M.color);
    raiz.innerHTML = `<div class="brf-busca"><div class="brf-radar">${'<i></i>'.repeat(3)}<b></b></div><div class="e">Buscando partida</div><div class="g">1 / ${M.busca}</div><div class="s">${M.nombre} · Sudamérica · misma versión</div></div>
      <div class="brf-main"><div class="brf-izq"><div class="brf-eb">Partida encontrada</div><div class="brf-modo">${M.nombre}</div><div class="brf-sub">${M.sub}</div>
        <div class="brf-paso"></div><h2 class="brf-tit"></h2><div class="brf-dots">${M.pasos.map(() => '<i></i>').join('')}</div><div class="brf-info"></div></div>
        <div class="brf-der">${M.pasos.filter(P => P.video).map(P => `<video class="brf-video" src="${P.video}" muted playsinline loop preload="auto"></video>`).join('')}<svg ${M.img ? `viewBox="${M.img.vista.join(' ')}" preserveAspectRatio="xMidYMid ${M.img.ajuste || 'slice'}"><image href="${M.img.src}" width="${M.img.w}" height="${M.img.h}"/>` : '>'}<g class="capa"></g></svg><div class="brf-hud"></div></div>
        <div class="brf-ayuda"><button type="button" data-ir="-1" aria-label="Paso anterior">←</button><button type="button" data-ir="1" aria-label="Paso siguiente">→</button>pasos · <b>Esc</b>volver a los andenes</div></div>
      <button class="brf-volver" type="button">Volver a los andenes</button>`;
    inner.appendChild(raiz);
    raiz.querySelector('.brf-volver').addEventListener('click', cerrar);
    raiz.querySelectorAll('[data-ir]').forEach(x => x.addEventListener('click', e => { e.stopPropagation(); if (b && b.paso >= 0) irA(b.paso + +x.dataset.ir); }));
    b = { M, raiz, paso: -1, t0: 0, buscaT0: performance.now(), capa: raiz.querySelector('.capa'), hud: raiz.querySelector('.brf-hud'), info: raiz.querySelector('.brf-info'), frame: null, svg: raiz.querySelector('svg'), vista: M.img ? M.img.vista.slice() : null, cam: null };
    requestAnimationFrame(() => raiz.classList.add('on'));
    b.raf = requestAnimationFrame(tic);
  }
  function irA(n) {
    const M = b.M;
    b.paso = Math.max(0, Math.min(M.pasos.length - 1, n));
    b.t0 = performance.now();
    const P = M.pasos[b.paso];
    b.capa.replaceChildren();
    b.hud.replaceChildren();
    b.info.replaceChildren();
    b.raiz.querySelector('.brf-paso').textContent = `Paso ${b.paso + 1} / ${M.pasos.length}`;
    b.raiz.querySelector('.brf-tit').textContent = P.tit;
    b.raiz.querySelectorAll('.brf-dots i').forEach((d, k) => d.classList.toggle('on', k <= b.paso));
    b.raiz.querySelector('[data-ir="-1"]').disabled = b.paso === 0;
    b.raiz.querySelector('[data-ir="1"]').disabled = b.paso === M.pasos.length - 1;
    if (M.img) b.cam = { de: b.vista.slice(), a: P.vista || M.img.vista, t0: b.t0 }; // un paso puede acercar la cámara a una parte del mapa
    b.raiz.querySelectorAll('.brf-video').forEach(v => {
      const este = v.getAttribute('src') === P.video;
      v.classList.toggle('on', este);
      if (este) { v.currentTime = 0; v.play().catch(() => {}); } else v.pause();
    });
    b.frame = P.escena ? P.escena({ capa: b.capa, hud: b.hud, info: b.info }) : () => {};
  }
  function tic(ahora) {
    if (!b) return;
    const busca = (ahora - b.buscaT0) / 1000;
    if (b.paso < 0) {
      // La búsqueda, acortada: entran jugadores y a los 2,2 s «Partida encontrada».
      const anillos = b.raiz.querySelectorAll('.brf-radar i');
      anillos.forEach((a, k) => { const f = ((busca + k * .5) % 1.5) / 1.5; a.style.transform = `scale(${.15 + f * .85})`; a.style.opacity = (1 - f).toFixed(2); });
      const n = Math.min(b.M.busca, 1 + Math.floor(busca * b.M.busca / 1.8));
      const g = b.raiz.querySelector('.brf-busca .g'), e = b.raiz.querySelector('.brf-busca .e');
      g.textContent = `${n} / ${b.M.busca}`;
      if (busca > 1.9) { e.textContent = 'Partida encontrada'; b.raiz.querySelector('.brf-busca .s').textContent = `${b.M.nombre} · ${b.M.mapa}`; }
      if (busca > 2.8) { b.raiz.querySelector('.brf-busca').classList.add('fuera'); b.raiz.querySelector('.brf-main').classList.add('on'); irA(0); }
    } else {
      if (b.cam) {
        const k = suave((ahora - b.cam.t0) / 900);
        b.vista = b.cam.de.map((v, j) => v + (b.cam.a[j] - v) * k);
        b.svg.setAttribute('viewBox', b.vista.map(v => v.toFixed(1)).join(' '));
        if (k >= 1) b.cam = null;
      }
      b.frame((ahora - b.t0) / 1000);
    }
    b.raf = requestAnimationFrame(tic);
  }
  function cerrar() {
    if (!b) return;
    cancelAnimationFrame(b.raf);
    const raiz = b.raiz;
    b = null;
    raiz.classList.remove('on');
    setTimeout(() => raiz.remove(), 350);
    stage.focus({ preventScroll: true });
  }
  // «Buscar partida» abre el briefing en lugar de la búsqueda (antes de que llegue al andén).
  stage.addEventListener('click', e => {
    const boton = e.target.closest('[data-act="search"]');
    if (!boton || !stage.contains(boton)) return;
    e.stopPropagation();
    e.preventDefault();
    abrir(+boton.closest('.ms-detail').dataset.i);
  }, true);
  // Con el briefing abierto, las teclas son suyas (no cambian de andén ni cierran la demo).
  addEventListener('keydown', e => {
    if (!b) return;
    if (e.key === 'Escape') cerrar();
    else if (['ArrowRight', ' ', 'Enter', 'PageDown'].includes(e.key)) { if (b.paso >= 0) irA(b.paso + 1); }
    else if (['ArrowLeft', 'PageUp'].includes(e.key)) { if (b.paso >= 0) irA(b.paso - 1); }
    else return;
    e.preventDefault();
    e.stopImmediatePropagation();
  }, true);
})();
