# Presentación del Sprint 3

Abrir `/presentacion/` desde el enlace independiente de la guía. El recorrido suma 20 minutos y se divide en tres partes, en este orden: Juego (apertura, tráiler, demos, arsenal y tecnología), Equipo (una métrica) y Financiación (propuesta comercial, números, costos e inversión), y después el cierre. Cada diapositiva muestra arriba su parte, y el índice las agrupa. No avanza automáticamente: el operador controla el ritmo.

## Para exponer

- Anterior / Siguiente: ubicación fija y botones grandes. Flechas o Page Up / Page Down también funcionan cuando el foco está en la diapositiva.
- Índice: saltar a cualquiera de los doce bloques.
- Proyector: fondo claro con texto oscuro para salas iluminadas.
- Pantalla completa: ocultar el navegador; F11 es alternativa.
- Oscurecer / B: pausar visualmente; Esc o el botón permiten volver.
- Tráiler: «Pantalla completa» lo reproduce ocupando toda la pantalla; Esc vuelve. Mientras el video está en pantalla completa, las flechas no cambian de diapositiva.
- Ensayar: iniciar/pausar el reloj de 20 minutos. El reloj no cambia las diapositivas y sigue durante las demos.
- Guion: notas para ensayar. Son visibles en la misma pantalla; no abrirlas durante la exposición si no se quieren proyectar.
- Las demos se abren dentro del recorrido, con Volver y Reiniciar siempre arriba. Son maquetas web; no se presentan como ejecución del juego.

## Actualización por etapas

Editar `content.js`: títulos, partes (`section`), tiempos, guion, tráiler, decisiones, métricas, propuesta comercial y números económicos. Mantener los tiempos de los doce bloques sumando 20 minutos. Las métricas desconocidas usan `null`, no cero. Los datos S1 son una foto histórica y nunca deben sobrescribirse con S3.

`metrics.s3` se completa con el corte final del Sprint 3. Hasta entonces aparece Pendiente. Fuente histórica: InformeSprint1.docx facilitado por el equipo, cierre 28/09/2026. No se publica el documento completo.

Para el tráiler, copiar el MP4 a `media/` y completar `trailer.src` con una ruta como `media/trailer.mp4` (y `trailer.poster` con una imagen, si se quiere). GitHub no acepta archivos de más de 100 MB: comprimir el video por debajo de eso. Para otros videos, completar `clips[].src` de la misma forma. Actualmente no hay clips y no se simula que los haya. Evitar videos pesados; un enlace externo puede servir de respaldo fuera del recorrido.

`arsenal.js` es una instantánea de las siete armas marcadas En el juego por la guía al 05/10/2026. No equivale a validar una build. Al actualizar el balance, regenerar o ajustar esa instantánea y su fecha. La pistola usa la recarga de 1,5 s indicada por EXTRA en la guía.

La propuesta comercial y los bloques Números, Costos e Inversión (antes del cierre) salen de la sección Modelo de negocio de la guía: juego gratuito en Steam con cristales de pago, solo cosméticos. `business` tiene los productos y precios propuestos; el pase y la tienda directa todavía no están en el juego. `economics` tiene los números del simulador de la guía: los tres escenarios (pesimista, promedio y optimista), todos con las mejoras del plan, y la inversión de los 5 sprints. Son simulaciones con supuestos, no resultados ni cifras de mercado. Al cambiar un supuesto en la guía, volver a copiar los números.

## Pendientes para el ensayo final

- Cargar el tráiler en `media/` y probarlo en pantalla completa.
- Elegir la métrica de la parte Equipo.
- Métricas S3, fecha de corte y fuente; aclarar denominadores de porcentajes.
- Actualizar disponibilidad/balance de armas contra el ejecutable que se mostrará.
- Cargar clips y ensayar la demo real con respaldo.
- Confirmar con el equipo los precios y las mejoras del plan (pase y tienda directa, bienvenida a US$ 0,99, juego en inglés y Photon a 10 envíos por segundo).
- Probar en el proyector real, resolución y conexión disponibles.

Las fuentes tipográficas y las imágenes se sirven localmente. La presentación principal no solicita métricas a GitHub durante la exposición. Las maquetas reutilizan la guía y sus dependencias; para una sesión sin internet, servir una copia completa del sitio localmente y comprobarla antes.

## Verificación de la parte económica (22/10/2026)

Se sumaron los bloques Números, Costos e Inversión antes del cierre, y equipo, métricas, decisiones, la demo de interfaz e Inversión bajaron un minuto cada uno para seguir en 20 minutos. Costos detalla la inversión inicial (sueldos, publicidad, otros gastos y Steam Direct) y un mes del promedio: comisión de Steam, retención de EE.UU., sueldos, Photon, Unity Pro, publicidad, otros gastos, Supabase, impuesto a las ganancias y la ganancia que queda. Los trece bloques quedan por encima de los controles a 1280 × 720 y 1024 × 768, sin desbordes. Probadas las cuatro pestañas de la propuesta comercial, sin errores de JavaScript. Las diapositivas no muestran los minutos de cada bloque ni la aclaración de que los números son simulados; los minutos quedan en el índice para ensayar.

## Verificación de esta versión (05/10/2026)

Recorrido revisado en navegador a 1280 × 720 y 1024 × 768: las diez pantallas y las siete fichas de armas quedan por encima de los controles. Probados índice, navegación, reloj con pausa y reinicio, guion, oscurecimiento, contraste para proyector, métricas S1/S3 y pestañas comerciales/técnicas. Comprobadas compra en tienda, selección de modo, cambio de estado del HUD y regreso al recorrido. Sin errores de JavaScript registrados durante esas pruebas.

La pantalla completa depende del navegador; el navegador integrado no la activó durante la prueba. El botón informa la alternativa F11. Falta probar en el equipo y proyector de la exposición.

Al completar S3, actualizar también `metrics.s3.name` y `metrics.s3.source`. Hasta completar todos sus valores se conserva la pantalla de pendientes.
