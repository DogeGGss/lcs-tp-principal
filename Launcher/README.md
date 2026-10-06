# Project Riftwalker · Launcher

Aplicación Windows x64 independiente de Unity. Implementa F22 / US200–208. El equipo trabaja en el mismo repositorio; los jugadores reciben las nuevas versiones desde GitHub Releases.

## Compilar y probar

Necesitás el SDK .NET 10 para desarrollar. Los jugadores **no necesitan instalar .NET**.

Desde la raíz del repositorio, en PowerShell:

```powershell
dotnet run --project Launcher/Tests/Riftwalker.Launcher.Tests.csproj
./Launcher/tools/Build-Launcher.ps1 -Version 0.1.0
```

El archivo distribuible es `Launcher/artifacts/publish/Riftwalker-Launcher.exe`. Los PDB que pueda generar la compilación no se distribuyen. Es un ejecutable autónomo de unos 60 MB: incluye el runtime de .NET (solo con los textos en español), por lo que su tamaño no representa solo el código del launcher. No se debe distribuir el EXE suelto de `bin/Debug`.

Vista previa sin instalar, consultar GitHub ni abrir el juego:

```powershell
& './Launcher/artifacts/publish/Riftwalker-Launcher.exe' --preview
```

Captura de la propia interfaz para revisión:

```powershell
& './Launcher/artifacts/publish/Riftwalker-Launcher.exe' --render 'D:/capturas/launcher.png'
```

La carpeta de destino de la captura debe existir. El texto de esta vista es demostrativo, nunca se publica como notas del parche.

La ventana no usa el marco de Windows: es un panel con las esquinas cortadas, que se mueve arrastrando la barra de arriba o el banner, y tiene sus propios botones de minimizar y cerrar (Alt+F4 y el ícono de la barra de tareas funcionan igual). En **Ajustes** hay tres tamaños: **Chica** (el panel al 80 %), **Normal** (1120 × 700) y **Grande**, en 16:9 y casi a pantalla completa. Se escala la ventana entera, así el diseño es el mismo en los tres. Para verlos en la vista previa: `--preview --tamano chica|normal|grande`. El ícono (`App/Assets/riftwalker.ico`) se genera con `Launcher/tools/Generar-Icono.ps1`, desde el rayo del logo.

## Qué se descarga y desde dónde

| Qué | Para quién | Dónde |
|---|---|---|
| Proyecto de Unity | El equipo | Este repositorio (`git clone` / `git pull`). Ni el launcher compilado ni los builds del juego están en git. |
| Launcher | Jugadores, una sola vez | Release fijo **`launcher`**: solo `Riftwalker-Launcher.exe` y `launcher.json`. Link que no cambia: <https://github.com/DogeGGss/lcs-tp-principal/releases/download/launcher/Riftwalker-Launcher.exe> |
| Versiones del juego | El launcher | Un release por versión (`v0.2.0`, `v0.2.0-exp.1`…): solo el ZIP del juego y `release.json`. Están publicadas, pero se instalan desde el launcher; la descripción lo aclara con el link de arriba. |

El release del launcher nunca se marca como «Latest»: ese lugar es siempre de la última versión estable del juego, que es la que lee `releases/latest/download/release.json`.

## Publicar el launcher

Solo cuando cambia el launcher (la primera vez, para crear su release). La versión es X.Y.Z, independiente de las del juego:

```powershell
./Launcher/tools/Publish-Launcher.ps1 -Version '0.1.0' -Upload -Publish
```

Compila el launcher en `Launcher/artifacts/launcher-v0.1.0/`, genera `launcher.json` (versión, tamaño y SHA-256) y lo sube al release `launcher`: la primera vez lo crea, y después reemplaza sus archivos sin cambiar el link. Sin `-Upload` solo genera los archivos; con `-Upload` sin `-Publish`, la primera vez queda como borrador. Los launchers instalados ven la versión nueva en `launcher.json` y se actualizan solos.

## Publicar una versión del juego: procedimiento del equipo

1. En Unity, abrí **Riftwalker > Publicar versión**. Indicá, por ejemplo, `0.2.0` o `0.2.0-exp.1` y una carpeta para el build. La herramienta usa las escenas habilitadas, genera Windows x64, pone la versión real en el juego y escribe `riftwalker-build.json`. Si falla, restaura la versión anterior del proyecto. No cambia el nombre de compañía/producto ni la ubicación de los guardados. **Si sale bien, la versión del proyecto queda cambiada: subí `ProjectSettings/ProjectSettings.asset` con ese cambio**, así el editor y los builds del equipo tienen la misma versión que el release (Photon solo junta versiones iguales).
2. Copiá `Launcher/tools/patch-notes.template.md` y completá **Qué trae**, **Cambios**, **Correcciones** y **Problemas conocidos**.
3. Prepará los archivos con un solo comando. `GameBuild` es la carpeta con `Project Riftwalker.exe` en su raíz:

```powershell
./Launcher/tools/Publish-Release.ps1 `
  -GameBuild 'D:/Builds/Project-Riftwalker-v0.2.0' `
  -Version '0.2.0' `
  -Notes 'D:/Builds/notas-0.2.0.md'
```

Esto empaqueta el juego y genera los hashes en `Launcher/artifacts/release-v0.2.0/`. Las carpetas de depuración que Unity deja junto al build (`*_BurstDebugInformation_DoNotShip` y `*_BackUpThisFolder_ButDontShipItWithYourGame`) quedan afuera del ZIP. A las notas les agrega al final un pie para quien abra el release en GitHub («se instala desde el launcher», con el link); el launcher no lo muestra. **No sube ni publica nada por defecto**. No reutilices un número de versión para builds diferentes.

4. Con GitHub CLI instalado y autenticado, agregá **`-Upload`** al comando para crear un **borrador** con los dos archivos. Revisalo en GitHub y publicalo cuando esté completo. **`-Upload -Publish`** publica directamente: usalo solo para una versión ya validada. Las versiones `-exp.N` quedan marcadas como pre-release automáticamente.
5. Al abrir el launcher, los jugadores reciben esa versión. No hay que modificar su configuración ni el código del launcher. Antes de publicar una estable, completá la lista de verificación de abajo.

También podés subir los archivos manualmente a GitHub:

| Archivo | Contenido |
|---|---|
| `Project-Riftwalker-v0.2.0.zip` | Build completo; EXE, `_Data`, dependencias y sello de versión en la raíz. |
| `release.json` | Contrato generado automáticamente: versión del juego, tamaño y SHA-256 del ZIP. |

El tag debe ser `v0.2.0`. La descripción del release es `notas.md` de la carpeta generada. Subí los dos archivos **antes de sacar el release de borrador**. Los releases históricos `sprint-1` y `experimental-*` no se modifican ni se instalan con este launcher.

El orden se determina por versión numérica: `0.1.10 > 0.1.9`, `0.2.0-exp.10 > 0.2.0-exp.2`, y `0.2.0 > 0.2.0-exp.10`. En el canal estable se ignoran pre-releases. En el experimental se consideran ambos. Al desactivarlo, una instalación experimental ofrece volver a la última estable.

## Instalación para jugadores

Descargá **Riftwalker-Launcher.exe** una sola vez desde su link fijo: <https://github.com/DogeGGss/lcs-tp-principal/releases/download/launcher/Riftwalker-Launcher.exe> (release [`launcher`](https://github.com/DogeGGss/lcs-tp-principal/releases/tag/launcher)). Las versiones del juego no se bajan a mano: las instala el launcher.

La primera apertura propone `%LOCALAPPDATA%/Project Riftwalker`. Se puede elegir otra carpeta vacía con permiso de escritura. Crea accesos directos en el escritorio y en el menú Inicio, sin pedir administrador. Después, **Instalar** descarga el juego. **Jugar** abre el juego y cierra el launcher. Podés volver a abrir el launcher para iniciar otra copia y probar multijugador en la misma computadora. La actualización del juego espera a que cierres todas sus copias.

Como el ejecutable no tiene firma digital, la primera vez que lo abrís Windows SmartScreen puede mostrar una ventana azul que dice **«Windows protegió su PC»**. Si lo bajaste del link oficial de arriba:

1. Tocá **Más información**, el texto que está debajo del mensaje.
2. Abajo aparece el botón **Ejecutar de todas formas**: tocalo y el launcher se abre.

Windows lo pregunta una sola vez por archivo. Si no aparece el botón **Ejecutar de todas formas** (por ejemplo, en una computadora de la facultad con restricciones), el launcher no se puede instalar ahí: consultá al administrador. El launcher no toca Defender ni la configuración de Windows.

## Actualizaciones y recuperación

- GitHub se consulta con un límite total de 10 segundos. Si no responde, permite abrir el juego instalado y muestra las últimas notas guardadas. La primera instalación sí necesita conexión.
- La API de GitHub sin cuenta permite 60 consultas por hora **por IP**, y en una red compartida (por ejemplo, la de la facultad) ese límite es de todos. Por eso la lista de versiones se guarda con su ETag: si no cambió, GitHub responde «sin cambios» y esa consulta no cuenta. Si igual se llega al límite, la última versión estable sale de `releases/latest/download/release.json`, que es una descarga común, y se muestran las notas guardadas.
- Cada descarga conserva su fragmento temporal, identificado por SHA-256. Reintentar usa HTTP Range; si el servidor lo ignora, reinicia limpiamente. Cancelar no modifica el juego instalado.
- Antes de descargar controla espacio para el ZIP restante, la copia nueva descomprimida y 128 MiB de margen. La instalación anterior y su copia de respaldo ya ocupada también cuentan en el espacio libre real.
- Comprueba tamaño y SHA-256, extrae fuera de `game/`, valida las rutas, el tamaño descomprimido y el sello de versión. Un archivo corrupto se vuelve a descargar automáticamente una vez; si vuelve a fallar, muestra el error.
- La carpeta nueva sustituye a `game/` solo después de verificarla. Conserva una versión anterior y recupera el cambio interrumpido entre renombres al siguiente arranque. No toca PlayerPrefs ni `Application.persistentDataPath`.
- El propio launcher se actualiza desde su release fijo (`launcher.json`), aparte de las versiones del juego, mediante una copia auxiliar: espera que termine el proceso anterior, sustituye el EXE y comprueba que la nueva ventana inició. Si falla ese inicio, restaura el EXE anterior y lo abre sin repetir inmediatamente el intento.
- Los logs están en `.riftwalker/launcher.log`. Las carpetas administradas rechazan enlaces/junctions y rutas fuera de la instalación.

Estructura instalada:

```text
Project Riftwalker/
  Riftwalker-Launcher.exe
  game/                         # Build y recibo de instalación
  .riftwalker/                  # Estado, notas, descargas y respaldo
```

Para desinstalar, cerrá el juego y el launcher, borrá **solo la carpeta elegida para Project Riftwalker** y los dos accesos directos. Las opciones y partidas del juego se conservan en las ubicaciones de Unity; no se borran automáticamente.

## Integración del juego

- `ReleaseVersion.cs`: comparación de versiones compartida con el launcher.
- `VersionDelJuego.cs`: etiqueta del menú y consulta asíncrona de versión antes de crear/unirse a salas. Caché de 60 segundos, timeout total de 10 s, fallo de red permite continuar. Una versión estable lee el `release.json` del último release (no usa la API, así no cuenta para el límite de GitHub); una experimental consulta la API, porque es la única que lista las experimentales. **En el editor y en los builds de desarrollo no se bloquea nada**, para poder probar versiones sin publicar.
- `Multijugador.cs`: conserva `PhotonNetwork.GameVersion = Application.version`, ahora con comprobación antes de conectar/crear/unirse. Las experimentales conservan su sufijo completo y solo se juntan con la misma versión exacta.
- `PublicarVersion.cs`: menú de build. No necesita cambios en escenas ni prefabs.

La comprobación de versión es una regla del cliente; no es un mecanismo antitrampas. Las salas siguen separadas por versión en Photon.

## Verificación antes del primer release público

- [ ] Generar dos builds **reales** con versiones distintas usando el menú de publicación.
- [ ] Probar el menú con etiqueta de versión y el bloqueo online cuando existe una versión posterior.
- [ ] Probar dos clientes Photon con versiones iguales y distintas, incluyendo experimental.
- [ ] Instalar desde cero en una computadora sin Unity ni .NET instalado.
- [ ] Actualizar de una versión real a la siguiente; comprobar opciones y progreso.
- [ ] Cortar/reanudar la red durante la descarga y comprobar la recuperación.
- [ ] Probar retorno experimental → estable y actualización del launcher con una versión mayor.

Estas comprobaciones de entorno no se sustituyen por las pruebas automatizadas.

## Pruebas automatizadas

`dotnet run --project Launcher/Tests/Riftwalker.Launcher.Tests.csproj` corre 23 pruebas sin red ni archivos fuera de la carpeta temporal:

- Orden de versiones y selección de canal estable/experimental.
- Descarga: reanudación con Range, servidor que ignora Range, fragmento incorrecto, archivo dañado, cancelación y corte en medio.
- Instalación: reemplazo de `game/` conservando configuración y respaldo, rutas peligrosas en el ZIP, sello de versión, tamaño descomprimido, recuperación de un cambio interrumpido y carpetas no administradas.
- Manifiesto y direcciones de descarga.
- Lista de versiones reutilizada con su ETag, y última estable desde `release.json` cuando la API no responde.

Opciones extra: `-- --package-root <carpeta del release>` instala de verdad el paquete que generó `Publish-Release.ps1`, y `-- --live` consulta los releases reales de GitHub (solo lectura).

## Referencias técnicas

- [Distribución autónoma en un solo archivo, Microsoft](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
- [API oficial de GitHub Releases](https://docs.github.com/en/rest/releases/releases).
- Tipografías Barlow incluidas con licencia OFL en `App/Assets/OFL-Barlow.txt`.
