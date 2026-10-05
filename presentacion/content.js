// Contenido editable del recorrido. null significa pendiente, nunca cero.
window.PRESENTATION = {
  title: 'Project Riftwalker', edition: 'Sprint 3 · en preparación',
  updated: '05/10/2026', source: 'InformeSprint1.docx · cierre 28/09/2026',
  metrics: {
    s1: {name:'Sprint 1 · 28/09/2026', planned:59, done:49, review:6, todo:4, tests:64, passed:58, failed:3, blocked:1, unrun:2},
    s3: {name:'Sprint 3 · pendiente de cierre', source:null, planned:null, done:null, review:null, todo:null, tests:null, passed:null, failed:null, blocked:null, unrun:null}
  },
  agreements: [
    ['Una historia por PR','Cambios pequeños, revisión y vínculo con la US.'],
    ['Sin sorpresas al integrar','Traer develop y avisar antes de editar escenas compartidas.'],
    ['Done exige evidencia','Probar los criterios de aceptación, no solo que “funcione”.'],
    ['Carga de trabajo visible','Revisar el reparto y tomar las pruebas cuando llegan a LPR.']
  ],
  decisions: [
    {name:'UI por código', problem:'Conflictos en escenas compartidas', choice:'ShopUIKit + maquetas interactivas', gain:'Medidas y estilo comunes', source:'DT-02 / DT-03 · Sprint 1'},
    {name:'Combate por datos', problem:'Cada arma necesita un balance propio', choice:'Núcleo compartido + fichas de armas', gain:'Ajustar daño sin duplicar disparos', source:'DT-08 · Sprint 1'},
    {name:'Multijugador', problem:'Conectar jugadores de distintas computadoras', choice:'Photon PUN 2', gain:'Integración de red sin un servidor propio', source:'DT-10 · decisión del Sprint 1; demo S3 pendiente'}
  ],
  business: [
    {name:'Skins', headline:'Elegí tu identidad', text:'Apariencias de armas y personajes.', model:'Compra directa de cosméticos', pending:'Falta definir catálogo y precios'},
    {name:'Pase de batalla', headline:'Una razón para volver', text:'Progresión por temporada y recompensas visuales.', model:'Ruta gratuita + ruta premium', pending:'Falta definir duración y recompensas'},
    {name:'Lootboxes', headline:'Descubrir una colección', text:'Recompensas cosméticas aleatorias.', model:'Mecanismo a evaluar', pending:'Falta decidir obtención, probabilidades y duplicados'}
  ],
  // Agregar rutas de clips reales dentro de presentacion/media/. No se publican archivos personales.
  clips: [
    {title:'Una ronda táctica', detail:'Coordinación · objetivo · resultado', src:null},
    {title:'De maqueta a juego', detail:'Mostrar el menú y una compra real', src:null},
    {title:'Recorrer la universidad', detail:'Un lugar reconocible dentro del juego', src:null}
  ],
  slides: [
    {id:'inicio', title:'Project Riftwalker', label:'Apertura', minutes:1, note:'Presentar al equipo y la idea en una frase. La presentación corresponde al Sprint 3; el informe S1 se usa como base histórica.'},
    {id:'producto', title:'Un universo. Tres formas de jugar.', label:'El juego', minutes:2, note:'Explicar el 4v4, Deathmatch y las oleadas. No afirmar que todos los modos están terminados. El informe S1 habla de oleadas infinitas y la guía de 10: confirmar la regla antes de exponer.'},
    {id:'equipo', title:'Cómo construimos juntos', label:'Equipo', minutes:2, note:'Acuerdos y acciones registrados en el informe S1. Ratificarlos para S3. Agregar integrantes, roles y un ejemplo real de colaboración; no confundir acciones propuestas con resultados.'},
    {id:'metricas', title:'Avance con evidencia', label:'Métricas', minutes:3, note:'S1: 49/59 Done = 83,1 %. Testing: 58/62 ejecutados = 93,5 %; el informe redondea a 94 %. De 64 casos, 3 fallaron, 1 quedó bloqueado y 2 sin ejecutar. No mezclar porcentajes ni vender estos números como actuales.'},
    {id:'decisiones', title:'Decidir para poder avanzar', label:'Tecnología', minutes:2, note:'Elegir dos decisiones y contar problema → elección → beneficio. La elección de Photon está documentada en S1; actualizar la evidencia de implementación y las limitaciones para S3.'},
    {id:'menus', title:'El diseño se puede probar', label:'Demo de interfaz', minutes:3, note:'Abrir la tienda, elegir categoría y arma, comprar y mostrar su ficha. Estas son maquetas web de la guía, no el ejecutable. Menús y HUD como demos alternativas; el botón Volver al recorrido queda siempre disponible.'},
    {id:'arsenal', title:'Cada arma cambia una decisión', label:'Arsenal', minutes:2, note:'Elegir Mitre y comparar con La Porteña o Último Tren. Precio = moneda de partida, no dinero real. Datos tomados de la guía el 05/10/2026; confirmar balance y disponibilidad antes de S3.'},
    {id:'juego', title:'Ahora, dentro del juego', label:'Demo del juego', minutes:2, note:'Reservado para una demo real o clips. Todavía no hay videos cargados. Preparar una ronda corta y un clip de respaldo; volver con el botón grande, sin buscar pestañas.'},
    {id:'negocio', title:'Entrar gratis. Elegir tu estilo.', label:'Propuesta comercial', minutes:2, note:'Pitch: shooter gratuito para jugar con amigos, identidad propia y monetización cosmética. Skins, pase y lootboxes son propuestas, no sistemas implementados. Validar el criterio de no vender ventajas. Precios, economía, temporadas y condiciones de lootboxes quedan por definir.'},
    {id:'cierre', title:'Del núcleo jugable a una experiencia completa', label:'Cierre', minutes:1, note:'Cerrar con una demo concreta y dos aprendizajes del Sprint 3. Reemplazar pendientes antes del ensayo final. El tiempo planificado suma exactamente 20 minutos.'}
  ]
};
