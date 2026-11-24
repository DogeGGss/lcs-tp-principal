# Menú, Perfil y Tienda en la página

Copia web del menú principal, del Perfil y de la Tienda de Project Riftwalker, como están en el juego (`MenuRiftwalker.cs`, `PantallaPerfil.cs`, `PestanasDeTienda.cs`, `PantallaTiendaDirecta.cs`, `PantallaEstacion.cs` y `PantallaPase.cs`): las mismas medidas en una pantalla de 1920 × 1080 que se escala a la ventana, las mismas imágenes y los mismos colores. La presentación la abre en la diapositiva Menú, perfil y tienda. Copiado del juego al 24/11/2026.

Cada pantalla tiene su dirección, para abrirla directo: `#perfil`, `#tienda`, `#estacion` (cápsulas), `#pase` y `#cristales`.

## Perfil

- **Todo desbloqueado:** 19 avatares, 19 banners, 71 títulos, 20 marcos y las 109 skins de armas, para probar cualquier combinación. Lo equipado se recuerda en ese navegador.
- **Cabecera:** el nivel de jugador (1.030 de ejemplo) con su rango: Pasajero, Guarda, Maquinista o Jefe de estación.
- **Estadísticas** (datos de ejemplo), con la carta de Invasión.
- **Personalización:** avatares, banners, títulos, marcos y armas. En Armas se elige el arma y su skin. Las skins del juego son materiales 3D: acá se ven sobre el ícono del arma de la tienda pintado con los colores de la skin, y la barra de cada carta son sus cuatro colores.
- **Emotes:** los 15 bailes con su ícono y su rareza, y la rueda de 4 espacios. En la vista previa uno de los personajes hace el baile en 3D, y con las flechas se elige cuál, como en el juego. En la Tienda, el Pase y las cápsulas baila un personaje sorteado cada vez que se abre la pantalla. El Clásico y Floss se ven con su ícono (ver Bailes en 3D).
- **Historial** (partidas de ejemplo) y **Cuenta** (en el juego la maneja el launcher; acá no hay cuenta de verdad).
- **Armas en 3D:** en la vista previa de Armas el arma se ve en 3D con la skin puesta, se mece sola y se gira arrastrando. Lo mismo en el detalle de la Tienda y del Pase y en la carta grande de las cápsulas. Las cartas de las grillas siguen con el ícono.
- **Marcos:** se arman como en `PerfilUI.Enmarcar`: cada marco se parte en dos mitades con la esquina y el adorno del centro fijos, y el banner llega hasta el borde interior del riel.

## Tienda

La tarjeta Tienda de abajo a la izquierda abre las cuatro pestañas. Cada vez que se carga la página la tienda arranca como un perfil nuevo (2.450 créditos, 3 boletos y 120 cristales, nada comprado y el pase en el nivel 12 sin comprar), para que todo traiga cosas nuevas. F9 suma 1.000 créditos, 10 boletos y 500 cristales, como en el juego.

- **Tienda:** las 8 ofertas del día con las reglas de `OfertasDe` (una épica o legendaria, una skin, un emote y uno de cada tipo del Perfil), precios por rareza (400, 800, 1.600 y 3.200 cristales) y la cuenta regresiva hasta las 00:00 de Argentina. Las ofertas salen de otro sorteo que el del juego: el mismo día pueden no coincidir.
- **Cápsulas:** la Estación del Rift con las reglas de `PerfilDatos` (probabilidades por rareza y por tipo, épicas y legendarias siempre skins o emotes, épico o mejor asegurado cada 10, canjes y créditos por repetidos) y la misma animación. Solo sale lo que en el juego sale de las cápsulas (`OrigenDeCosmeticos`).
- **Pase de batalla:** la temporada 1 con sus 40 niveles, los premios gratis cada 5, «Reclamar todo», comprar el pase con 950 cristales y los desafíos (con progreso de ejemplo).
- **Cristales:** la oferta de bienvenida y los cuatro paquetes. Las compras son simuladas: suman cristales y quedan en el historial, sin cobrar nada.

## Armas en 3D

`3d/armas3d.js` dibuja el arma con three.js (r160, copiado en `3d/vendor` con su licencia, sin depender de internet). Si el navegador no tiene WebGL queda la imagen de siempre.

- **Modelos** (`3d/modelos`): los FBX de `ModelosDeArmas.asset`, uno por arma.
- **Skins comunes:** cada parte del arma (Black, White, Grey y Main) toma el color de la barra de la skin, igual que en el juego.
- **Skins con diseño:** los shaders de `Assets/Art/Skins` (Patrón, Matrix, Cartel, Mapa, Dragón, Tormenta y Cuchillo) pasados a GLSL en `3d/sombreado.js`, con los valores de cada `.mat` copiados en `armas3d.js`. Las texturas que usan están en `3d/texturas` a 512 px. La luz es más simple que la del juego y no está el efecto de calor del disparo, así que se ven muy parecidas pero no idénticas.
- **Escala:** los patrones se miden en metros sobre el arma. El largo de cada arma (`LARGO` en `armas3d.js`) es estimado: si un patrón se ve más grande o más chico que en el juego, se ajusta ahí.

Si cambia un `.mat` o un shader de skin en el juego, hay que copiar el cambio a mano en `armas3d.js` o `sombreado.js`.

## Bailes en 3D

`3d/bailes3d.js` hace lo mismo que `VistaPreviaDeEmote`: la misma cámara, uno de los cuatro personajes y el baile del emote.

- **Personajes** (`3d/personajes`): los modelos de `CharacterData.modelo` pasados a glTF y achicados (sin las texturas incrustadas, con menos polígonos y comprimidos con meshopt). Las texturas de color y de brillo van aparte, a 1024 px o menos.
- **Bailes** (`3d/bailes`): los FBX de Mixamo de `Personajes/Emotes` pasados a JSON: por hueso, sus rotaciones, y de la cadera también la posición. Como todos los personajes tienen el esqueleto de Mixamo, cualquier baile sirve para cualquiera.
- **El Clásico y Floss** son clips `.anim` Humanoid de Unity (músculos, no huesos) y no hay un FBX del que sacarlos, así que se ven con su ícono.

Para pasar un baile nuevo: cargar el FBX con el `FBXLoader` de three.js y guardar `animations[0]` con el formato de los JSON de `3d/bailes` (los huesos sin el prefijo `mixamorig`). Después sumarlo a `BAILES` en `bailes3d.js`.

## Actualizar

Si cambian los cosméticos del juego, desde la carpeta del sprint de trabajo:

1. `node herramientas/generar-catalogo.js` arma `catalogo.js` desde `PerfilDatos.cs` (nombres, rarezas, rieles, interiores y cortes de los marcos).
2. `herramientas/preparar-menu.ps1` copia las imágenes de `Resources/Perfil` en tamaño web: banners a 1800 × 600 y marcos partidos en dos mitades a la mitad del original.
3. `python3 herramientas/generar-extras.py guia/menu/extras.js` arma `extras.js`: skins (`CatalogoDeSkins.cs`), emotes (`Resources/Emotes.asset`), de dónde sale cada cosmético y el premio de cada nivel del pase (`OrigenDeCosmeticos.cs`), desafíos (`PaseDeBatalla.cs`) y paquetes de cristales (`PerfilDatos.cs`). Los datos están copiados al principio del script: si cambian en el juego, hay que actualizarlos ahí.
4. `python3 herramientas/generar-skins.py "<proyecto>/Assets/UI/Tienda/Iconos" guia/menu/img/skins` pinta las imágenes de las skins. Los íconos de los emotes (`img/emotes`) son los de `Assets/Art/Emotes/Iconos` a 160 px con el anillo de su rareza.

Las herramientas tienen las rutas del proyecto y de la carpeta de salida al principio o como argumento; hay que ajustarlas si se mueven.

Las reglas de las cápsulas, los precios de la tienda y los números del pase están copiados en `menu.js`: si cambian en el juego, hay que actualizarlos a mano.
