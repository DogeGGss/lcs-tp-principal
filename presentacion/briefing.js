// Briefing de los modos (diapositiva «Modos de juego» de la presentación). En la demo de la selección de modo
// (?demo=modos&briefing=1), «Buscar partida» no abre la búsqueda: encuentra partida enseguida y muestra cómo se juega
// el modo, paso a paso sobre su mapa, para hablar por encima. Los números son los del juego (Sprint 5):
// RondasTacticas, DispositivoTactico, PartidaDeathmatch, ConfigInvasion, CooperativoDeInvasion y CaidaEnInvasion.
// Pasos: avanzan solos (con la barra de abajo); ← y → van y vuelven; Esc vuelve a los andenes.
(() => {
  const stage = document.getElementById('msStage'), inner = document.getElementById('msi');
  if (!stage || !inner) return;
  const NS = 'http://www.w3.org/2000/svg';
  const DURA = 9; // s por paso

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
  .brf-tit{font:700 58px/1 var(--display);text-transform:uppercase;margin:0;min-height:58px}
  .brf-txt{font:500 33px/1.36 var(--body);color:rgba(255,255,255,.88);margin:0;min-height:200px}
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
  .brf-cartas{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);display:flex;gap:22px;padding:30px;border-radius:14px;background:rgba(7,8,10,.9);box-shadow:0 0 0 2px rgba(255,255,255,.12);transition:opacity .4s ease,transform .4s ease}
  .brf-cartas.oculto{opacity:0;transform:translate(-50%,-44%)}
  .brf-carta{width:230px;height:300px;border-radius:12px;background:#121722;border-top:8px solid var(--r);padding:18px;display:flex;flex-direction:column;gap:10px}
  .brf-carta small{font:600 16px/1 var(--display);letter-spacing:.2em;text-transform:uppercase;color:var(--r)}
  .brf-carta b{font:700 34px/1 var(--display);text-transform:uppercase}
  .brf-carta span{font:500 19px/1.3 var(--body);color:rgba(255,255,255,.8)}
  .brf-cartas h4{position:absolute;left:0;right:0;top:-46px;margin:0;text-align:center;font:700 32px/1 var(--display);letter-spacing:.12em;text-transform:uppercase}
  .brf-barra{position:absolute;left:700px;top:930px;width:1150px;height:6px;border-radius:3px;background:rgba(255,255,255,.1);overflow:hidden}
  .brf-barra i{display:block;height:100%;width:0;background:var(--c)}
  .brf-ayuda{position:absolute;left:90px;bottom:70px;font:600 22px/1 var(--display);letter-spacing:.14em;text-transform:uppercase;color:rgba(255,255,255,.5)}
  .brf-ayuda b{display:inline-block;padding:6px 10px;margin-right:8px;border-radius:5px;background:rgba(255,255,255,.12);color:#fff;letter-spacing:.04em}
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

  // Táctico: el plano de MALVINAS-01 (1250 × 729): A arriba, B abajo, la base roja a la izquierda y la azul a la derecha.
  const T = { A: [606, 150], B: [632, 543], rojos: [[150, 335], [190, 362], [222, 330], [172, 398]], azules: [[1062, 338], [1098, 366], [1052, 398], [1122, 330]] };
  const aB = r => [r, [255, 470], [330, 540], [470, 520], [560, 556], [T.B[0] - 22 + Math.random() * 6, T.B[1] + 8]];
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
          const armas = ['fusil', 'escopeta', 'subfusil'].map((a, i) => { const im = el(c.capa, 'image', { href: `presentacion/media/armas/${a}.png`, width: 120, height: 60, opacity: 0 }); return im; });
          return t => {
            rojos.forEach((r, i) => r.mover(T.rojos[i][0] + Math.sin(t * 2 + i) * 4, T.rojos[i][1]));
            azules.forEach((a, i) => a.mover(T.azules[i][0] + Math.sin(t * 2 + i) * 4, T.azules[i][1]));
            chip.innerHTML = `<small>Ronda 1 · compra</small>${mmss(Math.max(0, 20 - t * 2.6))}`;
            armas.forEach((im, i) => { const k = suave(tramo(t, .6 + i * .5, 1.2 + i * .5)); set(im, { x: 120 + i * 100, y: 200 - k * 20, opacity: k }); });
          };
        } },
      { tit: 'Atacar o defender', txt: 'Un equipo lleva el dispositivo a A o a B; el otro se reparte para cuidar los dos. Sin reaparición: si morís, mirás a un compañero hasta la ronda siguiente.',
        escena(c) {
          const caminos = T.rojos.map(r => aB(r));
          const azA = [[T.azules[0], [960, 250], [820, 205], [700, 175], [T.A[0] + 30, T.A[1] + 10]], [T.azules[3], [990, 230], [840, 190], [720, 160], [T.A[0] + 50, T.A[1] - 10]]];
          const azB = [[T.azules[1], [980, 500], [860, 555], [760, 560], [T.B[0] + 40, T.B[1] + 5]], [T.azules[2], [950, 470], [850, 520], [740, 545], [T.B[0] + 60, T.B[1] - 15]]];
          const marcas = [T.A, T.B].map(p => el(c.capa, 'circle', { cx: p[0], cy: p[1], r: 40, fill: 'none', stroke: '#fff', 'stroke-width': 3 }));
          const rojos = caminos.map((_, i) => punto(c.capa, NARANJA, 11, i === 0 ? 'Dispositivo' : null));
          const disp = el(c.capa, 'circle', { r: 20, fill: 'none', stroke: NARANJA, 'stroke-width': 4 });
          const azules = [...azA, ...azB].map(() => punto(c.capa, AZUL, 11));
          return t => {
            marcas.forEach((m, i) => set(m, { r: 40 + 8 * Math.sin(t * 3 + i), opacity: .35 + .25 * Math.sin(t * 3 + i) }));
            caminos.forEach((cm, i) => rojos[i].mover(...por(cm, suave(tramo(t, .4 + i * .15, 6.5)))));
            const [dx, dy] = por(caminos[0], suave(tramo(t, .4, 6.5)));
            set(disp, { cx: dx, cy: dy, r: 20 + 4 * Math.sin(t * 6) });
            [...azA, ...azB].forEach((cm, i) => azules[i].mover(...por(cm, suave(tramo(t, .3 + i * .1, 5.5)))));
          };
        } },
      { tit: 'Plantar o desactivar', txt: 'Plantar el dispositivo tarda 4 s. Plantado, explota a los 45 s, salvo que los que defienden lo desactiven antes: eso tarda 7 s.',
        escena(c) {
          const fin = aB(T.rojos[0]).pop();
          const rojos = [[fin[0] - 8, fin[1] - 26], [fin[0] - 50, fin[1] + 6], [fin[0] - 30, fin[1] + 30], [fin[0] - 70, fin[1] - 20]].map(p => ({ p, d: punto(c.capa, NARANJA, 11) }));
          const llegan = [[[T.A[0] + 30, T.A[1] + 10], [700, 320], [690, 470], [T.B[0] + 32, T.B[1] - 18]], [[T.B[0] + 60, T.B[1] - 15], [T.B[0] + 26, T.B[1] + 10]], [[T.B[0] + 40, T.B[1] + 5], [T.B[0] + 70, T.B[1] + 30]]];
          const azules = llegan.map(() => punto(c.capa, AZUL, 11));
          const aro = el(c.capa, 'circle', { cx: T.B[0], cy: T.B[1], r: 46, fill: 'none', stroke: NARANJA, 'stroke-width': 7, transform: `rotate(-90 ${T.B[0]} ${T.B[1]})`, 'stroke-dasharray': 289, 'stroke-dashoffset': 289 });
          const chip = html(c.hud, 'brf-chip', '');
          chip.style.cssText = 'left:24px;top:24px';
          return t => {
            rojos.forEach(({ p, d }, i) => d.mover(p[0], p[1], i === 3 && t > 5 ? Math.max(0, 1 - (t - 5) * 2) : 1));
            llegan.forEach((cm, i) => azules[i].mover(...por(cm, suave(tramo(t, 2.5, 6))), i === 2 && t > 6.5 ? Math.max(0, 1 - (t - 6.5) * 2) : 1));
            if (t < 2) { set(aro, { 'stroke-dashoffset': 289 * (1 - t / 2), stroke: '#fff' }); chip.innerHTML = '<small>Plantando</small>4 s'; }
            else { set(aro, { stroke: NARANJA, 'stroke-dashoffset': 289 * Math.min(1, (t - 2) / 45 * 5) }); chip.innerHTML = `<small>Dispositivo plantado</small>${mmss(Math.max(0, 45 - (t - 2) * 5))}`; }
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
          const bajas = [[1, 6, 1], [3, 5, 2.6], [0, 7, 4.2], [1, 2, 5.8], [4, 3, 7.2]];
          const cura = js.map(() => el(c.capa, 'text', { fill: VERDE, 'font-size': 26, 'font-weight': 700, 'font-family': 'Barlow Condensed, sans-serif', opacity: 0 }, '+50'));
          return t => {
            js.forEach(({ d, cm }, i) => {
              const muerta = bajas.find(b => b[1] === i && t > b[2]);
              const [x, y] = por(cm, (t / 26 + i / 8) % 1);
              d.mover(x, y, muerta ? Math.max(0, 1 - (t - muerta[2]) * 2) : 1);
              const gano = bajas.filter(b => b[0] === i && t > b[2] && t < b[2] + 1.2).pop();
              set(cura[i], { x: x + 14, y: y - 18 - (gano ? (t - gano[2]) * 20 : 0), opacity: gano ? 1 - (t - gano[2]) / 1.2 : 0 });
            });
            feed.innerHTML = bajas.filter(b => t > b[2]).slice(-4).map(([a, b]) => `<div><b>${NOMBRES[a]}</b> ${a === 0 ? 'eliminaste' : 'eliminó'} a ${NOMBRES[b]}</div>`).join('');
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
                if (t >= 5) { const k = t - 5; [x, y] = [lejos[0] - k * 18, lejos[1] + k * 12]; o = Math.min(1, k * 2); }
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

  // Invasión: el minimapa del campus (1760 × 1332), con el puesto en el medio, el anillo de 15 m, los portales a 40 m y el Jefe.
  const I = { puesto: [846, 684], anillo: 51.5, jugadores: [[822, 670], [874, 674], [836, 712], [867, 705]], portales: [[971, 626], [727, 616], [810, 817]], jefe: [830, 506] };
  const COMPAS = ['Vos', 'Luka', 'David', 'Agus'];
  const INVASION = {
    nombre: 'Invasión', color: VERDE, sub: '1 a 4 jugadores · la universidad', mapa: 'la universidad', busca: 4,
    img: { src: 'presentacion/media/modos/campus.png', w: 1760, h: 1332, vista: [560, 430, 600, 428] },
    pasos: [
      { tit: 'Juntos contra la horda', txt: 'De 1 a 4 jugadores, contra oleadas cada vez más grandes: la oleada N trae 10 + 5 × (N − 1) invasores, y cada jugador de más suma la mitad.',
        escena(c) {
          const js = COMPAS.map((n, i) => punto(c.capa, VERDE, 4.4, n));
          const chip = html(c.hud, 'brf-chip', '<small>Oleada 3 · Normal · 4 jugadores</small>50 invasores');
          chip.style.cssText = 'left:24px;top:24px';
          return t => js.forEach((d, i) => d.mover(I.jugadores[i][0] + Math.sin(t * 1.3 + i) * 7, I.jugadores[i][1] + Math.cos(t + i * 2) * 5));
        } },
      { tit: 'Salen de los portales', txt: 'Los invasores salen de portales que se abren alrededor del puesto de suministros, entre 30 y 55 m, y van hacia el jugador más cercano.',
        escena(c) {
          const anillo = el(c.capa, 'circle', { cx: I.puesto[0], cy: I.puesto[1], r: I.anillo, fill: 'rgba(125,224,90,.08)', stroke: VERDE, 'stroke-width': 2.5, 'stroke-dasharray': '7 5', opacity: 0 });
          el(c.capa, 'rect', { x: I.puesto[0] - 7, y: I.puesto[1] - 7, width: 14, height: 14, rx: 2, fill: VERDE });
          const portales = I.portales.map(p => el(c.capa, 'circle', { cx: p[0], cy: p[1], r: 0, fill: 'rgba(176,97,255,.25)', stroke: VIOLETA, 'stroke-width': 3 }));
          const js = COMPAS.map(n => punto(c.capa, VERDE, 4.4, n));
          const enemigos = Array.from({ length: 12 }, (_, k) => ({ d: punto(c.capa, ROJO, 3.4), p: k % 3, sale: 1.2 + k * .45, va: k % 4 }));
          return t => {
            set(anillo, { opacity: suave(tramo(t, 0, .8)) });
            portales.forEach((p, i) => set(p, { r: Math.max(0, 14 * suave(tramo(t, .4 + i * .3, 1.2 + i * .3)) + Math.sin(t * 4 + i) * 1.5) }));
            js.forEach((d, i) => d.mover(I.jugadores[i][0] + Math.sin(t * 1.3 + i) * 6, I.jugadores[i][1] + Math.cos(t + i * 2) * 4));
            enemigos.forEach(e => {
              const k = suave(tramo(t, e.sale, e.sale + 5));
              const [x, y] = por([I.portales[e.p], I.jugadores[e.va]], k * .82);
              e.d.mover(x, y, t > e.sale ? 1 : 0);
            });
          };
        } },
      { tit: 'Cargar el puesto', txt: 'Las bajas dentro del anillo cargan el puesto. Al 100 %, cada uno elige 1 de 3: un arma, la Katana o una de las 27 cartas.',
        escena(c) {
          el(c.capa, 'circle', { cx: I.puesto[0], cy: I.puesto[1], r: I.anillo, fill: 'rgba(125,224,90,.08)', stroke: 'rgba(125,224,90,.35)', 'stroke-width': 2.5 });
          const carga = el(c.capa, 'circle', { cx: I.puesto[0], cy: I.puesto[1], r: I.anillo, fill: 'none', stroke: VERDE, 'stroke-width': 6, transform: `rotate(-90 ${I.puesto[0]} ${I.puesto[1]})`, 'stroke-dasharray': (2 * Math.PI * I.anillo).toFixed(1), 'stroke-dashoffset': (2 * Math.PI * I.anillo).toFixed(1) });
          el(c.capa, 'rect', { x: I.puesto[0] - 7, y: I.puesto[1] - 7, width: 14, height: 14, rx: 2, fill: VERDE });
          const js = COMPAS.map(n => punto(c.capa, VERDE, 4.4, n));
          const enemigos = Array.from({ length: 10 }, (_, k) => ({ d: punto(c.capa, ROJO, 3.4), desde: I.portales[k % 3], muere: .6 + k * .42 }));
          const chip = html(c.hud, 'brf-chip', '');
          chip.style.cssText = 'left:24px;top:24px';
          const cartas = html(c.hud, 'brf-cartas oculto', '<h4>Elegí 1 de 3</h4>'
            + [['Legendaria', '#FFC93C', 'Escolta', 'Un dron te acompaña: su rayo derrite al invasor más cercano.'], ['Épica', '#B061FF', 'Campo minado', 'Las granadas que tocan el piso quedan como minas.'], ['Arma', ROJO, 'Katana', 'Reemplaza a tu cuchillo. Corta a todos los que tenés delante.']]
              .map(([r, col, n, x]) => `<div class="brf-carta" style="--r:${col}"><small>${r}</small><b>${n}</b><span>${x}</span></div>`).join(''));
          const L = 2 * Math.PI * I.anillo;
          return t => {
            js.forEach((d, i) => d.mover(I.jugadores[i][0] + Math.sin(t * 1.3 + i) * 6, I.jugadores[i][1] + Math.cos(t + i * 2) * 4));
            let muertos = 0;
            enemigos.forEach(e => {
              const k = suave(tramo(t, e.muere - .9, e.muere));
              const [x, y] = por([e.desde, I.puesto], k * .7);
              const o = t < e.muere ? (t > e.muere - .9 ? 1 : 0) : Math.max(0, 1 - (t - e.muere) * 3);
              if (t >= e.muere) muertos++;
              e.d.mover(x, y, o);
            });
            const pct = Math.min(100, Math.round(muertos * 10));
            set(carga, { 'stroke-dashoffset': (L * (1 - pct / 100)).toFixed(1) });
            chip.innerHTML = `<small>Puesto de suministros</small>${pct} %`;
            cartas.classList.toggle('oculto', t < 5.4);
          };
        } },
      { tit: 'El Jefe y revivir', txt: 'Cada 10 oleadas llega el Jefe. Si te quedás sin vida, quedás caído 30 s: un compañero te revive en 5 s manteniendo E.',
        escena(c) {
          const js = COMPAS.map(n => punto(c.capa, VERDE, 4.4, n));
          const caido = el(c.capa, 'circle', { r: 9, fill: 'none', stroke: '#fff', 'stroke-width': 2, opacity: 0 });
          const jefe = punto(c.capa, ROJO, 12, 'Jefe');
          const chip = html(c.hud, 'brf-chip', '');
          chip.style.cssText = 'left:24px;top:24px';
          return t => {
            const cae = t > 2.2, revive = suave(tramo(t, 4, 6.5));
            js.forEach((d, i) => {
              let [x, y] = [I.jugadores[i][0] + Math.sin(t * 1.3 + i) * 5, I.jugadores[i][1] + Math.cos(t + i * 2) * 4];
              if (i === 0 && cae) [x, y] = [I.jugadores[0][0] - 10, I.jugadores[0][1] - 8];
              if (i === 1 && t > 3) [x, y] = por([[I.jugadores[1][0], I.jugadores[1][1]], [I.jugadores[0][0] - 2, I.jugadores[0][1] - 4]], suave(tramo(t, 3, 4)));
              d.mover(x, y, i === 0 && cae && revive < 1 ? .45 : 1);
            });
            jefe.mover(...por([I.jefe, [I.puesto[0] - 6, I.puesto[1] - 40]], suave(tramo(t, .3, 7))), suave(tramo(t, 0, .6)));
            set(caido, { cx: I.jugadores[0][0] - 10, cy: I.jugadores[0][1] - 8, opacity: cae && revive < 1 ? 1 : 0, 'stroke-dasharray': `${(57 * revive).toFixed(1)} 57` });
            chip.innerHTML = !cae ? '<small>Oleada 10</small>¡Llegó el Jefe!' : revive < 1 ? `<small>Vos · caído</small>${t < 4 ? `${Math.ceil(30 - (t - 2.2) * 2)} s para que te revivan` : `Luka te revive · ${Math.ceil(5 - revive * 5)} s`}` : '<small>Vos</small>¡De vuelta en la pelea!';
          };
        } }
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
        <div class="brf-paso"></div><h2 class="brf-tit"></h2><p class="brf-txt"></p><div class="brf-dots">${M.pasos.map(() => '<i></i>').join('')}</div></div>
        <div class="brf-der"><svg viewBox="${M.img.vista.join(' ')}" preserveAspectRatio="xMidYMid ${M.img.ajuste || 'slice'}"><image href="${M.img.src}" width="${M.img.w}" height="${M.img.h}"/><g class="capa"></g></svg><div class="brf-hud"></div></div>
        <div class="brf-barra"><i></i></div><div class="brf-ayuda"><b>←</b><b>→</b>pasos · <b>Esc</b>volver a los andenes</div></div>
      <button class="brf-volver" type="button">Volver a los andenes</button>`;
    inner.appendChild(raiz);
    raiz.querySelector('.brf-volver').addEventListener('click', cerrar);
    b = { M, raiz, paso: -1, t0: 0, buscaT0: performance.now(), capa: raiz.querySelector('.capa'), hud: raiz.querySelector('.brf-hud'), frame: null };
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
    b.raiz.querySelector('.brf-paso').textContent = `Paso ${b.paso + 1} / ${M.pasos.length}`;
    b.raiz.querySelector('.brf-tit').textContent = P.tit;
    b.raiz.querySelector('.brf-txt').textContent = P.txt;
    b.raiz.querySelectorAll('.brf-dots i').forEach((d, k) => d.classList.toggle('on', k <= b.paso));
    b.frame = P.escena({ capa: b.capa, hud: b.hud });
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
      const t = (ahora - b.t0) / 1000;
      b.frame(t);
      b.raiz.querySelector('.brf-barra i').style.width = `${Math.min(100, t / DURA * 100)}%`;
      if (t >= DURA && b.paso < b.M.pasos.length - 1) irA(b.paso + 1);
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
    else if (e.key === 'ArrowRight' || e.key === ' ' || e.key === 'Enter') { if (b.paso >= 0) irA(b.paso + 1); }
    else if (e.key === 'ArrowLeft') { if (b.paso >= 0) irA(b.paso - 1); }
    else return;
    e.preventDefault();
    e.stopImmediatePropagation();
  }, true);
})();
