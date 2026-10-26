using System;
using System.IO;
using UnityEngine.TestTools;

// Antes de entrar en Play, los tests de PlayMode prenden ModoPruebas: el juego guarda en un archivo temporal que arranca
// vacío y no usa el servicio online, así una corrida no toca el progreso real ni sube nada. Al terminar se borra ese
// archivo y el juego vuelve al modo normal. Se usa con [PrebuildSetup] y [PostBuildCleanup] en cada clase de tests.

public class PruebasAisladas : IPrebuildSetup, IPostBuildCleanup
{
    public void Setup()
    {
        Environment.SetEnvironmentVariable(ModoPruebas.Variable, "1");
        Borrar();
    }

    public void Cleanup()
    {
        Borrar();
        Environment.SetEnvironmentVariable(ModoPruebas.Variable, null);
    }

    private static void Borrar()
    {
        foreach (string archivo in new[] { Guardado.RutaPruebas, Guardado.RutaPruebas + ".bak" })
            if (File.Exists(archivo)) File.Delete(archivo);
    }
}
