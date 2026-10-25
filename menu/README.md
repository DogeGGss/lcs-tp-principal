# Menú, Perfil y Estación del Rift en la página

Copia web del menú principal, del Perfil y de la Estación del Rift de Project Riftwalker, como están en el juego (`MenuRiftwalker.cs`, `PantallaPerfil.cs` y `PantallaEstacion.cs`): las mismas medidas en una pantalla de 1920 × 1080 que se escala a la ventana, las mismas imágenes y los mismos colores. La presentación la abre en la diapositiva Menú, perfil y cápsulas.

- **Todo desbloqueado:** 19 avatares, 19 banners, 70 títulos y 19 marcos (más «Sin título» y «Sin marco»), para probar cualquier combinación. Lo equipado se recuerda en ese navegador.
- **Perfil:** Estadísticas (datos de ejemplo), Personalización y Emotes. «Cambiar nombre» cambia el nombre de la cabecera y del menú.
- **Marcos:** se arman como en `PerfilUI.Enmarcar`: cada marco se parte en dos mitades con la esquina y el adorno del centro fijos, y el banner llega hasta el borde interior del riel.
- **Estación del Rift:** la tarjeta de abajo a la izquierda (o el + de los boletos) abre la pantalla de cápsulas, con las reglas de ejemplo de `PerfilDatos` (probabilidades, tipo, épico o mejor asegurado cada 10, canjes y créditos por repetidos) y la misma animación. Cada vez que se carga la página arranca como un perfil nuevo (2.450 créditos, 3 boletos y 120 cristales, sin nada abierto), para que las cápsulas traigan cosas nuevas. F9 suma 1.000 créditos, 10 boletos y 500 cristales, como en el juego. «Equipar» cambia lo equipado en el Perfil.

## Actualizar

Si cambian los cosméticos del juego, desde la carpeta del sprint de trabajo:

1. `node herramientas/generar-catalogo.js` arma `catalogo.js` desde `PerfilDatos.cs` (nombres, rarezas, rieles, interiores y cortes de los marcos).
2. `herramientas/preparar-menu.ps1` copia las imágenes de `Resources/Perfil` en tamaño web: banners a 1800 × 600 y marcos partidos en dos mitades a la mitad del original.

Las dos herramientas tienen las rutas del proyecto y de la carpeta de salida al principio; hay que ajustarlas si se mueven.

Las reglas de las cápsulas están copiadas al principio de `menu.js`: si cambian en `PerfilDatos.cs`, hay que actualizarlas a mano.
