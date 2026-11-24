# Menú, Perfil y Tienda en la página

Copia web del menú principal, del Perfil y de la Tienda de Project Riftwalker, como están en el juego (`MenuRiftwalker.cs`, `PantallaPerfil.cs`, `PestanasDeTienda.cs`, `PantallaTiendaDirecta.cs`, `PantallaEstacion.cs` y `PantallaPase.cs`): las mismas medidas en una pantalla de 1920 × 1080 que se escala a la ventana, las mismas imágenes y los mismos colores. La presentación la abre en la diapositiva Menú, perfil y tienda. Copiado del juego al 24/11/2026.

Cada pantalla tiene su dirección, para abrirla directo: `#perfil`, `#tienda`, `#estacion` (cápsulas), `#pase` y `#cristales`.

## Perfil

- **Todo desbloqueado:** 19 avatares, 19 banners, 71 títulos, 20 marcos y las 109 skins de armas, para probar cualquier combinación. Lo equipado se recuerda en ese navegador.
- **Cabecera:** el nivel de jugador (1.030 de ejemplo) con su rango: Pasajero, Guarda, Maquinista o Jefe de estación.
- **Estadísticas** (datos de ejemplo), con la carta de Invasión.
- **Personalización:** avatares, banners, títulos, marcos y armas. En Armas se elige el arma y su skin. Las skins del juego son materiales 3D: acá se ven sobre el ícono del arma de la tienda pintado con los colores de la skin, y la barra de cada carta son sus cuatro colores.
- **Emotes:** los 15 bailes con su ícono y su rareza, y la rueda de 4 espacios. Los cuatro bailes que la presentación tiene grabados (`presentacion/media/emotes`) se ven en movimiento en la vista previa.
- **Historial** (partidas de ejemplo) y **Cuenta** (en el juego la maneja el launcher; acá no hay cuenta de verdad).
- **Marcos:** se arman como en `PerfilUI.Enmarcar`: cada marco se parte en dos mitades con la esquina y el adorno del centro fijos, y el banner llega hasta el borde interior del riel.

## Tienda

La tarjeta Tienda de abajo a la izquierda abre las cuatro pestañas. Cada vez que se carga la página la tienda arranca como un perfil nuevo (2.450 créditos, 3 boletos y 120 cristales, nada comprado y el pase en el nivel 12 sin comprar), para que todo traiga cosas nuevas. F9 suma 1.000 créditos, 10 boletos y 500 cristales, como en el juego.

- **Tienda:** las 8 ofertas del día con las reglas de `OfertasDe` (una épica o legendaria, una skin, un emote y uno de cada tipo del Perfil), precios por rareza (400, 800, 1.600 y 3.200 cristales) y la cuenta regresiva hasta las 00:00 de Argentina. Las ofertas salen de otro sorteo que el del juego: el mismo día pueden no coincidir.
- **Cápsulas:** la Estación del Rift con las reglas de `PerfilDatos` (probabilidades por rareza y por tipo, épicas y legendarias siempre skins o emotes, épico o mejor asegurado cada 10, canjes y créditos por repetidos) y la misma animación. Solo sale lo que en el juego sale de las cápsulas (`OrigenDeCosmeticos`).
- **Pase de batalla:** la temporada 1 con sus 40 niveles, los premios gratis cada 5, «Reclamar todo», comprar el pase con 950 cristales y los desafíos (con progreso de ejemplo).
- **Cristales:** la oferta de bienvenida y los cuatro paquetes. Las compras son simuladas: suman cristales y quedan en el historial, sin cobrar nada.

## Actualizar

Si cambian los cosméticos del juego, desde la carpeta del sprint de trabajo:

1. `node herramientas/generar-catalogo.js` arma `catalogo.js` desde `PerfilDatos.cs` (nombres, rarezas, rieles, interiores y cortes de los marcos).
2. `herramientas/preparar-menu.ps1` copia las imágenes de `Resources/Perfil` en tamaño web: banners a 1800 × 600 y marcos partidos en dos mitades a la mitad del original.
3. `python3 herramientas/generar-extras.py guia/menu/extras.js` arma `extras.js`: skins (`CatalogoDeSkins.cs`), emotes (`Resources/Emotes.asset`), de dónde sale cada cosmético y el premio de cada nivel del pase (`OrigenDeCosmeticos.cs`), desafíos (`PaseDeBatalla.cs`) y paquetes de cristales (`PerfilDatos.cs`). Los datos están copiados al principio del script: si cambian en el juego, hay que actualizarlos ahí.
4. `python3 herramientas/generar-skins.py "<proyecto>/Assets/UI/Tienda/Iconos" guia/menu/img/skins` pinta las imágenes de las skins. Los íconos de los emotes (`img/emotes`) son los de `Assets/Art/Emotes/Iconos` a 160 px con el anillo de su rareza.

Las herramientas tienen las rutas del proyecto y de la carpeta de salida al principio o como argumento; hay que ajustarlas si se mueven.

Las reglas de las cápsulas, los precios de la tienda y los números del pase están copiados en `menu.js`: si cambian en el juego, hay que actualizarlos a mano.
