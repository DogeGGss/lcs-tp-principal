# Evidencia de los tests automáticos

Evidencia de los casos del [plan de pruebas del Sprint 3](https://docs.google.com/spreadsheets/d/1iCopQX-nuzyus-aZ69fCgBleHXneO_9w6ZeDRsNy9Yk) que quedaron **Exitoso** con el encargado "Test automático". Cada caso tiene acá su sección:

- el criterio de aceptación;
- los tests que lo prueban, con link a su código;
- qué comprueba cada test;
- el resultado de la corrida.

## Corrida del 20/10/2026

| | EditMode | PlayMode |
|---|---|---|
| **Resultado** | ✅ 88 de 88 pasaron | ✅ 33 de 33 pasaron |
| **Hora** | 18:22 a 18:22 (19 s) | 18:24 a 18:26 (2 min 52 s) |
| **Archivo de resultados** | [2026-10-20-editmode.xml](resultados/2026-10-20-editmode.xml) | [2026-10-20-playmode.xml](resultados/2026-10-20-playmode.xml) |

- **Unity:** 6000.6.2f1. **Versión del juego:** 0.3.0-exp.6.
- **Código probado:** commit [`3c929cf`](https://github.com/DogeGGss/lcs-tp-principal/commit/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d). Es develop con la versión 0.3.0-exp.6 (#518), más el arreglo para que la pausa no frene los tests.
- Las horas de esta página son de Argentina. En los archivos de resultados están en UTC (3 horas más).
- **Para repetirla:** en Unity, *Window > General > Test Runner*, pestaña EditMode y después PlayMode, *Run All*. Cada sección dice cuál filtrar.

## Casos

| Caso | Historia | Criterio | Tests |
|---|---|---|---|
| [TC-826](#tc-826) | US-140 | CA2: Ganador | 5 de `DeathmatchTests` |
| [TC-994](#tc-994) | US-225 | CA6: Zonas de daño | `ModelosTests`, con los 4 personajes |
| [TC-1069](#tc-1069) | US-222 | CA3: Una sola contabilización | 1 de `EstadisticasTests` |
| [TC-1072](#tc-1072) | US-222 | CA6: Primer uso y actualización | 1 de `EstadisticasTests` |
| [TC-1074](#tc-1074) | US-223 | CA2: Datos consistentes | 1 de `EstadisticasTests` |
| [TC-1075](#tc-1075) | US-223 | CA3: Casos límite | 2 de `EstadisticasTests` |
| [TC-1076](#tc-1076) | US-223 | CA4: Precisión | 3 de `EstadisticasTests` |

## TC-826

**US-140 · CA2: Ganador** · ✅ Pasó: 5 de 5 tests

> Gana el primero que llega a 25 bajas y la partida termina en ese momento. Si se terminan los 8 minutos, gana el que tiene más bajas; si hay empate, el que tiene menos muertes y, si siguen empatados, el que llegó primero a esa cantidad de bajas.

Los tests usan las mismas funciones con las que el anfitrión anota cada baja (`ContarBaja`) y ordena la tabla y elige al ganador (`Puestos`), en `PartidaDeathmatch`.

| Test | Qué comprueba | Resultado |
|---|---|---|
| [`Gana_al_llegar_a_25_bajas`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/DeathmatchTests.cs#L19-L25) | Con 24 bajas no se gana; con 25, sí. Si nadie llega, la partida dura 8 minutos. | ✅ Pasó |
| [`Mas_bajas_va_primero`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/DeathmatchTests.cs#L27-L32) | Al terminar el tiempo va primero el de más bajas, aunque tenga más muertes. | ✅ Pasó |
| [`Empate_en_bajas_desempata_por_muertes`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/DeathmatchTests.cs#L34-L38) | Con las mismas bajas va primero el de menos muertes. | ✅ Pasó |
| [`Empate_total_gana_el_que_llego_primero`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/DeathmatchTests.cs#L40-L47) | Con las mismas bajas y muertes va primero el que llegó antes a esas bajas. | ✅ Pasó |
| [`Ordena_la_tabla`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/DeathmatchTests.cs#L49-L57) | Una tabla de 4 jugadores, con empates de los dos tipos, queda en el orden correcto. | ✅ Pasó |

**Para repetirlo:** Test Runner > EditMode > `DeathmatchTests`.

## TC-994

**US-225 · CA6: Zonas de daño** · ✅ Pasó con los 4 personajes

> Las zonas de daño (cabeza, cuerpo y piernas) siguen al modelo nuevo, así un tiro a la cabeza cuenta como cabeza en todos los personajes.

Test: [`ModelosTests.Las_zonas_de_danio_siguen_al_modelo`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/ModelosTests.cs#L22-L71). Arma el cuerpo del prefab del jugador y le pone el modelo de cada personaje con `CuerpoDePersonaje`, como en la partida. Después comprueba:

1. que el modelo se pueda poner (es humanoide);
2. que estén las 12 zonas de daño del prefab;
3. que todas cuelguen de huesos del modelo nuevo;
4. que la zona de la cabeza esté en el hueso de la cabeza;
5. que un tiro de frente a la altura de la cabeza, buscado como lo hace el arma, encuentre primero la zona de la cabeza.

| Personaje | Resultado |
|---|---|
| Alfa | ✅ Pasó (2,7 s) |
| Bravo | ✅ Pasó (3,0 s) |
| Charlie | ✅ Pasó (0,1 s) |
| Delta | ✅ Pasó (0,3 s) |

**Para repetirlo:** Test Runner > PlayMode > `ModelosTests`.

## TC-1069

**US-222 · CA3: Una sola contabilización** · ✅ Pasó

> Procesar dos veces un resultado, reabrir su pantalla o reconectar a la misma partida no duplica sus aportes. Se identifica cada partida y lo ya incorporado.

| Test | Qué comprueba | Resultado |
|---|---|---|
| [`La_misma_partida_no_se_suma_dos_veces`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L66-L76) | Cada partida se identifica por la sala y la hora del servidor en que empezó, que son las mismas al reconectar. Sumarla dos veces no cambia nada: siguen 1 partida y 12 bajas. Una partida sin identificar, o vacía, no se suma. | ✅ Pasó |

**Para repetirlo:** Test Runner > EditMode > `EstadisticasTests`.

## TC-1072

**US-222 · CA6: Primer uso y actualización** · ✅ Pasó

> Los contadores nuevos comienzan en cero sin borrar los anteriores. No se inventan totales históricos que el juego nunca registró.

| Test | Qué comprueba | Resultado |
|---|---|---|
| [`Leer_datos_rotos_o_viejos`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L115-L133) | Lo guardado se vuelve a leer igual: siguen las 10 bajas. Un guardado de una versión vieja, sin estos contadores, arranca con ellos en cero y no inventa nada. Datos rotos o vacíos arrancan sin datos. | ✅ Pasó |

**Para repetirlo:** Test Runner > EditMode > `EstadisticasTests`.

## TC-1074

**US-223 · CA2: Datos consistentes** · ✅ Pasó

> Se usan bajas, muertes y asistencias del mismo conjunto de partidas. Zombies y cometas no contribuyen.

| Test | Qué comprueba | Resultado |
|---|---|---|
| [`Suma_por_modo_y_general`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L42-L64) | Con una partida del Táctico y una de Deathmatch, el KDA sale de las bajas, asistencias y muertes de esas mismas partidas: (32 + 8) / 14. No es el promedio de cada partida. Zombies y cometas tienen sus propias estadísticas y no entran en estos totales. | ✅ Pasó |

**Para repetirlo:** Test Runner > EditMode > `EstadisticasTests`.

## TC-1075

**US-223 · CA3: Casos límite** · ✅ Pasó: 2 de 2 tests

> Cero partidas, cero muertes y datos antiguos incompletos tienen un resultado definido; nunca se muestran NaN, infinito ni un valor inventado.

| Test | Qué comprueba | Resultado |
|---|---|---|
| [`Kda_sin_datos_y_sin_muertes`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L25-L34) | Sin partidas, el KDA es 0. Sin muertes se divide por 1: 5 bajas y 3 asistencias dan 8. Nunca da NaN ni infinito. | ✅ Pasó |
| [`Leer_datos_rotos_o_viejos`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L115-L133) | Un guardado viejo o incompleto se lee con todo en cero, así que el KDA queda definido. | ✅ Pasó |

**Para repetirlo:** Test Runner > EditMode > `EstadisticasTests`.

## TC-1076

**US-223 · CA4: Precisión** · ✅ Pasó: 3 de 3 tests

> El valor se calcula con los totales válidos y solo se redondea para mostrarlo. Se verifica con ejemplos conocidos una vez aprobada la fórmula.

| Test | Qué comprueba | Resultado |
|---|---|---|
| [`Kda_formula`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L36-L40) | Ejemplo conocido: (10 bajas + 2 asistencias) / 4 muertes = 3. | ✅ Pasó |
| [`Suma_por_modo_y_general`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L42-L64) | El KDA se calcula con los totales de todas las partidas, sin redondear. | ✅ Pasó |
| [`Formatos_del_ranking`](https://github.com/DogeGGss/lcs-tp-principal/blob/3c929cf5b9e667fb74f3af5cbe9b1eb70107445d/My%20project/Assets/Tests/EditMode/EstadisticasTests.cs#L161-L169) | Solo se redondea al mostrarlo: un KDA de 1,789 se ve "1,79". | ✅ Pasó |

**Para repetirlo:** Test Runner > EditMode > `EstadisticasTests`.
