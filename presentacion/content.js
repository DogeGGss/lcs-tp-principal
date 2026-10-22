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
  // Propuesta de precios de la guía (Modelo de negocio). Cosméticos: nada da ventaja en la partida.
  business: [
    {name:'Tienda directa', headline:'Elegí tu identidad', text:'Avatares, banners y apariencias a elección, sin azar.', model:'400 a 3.200 cristales', pending:'Según la rareza: común, raro, épico o legendario.'},
    {name:'Pase de temporada', headline:'Una razón para volver', text:'40 niveles cada 8 semanas, con un camino gratis para todos.', model:'950 cristales', pending:'Devuelve 500 cristales a lo largo de la temporada.'},
    {name:'Cápsulas', headline:'Descubrir una colección', text:'Cosméticos al azar, con las probabilidades a la vista.', model:'100 cristales', pending:'Épico asegurado cada 10. También se ganan jugando.'},
    {name:'Cristales', headline:'Precios en dólares', text:'Paquetes en Steam, con precio regional para Latinoamérica.', model:'US$ 4,99 a 49,99', pending:'Mitad de precio en Latinoamérica. Bienvenida: US$ 0,99.'}
  ],
  // Números del simulador de la guía (Modelo de negocio), en dólares por mes. Ya incluyen el plan:
  // pase y tienda directa, bienvenida a US$ 0,99, juego en inglés y Photon a 10 envíos por segundo.
  // daily: juegan en un día; peak: a la vez en la hora pico; latam: % de los que pagan que son de Latinoamérica;
  // arppuLatam / arppuRest: gasto por mes de cada pagador allá y en el resto (arppu es el promedio de los dos).
  economics: {
    source: 'Simulador de la guía · precios de Photon, Supabase, Steam y Unity de octubre de 2026 · dólar $ 1.550',
    investment: {total:38481, monthlyFixed:16856, salaries:15456,
      items:[['Desarrollo: los 5 sprints con el equipo', 33381], ['Marketing de lanzamiento', 5000], ['Steam Direct', 100]],
      // Detalle para el bloque Costos: los 64 días de los 5 sprints, con sueldos de US$ 1.600 brutos × 1,38.
      detail:[['Sueldos de los 7 durante los 5 sprints', 32539], ['Publicidad de lanzamiento', 5000], ['Otros gastos del desarrollo', 842], ['Publicar en Steam (Steam Direct)', 100]],
      salary:{gross:1600, factor:1.38, perPerson:2208, days:64}},
    // Un mes del escenario promedio: a dónde va lo que pagan los jugadores (left es la ganancia que queda).
    month: {gross:38475, left:2770, items:[['Comisión de Steam (30 %)', 11542], ['Retención de EE.UU. (30 %)', 1616], ['Sueldos de los 7', 15456], ['Photon: servidores de las partidas', 3077], ['Unity Pro: licencias de los 7', 1348], ['Publicidad', 1000], ['Otros gastos', 400], ['Supabase: cuentas y datos', 79], ['Impuesto a las ganancias (30 %)', 1187]]},
    scenarios: [
      {name:'Pesimista', players:25000, daily:5000, peak:780, latam:65, arppuLatam:3.9, arppuRest:12, payers:500, conv:2, arppu:6.73, gross:3367, net:2251, costs:17317, result:-15066, afterTax:-15066, breakEven:276583, payback:null, cash3:-588012, perDollar:null},
      {name:'Promedio', players:100000, daily:25000, peak:4167, latam:45, arppuLatam:6.5, arppuRest:18, payers:3000, conv:3, arppu:12.83, gross:38475, net:25317, costs:21360, result:3957, afterTax:2770, breakEven:82190, payback:31, cash3:16016, perDollar:1.42},
      {name:'Optimista', players:250000, daily:75000, peak:17188, latam:35, arppuLatam:7.8, arppuRest:21.6, payers:10000, conv:4, arppu:16.77, gross:167700, net:108586, costs:29409, result:79177, afterTax:55424, breakEven:46497, payback:3, cash3:1854733, perDollar:49.2}
    ]
  },
  // Agregar rutas de clips reales dentro de presentacion/media/. No se publican archivos personales.
  clips: [
    {title:'Una ronda táctica', detail:'Coordinación · objetivo · resultado', src:null},
    {title:'De maqueta a juego', detail:'Mostrar el menú y una compra real', src:null},
    {title:'Recorrer la universidad', detail:'Un lugar reconocible dentro del juego', src:null}
  ],
  slides: [
    {id:'inicio', title:'Project Riftwalker', label:'Apertura', minutes:1, note:'Presentar al equipo y la idea en una frase. La presentación corresponde al Sprint 3; el informe S1 se usa como base histórica.'},
    {id:'producto', title:'Un universo. Tres formas de jugar.', label:'El juego', minutes:2, note:'Explicar el 4v4, Deathmatch y las oleadas. No afirmar que todos los modos están terminados. El informe S1 habla de oleadas infinitas y la guía de 10: confirmar la regla antes de exponer.'},
    {id:'equipo', title:'Cómo construimos juntos', label:'Equipo', minutes:1, note:'Acuerdos y acciones registrados en el informe S1. Ratificarlos para S3. Agregar integrantes, roles y un ejemplo real de colaboración; no confundir acciones propuestas con resultados.'},
    {id:'metricas', title:'Avance con evidencia', label:'Métricas', minutes:2, note:'S1: 49/59 Done = 83,1 %. Testing: 58/62 ejecutados = 93,5 %; el informe redondea a 94 %. De 64 casos, 3 fallaron, 1 quedó bloqueado y 2 sin ejecutar. No mezclar porcentajes ni vender estos números como actuales.'},
    {id:'decisiones', title:'Decidir para poder avanzar', label:'Tecnología', minutes:1, note:'Elegir una decisión y contar problema → elección → beneficio. La elección de Photon está documentada en S1; actualizar la evidencia de implementación y las limitaciones para S3.'},
    {id:'menus', title:'El diseño se puede probar', label:'Demo de interfaz', minutes:2, note:'Abrir la tienda, elegir categoría y arma, comprar y mostrar su ficha. Estas son maquetas web de la guía, no el ejecutable. Menús y HUD como demos alternativas; el botón Volver al recorrido queda siempre disponible.'},
    {id:'arsenal', title:'Cada arma cambia una decisión', label:'Arsenal', minutes:2, note:'Elegir Mitre y comparar con La Porteña o Último Tren. Precio = moneda de partida, no dinero real. Datos tomados de la guía el 05/10/2026; confirmar balance y disponibilidad antes de S3.'},
    {id:'juego', title:'Ahora, dentro del juego', label:'Demo del juego', minutes:2, note:'Reservado para una demo real o clips. Todavía no hay videos cargados. Preparar una ronda corta y un clip de respaldo; volver con el botón grande, sin buscar pestañas.'},
    {id:'negocio', title:'Entrar gratis. Elegir tu estilo.', label:'Propuesta comercial', minutes:2, note:'Pitch: shooter gratuito para jugar con amigos, con monetización cosmética: nada de lo que se vende da ventaja en la partida. Recorrer las cuatro pestañas: tienda directa, pase, cápsulas y cristales. Los precios están definidos; el pase y la tienda directa todavía no están en el juego.'},
    {id:'escenarios', title:'Tres escenarios, un mismo plan', label:'Números', minutes:2, note:'Los tres escenarios comparten lo decidido: 7 personas con sueldo promedio, los 5 sprints y el producto del sprint 5; cambia cuántos juegan y cuánto pagan. Los números ya incluyen el plan: pase y tienda directa, bienvenida a US$ 0,99, juego en inglés y Photon a 10 envíos por segundo. El promedio gana US$ 4.000 por mes con 100.000 jugadores por mes y deja de perder plata desde 82.000. Las horas de juego son las de un shooter competitivo: en el promedio, el 25 % juega cada día, 2 horas. El pesimista no se sostiene. Son simulaciones con supuestos; el detalle está en la guía.'},
    {id:'costos', title:'A dónde va cada dólar', label:'Costos', minutes:1, note:'La inversión se paga una sola vez: US$ 38.500, y el 85 % son los sueldos de los 7 durante los 5 sprints (US$ 1.600 brutos cada uno, × 1,38 por aguinaldo, contribuciones y ART: US$ 2.208 por persona por mes). Después, en un mes del promedio, de cada US$ 100 que pagan los jugadores: 34 se van en la comisión de Steam y la retención de EE.UU. (30 % de lo que se vende allá), 40 en sueldos, 8 en Photon, 3,5 en Unity Pro (se paga porque se pasan los US$ 200.000 por año), 2,6 en publicidad, 1 en otros gastos, 0,2 en Supabase y 3,1 en el impuesto a las ganancias. Quedan 7,20 de ganancia.'},
    {id:'inversion', title:'Qué se invierte y qué vuelve', label:'Inversión', minutes:1, note:'Los US$ 38.500 del bloque anterior: en el promedio se recuperan en el mes 31 y a los 3 años vuelven US$ 1,42 por cada dólar; en el optimista, en el mes 3. Los 100.000 jugadores por mes del promedio son 25.000 por día y 4.167 a la vez en la hora pico (cada uno juega 2 horas, como en un competitivo): Photon cobra por ese pico. El gasto de US$ 12,83 es el promedio de dos grupos: en Latinoamérica (45 % de los que pagan), el pase cada 8 semanas y un paquete chico por mes dan unos US$ 5, y con la tienda directa US$ 6,50; en el resto (55 %), el pase y un paquete mediano dan unos US$ 15, y con la tienda directa US$ 18. Cerrar con los números a seguir: jugadores por mes, porcentaje que paga y gasto por pagador.'},
    {id:'cierre', title:'Del núcleo jugable a una experiencia completa', label:'Cierre', minutes:1, note:'Cerrar con una demo concreta y dos aprendizajes del Sprint 3. Reemplazar pendientes antes del ensayo final. El tiempo planificado suma exactamente 20 minutos.'}
  ]
};
