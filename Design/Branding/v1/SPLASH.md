# Apertura animada de Riftwalker

La versión actual reemplaza la ilustración por negro puro y el logo original. No utiliza video ni una nueva imagen generada.

- 0–0,3 s: negro.
- 0,3–1,05 s: grieta naranja vertical con núcleo claro y chispas.
- 1,05–2,35 s: los bordes se separan y una máscara descubre el logo desde el centro.
- 2,35–3,5 s: logo completo y estable.
- 3,5–4,2 s: fundido a negro.
- Se activa el menú y se retira el negro durante 0,35 s.

intro-preview.html aproxima en navegador los mismos tiempos, geometría y logo. La rasterización puede diferir del Canvas Unity. Botón Repetir para volver a verla.

## Integración

Splash nativo desactivado, incluido el logo Unity. RiftwalkerBuildBranding inserta RiftwalkerIntro en la copia de MenuPrincipal que procesa la build Windows. No escribe en la escena fuente ni cambia sus índices. Los objetos raíz originalmente activos esperan a que termine; los originalmente inactivos se conservan. La textura original queda referenciada directamente.

Solo se reproduce si MenuPrincipal es la primera escena, una vez por ejecución. Volver al menú no la repite. Batch y clientes de prueba -unirse la omiten. Usa tiempo no escalado y no altera cargas entre mapas. El motor debe inicializarse antes de ejecutar esta animación.

El fondo anterior queda archivado como propuesta descartada, sin referencias en el arranque.

## Validación

Runtime y Editor compilados contra referencias de Unity 6000.6.2f1. No se abrió Unity ni se produjo una build. Pendiente comprobar apertura, primer ingreso al menú y regreso al menú en un ejecutable.

Integración documentada: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/build/iprocessscenewithreport
