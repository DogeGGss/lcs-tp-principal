# Presentación del Sprint 5 (entrega final)

Abrir `/presentacion/` desde el enlace independiente de la guía. El recorrido suma 20 minutos y se divide en tres partes, en este orden: Juego (apertura, tráiler, demos, menú, perfil y cápsulas), Equipo (las métricas de cada sprint y lo que sumó cada uno) y Financiación (propuesta comercial, números, costos y escala), y después el cierre. Cada diapositiva muestra arriba su parte, y el índice las agrupa. No avanza automáticamente: el operador controla el ritmo.

## Para exponer

- Anterior / Siguiente: ubicación fija y botones grandes. Flechas o Page Up / Page Down también funcionan cuando el foco está en la diapositiva.
- Índice: saltar a cualquiera de los trece bloques.
- Proyector: fondo claro con texto oscuro para salas iluminadas.
- Pantalla completa: ocultar el navegador; F11 es alternativa.
- Tráiler: «Pantalla completa» lo reproduce ocupando toda la pantalla; Esc vuelve. Mientras el video está en pantalla completa, las flechas no cambian de diapositiva.
- Tiempo: iniciar/pausar el reloj de 20 minutos. El reloj no cambia las diapositivas y sigue durante las demos.
- Las demos se abren dentro del recorrido, con Volver y Reiniciar siempre arriba. Son maquetas web; no se presentan como ejecución del juego.

## Actualización por etapas

Editar `content.js`: títulos, partes (`section`), tiempos, guion, quién lee cada diapositiva (`speaker`, se ve arriba al lado del sprint), tráiler, avance de cada sprint (`progress`), métricas, propuesta comercial y números económicos. Mantener los tiempos de los trece bloques sumando 20 minutos. Las métricas desconocidas usan `null`, no cero. Los datos S1 son una foto histórica y nunca deben sobrescribirse con S3.

`metrics` tiene los tres sprints (`s1` del informe del Sprint 1; `s2` del informe de avance y la retrospectiva del Sprint 2; `s3` pendiente): la diapositiva Métricas los muestra lado a lado para mostrar el avance. Un sprint sin datos muestra rayas. Las diapositivas no llevan textos internos (pendientes, «por completar», «a confirmar»): eso va en el guion o en esta lista. Fuente histórica: InformeSprint1.docx facilitado por el equipo, cierre 28/09/2026. No se publica el documento completo.

Menú, perfil y cápsulas (diapositiva 6) abre `/menu/`: una copia web del menú principal, del Perfil y de la Estación del Rift del juego, con las medidas de `MenuRiftwalker`, `PantallaPerfil` y `PantallaEstacion`, las imágenes de `Resources/Perfil` y todo desbloqueado en el Perfil. La diapositiva Conjuntos lista los 19 conjuntos (avatar, banner y marco del mismo personaje y estilo) con `menu/marcos.js`, el mismo código que arma los marcos del menú. El catálogo (`menu/catalogo.js`) se genera desde `PerfilDatos.cs`: si cambian los cosméticos del juego, hay que regenerarlo y volver a copiar las imágenes.

El tráiler está en `media/trailer.mp4` (`trailer.src`), con un cuadro del propio video como portada (`trailer.poster`, `media/trailer-poster.jpg`). Es el del equipo recomprimido a 1080p, unos 4,5 Mbps (71,5 MB), con el índice al principio para que arranque sin bajarlo entero: GitHub no acepta archivos de más de 100 MB. Si se cambia, recomprimirlo por debajo de eso. Para otros videos, completar `clips[].src` de la misma forma. Actualmente no hay clips y no se simula que los haya. Evitar videos pesados; un enlace externo puede servir de respaldo fuera del recorrido.

`arsenal.js` es una instantánea de las siete armas marcadas En el juego por la guía al 05/10/2026. No equivale a validar una build. Al actualizar el balance, regenerar o ajustar esa instantánea y su fecha. La pistola usa la recarga de 1,5 s indicada por EXTRA en la guía. Las imágenes de `media/armas/` (una por `id`) son renders de los modelos de primera persona del juego, de costado y con fondo transparente, hechos desde Unity el 23/10/2026; si cambia un modelo, hay que volver a renderizarla.

La propuesta comercial y los bloques Números, Costos y Escala (antes del cierre) salen de la sección Modelo de negocio de la guía: juego gratuito en Steam con cristales de pago, solo cosméticos. `business` tiene los productos y precios, y `visual` elige la imagen de cada uno (tienda, pase, cápsulas o cristales); desde el Sprint 5 los cuatro están en el juego, en la Tienda del menú principal. `economics` tiene los números del simulador de la guía: los tres escenarios (pesimista, promedio y optimista), todos con las mejoras del plan, y la inversión de los 5 sprints. Son simulaciones con supuestos, no resultados ni cifras de mercado. Al cambiar un supuesto en la guía, volver a copiar los números.

## Pendientes para el ensayo final

- Probar el tráiler en pantalla completa en el proyector.
- Cargar el video de la diapositiva secreta (`secret.video`, el portal abriéndose en la universidad) y ensayarla en el proyector, con sonido.
- Actualizar disponibilidad/balance de armas contra el ejecutable que se mostrará.
- Cargar clips y ensayar la demo real con respaldo.
- Confirmar con el equipo los precios y las mejoras del plan (pase y tienda directa, bienvenida a US$ 0,99, juego en inglés y Photon a 10 envíos por segundo).
- Probar en el proyector real, resolución y conexión disponibles.

Las fuentes tipográficas y las imágenes se sirven localmente. La presentación principal no solicita métricas a GitHub durante la exposición. Las maquetas reutilizan la guía y sus dependencias; para una sesión sin internet, servir una copia completa del sitio localmente y comprobarla antes.

## Verificación de la parte económica (22/10/2026)

Se sumaron los bloques Números, Costos e Inversión antes del cierre, y equipo, métricas, decisiones, la demo de interfaz e Inversión bajaron un minuto cada uno para seguir en 20 minutos. Costos detalla la inversión inicial (sueldos, publicidad, otros gastos y Steam Direct) y un mes del promedio: comisión de Steam, retención de EE.UU., sueldos, Photon, Unity Pro, publicidad, otros gastos, Supabase, impuesto a las ganancias y la ganancia que queda. Los trece bloques quedan por encima de los controles a 1280 × 720 y 1024 × 768, sin desbordes. La propuesta comercial muestra los cuatro productos a la vez, cada uno con su imagen (avatares por rareza, el pase con sus premios, las probabilidades de las cápsulas y los paquetes de cristales), sin errores de JavaScript. Las diapositivas no muestran los minutos de cada bloque ni la aclaración de que los números son simulados; los minutos quedan en el índice para ensayar.

## Verificación de esta versión (05/10/2026)

Recorrido revisado en navegador a 1280 × 720 y 1024 × 768: las diez pantallas y las siete fichas de armas quedan por encima de los controles. Probados índice, navegación, reloj con pausa y reinicio, guion, oscurecimiento, contraste para proyector, métricas S1/S3 y pestañas comerciales/técnicas. Comprobadas compra en tienda, selección de modo, cambio de estado del HUD y regreso al recorrido. Sin errores de JavaScript registrados durante esas pruebas.

La pantalla completa depende del navegador; el navegador integrado no la activó durante la prueba. El botón informa la alternativa F11. Falta probar en el equipo y proyector de la exposición.

Al completar S3, actualizar también `metrics.s3.name` y `metrics.s3.source`. Hasta completar todos sus valores se conserva la pantalla de pendientes.

## Emotes y tiempo por estado (24/10/2026)

Emotes muestra a los cuatro personajes bailando, cada uno con un baile distinto (Unzué Flair, Necochea Silly, Goya Rumba y Soledad Shuffle). Son PNG animados en `media/emotes/`, grabados de la vista previa de emotes del Perfil tal como la dibuja el juego en Play (`VistaPreviaDeEmote.Mostrar` y su textura de 600 × 640, cuadro por cuadro con el tiempo del juego fijo), recortados a la misma escala, con fondo transparente y a 15 cuadros por segundo (Flair, 30). Grabarlos en modo edición, moviendo la animación a mano, no da el mismo baile que el juego.

Tiempo por estado sale del registro del Action «Track US status time» (rama `tracker-data`, `.github/project_status_log.csv`): cuántas historias esperan prueba (LPR) al final de cada día y la espera mediana en LPR de cada sprint. El registro llega al 22/10/2026 (después venció el token del Action). Para hacerles lugar, Arsenal y Métricas bajaron a un minuto cada uno.

## Tráiler e historia (25/10/2026)

Historia va después del tráiler y cuenta el universo del juego en tres momentos (los portales, la invasión y el programa Riftwalker), cada uno con un cuadro del tráiler (`media/historia/`), y abajo los modos que representan las facetas de un Riftwalker con su mapa: Táctico en MALVINAS-01 (`../img/mapa.jpg`) y Deathmatch en Colonia ARA San Juan (la imagen de su pantalla de carga). Oleadas va sin mapa ni detalle, con la ilustración de su tarjeta del juego: se muestra más adelante. El texto está en `story` (content.js) y el guion completo, para leer en voz alta, en la nota de la diapositiva. Para hacerle lugar, la demo de interfaz e Inversión bajaron a un minuto cada una. Revisado a 1280 × 720, 1024 × 768 y 1920 × 1080: queda por encima de los controles, sin desbordes.

## Sprint 3 (25/10/2026)

Métricas y Avance usan el «Informe de avances - Sprint 3»: 66 de las 68 historias en Done (la US 222 espera el Modo Zombie y la US 169 no se empezó) y, de los 309 casos de la planilla al 27/10, los 302 ejecutados pasan y quedan 7 sin ejecutar (6 de la US 169 y 1 de la US 222). Avance suma el Sprint 3 (Deathmatch en su mapa, las habilidades con sus íconos, campo de práctica, tutorial y cometas, y perfil, logros, cápsulas, guardado y launcher) con la vista aérea de Colonia ARA San Juan. Las notas de Métricas, Tiempo por estado y Avance tienen los datos del informe para decirlos en voz alta. Revisado a 1280 × 720 y 1024 × 768: queda por encima de los controles.

## Quién lee y diapositiva secreta (25/10/2026)

Lee cada diapositiva: Historia, Nahuel Velazquez; Demo de interfaz, David Ramos; Arsenal, Lucas Huber; Menú, perfil y cápsulas, Luka Gómez; Métricas y Avance, Agustín Barreto; Propuesta comercial y Números, Ulises Fonseca; Costos e Inversión, Nicolás Gauna. Apertura, Tráiler y Cierre quedan como «Equipo Riftwalker».

Después del cierre hay una diapositiva secreta: en «16 / 16» Siguiente (o →, o el control remoto) sigue habilitado y rompe la página de a poco, en unos 8 segundos: empieza con fallas chiquitas que van creciendo (temblor, colores separados, franjas invertidas y pedazos corridos), a los 2 s se agrieta desde un golpe cerca del centro, entre los 4 y los 7 s la barra de arriba, los controles y la diapositiva se aflojan y se caen de a uno, y en el lugar del golpe se abre una grieta de luz que se vuelve el portal; ahí pasa el video de `secret.video` a pantalla completa, con sonido (el navegador lo deja porque ya hubo clics en la página). No figura en el índice, en el contador ni en el reloj, y no cambia la dirección. ← o Esc vuelve al cierre como estaba (en pantalla completa, Esc primero sale de la pantalla completa: usar ←). Sin video, termina en el portal abierto. El video se precarga al llegar a Inversión. Probada a 1280 × 720 con el tráiler como video de prueba.

## Sueldos por horas (26/10/2026)

Los sueldos salen de las horas que registró cada uno en el Sprint 3 (Retrospectiva y métricas del Sprint 3): cada uno cobra el sueldo junior de lo que hace por sus horas, sobre 80 h de tiempo completo por sprint (356,9 h entre los 7, el 64 %). Los sueldos pasan de US$ 7.139 a US$ 4.846 brutos por mes (US$ 6.687 con cargas) y la inversión, de US$ 26.700 a US$ 20.000. Números, Costos e Inversión tienen los números nuevos del simulador de la guía; en el promedio se gana US$ 7.495 por mes antes del impuesto (US$ 5.246 después), el equilibrio baja a 46.000 jugadores y se recupera en el mes 10.

En pantallas bajas (1280 × 720 y 1024 × 768), la última columna de la fila de abajo de Inversión es más ancha (1,4 veces las otras): el gasto por pagador entra en dos renglones y la fila queda por encima de los controles.

## Video de la diapositiva secreta (26/10/2026)

La diapositiva secreta pasa media/invasion.mp4 (1920 × 1080, 28 s, 15,5 MB, tal como llegó: ya empieza a reproducirse mientras baja). La grieta de luz ya no se abre en un círculo: se abre en un recuadro con la forma del video, en el centro (88 % del ancho o 84 % del alto de la pantalla, lo que entre), y su contorno de luz es el borde del video, que pasa adentro. Sin video queda el portal redondo. Probado a 1280 × 720 y 1920 × 1080: el video y el recuadro coinciden y ← lo cierra y vuelve al cierre.

## Sin Conjuntos ni Emotes (27/10/2026)

Se sacaron las diapositivas Conjuntos y Emotes: los emotes no entran en la entrega del Sprint 3 (v0.3.0). Quedan catorce bloques; para seguir en 20 minutos, Menú, perfil y cápsulas y la Demo de interfaz pasan a 2 minutos cada una. El código que las dibuja (`sets()`, `dances()`) y los PNG de `media/emotes/` siguen, por si vuelven.

## Sin Tiempo por estado (27/10/2026)

Se sacó la diapositiva Tiempo por estado. Quedan trece bloques; para seguir en 20 minutos, Métricas pasa a 2 minutos (la lee Agustín, como la que se sacó). El código que la dibuja (`statusTime()`) y sus datos siguen, por si vuelve.

## Números con la ganancia neta (27/10/2026)

Cada escenario muestra en grande lo que queda después de todo: Steam, la retención de EE.UU., los gastos y el impuesto a las ganancias (`afterTax`), igual que la última línea de Costos. La fila «Steam, retención e impuestos» hace que la tarjeta cierre: ganancias − Steam e impuestos − costos = lo que queda. El recupero, los 3 años y el equilibrio ya usaban ese número (el equilibrio es igual antes y después del impuesto).

## Escala (04/11/2026)

Escala reemplaza a Inversión, que se sacó porque sus números ya están en Números y Costos (su código, `investment()`, queda). Muestra qué pasa si el juego funciona: el equipo pasa a tiempo completo. Son tres paradas (hoy, la señal y tiempo completo) y abajo lo que gana el inversor con el equipo completo en el promedio y el optimista, y por qué: más contenido trae más jugadores. Hoy entre los 7 son 357 horas por sprint (el 64 % de un tiempo completo) y US$ 6.687 por mes en sueldos; a tiempo completo, 560 horas y US$ 9.851, cada uno con el sueldo junior completo de lo que hace. La señal es la inversión recuperada y 67.000 jugadores por mes, desde donde el equipo completo no pierde plata: en el promedio llega en el mes 10 y en el optimista en el mes 2; en el pesimista no llega. Con más horas sale más contenido y vienen más jugadores: con un 19 % más el equipo completo ya se paga solo, y la diapositiva muestra lo que queda con un 30 % más (la mitad de lo que suben las horas, una estimación nuestra): el promedio pasa de US$ 5.246 a US$ 6.529 por mes y el optimista de US$ 45.323 a US$ 58.639. Los números (`economics.scale`) salen del modelo de la guía, con las mejoras del plan; si cambia un supuesto, volver a calcularlos. La lee Nicolás Gauna, como Costos, y dura el minuto de Inversión: siguen siendo trece bloques y 20 minutos.

## Sprint 5 (23/11/2026)

Métricas y Avance suman el Sprint 5, el último, con datos del informe de avances, la retrospectiva y la planilla del plan de pruebas al 23/11:
- **Métricas:** 74 de las 79 historias del sprint en Done. Las 5 que quedan son las armas nuevas de la F33, sin empezar. De los 449 casos, los 396 ejecutados pasan y quedan 53 sin ejecutar, de las armas nuevas y de las puertas de los módulos. La nota suma la velocidad (310 story points, la más alta), los bugs (33 nuevos resueltos, ninguno abierto) y el cierre del proyecto (332 de 337 historias, 98,5 %).
- **Avance:** el Sprint 5 tiene una captura de la Katana en el campus (`media/sprints/s5.jpg`, del informe). Lo nuevo: la Invasión cooperativa, las 27 cartas con las legendarias y la Katana, la tienda con el pase y las skins, y los idiomas, los perfiles y el mapa terminado.

Las dos diapositivas pasan a cinco tarjetas. En pantallas bajas, Métricas usa letra más chica y en Avance se achica la fecha de cada sprint, para que entren arriba de los controles. Revisado a 1280 × 720 y a 1024 × 768. El Tráiler (14 px) y la f(x) ya quedaban debajo de los controles antes de este cambio.

Además:
- **Propuesta comercial:** dice cómo quedaron la tienda directa y el pase en el juego.
- **Escala:** habla de lo que traería cada temporada nueva, porque la Invasión cooperativa y las skins ya están.
- **Preguntas del cierre:** Idiomas muestra los cinco idiomas en el juego. Modo Invasión cuenta el cooperativo y muestra 10 de las 27 cartas, de las cuatro rarezas.
- **Datos de Historia:** el modo Invasión tiene su mapa. Esa diapositiva no está en el recorrido.

## Si no despega (23/11/2026)

Reemplaza a Si no funciona. En la presentación anterior una profesora marcó que sacar a 5 de los 7 en el mes 3 era abandonar el barco muy rápido. Ahora nadie se va antes de que termine la rampa del escenario pesimista, que son 9 meses:
- **Lanzamiento:** se pone la mitad de la publicidad.
- **Mes 3 (Corregir):** con menos de 700 pagadores se busca qué falla y ahí va la otra mitad de la publicidad (US$ 2.500).
- **Mes 6 (Ajustar):** con menos de 1.400 pagadores, los 7 siguen a un tercio de sus horas y sin publicidad mensual (US$ 2.969 por mes de gastos).
- **Mes 9 (Decidir):** con menos de 1.400 pagadores se cierra.

En el pesimista, el inversor pierde como mucho US$ 76.865: la inversión, 9 meses y la desvinculación de los 7, que ya pasaron el período de prueba (US$ 7.577). Con el plan anterior eran US$ 49.941; ese número queda solo en la nota, para las preguntas.

En el promedio y el optimista no cambia nada. Los números (`economics.guard`) salen del modelo de la guía con el mismo cálculo que el plan anterior. Revisado a 1280 × 720 y a 1024 × 768.

## Arsenal, en las preguntas (23/11/2026)

Se sacó la diapositiva Arsenal: la ficha de cada arma pasa a las preguntas del cierre, como una burbuja más del pizarrón. Es la misma ficha, con el desplegable, y al cambiar de arma se vuelve a dibujar adentro del popup sin cerrarlo. Quedan trece bloques. Para seguir en 20 minutos, Avance pasa a 2 minutos, porque ahora tiene cinco sprints. Lucas Huber leía Arsenal y no tiene otra diapositiva. El código de la diapositiva (`arsenal()`) sigue y lo usa el popup.


## Modos de juego (24/11/2026)

La demo de interfaz pasa a ser **Modos de juego** («Tres modos, tres formas de jugar»). Se sacaron la Tienda y el HUD: queda solo la selección de modo del andén 3, con Táctico, Deathmatch e Invasión. La tarjeta de la diapositiva muestra los tres mapas.

Al tocar «Buscar partida», en vez de la sala de espera se abre un resumen visual del modo (`briefing.js`, que `demo.js` carga solo cuando la presentación abre la demo con `&briefing=1`; la guía sigue mostrando la búsqueda normal). Primero simula que encuentra la partida y después muestra cuatro pasos sobre el mapa. Los pasos se pasan a mano: con ← y → (también Espacio, Enter, Av Pág y Re Pág, que es lo que mandan los punteros de presentación) o con las flechas de abajo; Esc vuelve a los andenes. Cada escena termina quieta o se repite, así el paso puede quedar en pantalla lo que dure la explicación. En pantalla va solo el título grande de cada paso: la explicación se dice, y está entera en la nota de la diapositiva (`content.js`, sale de `txt` en `briefing.js`). Los números son los del juego:
- **Táctico** (plano de MALVINAS-01, `media/modos/tactico.jpg`; cada paso arranca donde terminó el anterior, y como no hay reaparición, el que cae queda con una cruz y sale en las bajas de arriba a la derecha; en «Plantar o desactivar» la cámara se acerca a B): la compra, con las 7 armas de la tienda debajo del plano (nombre, tipo y precio, de `CatalogoTactico.asset`), la plata ($ 800 al empezar, $ 200 por baja, $ 3.000 al ganar, $ 1.900 o más al perder, $ 300 por plantar), la bomba en A o B (4 s plantar, 45 s, 7 s desactivar), rondas de 100 s con cambio de lado después de la 6 y gana el primero en llegar a 7.
- **Deathmatch** (Colonia ARA San Juan): hasta 8, armas gratis en la tienda, 50 de vida por baja, reaparecés a los 3 s y gana el primero a 25 bajas o el que más tenga a los 8 minutos.
- **Invasión** (sin mapa dibujado: cada paso es un video de 12 s grabado en el juego, en bucle, `media/modos/invasion-1.mp4` a `invasion-4.mp4`): de 1 a 4, la oleada N trae 10 + 5 × (N − 1) invasores y cada jugador de más suma la mitad, los portales a 30–55 m del puesto, las bajas dentro del anillo cargan el puesto y al 100 % cada uno elige 1 de 3 (un arma, la Katana o una de las 27 cartas), el Jefe cada 10 oleadas y el compañero que te revive en 5 s con E. En «Cargar el puesto», las tres cartas (Escolta, Campo minado y Katana) aparecen debajo del título, sobre el fondo negro, para no tapar el video.

### Las tomas de Invasión (24/11/2026)

Se grabaron en Unity, en Play, con scripts que no quedan en el proyecto: cámara isométrica, el HUD apagado, los 4 personajes (Vos, Luka, David y Agus, con el nombre grande arriba) manejados por bots, y los invasores con los portales y el puesto del juego. Se grabó con `MediaEncoder` a 1400 × 1000 y 30 cuadros por segundo, se recomprimió a 3,5 Mbps (unos 5 MB cada una, sin audio) y se movió el índice (`moov`) al principio, para que el video arranque sin bajarlo entero.
1. **Juntos contra la horda**: los cuatro solos, entre el TUM, el Módulo 7 y los dos módulos del medio.
2. **Salen de los portales**: la entrada de la UNGS; cada uno le tira a los invasores de un portal distinto.
3. **Cargar el puesto**: el puesto en la Biblioteca, con más invasores, uno con la Katana y otro con el lanzagranadas.
4. **El Jefe y revivir**: el Auditorio, desde la puerta grande, con el Jefe y muchos invasores de todos los tipos que salen de los portales.

Lo lee David Ramos y sigue en 2 minutos. Revisado a 1280 × 720 y a 1024 × 768, los doce pasos, sin errores de JavaScript (con los videos de Invasión, otra vez el 24/11).

Los scripts y la hoja de estilos se piden con `?v=AAAAMMDD` (en `index.html` de la presentación, `presentacion/demo.js` en la guía y `briefing.js` en `demo.js`). GitHub Pages deja que el navegador guarde los archivos 10 minutos, y sin ese número una presentación abierta antes del cambio sigue con la versión vieja. La demo de modos también se abre con `&v=` en `demoSrc` (app.js), porque la guía es el mismo archivo y también queda guardada. Al cambiar uno de esos archivos, subir el número (en el mismo día, `AAAAMMDD02`, `03`…).
