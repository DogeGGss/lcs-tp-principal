// Contenido editable del recorrido. null significa pendiente, nunca cero.
window.PRESENTATION = {
  title: 'Project Riftwalker', edition: 'Sprint 3',
  // Quién lee cada diapositiva: se ve arriba, al lado del sprint. Cada diapositiva puede tener su speaker; si no, va este.
  speaker: 'Equipo Riftwalker',
  updated: '05/10/2026', source: 'InformeSprint1.docx · cierre 28/09/2026',
  metrics: {
    s1: {name:'Sprint 1 · 28/09/2026', planned:59, done:49, review:6, todo:4, tests:64, passed:58, failed:3, blocked:1, unrun:2},
    s2: {name:'Sprint 2 · 12/10/2026', source:'Informe de avance y retrospectiva del Sprint 2', planned:60, done:59, review:0, todo:1, tests:414, passed:414, failed:0, blocked:0, unrun:0},
    // S3: informe de avances del Sprint 3. Las pruebas son las de la planilla al 23/10 (el informe las cierra al final del testing).
    s3: {name:'Sprint 3 · 26/10/2026', source:'Informe de avances del Sprint 3 (pruebas al 23/10)', planned:68, done:66, review:1, todo:1, tests:299, passed:267, failed:1, blocked:0, unrun:31}
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
      salary:{gross:4846, factor:1.38, team:6687, days:64}},
    // Un mes del escenario promedio: a dónde va lo que pagan los jugadores (left es la ganancia que queda).
    month: {gross:30525, left:5246, items:[['Comisión de Steam (30 %)', 9158], ['Retención de EE.UU. (30 %)', 1282], ['Sueldos de los 7', 6687], ['Photon: servidores de las partidas', 3077], ['Unity Pro: licencias de los 7', 1348], ['Publicidad', 1000], ['Otros gastos', 400], ['Supabase: cuentas y datos', 79], ['Impuesto a las ganancias (30 %)', 2248]]},
    scenarios: [
      {name:'Pesimista', players:25000, daily:5000, peak:780, latam:65, arppuLatam:3.39, arppuRest:9.86, payers:500, conv:2, arppu:5.66, gross:2828, net:1890, costs:8548, result:-6658, afterTax:-6658, breakEven:155479, payback:null, cash3:-265416, perDollar:null},
      {name:'Promedio', players:100000, daily:25000, peak:4167, latam:45, arppuLatam:4.69, arppuRest:14.66, payers:3000, conv:3, arppu:10.18, gross:30525, net:20086, costs:12591, result:7495, afterTax:5246, breakEven:46486, payback:10, cash3:140970, perDollar:8.04},
      {name:'Optimista', players:250000, daily:75000, peak:17188, latam:35, arppuLatam:5.99, arppuRest:17.06, payers:10000, conv:4, arppu:13.19, gross:131872, net:85387, costs:20640, result:64747, afterTax:45323, breakEven:26408, payback:2, cash3:1533921, perDollar:77.62}
    ]
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
  // La historia del juego (diapositiva Historia, después del tráiler): tres momentos con cuadros del tráiler y los modos
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
    ]}
  ],
  slides: [
    {id:'inicio', title:'Project Riftwalker', section:'Juego', label:'Apertura', minutes:1, note:'Presentar al equipo y la idea en una frase. La presentación corresponde al Sprint 3; el informe S1 se usa como base histórica.'},
    {id:'trailer', title:'Project Riftwalker', section:'Juego', label:'Tráiler', minutes:2, note:'Pasar el tráiler con «Pantalla completa». Al terminar, Esc y Siguiente.'},
    {id:'historia', speaker:'Nahuel Velazquez', title:'Los portales que abrieron una guerra', section:'Juego', label:'Historia', minutes:2, note:'Project RIFTWALKER se desarrolla en un futuro de ciencia ficción en el que la humanidad descubre cómo viajar entre dimensiones. Esto le permite acceder a nuevos recursos y alcanzar un gran desarrollo tecnológico. Sin embargo, a través de los portales dimensionales también aparecen criaturas hostiles que buscan invadir planetas y apropiarse de sus recursos. La amenaza se extiende cuando estas criaturas aprenden a utilizar la misma tecnología que desarrolló la humanidad. Como respuesta, se crea el programa RIFTWALKER, que entrena soldados especiales capaces de atravesar fracturas dimensionales, llamadas Rifts, para combatir a estas criaturas. Dentro del juego, esto se representa mediante tres modos. El primero es el combate táctico 4 contra 4, orientado al trabajo en equipo y la estrategia. El segundo es el Deathmatch, que permite entrenar las habilidades individuales. Finalmente, tenemos el modo de oleadas, donde el jugador debe enfrentarse a una invasión real y sobrevivir a enemigos cada vez más difíciles. Cada modo tiene su propio mapa, diseñado para acompañar su propósito y su ambientación dentro del universo del juego. En conjunto, los tres modos representan distintas facetas de los Riftwalkers: el entrenamiento táctico, el perfeccionamiento individual y el combate directo contra la amenaza interdimensional. La idea central es que una tecnología que inicialmente llevó a la humanidad a una era de prosperidad terminó convirtiéndose en la puerta de entrada de una guerra que amenaza a todo el multiverso.'},
    {id:'menus', speaker:'David Ramos', title:'El diseño se puede probar', section:'Juego', label:'Demo de interfaz', minutes:1, note:'Abrir la tienda, elegir categoría y arma, comprar y mostrar su ficha. Estas son maquetas web de la guía, no el ejecutable. Menús y HUD como demos alternativas; el botón Volver al recorrido queda siempre disponible.'},
    {id:'arsenal', speaker:'Lucas Huber', title:'Cada arma cambia una decisión', section:'Juego', label:'Arsenal', minutes:1, note:'Elegir Mitre y comparar con La Porteña o Último Tren. Precio = moneda de partida, no dinero real. Datos tomados de la guía el 05/10/2026; confirmar balance y disponibilidad antes de S3.'},
    {id:'juego', speaker:'Luka Gómez', title:'El menú, como está en el juego', section:'Juego', label:'Menú, perfil y cápsulas', minutes:1, note:'Abrir el menú: es la versión web del menú del juego, con las mismas imágenes y todo desbloqueado. Entrar al Perfil desde la credencial de arriba a la derecha y, en Personalización, armar una combinación en vivo: un avatar, un banner, un título y un marco (por ejemplo, el conjunto de Entre sombras). La cabecera y la credencial del menú cambian al instante. Mostrar también Emotes: los bailes que van en la rueda de la T. Volver al menú y abrir la Estación del Rift (la tarjeta de abajo a la izquierda): abrir una cápsula con un boleto y mostrar que las probabilidades están siempre a la vista, con un épico o mejor asegurado cada 10 cápsulas. Los boletos se canjean con créditos o cristales; si se acaban, F9 suma monedas como en el juego.'},
    {id:'conjuntos', speaker:'Luka Gómez', title:'Todos los conjuntos', section:'Juego', label:'Conjuntos', minutes:1, note:'Recorrer la lista con la rueda del mouse: cada conjunto junta un avatar, un banner y un marco del mismo personaje y estilo, de los iniciales a los legendarios. Son los 19 que hay hoy en el juego; en el Perfil también se pueden mezclar entre sí.'},
    {id:'emotes', speaker:'Luka Gómez', title:'Cualquier personaje, cualquier baile', section:'Juego', label:'Emotes', minutes:1, note:'Cuatro de los seis bailes del juego, cada uno en un personaje distinto (Unzué hace Flair, Necochea Silly, Goya Rumba y Soledad Shuffle): ningún baile es de un personaje, cualquiera puede equipar cualquiera. Se equipan en el Perfil (pestaña Emotes), van en la rueda de la T y, mientras se baila, la cámara pasa a tercera persona para que el jugador se vea.'},
    {id:'metricas', speaker:'Agustín Barreto', title:'Avance con evidencia', section:'Equipo', label:'Métricas', minutes:1, note:'Mostrar el avance de los sprints: historias en Done y pruebas que pasan. S1: 49 de 59 en Done (83,1 %); 58 de 62 pruebas ejecutadas pasan (93,5 %). S2: 59 de 60 en Done (98,3 %; solo quedó sin empezar la US 183, diseño de los personajes) y las 414 pruebas pasan. La velocidad se duplicó: 140 puntos en el S1 y 281 en el S2. S3: 66 de 68 en Done (97,1 %); la US 222 espera el Modo Zombie para probarse completa y la US 169 (penetración de superficies) no se empezó: las dos pasan al Sprint 4. De las pruebas, 267 de 268 ejecutadas pasan (99,6 %) y quedan 31 por ejecutar antes del cierre (planilla al 23/10). Además, 32 tests automáticos repiten las pruebas de sonido.'},
    {id:'tiempo', speaker:'Agustín Barreto', title:'Dónde se trababan las historias', section:'Equipo', label:'Tiempo por estado', minutes:1, note:'Un Action de GitHub revisa el tablero cada 5 minutos y anota cada cambio de estado de cada historia, con cuánto tiempo estuvo en el anterior. En el Sprint 2 la cola de historias esperando prueba (LPR) creció hasta 87 el 06/10, y la espera mediana llegó a 5,7 días. Con ese dato pasamos gente de desarrollo a testing: al cierre del Sprint 2 la cola bajó a 25, y en el Sprint 3 la espera mediana bajó a 3,1 días. La prueba en sí también se aceleró: 23 horas en promedio en TESTING (28 en el Sprint 2) y 10 en R2D (54 en el Sprint 2). Lo que sigue lento es la espera en LPR: para el Sprint 4, repartir las historias entre todos los testers apenas llegan y sumar más tests automáticos. Falta decir cuándo se tomó la decisión y quiénes pasaron a testing: el informe del Sprint 3 todavía no lo tiene (el registro llega al 22/10).'},
    {id:'avance', speaker:'Agustín Barreto', title:'Lo que sumó cada sprint', section:'Equipo', label:'Avance', minutes:1, note:'Contarlo en voz alta, señalando las imágenes: el Sprint 1 armó el núcleo jugable en el mapa de pruebas; el Sprint 2 llevó el juego online con el Modo Táctico completo en la estación espacial, los personajes con el Trasbordo, cinco armas nuevas y las granadas; el Sprint 3 sumó el Deathmatch en línea en su propio mapa (Colonia ARA San Juan), las habilidades de los cuatro personajes (Barrera, Vista aérea, Callejero y Trasbordo), el campo de práctica con el tutorial y el minijuego de cometas, y el Perfil con logros y ranking global, cápsulas, guardado en la nube y el launcher.'},
    {id:'negocio', speaker:'Ulises Fonseca', title:'Entrar gratis. Elegir tu estilo.', section:'Financiación', label:'Propuesta comercial', minutes:2, note:'Pitch: shooter gratuito para jugar con amigos, con monetización cosmética: nada de lo que se vende da ventaja en la partida. Contar los cuatro de izquierda a derecha: la tienda directa (el cosmético que elijas, por rareza), el pase de temporada, las cápsulas con sus probabilidades y los paquetes de cristales en Steam. Los precios están definidos; el pase y la tienda directa todavía no están en el juego. Las rarezas cambian: las skins de armas (se suman más adelante) son lo más exclusivo, en legendario; los bailes van en épico, y los avatares, banners y marcos bajan a común y raro.'},
    {id:'escenarios', speaker:'Ulises Fonseca', title:'Tres escenarios, un mismo plan', section:'Financiación', label:'Números', minutes:2, note:'Los tres escenarios comparten lo decidido: 7 personas, cada una con el sueldo junior de lo que hace por las horas que trabaja, los 5 sprints y el producto del sprint 5; cambia cuántos juegan y cuánto pagan. Los números ya incluyen el plan: pase y tienda directa, bienvenida a US$ 0,99, juego en inglés y Photon a 10 envíos por segundo. El promedio gana US$ 7.500 por mes con 100.000 jugadores por mes y deja de perder plata desde 46.000. Las horas de juego son las de un shooter competitivo: en el promedio, el 25 % juega cada día, 2 horas. El pesimista no se sostiene. Son simulaciones con supuestos; el detalle está en la guía.'},
    {id:'costos', speaker:'Nicolás Gauna', title:'A dónde va cada dólar', section:'Financiación', label:'Costos', minutes:1, note:'La inversión se paga una sola vez: US$ 20.000, y el 70 % son los sueldos de los 7 durante los 5 sprints. Cada uno cobra el sueldo junior de lo que hizo en el proyecto (desarrollo, testing, UX/UI o Scrum Master) por las horas que trabaja. Son las horas que registró cada uno en el Sprint 3, sobre 80 de tiempo completo: Ulises 89,5, Luka 77,3, Nahuel 64,3, Lucas 52, Agustín 35,5, Nicolás 22 y David 16,3; entre los 7, 357 horas, el 64 % de tiempo completo. Son US$ 4.846 brutos, × 1,38 por aguinaldo, contribuciones y ART: US$ 6.687 por mes. Después, en un mes del promedio, de los US$ 30.500 que pagan los jugadores: US$ 10.400 se van en la comisión de Steam y la retención de EE.UU. (30 % de lo que se vende allá), US$ 6.700 en sueldos, US$ 3.100 en Photon, US$ 1.350 en Unity Pro (se paga porque se pasan los US$ 200.000 por año), US$ 1.000 en publicidad, US$ 400 en otros gastos, US$ 80 en Supabase y US$ 2.250 en el impuesto a las ganancias. Quedan US$ 5.250 de ganancia.'},
    {id:'inversion', speaker:'Nicolás Gauna', title:'Qué se invierte y qué vuelve', section:'Financiación', label:'Inversión', minutes:1, note:'Los US$ 20.000 del bloque anterior: en el promedio se recuperan en el mes 10 y a los 3 años vuelven US$ 8,04 por cada dólar; en el optimista, en el mes 2. Los 100.000 jugadores por mes del promedio son 25.000 por día y 4.167 a la vez en la hora pico (cada uno juega 2 horas, como en un competitivo): Photon cobra por ese pico. El gasto de US$ 10,18 es el promedio de dos grupos: en Latinoamérica (45 % de los que pagan), un paquete chico por mes y el pase dan US$ 3,60 (el pase devuelve 500 de sus 950 cristales, así que renovarlo cuesta US$ 1,11 por mes), y con la tienda directa US$ 4,70; en el resto (55 %), un paquete mediano y el pase dan US$ 12,20, y con la tienda directa US$ 14,70. Cerrar con los números a seguir: jugadores por mes, porcentaje que paga y gasto por pagador.'},
    {id:'cierre', title:'Gracias', section:'Cierre', label:'Cierre', minutes:1, note:'Agradecer al profesor y a la clase por la atención y abrir a preguntas. Al terminar las preguntas, Siguiente (o →) abre la diapositiva secreta: la página se rompe y pasa el video del portal en la universidad. ← vuelve al cierre (con pantalla completa, Esc primero la cierra).'}
  ]
};
