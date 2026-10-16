# Logros, ranking y progreso del jugador

## Activación (por el dueño del proyecto)

1. Crear cuenta en https://supabase.com/dashboard/sign-up y crear un proyecto.
2. En Authentication, habilitar Anonymous Sign-Ins. El juego crea una identidad anónima por instalación y conserva su sesión; no pide correo al jugador.
3. En SQL Editor ejecutar el contenido completo de `Backend/Supabase/logros.sql`. **Cada vez que cambie** (por ejemplo, al sumarse `rw_sync_progress` para el resto del progreso, US 162) hay que volver a ejecutarlo: es reejecutable y no borra nada. Mientras no se ejecute, el juego sigue sincronizando los logros y avisa en la pantalla de Logros que falta actualizar la base.
4. En los ajustes de conexión del proyecto copiar **Project URL** y la **publishable key** (`sb_publishable_...`). Nunca usar la contraseña de la base, `service_role` ni `sb_secret_` en Unity.
5. En Unity seleccionar `Assets/Resources/LogrosOnline.asset`, completar `Url` y `Clave Publica`, activar `Habilitado` y generar una nueva build. No necesita modificaciones manuales a escenas.
6. Entrar con un nombre de jugador y abrir Logros. Verificar conexión, progreso, ranking y listado de propietarios desde dos instalaciones. El SQL de pruebas opcional `logros.test.sql` se ejecuta en SQL Editor y revierte sus datos al terminar.

Documentación oficial: https://supabase.com/docs/guides/auth/auth-anonymous

## Alcance

Nueve logros: destruir **10, 50 y 100** cometas de cada color (azul, verde, dorado), acumulados entre partidas. Solo cuenta el impacto que destruye un cometa válido; no disparos fallidos. Cada desbloqueo muestra una notificación y reproduce un sonido en el mixer de Interfaz.

- **Logros:** progreso real, porcentaje global y botón para listar sus propietarios.
- **Ranking:** jugadores ordenados por cantidad obtenida, de 0 a 9; los empates comparten puesto. Se pueden consultar los nueve logros de cada jugador. Páginas de 50.
- Porcentaje = propietarios del logro / jugadores registrados que sincronizaron alguna vez, incluidos los de cero logros.
- Los nombres no son identificadores: dos personas pueden tener el mismo nombre. Cambiar el nombre no crea otro jugador.

## Guardado y conexión

Todo el progreso del jugador se guarda en un solo archivo, `jugador.json` (`jugador-editor.json` en el editor), en `Application.persistentDataPath`, con respaldo `.bak`, guardado atómico y versión de formato (`Guardado`, US 162 y US 163). Los logros van ahí, en la clave `Logros.Cometas`; la primera vez se migra lo que había en `logros-cometas.json`, que no se borra. Se conserva entre builds si no cambia Company Name / Product Name ni se borra esa carpeta. Un archivo dañado se recupera del respaldo; si ambos fallan, el juego arranca de cero, lo avisa y deja el dañado aparte.

Los logros nuevos se agregan siempre **al final** de `LogrosCometas.Catalogo`: las fechas del guardado van en ese orden. Un guardado de una versión con menos (o más) logros se sigue leyendo; sus fechas se ajustan al catálogo actual.

**Dos juegos abiertos en la misma PC** (dos builds) comparten el archivo. Antes de guardar, cada uno junta su progreso con el del archivo (el máximo de cada color y de cada estadística del minijuego), así ninguno pisa al otro; los aciertos que hagan al mismo tiempo no se suman entre sí. El editor usa su propio archivo, pero comparte la sesión online con los builds: sus pruebas también cuentan para el ranking.

**Resto del progreso en la nube** (`rw_sync_progress`, tabla `rw_player_progress`): estadísticas del minijuego (US 159), récords de Zombie y cosméticos del Perfil. El juego manda listas `{clave, valor}` y la base junta: contadores con el máximo, mejores tiempos con el mínimo, cosméticos obtenidos con la unión y equipados con lo último. Las opciones y las monedas no se suben: las monedas bajan al gastarse y juntarlas con el máximo devolvería lo gastado (con cuentas, US 237, el saldo lo tiene que manejar la base).

La sesión anónima y la última consulta del ranking se guardan por dominio del proyecto en esa misma carpeta. No compartir esos archivos de sesión. El JSON local no es una copia portable de una cuenta: borrar la sesión crea otra identidad, y no hay recuperación por correo ni fusión de cuentas en esta entrega.

Supabase cambia el token de renovación cada vez que se usa y anula la sesión si alguien reusa uno viejo. Por eso, antes de renovar, el juego vuelve a leer el archivo de sesión (otro juego abierto en la PC pudo renovarla). Si aun así la sesión quedó anulada, crea otra identidad anónima y lo avisa en la pantalla de Logros; la anterior queda en el ranking con el mismo nombre y lo que tenía.

El progreso local funciona sin internet. Se sincronizan totales acumulados, usando el máximo entre nube y PC: reintentar no duplica puntos. Los cambios se envían como máximo cada 15 segundos; el ranking se consulta al abrir, al cambiar de página/filtro, manualmente y cada 60 segundos mientras la pantalla permanece abierta. Errores de red aplican espera creciente de 10 a 120 segundos y conservan la caché visible con su fecha.

## Seguridad y límites de esta entrega

Las tablas tienen RLS y no permiten acceso directo desde el cliente. Las RPC identifican al jugador por su sesión, no por un ID enviado en el cuerpo; los desbloqueos se calculan en la base a partir de los contadores. No se distribuyen claves administrativas.

Los contadores siguen siendo declarados por el cliente: esto **no es un ranking resistente a trampas**. Para el proyecto universitario permite demostrar persistencia compartida. Una publicación competitiva requiere validar los eventos en un servidor autoritativo y añadir protección contra altas abusivas. El flujo actual no incorpora CAPTCHA ni vinculación a cuentas recuperables; si se exige CAPTCHA en Supabase habrá que integrar ese flujo antes de permitir nuevas identidades.

La base valida el nombre (3 a 16 caracteres: letras, números, espacio, `-` y `_`, como el juego). Si lo rechaza, el juego lo dice en Logros ("El ranking online no aceptó tu nombre"). Conviene probar una vez un nombre con ñ o tildes: que las acepte depende de la configuración regional de la base.

No se modificaron economías, recompensas, cosméticos ni otros tipos de logro. Los contadores del minijuego anteriores a esta integración no se migran porque no estaban guardados por color en este sistema.

## Validación

`My project/AgentScripts/ValidateAchievements.cs` prueba los límites, saltos de metas, independencia por color, ausencia de duplicados, monotonía, JSON, guardados de versiones con otra cantidad de logros, la unión de dos progresos (dos juegos en la misma PC), respaldo atómico y rechazo de claves secretas sin modificar el progreso del usuario. Ejecutar con Unity CLI `run_script`, entrada `ValidateAchievements.Main`.

La prueba real entre jugadores requiere completar la configuración anterior. `Assets/Resources/LogrosOnline.asset` ya tiene la URL del proyecto y la clave publicable, y está habilitado: esa clave es pública por diseño (las tablas están protegidas por RLS y solo se usan las RPC). Nunca poner ahí `service_role` ni `sb_secret_` (el juego las rechaza).
