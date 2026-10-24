// Marcos del Perfil como en PerfilUI.Enmarcar e InteriorMarco. Lo usan el menú y la presentación.
// El alto del recuadro manda: el riel queda del mismo grosor arriba y a los costados. Cada mitad se corta en nueve con la
// esquina y medio adorno del centro fijos; los rieles se estiran o se repiten. Las mitades están a la mitad del original.
window.Marcos = (() => {
  const C = window.CATALOGO, ALTO = 1280, BORDE = 230, ESC = 0.5;
  // HTML del marco para un recuadro de w × h en (x, y). adentro: sin salirse del recuadro (las cartas de la colección).
  function html(id, x, y, w, h, adentro, base = 'img/marcos/') {
    if (!id || !C.rieles[id]) return '';
    const r = adentro ? [0, 0, 0, 0] : C.rieles[id];
    const s = h / (ALTO - r[1] - r[3]);
    const ancho = w + (r[0] + r[2]) * s, alto = h + (r[1] + r[3]) * s;
    const [esq, cen, rep] = C.cortes[id];
    const bv = BORDE * s, be = esq * s, bc = cen * s;
    const mitad = (lado) => {
      const borde = lado === 'i' ? `${bv}px ${bc}px ${bv}px ${be}px` : `${bv}px ${be}px ${bv}px ${bc}px`;
      const corte = (lado === 'i' ? [BORDE, cen, BORDE, esq] : [BORDE, esq, BORDE, cen]).map(v => v * ESC).join(' ');
      return `<div style="position:absolute;top:0;height:100%;box-sizing:border-box;left:${lado === 'i' ? 0 : ancho / 2}px;width:${ancho / 2}px;` +
        `border-style:solid;border-color:transparent;border-width:${borde};border-image:url(${base}${id}-${lado}.png) ${corte} ${rep ? 'round' : 'stretch'}"></div>`;
    };
    return `<div class="marco" style="position:absolute;pointer-events:none;left:${x - r[0] * s}px;top:${y - r[1] * s}px;width:${ancho}px;height:${alto}px">${mitad('i')}${mitad('d')}</div>`;
  }
  // Cuánto hay desde cada borde del recuadro hasta el borde interior del riel: ahí termina el banner.
  function interior(id, h, adentro) {
    if (!id || !C.rieles[id]) return [0, 0, 0, 0];
    const r = adentro ? [0, 0, 0, 0] : C.rieles[id], it = C.interiores[id];
    const s = h / (ALTO - r[1] - r[3]);
    return it.map((v, i) => Math.max((v - r[i]) * s - 2, 0));
  }
  // Los conjuntos: cada banner con marco y el avatar del mismo personaje y estilo.
  function conjuntos() {
    return C.banners.filter(b => C.rieles[b.id]).map(b => ({
      banner: b, marco: C.marcos.find(m => m.id === b.id),
      avatar: C.avatares.find(a => a.personaje === b.personaje && a.estilo === b.estilo),
    }));
  }
  return { html, interior, conjuntos };
})();
