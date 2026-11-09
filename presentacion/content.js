// Contenido editable del recorrido. null significa pendiente, nunca cero.
window.PRESENTATION = {
  title: 'Project Riftwalker', edition: 'Sprint 3',
  // Quién lee cada diapositiva: se ve arriba, al lado del sprint. Cada diapositiva puede tener su speaker; si no, va este.
  speaker: 'Equipo Riftwalker',
  updated: '05/10/2026', source: 'InformeSprint1.docx · cierre 28/09/2026',
  metrics: {
    s1: {name:'Sprint 1 · 28/09/2026', planned:59, done:49, review:6, todo:4, tests:64, passed:58, failed:3, blocked:1, unrun:2},
    s2: {name:'Sprint 2 · 12/10/2026', source:'Informe de avance y retrospectiva del Sprint 2', planned:60, done:59, review:0, todo:1, tests:414, passed:414, failed:0, blocked:0, unrun:0},
    // S3: informe de avances del Sprint 3; las pruebas, de la planilla al 27/10 (309 casos: 302 exitosos y 7 sin ejecutar).
    s3: {name:'Sprint 3 · 26/10/2026', source:'Informe de avances del Sprint 3 y planilla del plan de pruebas al 27/10', planned:68, done:66, review:1, todo:1, tests:309, passed:302, failed:0, blocked:0, unrun:7},
    // S4: informe de avances del Sprint 4 (87 US en el tablero: 85 en Done y 2 bloqueadas, la US 290 y la US 308) y
    // planilla del plan de pruebas v2 (398 casos vigentes: 394 exitosos, ninguno con error y 4 bloqueados de la US 189).
    s4: {name:'Sprint 4 · 09/11/2026', source:'Informe de avances del Sprint 4 y planilla del plan de pruebas v2', planned:87, done:85, review:0, todo:0, stuck:2, tests:398, passed:394, failed:0, blocked:4, unrun:0}
  },
  // Propuesta de precios de la guía (Modelo de negocio). Cosméticos: nada da ventaja en la partida.
  business: [
    {name:'Tienda directa', headline:'Elegí tu identidad', text:'Sin azar. Skins de armas en legendario, bailes en épico y el resto en común y raro.', model:'400 a 3.200 cristales', note:'Según la rareza', visual:'tienda'},
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
    investment: {total:20020, monthlyFixed:8087, salaries:6687,
      items:[['Desarrollo: los 5 sprints con el equipo', 14920], ['Marketing de lanzamiento', 5000], ['Steam Direct', 100]],
      // Detalle para el bloque Costos: los 64 días de los 5 sprints. Cada uno cobra el sueldo junior de lo que hizo en el
      // proyecto (desarrollo, testing, UX/UI o Scrum Master) por las horas que trabajó en el Sprint 3 (80 h es tiempo
      // completo): 356,9 h entre los 7, US$ 4.846 brutos, × 1,38.
      detail:[['Sueldos de los 7 durante los 5 sprints', 14078], ['Publicidad de lanzamiento', 5000], ['Otros gastos del desarrollo', 842], ['Publicar en Steam (Steam Direct)', 100]],
      // total: los sueldos de la inversión (detail). hours: las del Sprint 3 (356,9 cada 14 días) durante los 64 días.
      salary:{gross:4846, factor:1.38, team:6687, days:64, total:14078, hours:1631.5}},
    // Un mes del escenario promedio: a dónde va lo que pagan los jugadores (left es la ganancia que queda).
    month: {gross:30525, left:5246, items:[['Comisión de Steam (30 %)', 9158], ['Retención de EE.UU. (30 %)', 1282], ['Sueldos de los 7', 6687], ['Photon: servidores de las partidas', 3077], ['Unity Pro: licencias de los 7', 1348], ['Publicidad', 1000], ['Otros gastos', 400], ['Supabase: cuentas y datos', 79], ['Impuesto a las ganancias (30 %)', 2248]]},
    scenarios: [
      {name:'Pesimista', players:25000, daily:5000, peak:780, latam:65, arppuLatam:3.39, arppuRest:9.86, payers:500, conv:2, arppu:5.66, gross:2828, net:1890, costs:8548, result:-6658, afterTax:-6658, breakEven:155479, payback:null, cash3:-265416, perDollar:null, ramp:9},
      {name:'Promedio', players:100000, daily:25000, peak:4167, latam:45, arppuLatam:4.69, arppuRest:14.66, payers:3000, conv:3, arppu:10.18, gross:30525, net:20086, costs:12591, result:7495, afterTax:5246, breakEven:46486, payback:10, cash3:140970, perDollar:8.04, ramp:6},
      {name:'Optimista', players:250000, daily:75000, peak:17188, latam:35, arppuLatam:5.99, arppuRest:17.06, payers:10000, conv:4, arppu:13.19, gross:131872, net:85387, costs:20640, result:64747, afterTax:45323, breakEven:26408, payback:2, cash3:1533921, perDollar:77.62, ramp:4}
    ],
    // Diapositiva f(x): lo que queda por mes (después del impuesto) según x, los jugadores que pagan en el mes, con el gasto
    // por pagador y las proporciones del promedio (US$ 10,18 y 3 %). points: [x, US$ por mes], cada 250 pagadores.
    // breakEven: desde donde no pierde (1.395); full: desde donde el equipo completo no pierde (1.998, la señal de Escala).
    // example: lo que preguntó Amin, 20.000 jugadores de los que paga el 1 % (200). marks: cada escenario con lo suyo.
    fx: {conv:3, arppu:10.18, breakEven:1395, full:1998, example:{players:20000, conv:1, payers:200, result:-6926},
      points:[[0,-8112],[250,-6643],[500,-5174],[750,-3645],[1000,-2232],[1250,-818],[1500,271],[1750,1232],[2000,2224],[2250,3216],[2500,3265],[2750,4256],[3000,5246],[3250,6218],[3500,7190],[3750,8161],[4000,9133],[4250,10105],[4500,11076],[4750,12013],[5000,12985],[5250,13956],[5500,14928],[5750,15899],[6000,16871],[6250,17843],[6500,18814],[6750,19786],[7000,20758],[7250,21729],[7500,22701],[7750,23672],[8000,24644],[8250,25616],[8500,26587],[8750,27559],[9000,28530],[9250,29502],[9500,30474],[9750,31445],[10000,32417]],
      marks:[{name:'Pesimista', payers:500, result:-6658}, {name:'Promedio', payers:3000, result:5246}, {name:'Optimista', payers:10000, result:45323}]},
    // Diapositiva Si no funciona: puntos de control para que el inversor pierda poco si el juego no despega. Calculado con el
    // modelo de la guía: la publicidad de lanzamiento va en dos tramos de US$ 2.500; en el mes 3, con menos de 700 pagadores
    // (la mitad del equilibrio) no se pone el segundo tramo y se pasa a mantenimiento (2 de los 7 y sin publicidad mensual);
    // en el mes 6, con menos de 1.400, se cierra. En el promedio y el optimista no cambia nada.
    // worst: lo máximo que pierde el inversor en el pesimista con el plan; without: el pesimista a los 3 años sin el plan.
    guard: {firstTranche:2500, checkpoint3:700, checkpoint6:1400, keep:2, worst:-45413, without:-265416, maintenance:2528},
    // Diapositiva Escala: si funciona, los 7 pasan a tiempo completo (80 h por sprint cada uno, con el sueldo junior completo
    // de lo que hace: sueldoCompletoDe en la guía). Calculado con el modelo de la guía, con las mejoras del plan. El cambio
    // llega con la señal: la inversión recuperada y los jugadores por mes desde los que el equipo completo no pierde plata
    // (breakEven). Como pasa después del recupero, el mes de recupero no cambia. El pesimista no llega a la señal.
    // Con más horas sale más contenido y vienen más jugadores: suponemos growth % más (la mitad de lo que suben las horas).
    // payoff: con ese % más de jugadores el equipo completo ya se paga solo en el promedio (el inversor gana lo mismo que hoy).
    // now: lo que queda por mes hoy (economics.scenarios); flat: con los 7 completos y los mismos jugadores; grown: con los 7
    // completos y growth % más de jugadores.
    scale: {hours:356.9, fullHours:560, salaries:6687, fullSalaries:9851, breakEven:66593, growth:30, payoff:19,
      scenarios:[{name:'Promedio', payback:10, now:5246, flat:3031, grown:6529}, {name:'Optimista', payback:2, now:45323, flat:43108, grown:58639}],
      service:'Cada temporada de 8 semanas trae algo nuevo: skins de armas, mapas, la Invasión cooperativa. Vuelven los que dejaron y llegan jugadores nuevos.'}
  },
  // Los cuatro personajes bailando (diapositiva Emotes): PNG animados hechos en Unity con los bailes del juego.
  emotes: [{name:'Unzué', dance:'Flair', src:'media/emotes/alfa-flair.png'}, {name:'Necochea', dance:'Silly', src:'media/emotes/bravo-silly.png'},
    {name:'Goya', dance:'Rumba', src:'media/emotes/charlie-rumba.png'}, {name:'Soledad', dance:'Shuffle', src:'media/emotes/astra-shuffle.png'}],
  // Tiempo por estado (diapositiva Tiempo por estado), del registro del Action «Track US status time» (rama tracker-data).
  // queue: historias esperando prueba (LPR) a las 23:59 de cada día, armado con cuánto estuvo cada una en LPR.
  // wait: espera mediana en LPR de las historias que salieron de LPR en ese sprint. El registro llega al 22/10.
  statusTime: {queue:[['26/09',19],['27/09',5],['28/09',5],['29/09',15],['30/09',22],['01/10',31],['02/10',37],['03/10',44],['04/10',44],['05/10',57],['06/10',87],['07/10',77],['08/10',75],['09/10',63],['10/10',55],['11/10',53],['12/10',25],['13/10',25],['14/10',28],['15/10',26],['16/10',46],['17/10',30],['18/10',24],['19/10',18],['20/10',22],['21/10',23],['22/10',26]],
    sprints:[{name:'Sprint 1', from:'26/09', to:'28/09', wait:'15 h', note:'Datos incompletos: el script se creó a mitad del sprint.'}, {name:'Sprint 2', from:'29/09', to:'12/10', wait:'5,7 días'}, {name:'Sprint 3', from:'13/10', to:'22/10', wait:'3,1 días'}],
    decision:'Pasamos gente de desarrollo a testing.'},
  // Tráiler de la diapositiva 2: el MP4 va en presentacion/media/ (GitHub no acepta más de 100 MB). poster: cuadro del tráiler.
  trailer: {src:'media/trailer.mp4', poster:'media/trailer-poster.jpg'},
  // Diapositiva secreta, después del cierre: Siguiente en «16 / 16» rompe la página y pasa el video del portal abriéndose en la
  // universidad, para anunciar el modo invasión. No figura en el índice, el contador ni el reloj; ← o Esc vuelve al cierre.
  // video: el MP4 en presentacion/media/. El portal se abre en un recuadro con la forma del video y su contorno de luz es el
  // borde del video. Sin video, queda el portal redondo abierto.
  secret: {video:'media/invasion.mp4'},
  // Material de referencia de la historia: Nahuel la narra durante el tráiler. Tres momentos y los modos
  // que la representan, con su mapa. Oleadas va sin mapa ni detalle: se muestra más adelante.
  story: {
    chapters: [
      {title:'Viajar entre dimensiones', image:'media/historia/portal.jpg'},
      {title:'Un nuevo enemigo', image:'media/historia/invasion.jpg'},
      {title:'El programa Riftwalker', image:'media/historia/riftwalkers.jpg'}
    ],
    facets: 'Tres modos, tres facetas de un Riftwalker',
    modes: [
      {name:'Táctico · 4 vs 4', facet:'Entrenamiento táctico: equipo y estrategia', map:'MALVINAS-01', image:'../img/mapa.jpg'},
      {name:'Deathmatch', facet:'Perfeccionamiento individual', map:'Colonia ARA San Juan', image:'media/historia/colonia.jpg'},
      {name:'Oleadas', facet:'Combate directo contra la invasión', map:null, image:'media/historia/oleadas.jpg'}
    ]
  },
  // Lo que sumó cada sprint, para contarlo en voz alta (diapositiva Avance). image: foto grande del sprint;
  // items: lo nuevo, con imágenes chicas opcionales (icons: íconos blancos, sobre un círculo oscuro). Un sprint sin items muestra solo su nombre y fechas.
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
    {name:'Sprint 3', dates:'13/10 al 26/10', title:'Deathmatch y progreso', image:'media/historia/colonia.jpg', items:[
      {text:'Deathmatch en línea, en su propio mapa'},
      {text:'Las habilidades de los cuatro personajes', images:['../img/barrera.png', '../img/vista-aerea.png', '../img/callejero.png', '../img/trasbordo.png'], icons:true},
      {text:'Campo de práctica, tutorial y minijuego de cometas'},
      {text:'Perfil, logros, cápsulas, guardado en la nube y launcher'}
    ]},
    {name:'Sprint 4', dates:'27/10 al 09/11', title:'Invasión y cuentas', image:'media/campus/campus-5.jpg', items:[
      {text:'Modo Invasión en el mapa de la universidad'},
      {text:'Invasores, Watchers, alfas y el Jefe, con cartas en cada puesto'},
      {text:'Cuentas en el launcher y buscar partida'},
      {text:'Emotes y animaciones de todas las armas', images:['media/emotes/alfa-flair.png', 'media/emotes/bravo-silly.png', 'media/emotes/charlie-rumba.png', 'media/emotes/astra-shuffle.png'], round:true}
    ]}
  ],
  slides: [
    {id:'inicio', title:'Project Riftwalker', section:'Juego', label:'Apertura', minutes:1, note:'Presentar al equipo y la idea en una frase. La presentación corresponde al Sprint 3; el informe S1 se usa como base histórica.'},
    {id:'trailer', speaker:'Nahuel Velazquez', title:'Project Riftwalker', section:'Juego', label:'Tráiler', minutes:2, note:'Narrar la historia mientras se reproduce el tráiler. Project RIFTWALKER se desarrolla en un futuro de ciencia ficción en el que la humanidad descubre cómo viajar entre dimensiones. Esto le permite acceder a nuevos recursos y alcanzar un gran desarrollo tecnológico. Sin embargo, a través de los portales dimensionales también aparecen criaturas hostiles que buscan invadir planetas y apropiarse de sus recursos. La amenaza se extiende cuando estas criaturas aprenden a utilizar la misma tecnología que desarrolló la humanidad. Como respuesta, se crea el programa RIFTWALKER, que entrena soldados especiales capaces de atravesar fracturas dimensionales, llamadas Rifts, para combatir a estas criaturas. Dentro del juego, esto se representa mediante tres modos. El primero es el combate táctico 4 contra 4, orientado al trabajo en equipo y la estrategia. El segundo es el Deathmatch, que permite entrenar las habilidades individuales. Finalmente, tenemos el modo de oleadas, donde el jugador debe enfrentarse a una invasión real y sobrevivir a enemigos cada vez más difíciles. Cada modo tiene su propio mapa, diseñado para acompañar su propósito y su ambientación dentro del universo del juego. En conjunto, los tres modos representan distintas facetas de los Riftwalkers: el entrenamiento táctico, el perfeccionamiento individual y el combate directo contra la amenaza interdimensional. La idea central es que una tecnología que inicialmente llevó a la humanidad a una era de prosperidad terminó convirtiéndose en la puerta de entrada de una guerra que amenaza a todo el multiverso. Al terminar el video, Esc y Siguiente.'},
    {id:'menus', speaker:'David Ramos', title:'El diseño se puede probar', section:'Juego', label:'Demo de interfaz', minutes:2, note:'Abrir la tienda, elegir categoría y arma, comprar y mostrar su ficha. Estas son maquetas web de la guía, no el ejecutable. Menús y HUD como demos alternativas; el botón Volver al recorrido queda siempre disponible.'},
    {id:'arsenal', speaker:'Lucas Huber', title:'Cada arma cambia una decisión', section:'Juego', label:'Arsenal', minutes:1, note:'Elegir Mitre y comparar con La Porteña o Último Tren. Precio = moneda de partida, no dinero real. Datos tomados de la guía el 05/10/2026; confirmar balance y disponibilidad antes de S3.'},
    {id:'juego', speaker:'Luka Gómez', title:'El menú, como está en el juego', section:'Juego', label:'Menú, perfil y cápsulas', minutes:2, note:'Abrir el menú: es la versión web del menú del juego, con las mismas imágenes y todo desbloqueado. Entrar al Perfil desde la credencial de arriba a la derecha y, en Personalización, armar una combinación en vivo: un avatar, un banner, un título y un marco (por ejemplo, el conjunto de Entre sombras). La cabecera y la credencial del menú cambian al instante. Volver al menú y abrir la Estación del Rift (la tarjeta de abajo a la izquierda): abrir una cápsula con un boleto y mostrar que las probabilidades están siempre a la vista, con un épico o mejor asegurado cada 10 cápsulas. Los boletos se canjean con créditos o cristales; si se acaban, F9 suma monedas como en el juego.'},
    {id:'metricas', speaker:'Agustín Barreto', title:'Avance con evidencia', section:'Equipo', label:'Métricas', minutes:2, note:'Mostrar el avance de los sprints: historias en Done y pruebas que pasan. S1: 49 de 59 en Done (83,1 %); 58 de 62 pruebas ejecutadas pasan (93,5 %). S2: 59 de 60 en Done (98,3 %; solo quedó sin empezar la US 183, diseño de los personajes) y las 414 pruebas pasan. La velocidad se duplicó: 140 puntos en el S1 y 281 en el S2. S3: 66 de 68 en Done (97,1 %); la US 222 espera el Modo Zombie para probarse completa y la US 169 (penetración de superficies) no se empezó: las dos pasan al Sprint 4. De las pruebas, las 302 ejecutadas pasan (100 %) y quedan 7 sin ejecutar: 6 de la US 169, que no se empezó, y 1 de la US 222, que necesita el Modo Zombie (planilla al 27/10). Además, 32 tests automáticos repiten las pruebas de sonido. S4: 85 de 87 en Done (97,7 %, con el 100 % de los 252 story points); quedan bloqueadas la US 290 (invasores en red), que depende del cooperativo del Sprint 5, y la US 308 (estadísticas de la Invasión nueva). De las pruebas, 394 de 398 pasan (99 %), ninguna falla y las 4 bloqueadas son de la US 189 (rendimiento del mapa de la uni), que también espera el cooperativo.'},
    {id:'avance', speaker:'Agustín Barreto', title:'Lo que sumó cada sprint', section:'Equipo', label:'Avance', minutes:1, note:'Contarlo en voz alta, señalando las imágenes: el Sprint 1 armó el núcleo jugable en el mapa de pruebas; el Sprint 2 llevó el juego online con el Modo Táctico completo en la estación espacial, los personajes con el Trasbordo, cinco armas nuevas y las granadas; el Sprint 3 sumó el Deathmatch en línea en su propio mapa (Colonia ARA San Juan), las habilidades de los cuatro personajes (Barrera, Vista aérea, Callejero y Trasbordo), el campo de práctica con el tutorial y el minijuego de cometas, y el Perfil con logros y ranking global, cápsulas, guardado en la nube y el launcher; el Sprint 4 trajo el Modo Invasión en el mapa de la universidad, con oleadas de invasores, Watchers, alfas y el Jefe, y un puesto de suministros donde se elige una carta de tres; además, las cuentas en el launcher con buscar partida, los emotes y las animaciones de todas las armas.'},
    {id:'negocio', speaker:'Ulises Fonseca', title:'Entrar gratis. Elegir tu estilo.', section:'Financiación', label:'Propuesta comercial', minutes:2, note:'Pitch: shooter gratuito para jugar con amigos, con monetización cosmética: nada de lo que se vende da ventaja en la partida. Contar los cuatro de izquierda a derecha: la tienda directa (el cosmético que elijas, por rareza), el pase de temporada, las cápsulas con sus probabilidades y los paquetes de cristales en Steam. Los precios están definidos; el pase y la tienda directa todavía no están en el juego. Las rarezas cambian: las skins de armas (se suman más adelante) son lo más exclusivo, en legendario; los bailes van en épico, y los avatares, banners y marcos bajan a común y raro.'},
    {id:'escenarios', speaker:'Ulises Fonseca', title:'Tres escenarios, un mismo plan', section:'Financiación', label:'Números', minutes:2, note:'Los tres escenarios comparten lo decidido: 7 personas, cada una con el sueldo junior de lo que hace por las horas que trabaja, los 5 sprints y el producto del sprint 5; cambia cuántos juegan y cuánto pagan. Los números ya incluyen el plan: pase y tienda directa, bienvenida a US$ 0,99, juego en inglés y Photon a 10 envíos por segundo. Cada número grande es lo que queda después de todo: Steam, la retención de EE.UU., los gastos y el impuesto a las ganancias. El promedio deja US$ 5.250 por mes con 100.000 jugadores por mes (US$ 7.500 antes del impuesto) y deja de perder plata desde 46.000; el optimista, US$ 45.300. Las horas de juego son las de un shooter competitivo: en el promedio, el 25 % juega cada día, 2 horas, así que de los 100.000 jugadores por mes hay 4.167 a la vez en la hora pico, que es lo que cobra Photon. Cada uno que paga gasta US$ 10,18 por mes: en Latinoamérica, un paquete chico y el pase con la tienda directa (US$ 4,70); en el resto, un paquete mediano y el pase (US$ 14,70). Lo que se va en Steam, la retención, los gastos y el impuesto está en la diapositiva siguiente. El pesimista no se sostiene. Remarcar que son números de cada mes y que se repiten mientras la gente juegue: no vendemos un juego, vendemos un servicio. Un juego que se compra una vez vende casi todo en el lanzamiento y después se apaga; acá cada temporada de 8 semanas trae un pase nuevo y cosas nuevas en la tienda, y eso vuelve a cobrar. Por eso el recupero se cuenta en meses y a los 3 años el promedio devuelve US$ 8,04 por cada dólar invertido. Son simulaciones con supuestos; el detalle está en la guía.'},
    {id:'fx', speaker:'Ulises Fonseca', title:'Cuánto deja cada jugador que paga', section:'Financiación', label:'Jugadores que pagan', minutes:1, note:'Esta es la f(x): lo que queda por mes según x, la cantidad de jugadores que pagan, con el gasto por pagador del promedio (US$ 10,18). Arranca perdiendo US$ 8.100 por mes, que son los gastos fijos, y cada pagador suma unos US$ 6 después de Steam, la retención y el impuesto. Desde 1.395 pagadores no pierde plata. Definimos qué es que vaya bien con x y no con los jugadores: va bien con 2.000 pagadores o más durante 3 meses seguidos, una vez que terminó la rampa, porque desde ahí también se paga el equipo completo; entre 1.400 y 2.000 se sostiene con el equipo de hoy, y por debajo de 1.400 pierde plata. El ejemplo que nos preguntó Amin: 20.000 jugadores de los que paga el 1 % son 200 pagadores, y eso pierde US$ 6.900 por mes; no es ir bien. Los jugadores no llegan todos el primer mes: crecen hasta el número del escenario en 9, 6 o 4 meses y después se mantienen, porque es un servicio con temporadas. Lo que pasa si no llega está en la diapositiva Si no funciona.'},
    {id:'costos', speaker:'Nicolás Gauna', title:'A dónde va cada dólar', section:'Financiación', label:'Costos', minutes:1, note:'La inversión se paga una sola vez: US$ 20.000, y el 70 % son los sueldos de los 7 durante los 5 sprints. Cada uno cobra el sueldo junior de lo que hizo en el proyecto (desarrollo, testing, UX/UI o Scrum Master) por las horas que trabaja. Son las horas que registró cada uno en el Sprint 3, sobre 80 de tiempo completo: Ulises 89,5, Luka 77,3, Nahuel 64,3, Lucas 52, Agustín 35,5, Nicolás 22 y David 16,3; entre los 7, 357 horas, el 64 % de tiempo completo. Son US$ 4.846 brutos, × 1,38 por aguinaldo, contribuciones y ART: US$ 6.687 por mes. En toda la inversión, al ritmo del Sprint 3, son unas 1.630 horas entre los 7 durante los 5 sprints: cerca de US$ 8,60 por hora, con todo incluido, que a tiempo completo es un sueldo de unos US$ 1.080 brutos por mes (unos $ 1,4 millones en mano). Si preguntan si es poco: es la mediana de la encuesta de sueldos de Sysarmy 2026.1 para un junior en relación de dependencia en Argentina, al dólar oficial, según el rol de cada uno: desde US$ 7,17 por hora en testing hasta US$ 10,77 (Ulises, Scrum Master y desarrollo). Es lo que cuesta de verdad un equipo junior acá, y es una ventaja: por eso el juego se sostiene desde 46.000 jugadores por mes. Después, en un mes del promedio, de los US$ 30.500 que pagan los jugadores: US$ 10.400 se van en la comisión de Steam y la retención de EE.UU. (30 % de lo que se vende allá), US$ 6.700 en sueldos, US$ 3.100 en Photon, US$ 1.350 en Unity Pro (se paga porque se pasan los US$ 200.000 por año), US$ 1.000 en publicidad, US$ 400 en otros gastos, US$ 80 en Supabase y US$ 2.250 en el impuesto a las ganancias. Quedan US$ 5.250 de ganancia.'},
    {id:'escala', speaker:'Nicolás Gauna', title:'Si funciona, los 7 a tiempo completo', section:'Financiación', label:'Escala', minutes:1, note:'Durante los 5 sprints cada uno pone las horas que puede: entre los 7, 357 por sprint, el 64 % de un tiempo completo, y en sueldos son US$ 6.687 por mes. Si el juego funciona, el equipo pasa a tiempo completo: los 7 a 80 horas, 560 por sprint (57 % más), y los sueldos pasan a US$ 9.851 por mes, cada uno con el sueldo junior completo de lo que hace. No se pasa de entrada. La señal es haber recuperado la inversión y tener 67.000 jugadores por mes, que es desde donde el equipo completo no pierde plata (hoy son 46.000). En el promedio llega en el mes 10 y en el optimista en el mes 2; en el pesimista no llega, y el equipo sigue como está. ¿Para qué? Porque es un juego que también es un servicio: con más horas sale más contenido, y cada temporada de 8 semanas trae algo nuevo (skins de armas, mapas, la Invasión cooperativa). Eso trae de vuelta a los que dejaron de jugar y suma jugadores nuevos. Con un 19 % más de jugadores, el equipo completo ya se paga solo, y desde ahí cada jugador de más es ganancia para el inversor. Suponemos un 30 % más, la mitad de lo que suben las horas: el promedio pasa de US$ 5.246 a US$ 6.529 por mes y el optimista de US$ 45.323 a US$ 58.639. Si preguntan: el 30 % es una estimación nuestra; lo firme es que alcanza con un 19 %. Con los mismos jugadores, el equipo completo le costaría al inversor US$ 2.215 por mes (el promedio quedaría en US$ 3.031), pero un juego que no saca nada nuevo pierde jugadores: si con el equipo de hoy se perdiera más del 26 %, el inversor ganaría menos que con el equipo completo. Por cada dólar de la inversión, a los 3 años vuelven US$ 8,04 con el equipo de hoy.'},
    {id:'proteccion', speaker:'Nicolás Gauna', title:'Si no funciona, el inversor pierde poco', section:'Financiación', label:'Si no funciona', minutes:1, note:'No planeamos solo el caso bueno. Hay tres puntos de control, medidos en jugadores que pagan. En el lanzamiento se pone la mitad de la publicidad, US$ 2.500. En el mes 3, si hay menos de 700 pagadores (la mitad de lo que hace falta para no perder), no se pone la otra mitad, se corta la publicidad mensual y el juego pasa a mantenimiento con 2 de los 7: los gastos bajan a unos US$ 2.500 por mes. En el mes 6, si no llega a 1.400 pagadores, se cierra. Con eso, en el escenario pesimista el inversor pierde como mucho US$ 45.400 (la inversión y 6 meses de operación); sin el plan, a los 3 años perdería US$ 265.400. En el promedio y el optimista no cambia nada: a los 3 meses ya tienen 1.500 y 7.500 pagadores. Además, el inversor cobra primero: hasta recuperar lo que puso, la ganancia es suya.'},
    {id:'cierre', title:'Gracias', section:'Cierre', label:'Cierre', minutes:1, note:'Agradecer al profesor y a la clase por la atención y abrir a preguntas. Al terminar las preguntas, Siguiente (o →) abre la diapositiva secreta: la página se rompe y pasa el video del portal en la universidad. ← vuelve al cierre (con pantalla completa, Esc primero la cierra).'}
  ]
};
