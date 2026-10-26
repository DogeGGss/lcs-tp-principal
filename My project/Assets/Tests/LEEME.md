# Tests automáticos de Project Riftwalker

Se corren desde **Window > General > Test Runner**.

| Pestaña | Carpeta | Qué prueba | Tarda |
|---|---|---|---|
| **EditMode** | `Tests/EditMode` | Reglas puras: versiones, partida, Deathmatch, habilidades, estadísticas, ranking, logros y guardado | Menos de 1 s |
| **PlayMode** | `Tests` | Componentes en una escena vacía (billetera, vida, tienda), zonas de daño de cada personaje, lo que se sube a la nube y escenas reales (smoke test y práctica) | Cerca de 1 min |
| **PlayMode**, categoría "Regresión de sonidos" | `Tests/Sonidos` | Que cada acción del jugador siga sonando, por su canal del mezclador y con su alcance, y que con cada volumen de Opciones en 0 su canal no se escuche (RS-01 a RS-32) | Cerca de 2 min |

## Regresión de sonidos

Cada test hace la acción como si la hiciera el jugador: `KeyBindings.Apretar(GameAction.Disparar)` aprieta la acción
por el mismo camino que una tecla (solo funciona con `ModoPruebas`). `Oido` junta las fuentes de audio que empiezan a
sonar después y el test comprueba el canal (SFX, Interfaz o Musica) y, si la historia lo pide, el alcance. Corren en
el campo de práctica (sin red) y en el menú. Los resultados se anotan en la pestaña "Regresión automática" del plan
de pruebas. Para agregar uno, copiá uno parecido de `RegresionSonidosTests` y sumá su fila con el número siguiente.

## Convenciones

- `Category("US 135")`: la historia del backlog que cubre el test. En el Test Runner se puede filtrar por historia.
- `Description("TC-843 CA3: ...")`: el caso de prueba del plan y el criterio de aceptación, si existen.
- Los nombres de los tests son frases (`Tope_de_9000`), así el reporte se lee solo.
- Un test por regla. Si una regla tiene varios valores, se usa `[TestCase]` en lugar de repetir el test.
- Los números de balance (recargas, cargas, radios) salen del código o de los assets de `Assets/Data`. Si alguien
  cambia el balance a propósito, se actualizan el test y la historia en el backlog.

## No tocan nada real

- Los de EditMode trabajan con objetos sueltos y archivos en una carpeta temporal que se borra al terminar.
- Los de PlayMode llevan `[PrebuildSetup]` y `[PostBuildCleanup]` con `PruebasAisladas`. Antes de entrar en Play
  prenden `ModoPruebas` (variable de entorno `RIFTWALKER_PRUEBAS=1`). Así, el juego guarda en un archivo temporal
  que arranca vacío y no usa el servicio online. Al terminar se apaga solo.
- Si agregás una clase de PlayMode que abra escenas o use el guardado, ponele esos dos atributos.

## Por línea de comandos

```
Unity.exe -batchmode -projectPath "My project" -runTests -testPlatform EditMode -testResults resultados.xml
```

Con Unity abierto no se puede abrir otra instancia del mismo proyecto: en ese caso, usá el Test Runner.
