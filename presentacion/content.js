// Contenido editable del recorrido. null significa pendiente, nunca cero.
window.PRESENTATION = {
  title: 'Project Riftwalker', edition: 'Sprint 3',
  updated: '05/10/2026', source: 'InformeSprint1.docx · cierre 28/09/2026',
  metrics: {
    s1: {name:'Sprint 1 · 28/09/2026', planned:59, done:49, review:6, todo:4, tests:64, passed:58, failed:3, blocked:1, unrun:2},
    s2: {name:'Sprint 2 · 12/10/2026', source:'Informe de avance y retrospectiva del Sprint 2', planned:60, done:59, review:0, todo:1, tests:414, passed:414, failed:0, blocked:0, unrun:0},
    s3: {name:'Sprint 3', source:null, planned:null, done:null, review:null, todo:null, tests:null, passed:null, failed:null, blocked:null, unrun:null}
  },
  // Propuesta de precios de la guía (Modelo de negocio). Cosméticos: nada da ventaja en la partida.
  business: [
    {name:'Tienda directa', headline:'Elegí tu identidad', text:'Avatares, banners y marcos a elección, sin azar.', model:'400 a 3.200 cristales', note:'Según la rareza', visual:'tienda'},
    {name:'Pase de temporada', headline:'Una razón para volver', text:'40 niveles cada 8 semanas, con un camino gratis.', model:'950 cristales', note:'Devuelve 500 cristales en la temporada', visual:'pase'},
    {name:'Cápsulas', headline:'Descubrir la colección', text:'Cosméticos al azar, con las probabilidades a la vista.', model:'100 cristales', note:'Épico asegurado cada 10 · también se ganan jugando', visual:'capsulas'},
    {name:'Cristales', headline:'Precios en dólares', text:'Paquetes en Steam, a mitad de precio en Latinoamérica.', model:'US$ 4,99 a 49,99', note:'Bienvenida a US$ 0,99 en Latinoamérica', visual:'cristales'}
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
  // Tráiler de la diapositiva 2: copiar el MP4 a presentacion/media/ y poner la ruta, por ejemplo 'media/trailer.mp4'.
  trailer: {src:null, poster:null},
  // Lo que sumó cada sprint, para contarlo en voz alta (diapositiva Avance). image: foto grande del sprint;
  // items: lo nuevo, con imágenes chicas opcionales. Un sprint sin items muestra solo su nombre y fechas.
  progress: [
    {name:'Sprint 1', dates:'21/09 al 28/09', title:'El núcleo jugable', image:'media/sprints/s1.jpg', items:[
      {text:'Moverse, correr, saltar y agacharse'},
      {text:'Las primeras armas: La Porteña, el Mitre y el cuchillo', images:['media/armas/pistola.png', 'media/armas/fusil.png']},
      {text:'Vida y escudo, HUD y tienda'},
      {text:'Menús, pausa, opciones y controles'}
    ]},
    {name:'Sprint 2', dates:'29/09 al 12/10', title:'Online y Modo Táctico', image:'../img/tactico-vista.jpg', items:[
      {text:'Salas online con código, hasta 8 jugadores'},
      {text:'Modo Táctico: rondas, dispositivo y economía'},
      {text:'Cuatro personajes y el Trasbordo', images:['../img/alfa.png', '../img/bravo.png', '../img/charlie.png', '../img/astra.png'], round:true},
      {text:'Cinco armas nuevas y tres granadas', images:['media/armas/subfusil.png', 'media/armas/rafaga.png', 'media/armas/escopeta.png', 'media/armas/francotirador.png', 'media/armas/pistolaPesada.png']}
    ]},
    {name:'Sprint 3', dates:'13/10 al 26/10', title:null, image:null, items:[]}
  ],
  slides: [
    {id:'inicio', title:'Project Riftwalker', section:'Juego', label:'Apertura', minutes:1, note:'Presentar al equipo y la idea en una frase. La presentación corresponde al Sprint 3; el informe S1 se usa como base histórica.'},
    {id:'trailer', title:'Project Riftwalker', section:'Juego', label:'Tráiler', minutes:2, note:'Pasar el tráiler con «Pantalla completa». Al terminar, Esc y Siguiente.'},
    {id:'menus', title:'El diseño se puede probar', section:'Juego', label:'Demo de interfaz', minutes:2, note:'Abrir la tienda, elegir categoría y arma, comprar y mostrar su ficha. Estas son maquetas web de la guía, no el ejecutable. Menús y HUD como demos alternativas; el botón Volver al recorrido queda siempre disponible.'},
    {id:'arsenal', title:'Cada arma cambia una decisión', section:'Juego', label:'Arsenal', minutes:2, note:'Elegir Mitre y comparar con La Porteña o Último Tren. Precio = moneda de partida, no dinero real. Datos tomados de la guía el 05/10/2026; confirmar balance y disponibilidad antes de S3.'},
    {id:'juego', title:'El menú, como está en el juego', section:'Juego', label:'Menú y perfil', minutes:1, note:'Abrir el menú: es la versión web del menú del juego, con las mismas imágenes y todo desbloqueado. Entrar al Perfil desde la credencial de arriba a la derecha y, en Personalización, armar una combinación en vivo: un avatar, un banner, un título y un marco (por ejemplo, el conjunto de Entre sombras). La cabecera y la credencial del menú cambian al instante. Mostrar también Emotes: los bailes que van en la rueda de la T.'},
    {id:'conjuntos', title:'Todos los conjuntos', section:'Juego', label:'Conjuntos', minutes:1, note:'Recorrer la lista con la rueda del mouse: cada conjunto junta un avatar, un banner y un marco del mismo personaje y estilo, de los iniciales a los legendarios. Son los 19 que hay hoy en el juego; en el Perfil también se pueden mezclar entre sí.'},
    {id:'metricas', title:'Avance con evidencia', section:'Equipo', label:'Métricas', minutes:2, note:'Mostrar el avance de los sprints: historias en Done y pruebas que pasan. S1: 49 de 59 en Done (83,1 %); 58 de 62 pruebas ejecutadas pasan (93,5 %). S2: 59 de 60 en Done (98,3 %; solo quedó sin empezar la US 183, diseño de los personajes) y las 414 pruebas pasan. La velocidad se duplicó: 140 puntos en el S1 y 281 en el S2. Completar el S3 con su informe.'},
    {id:'avance', title:'Lo que sumó cada sprint', section:'Equipo', label:'Avance', minutes:1, note:'Contarlo en voz alta, señalando las imágenes: el Sprint 1 armó el núcleo jugable en el mapa de pruebas; el Sprint 2 llevó el juego online con el Modo Táctico completo en la estación espacial, los personajes con el Trasbordo, cinco armas nuevas y las granadas. El Sprint 3 se completa al cierre del sprint.'},
    {id:'negocio', title:'Entrar gratis. Elegir tu estilo.', section:'Financiación', label:'Propuesta comercial', minutes:2, note:'Pitch: shooter gratuito para jugar con amigos, con monetización cosmética: nada de lo que se vende da ventaja en la partida. Contar los cuatro de izquierda a derecha: la tienda directa (el cosmético que elijas, por rareza), el pase de temporada, las cápsulas con sus probabilidades y los paquetes de cristales en Steam. Los precios están definidos; el pase y la tienda directa todavía no están en el juego.'},
    {id:'escenarios', title:'Tres escenarios, un mismo plan', section:'Financiación', label:'Números', minutes:2, note:'Los tres escenarios comparten lo decidido: 7 personas con sueldo promedio, los 5 sprints y el producto del sprint 5; cambia cuántos juegan y cuánto pagan. Los números ya incluyen el plan: pase y tienda directa, bienvenida a US$ 0,99, juego en inglés y Photon a 10 envíos por segundo. El promedio gana US$ 4.000 por mes con 100.000 jugadores por mes y deja de perder plata desde 82.000. Las horas de juego son las de un shooter competitivo: en el promedio, el 25 % juega cada día, 2 horas. El pesimista no se sostiene. Son simulaciones con supuestos; el detalle está en la guía.'},
    {id:'costos', title:'A dónde va cada dólar', section:'Financiación', label:'Costos', minutes:1, note:'La inversión se paga una sola vez: US$ 38.500, y el 85 % son los sueldos de los 7 durante los 5 sprints (US$ 1.600 brutos cada uno, × 1,38 por aguinaldo, contribuciones y ART: US$ 2.208 por persona por mes). Después, en un mes del promedio, de cada US$ 100 que pagan los jugadores: 34 se van en la comisión de Steam y la retención de EE.UU. (30 % de lo que se vende allá), 40 en sueldos, 8 en Photon, 3,5 en Unity Pro (se paga porque se pasan los US$ 200.000 por año), 2,6 en publicidad, 1 en otros gastos, 0,2 en Supabase y 3,1 en el impuesto a las ganancias. Quedan 7,20 de ganancia.'},
    {id:'inversion', title:'Qué se invierte y qué vuelve', section:'Financiación', label:'Inversión', minutes:2, note:'Los US$ 38.500 del bloque anterior: en el promedio se recuperan en el mes 31 y a los 3 años vuelven US$ 1,42 por cada dólar; en el optimista, en el mes 3. Los 100.000 jugadores por mes del promedio son 25.000 por día y 4.167 a la vez en la hora pico (cada uno juega 2 horas, como en un competitivo): Photon cobra por ese pico. El gasto de US$ 12,83 es el promedio de dos grupos: en Latinoamérica (45 % de los que pagan), el pase cada 8 semanas y un paquete chico por mes dan unos US$ 5, y con la tienda directa US$ 6,50; en el resto (55 %), el pase y un paquete mediano dan unos US$ 15, y con la tienda directa US$ 18. Cerrar con los números a seguir: jugadores por mes, porcentaje que paga y gasto por pagador.'},
    {id:'cierre', title:'Del núcleo jugable a una experiencia completa', section:'Cierre', label:'Cierre', minutes:1, note:'Cerrar con una demo concreta y dos aprendizajes del Sprint 3. Reemplazar pendientes antes del ensayo final. El tiempo planificado suma exactamente 20 minutos.'}
  ]
};
