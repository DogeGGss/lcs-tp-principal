# Uso en las builds de Unity

Los PNG de esta propuesta se copiaron a `My project/Assets/UI/Branding/`.

- **Windows:** `RiftwalkerIcon.png` se asigna a todos los tamaños del ícono de Standalone y al ícono por defecto. Unity genera los íconos del ejecutable a partir del PNG; no necesita importar el ICO.
- **Inicio:** splash nativo desactivado. Una apertura propia revela `RiftwalkerLogo.png` desde una grieta naranja sobre negro, dura unos 4,5 segundos y activa el menú. El fondo de campus anterior ya no se utiliza. Consultar `SPLASH.md`.
- **Automático:** `Assets/Editor/RiftwalkerBuildBranding.cs` ejecuta la configuración antes de cualquier build Windows, incluidas Build Profiles, Build And Run y «Riftwalker > Publicar versión».
- **Vista previa:** abrir `intro-preview.html` para una aproximación de la animación. La versión Unity se inserta automáticamente en la copia de MenuPrincipal durante la build; no aparece en la vista previa del splash nativo ni al pulsar Play en una escena sin modificar.

No se abrieron escenas ni se inició Unity para realizar esta configuración. El cambio de Player Settings se aplica mediante la API del Editor cuando se ejecuta el menú o la próxima build. La visualización final del arranque y del ejecutable requiere generar y abrir una build.
