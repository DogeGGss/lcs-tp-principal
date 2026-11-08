# Idiomas del juego y del launcher

El catálogo fuente está en `My project/Assets/Resources/Localization`. `locales.json` declara los idiomas y sus nombres nativos; cada carpeta de idioma contiene los mismos archivos y claves. Español (`es`) es la base e inglés (`en`) es el segundo idioma.

Los catálogos JSON son la única fuente de traducciones. Unity genera su colección `Riftwalker` mediante Unity Localization. El launcher incluye esos JSON como recursos de su aplicación. La selección se guarda en `idioma.json`, junto al guardado del juego, en `LocalLow/Equipo 4/Project Riftwalker` en Windows. No se reescribe `jugador.json` ni se modifican IDs de cuenta, cosméticos, logros o red.

## Añadir un idioma

Desde la raíz del repositorio:

```powershell
node tools/localization/manage.mjs add pt-BR "Português brasileiro"
node tools/localization/manage.mjs export pt-BR traducciones-pt-BR.csv
```

Se crean todas las claves del idioma nuevo, inicialmente sin traducción. Completar el CSV conservando claves, fuente española y variables; importar y validar:

```powershell
node tools/localization/manage.mjs import pt-BR traducciones-pt-BR.csv
node tools/localization/manage.mjs validate
```

Los valores vacíos, claves omitidas o duplicadas y variables alteradas producen un error. No rellenar huecos con el español para aparentar cobertura completa. El selector del juego y del launcher toma sus opciones del manifiesto, sin listas de idiomas escritas en los controladores.

Después, en Unity, ejecutar `Riftwalker > Idiomas > Sincronizar catálogos`. También se puede ejecutar `LocalizationCatalogBuild.SyncAndBuild` con Unity CLI. El preprocesador de build sincroniza la colección y construye Addressables antes del player. Revisar las pantallas con el idioma nuevo, particularmente textos largos; escrituras que necesitan otras fuentes requieren sus assets y comprobaciones visuales.

## Añadir textos

- Crear una clave estable con contexto: `ui.options.reset_confirm`, `profile.action.equip`, etc.
- Incluir su valor en cada idioma del mismo dominio JSON.
- En código del juego, usar `L10n.T(key, argumentos)` para valores que se actualizan y `L10n.Bind(texto, key, argumentos)` para rótulos que deben refrescarse automáticamente.
- `L10n.Source` y `BindSource` son adaptadores para textos españoles existentes y datos serializados. `BindSource` conserva la clave original tras traducir; no volver a buscar una clave a partir del texto ya traducido.
- Traducir la plantilla completa antes de insertar números, teclas, nombres de jugador o valores remotos. Conservar `{0}`, `{1:00}`, etiquetas TMP y tokens como `{Plantar}`.
- Nunca localizar nicknames, correo, contraseñas, IDs, rutas Resources, nombres de escena/objeto, propiedades Photon o mensajes que se usan como códigos de resultado. Traducir su presentación.
- Los nombres propios del universo y las armas se conservan. Los títulos cosméticos y las descripciones se localizan solo al mostrarse; su identidad persistida queda intacta.

## Validación

`node tools/localization/manage.mjs validate` comprueba ambos clientes, las claves, valores y variables. Las pruebas de localización de Unity cubren fallback, argumentos, persistencia y refresco de un componente al cambiar `SelectedLocale`. Ejecutar las pruebas existentes de reglas de cuenta y guardado para detectar regresiones. Las pruebas del launcher cubren la misma preferencia y los recursos embebidos.

La ausencia de errores en catálogos no demuestra por sí sola que todos los textos entren en pantalla. Las imágenes del mundo con carteles del lugar y las publicaciones externas conservan su contenido original; las traducciones de UI están incluidas en la instalación y funcionan sin conexión.