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
  // Números del simulador de la guía (Modelo de negocio), en dólares por mes. "con": con las mejoras del plan.
  economics: {
    source: 'Simulador de la guía · precios de Photon, Supabase, Steam y Unity de octubre de 2026 · dólar $ 1.550',
    plan: ['Pase de temporada y tienda directa', 'Bienvenida a US$ 0,99', 'Juego en inglés', 'Photon a 10 envíos por segundo'],
    investment: {total:38481, monthlyFixed:16856, salaries:15456,
      items:[['Desarrollo: los 5 sprints con el equipo', 33381], ['Marketing de lanzamiento', 5000], ['Steam Direct', 100]]},
    scenarios: {
      con: [
        {name:'Pesimista', players:25000, payers:500, conv:2, arppu:6.73, gross:3367, net:2251, costs:17104, result:-14853, afterTax:-14853, breakEven:237305, payback:null, cash3:-581231, perDollar:null},
        {name:'Promedio', players:100000, payers:3000, conv:3, arppu:12.82, gross:38475, net:25317, costs:20129, result:5188, afterTax:3631, breakEven:76982, payback:24, cash3:45760, perDollar:2.19},
        {name:'Optimista', players:250000, payers:10000, conv:4, arppu:16.77, gross:167700, net:108586, costs:25253, result:83332, afterTax:58333, breakEven:44088, payback:3, cash3:1955095, perDollar:51.81}
      ],
      sin: [
        {name:'Pesimista', players:25000, payers:375, conv:1.5, arppu:4.4, gross:1650, net:1120, costs:17221, result:-16101, afterTax:-16101, breakEven:699254, payback:null, cash3:-621092, perDollar:null},
        {name:'Promedio', players:100000, payers:2500, conv:2.5, arppu:9, gross:22500, net:15041, costs:19583, result:-4542, afterTax:-4542, breakEven:148909, payback:null, cash3:-232231, perDollar:null},
        {name:'Optimista', players:250000, payers:8750, conv:3.5, arppu:12, gross:105000, net:69090, costs:28038, result:41052, afterTax:28736, breakEven:76273, payback:4, cash3:933031, perDollar:25.25}
      ]
    }
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
    {id:'escenarios', title:'Tres escenarios, un mismo plan', label:'Números', minutes:2, note:'Los tres escenarios comparten lo decidido: 7 personas con sueldo promedio, los 5 sprints y el producto del sprint 5; cambia cuántos juegan y cuánto pagan. Con el plan, el promedio gana US$ 5.200 por mes con 100.000 jugadores por mes y deja de perder plata desde 77.000. Tocar «Sin el plan» para mostrar por qué hacen falta las mejoras: el promedio pierde US$ 4.500. El pesimista no se sostiene. Son simulaciones con supuestos; el detalle está en la guía.'},
    {id:'inversion', title:'Qué se invierte y qué vuelve', label:'Inversión', minutes:2, note:'Se invierten US$ 38.500: casi todo es el desarrollo de los 5 sprints. Después, el juego necesita unos US$ 16.900 fijos por mes, y los sueldos son US$ 15.500 de eso. Con el plan, en el promedio se recupera en el mes 24 y a los 3 años vuelven US$ 2,19 por cada dólar; en el optimista, en el mes 3. Cerrar con los tres números a seguir: jugadores por mes, porcentaje que paga y gasto por pagador.'},
    {id:'cierre', title:'Del núcleo jugable a una experiencia completa', label:'Cierre', minutes:1, note:'Cerrar con una demo concreta y dos aprendizajes del Sprint 3. Reemplazar pendientes antes del ensayo final. El tiempo planificado suma exactamente 20 minutos.'}
  ]
};
