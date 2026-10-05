# Presentación del Sprint 3

Abrir `/presentacion/` desde el enlace independiente de la guía. El recorrido suma 20 minutos. No avanza automáticamente: el operador controla el ritmo.

## Para exponer

- Anterior / Siguiente: ubicación fija y botones grandes. Flechas o Page Up / Page Down también funcionan cuando el foco está en la diapositiva.
- Índice: saltar a cualquiera de los diez bloques.
- Proyector: fondo claro con texto oscuro para salas iluminadas.
- Pantalla completa: ocultar el navegador; F11 es alternativa.
- Oscurecer / B: pausar visualmente; Esc o el botón permiten volver.
- Ensayar: iniciar/pausar el reloj de 20 minutos. El reloj no cambia las diapositivas y sigue durante las demos.
- Guion: notas para ensayar. Son visibles en la misma pantalla; no abrirlas durante la exposición si no se quieren proyectar.
- Las demos se abren dentro del recorrido, con Volver y Reiniciar siempre arriba. Son maquetas web; no se presentan como ejecución del juego.

## Actualización por etapas

Editar `content.js`: títulos, tiempos, guion, acuerdos, decisiones, métricas y propuesta comercial. Mantener los diez tiempos sumando 20 minutos. Las métricas desconocidas usan `null`, no cero. Los datos S1 son una foto histórica y nunca deben sobrescribirse con S3.

`metrics.s3` se completa con el corte final del Sprint 3. Hasta entonces aparece Pendiente. Fuente histórica: InformeSprint1.docx facilitado por el equipo, cierre 28/09/2026. No se publica el documento completo.

Para videos, copiar MP4 a `media/` y completar `clips[].src` con una ruta como `media/ronda.mp4`. Actualmente no hay clips y no se simula que los haya. Evitar videos pesados; un enlace externo puede servir de respaldo fuera del recorrido.

`arsenal.js` es una instantánea de las siete armas marcadas En el juego por la guía al 05/10/2026. No equivale a validar una build. Al actualizar el balance, regenerar o ajustar esa instantánea y su fecha. La pistola usa la recarga de 1,5 s indicada por EXTRA en la guía.

La monetización es una propuesta sin implementar: juego gratuito, skins, pase y lootboxes a evaluar. Catálogo, precios, temporadas, probabilidades, duplicados y criterio de no vender ventajas requieren definición del equipo. No se agregan ingresos ni cifras de mercado inventadas.

## Pendientes para el ensayo final

- Integrantes/roles y acuerdos ratificados; ejemplos del resultado de las decisiones.
- Métricas S3, fecha de corte y fuente; aclarar denominadores de porcentajes.
- Confirmar modos y reglas: el informe S1 dice oleadas infinitas y la guía habla de diez oleadas.
- Actualizar disponibilidad/balance de armas contra el ejecutable que se mostrará.
- Cargar clips y ensayar la demo real con respaldo.
- Resolver el alcance de monetización propuesto.
- Probar en el proyector real, resolución y conexión disponibles.

Las fuentes tipográficas y las imágenes se sirven localmente. La presentación principal no solicita métricas a GitHub durante la exposición. Las maquetas reutilizan la guía y sus dependencias; para una sesión sin internet, servir una copia completa del sitio localmente y comprobarla antes.

## Verificación de esta versión (05/10/2026)

Recorrido revisado en navegador a 1280 × 720 y 1024 × 768: las diez pantallas y las siete fichas de armas quedan por encima de los controles. Probados índice, navegación, reloj con pausa y reinicio, guion, oscurecimiento, contraste para proyector, métricas S1/S3 y pestañas comerciales/técnicas. Comprobadas compra en tienda, selección de modo, cambio de estado del HUD y regreso al recorrido. Sin errores de JavaScript registrados durante esas pruebas.

La pantalla completa depende del navegador; el navegador integrado no la activó durante la prueba. El botón informa la alternativa F11. Falta probar en el equipo y proyector de la exposición.

Al completar S3, actualizar también `metrics.s3.name` y `metrics.s3.source`. Hasta completar todos sus valores se conserva la pantalla de pendientes.
