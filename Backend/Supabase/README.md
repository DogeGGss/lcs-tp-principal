# Logros, ranking, progreso del jugador y opiniones

## Activación (por el dueño del proyecto)

1. Crear cuenta en https://supabase.com/dashboard/sign-up y crear un proyecto.
2. En Authentication, habilitar Anonymous Sign-Ins. Para jugar hace falta una cuenta (ver «Cuentas»); la identidad anónima queda para el editor sin cuenta y las versiones viejas del juego, que pueden mandar opiniones y ver el ranking, pero no suben progreso ni aparecen en él.
3. En SQL Editor ejecutar el contenido completo de `Backend/Supabase/logros.sql`. **Cada vez que cambie** (por ejemplo, al sumarse `rw_sync_progress` para el resto del progreso, US 162, o `rw_stat_board` para el ranking por estadísticas, US 226) hay que volver a ejecutarlo: es reejecutable y no borra nada. Mientras no se ejecute, el juego sigue sincronizando los logros y avisa en la pantalla de Logros que falta actualizar la base.
4. En los ajustes de conexión del proyecto copiar **Project URL** y la **publishable key** (`sb_publishable_...`). Nunca usar la contraseña de la base, `service_role` ni `sb_secret_` en Unity.
5. En Unity seleccionar `Assets/Resources/LogrosOnline.asset`, completar `Url` y `Clave Publica`, activar `Habilitado` y generar una nueva build. No necesita modificaciones manuales a escenas.
6. Entrar con un nombre de jugador y abrir Logros. Verificar conexión, progreso, ranking y listado de propietarios desde dos instalaciones. El SQL de pruebas opcional `logros.test.sql` se ejecuta en SQL Editor y revierte sus datos al terminar.

Documentación oficial: https://supabase.com/docs/guides/auth/auth-anonymous

## Opiniones y aviso en Discord

"Danos tu opinión" (menú principal) guarda cada opinión en la tabla `rw_feedback` con la RPC `rw_send_feedback`, y la base avisa en un canal de Discord por cada una nueva.

1. En SQL Editor ejecutar `Backend/Supabase/opiniones.sql` (después de `logros.sql`). Es reejecutable. Activa `pg_net`, la extensión con la que la base le manda el aviso a Discord.
2. En Discord: Configuración del canal → Integraciones → Webhooks → Nuevo webhook (se puede poner de nombre "Riftwalker" y el logo de avatar). Copiar su URL.
3. Guardar esa URL **en el Vault de Supabase**, nunca en el juego ni en el repositorio: con ella cualquiera puede escribir en el canal. En SQL Editor, una sola vez:

   ```sql
   select vault.create_secret('PEGAR_ACA_LA_URL_DEL_WEBHOOK', 'discord_feedback_webhook', 'Webhook de Discord para las opiniones del juego');
   ```

   Para cambiarla más adelante: `select vault.update_secret((select id from vault.secrets where name = 'discord_feedback_webhook'), 'NUEVA_URL');`
4. Probar mandando una opinión desde el juego: tiene que aparecer en Table Editor → `rw_feedback` y en el canal. Opcional: `opiniones.test.sql` prueba la RPC sin dejar datos ni avisar.

Cómo funciona:

- El juego guarda la opinión en `opiniones-pendientes.json` (en `Application.persistentDataPath`) y `LogrosOnline` la manda con la sesión anónima del jugador. Sin conexión queda ahí y se envía cuando vuelve. Cada opinión lleva un id generado por el juego: si se reenvía, la base no la duplica ni avisa dos veces.
- La base valida el tipo (opinión, sugerencia o error), la valoración (0 a 5) y el texto (1 a 500 caracteres), y acepta como máximo una cada 10 s y 20 por día por jugador. Se guardan el nombre del jugador (el del ranking) y la versión del juego.
- El aviso lo manda un trigger con `pg_net` después de guardar, así un problema con Discord nunca hace perder la opinión. Va como mensaje con el tipo, el texto, la valoración, el jugador y la versión. No hace menciones aunque el texto tenga "@everyone".
- Si falta el webhook en el Vault, las opiniones se guardan igual y no se avisa. Si falta correr `opiniones.sql`, el juego las deja en la PC y lo dice al enviar.

## Alcance

Veinticinco logros, acumulados entre partidas. Cada desbloqueo muestra una notificación y reproduce un sonido en el mixer de Interfaz.

- **Cometas (9):** destruir **10, 50 y 100** cometas de cada color (azul, verde, dorado). Solo cuenta el impacto que destruye un cometa válido; no disparos fallidos. La base los desbloquea en `rw_sync_comets`.
- **Combate (16):** salen de las estadísticas del jugador (F24) de las partidas terminadas de Táctico y Deathmatch: bajas (10, 100 y 500), bajas a la cabeza (10, 50 y 200), 50 asistencias, victorias (1 y 25), MVP (1 y 10), mejor racha en Deathmatch (5 y 10), 10 plantados, 10 desactivados y 50 partidas. En el catálogo tienen `stat` en vez de color, y la base los desbloquea en `rw_sync_progress` con `rw_stat_value`, la misma función que usa el ranking. Los nuevos se agregan al final del catálogo, en `logros.sql` y en `LogrosCometas.Catalogo`.

- **Logros:** progreso real, porcentaje global y botón para listar sus propietarios.
- **Ranking (US 226):** todos los jugadores ordenados por la categoría elegida: General (partidas, victorias, % de victorias, bajas, muertes, asistencias, bajas a la cabeza, KDA, tiros a la cabeza, horas jugadas), Táctico, Deathmatch, Minijuego (cometas de cada color, puntos, récord, precisión, mejor reacción) o Logros (cantidad obtenida, con los logros de cada jugador). Los empates comparten puesto; páginas de 50; se ve el puesto propio aunque no esté en la página. Los porcentajes y promedios piden 5 partidas terminadas (50 disparos en el minijuego) y solo aparecen los jugadores con un valor mayor que 0; en la mejor reacción gana el menor tiempo. Lo calcula `rw_stat_board` con lo que ya sube cada juego (`rw_sync_comets` y `rw_sync_progress`); las categorías están en `CategoriasRanking`, con los mismos ids que la función.
- Porcentaje = propietarios del logro / jugadores registrados que sincronizaron alguna vez, incluidos los de cero logros.
- Los nombres no son identificadores: dos personas pueden tener el mismo nombre. Cambiar el nombre no crea otro jugador.

## Guardado y conexión

Todo el progreso del jugador se guarda en un solo archivo, `jugador.json` (`jugador-editor.json` en el editor), en `Application.persistentDataPath`, con respaldo `.bak`, guardado atómico y versión de formato (`Guardado`, US 162 y US 163). Los logros van ahí, en la clave `Logros.Cometas`; la primera vez se migra lo que había en `logros-cometas.json`, que no se borra. Se conserva entre builds si no cambia Company Name / Product Name ni se borra esa carpeta. Un archivo dañado se recupera del respaldo; si ambos fallan, el juego arranca de cero, lo avisa y deja el dañado aparte.

Los logros nuevos se agregan siempre **al final** de `LogrosCometas.Catalogo`: las fechas del guardado van en ese orden. Un guardado de una versión con menos (o más) logros se sigue leyendo; sus fechas se ajustan al catálogo actual.

**Dos juegos abiertos en la misma PC** (dos builds) comparten el archivo. Antes de guardar, cada uno junta su progreso con el del archivo (el máximo de cada color y de cada estadística del minijuego), así ninguno pisa al otro; los aciertos que hagan al mismo tiempo no se suman entre sí. El editor usa su propio archivo, pero comparte la sesión online con los builds: sus pruebas también cuentan para el ranking.

**Resto del progreso en la nube** (`rw_sync_progress`, tabla `rw_player_progress`): estadísticas del jugador (F24) y del minijuego (US 159), récords de Zombie y el Perfil: cosméticos, cápsulas abiertas, pity, total gastado, compras, packs de una sola vez y últimos premios. El juego manda listas `{clave, valor}` y la base junta: contadores con el máximo, mejores tiempos con el mínimo, cosméticos, packs y compras con la unión, y equipados y últimos premios con lo último. El pity se manda como «la cápsula del último épico», que solo sube, así también se junta con el máximo. En una PC nueva el juego no manda lo equipado hasta traer lo de la nube: si no, lo de entrada pisaría lo que el jugador tenía. Las opciones no se suben. Las monedas tienen su propio saldo (ver «Monedas de la cuenta»).

La sesión anónima y la última consulta del ranking se guardan por dominio del proyecto en esa misma carpeta. No compartir esos archivos de sesión. El JSON local no es una copia portable de una cuenta: borrar la sesión crea otra identidad, y no hay recuperación por correo ni fusión de cuentas en esta entrega.

Supabase cambia el token de renovación cada vez que se usa y anula la sesión si alguien reusa uno viejo. Por eso, antes de renovar, el juego vuelve a leer el archivo de sesión (otro juego abierto en la PC pudo renovarla). Si aun así la sesión quedó anulada, crea otra identidad anónima y lo avisa en la pantalla de Logros; la anterior queda en el ranking con el mismo nombre y lo que tenía.

El progreso local funciona sin internet. Se sincronizan totales acumulados, usando el máximo entre nube y PC: reintentar no duplica puntos. Los cambios se envían enseguida (con 2 segundos entre una sincronización y la siguiente) y, al cerrar el juego, si queda algo sin subir, el juego espera hasta 4 segundos a que salga; el ranking se consulta al abrir, al cambiar de página/filtro, manualmente y cada 60 segundos mientras la pantalla permanece abierta. Errores de red aplican espera creciente de 10 a 120 segundos y conservan la caché visible con su fecha.

## Cuentas (US 237, US 269, US 270, US 281 y US 282)

La cuenta se maneja en el launcher, como en un cliente (`Launcher/Core/Cuentas.cs`; diseño en la guía, `#cuenta-launcher`): ahí se crea, se inicia sesión y se cierra. Para jugar hace falta una cuenta, y JUGAR abre el juego con ella. Las cuentas arrancan de cero: el progreso que había en la PC antes de las cuentas no pasa a ninguna.

- El launcher guarda la sesión (nunca la contraseña) en la carpeta de guardado del juego: `%USERPROFILE%\AppData\LocalLow\Equipo 4\Project Riftwalker\cuenta-sesion-<host>.json`. El juego la lee al abrir (`LogrosOnline.Cuenta.cs`), aunque se lo abra sin el launcher. Supabase cambia el token de renovación cada vez que se usa: los dos leen el archivo antes de renovarla.
- Con la cuenta, el progreso va a su propio archivo (`jugador-<id>.json`) y a la nube. Las opciones siguen en `jugador.json`.
- Sin una sesión, el juego publicado muestra «Iniciá sesión en el launcher» y no deja jugar. En el editor (y en los builds de desarrollo) el Perfil deja crear la cuenta y entrar, para probar sin launcher; *Riftwalker > Cuenta > Como en el juego publicado* hace que el editor se comporte como el juego publicado.

El launcher (y el Perfil, en el editor) hace estos pedidos a Supabase Auth:

1. Crear: `POST /auth/v1/signup` con el mail y la contraseña. Supabase manda un código de 6 números. Si el mail ya tiene cuenta, Supabase responde sin identidades y se ofrece iniciar sesión.
2. Confirmar: `POST /auth/v1/verify` con `type: signup`, el mail y el código. Devuelve la sesión de la cuenta.
3. Pedir otro código: `POST /auth/v1/resend` con `type: signup`.
4. Iniciar sesión: `POST /auth/v1/token?grant_type=password`. Si el mail no está confirmado, se manda otro código y se pide confirmarlo.
5. Cerrar sesión: `POST /auth/v1/logout?scope=local`.

El nombre de jugador lo pide el juego la primera vez y lo guarda en la cuenta (`PUT /auth/v1/user` con `data.nombre`): así aparece en el launcher y en otra PC. Si no se pudo guardar (sin conexión), el juego lo reintenta en cada sincronización.

**Configuración en el panel de Supabase (una sola vez):**

- *Authentication › Sign In / Providers › Email*: habilitado, con *Confirm email* prendido. La contraseña mínima del juego es de 8 caracteres; conviene poner el mismo mínimo en *Password Requirements*.
- *Anonymous Sign-Ins* queda habilitado mientras haya versiones viejas del juego en uso (y para el editor sin cuenta). Las sesiones anónimas no suben progreso ni entran al ranking.
- *Authentication › Emails › SMTP Settings*: un SMTP propio. El mail que trae Supabase solo les escribe a los miembros del proyecto y manda muy pocos por hora. El proyecto usa una cuenta de Gmail con una contraseña de aplicación, que se escribe solo en ese panel (nunca en el juego ni en el repositorio).
- *Authentication › Emails › Templates*: el juego pide el **código** (`{{ .Token }}`), no el link. Plantilla **Confirm signup**:
  - Asunto: `Tu código para confirmar la cuenta`
  - Cuerpo: `<h2>Project Riftwalker</h2><p>¡Hola! Este es tu código para confirmar tu cuenta de Project Riftwalker:</p><p style="font-size:28px;letter-spacing:6px"><b>{{ .Token }}</b></p><p>Escribilo en el launcher, donde creaste la cuenta. Vence en 1 hora.</p><p>Si no creaste una cuenta, ignorá este mail.</p>`
- *Authentication › Rate Limits*: con el SMTP propio, subir los mails por hora según los jugadores esperados.

**Reset antes de abrir las cuentas (US 281, una sola vez):**

1. Volver a correr `logros.sql`: `rw_sync_comets` y `rw_sync_progress` pasan a rechazar las sesiones anónimas (`Account required`).
2. Correr `reset-cuentas.sql`. **No se puede deshacer.** Muestra primero qué va a borrar y después borra el ranking, los logros y el progreso de las pruebas, y los jugadores anónimos sin opiniones. Los anónimos con opiniones quedan, porque borrarlos borraría sus opiniones.

## Monedas de la cuenta (US 272)

El saldo de créditos, boletos y cristales de una cuenta lo lleva la base: en SQL Editor ejecutar `Backend/Supabase/monedas.sql` (después de `logros.sql`; es reejecutable y no borra nada). La prueba opcional `monedas.test.sql` revierte sus datos al terminar.

- Tablas `rw_wallet` (el saldo) y `rw_wallet_moves` (cada movimiento con un id que genera el juego), con RLS y sin acceso directo. La RPC `rw_wallet_apply` aplica los movimientos en orden y devuelve el saldo y los ids rechazados.
- Lo ganado (partidas, repetidos de las cápsulas, compras simuladas y F9) se suma enseguida en el juego y espera en una fila (en el archivo de la cuenta) hasta que sube; sin conexión espera hasta que vuelva.
- Para gastar (abrir cápsulas y canjear) la base confirma antes: si no alcanza el saldo de la cuenta (se gastó en otra PC), lo rechaza y no cambia nada. Sin conexión no se gasta y la Estación lo avisa.
- Reintentar no repite: un movimiento que ya llegó no se vuelve a aplicar, y uno rechazado sigue rechazado.
- Las cuentas arrancan en cero. Las monedas que había en la PC (o en el archivo de una cuenta antes de esta versión) no pasan a la cuenta: la primera vez que se conecta, el juego toma el saldo de la base.
- «Reiniciar datos de prueba» (Tienda) es solo sin cuenta.
- Mientras no se corra `monedas.sql`, el juego sigue con las monedas en la PC, como antes.
- Como el resto, no es a prueba de trampas: el sorteo de las cápsulas sigue en el juego y la base acepta lo que el juego dice que ganó (con topes por movimiento).
## Seguridad y límites de esta entrega

Las tablas tienen RLS y no permiten acceso directo desde el cliente. Las RPC identifican al jugador por su sesión, no por un ID enviado en el cuerpo; los desbloqueos se calculan en la base a partir de los contadores. No se distribuyen claves administrativas.

Los contadores siguen siendo declarados por el cliente: esto **no es un ranking resistente a trampas**. Para el proyecto universitario permite demostrar persistencia compartida. Una publicación competitiva requiere validar los eventos en un servidor autoritativo y añadir protección contra altas abusivas. El flujo actual no incorpora CAPTCHA; si se exige CAPTCHA en Supabase habrá que integrar ese flujo antes de permitir nuevas identidades.

La base valida el nombre (3 a 16 caracteres: letras, números, espacio, `-` y `_`, como el juego). Si lo rechaza, el juego lo dice en Logros ("El ranking online no aceptó tu nombre"). Conviene probar una vez un nombre con ñ o tildes: que las acepte depende de la configuración regional de la base.

No se modificaron economías, recompensas, cosméticos ni otros tipos de logro. Los contadores del minijuego anteriores a esta integración no se migran porque no estaban guardados por color en este sistema.

## Validación

Los tests automáticos de EditMode (`My project/Assets/Tests/EditMode`, ver `Assets/Tests/LEEME.md`) prueban los límites, saltos de metas, independencia por color, ausencia de duplicados, monotonía, JSON, guardados de versiones con otra cantidad de logros, la unión de dos progresos (dos juegos en la misma PC), respaldo atómico y rechazo de claves secretas sin modificar el progreso del usuario. También comprueban que cada logro, estadística y categoría del ranking del juego exista en `logros.sql`. Se corren desde *Window > General > Test Runner*, pestaña EditMode.

La prueba real entre jugadores requiere completar la configuración anterior. `Assets/Resources/LogrosOnline.asset` ya tiene la URL del proyecto y la clave publicable, y está habilitado: esa clave es pública por diseño (las tablas están protegidas por RLS y solo se usan las RPC). Nunca poner ahí `service_role` ni `sb_secret_` (el juego las rechaza).
