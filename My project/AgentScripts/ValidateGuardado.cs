using System;
using System.IO;
using UnityEngine;

// Pruebas del guardado del jugador (US 159, US 162 y US 163) sin tocar el progreso real: usan archivos temporales y
// una clave de PlayerPrefs propia que se borra al terminar. Se corre con: unity command run_script
// --file AgentScripts/ValidateGuardado.cs --entry ValidateGuardado.Main
public static class ValidateGuardado
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }

    public static string Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "riftwalker-guardado-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        const string prefsKey = "Prueba.ValidateGuardado.Entero";
        try
        {
            string path = Path.Combine(folder, "jugador.json");

            // Roundtrip with the three types, and type mismatch like PlayerPrefs.
            var a = new Guardado.Almacen(path, false); a.Cargar();
            Check(a.Nuevo, "new file");
            a.SetInt("n", 42); a.SetFloat("f", 0.25f); a.SetString("s", "Ñandú");
            Check(a.Guardar() && File.Exists(path), "first save");
            var b = new Guardado.Almacen(path, false); b.Cargar();
            Check(b.GetInt("n", 0) == 42 && Mathf.Approximately(b.GetFloat("f", 0f), 0.25f) && b.GetString("s", "") == "Ñandú", "roundtrip");
            Check(b.GetInt("s", -1) == -1 && b.GetString("n", "x") == "x", "type mismatch returns default");

            // Two games: each keeps what it changed, nothing is lost; registered progress keeps the maximum.
            Guardado.RegistrarMaximo("Prueba.Validacion.");
            var uno = new Guardado.Almacen(path, false); uno.Cargar();
            var dos = new Guardado.Almacen(path, false); dos.Cargar();
            uno.SetString("de-uno", "1"); uno.SetInt("Prueba.Validacion.record", 5); uno.Guardar();
            dos.SetString("de-dos", "2"); dos.SetInt("Prueba.Validacion.record", 3); dos.Guardar();
            var c = new Guardado.Almacen(path, false); c.Cargar();
            Check(c.GetString("de-uno", "") == "1" && c.GetString("de-dos", "") == "2", "two games keep both changes");
            Check(c.GetInt("Prueba.Validacion.record", 0) == 5, "registered key keeps maximum");

            // DeleteKey survives a save and a merge.
            c.DeleteKey("de-uno"); c.Guardar();
            var d = new Guardado.Almacen(path, false); d.Cargar();
            Check(!d.HasKey("de-uno") && d.HasKey("de-dos"), "delete persists");

            // US 163, CA3: damaged file -> backup; both damaged -> fresh start, notice, damaged copy kept apart.
            File.WriteAllText(path, "{ esto no es json");
            var e = new Guardado.Almacen(path, false); e.Cargar();
            Check(e.Aviso != null && e.Pendiente && e.HasKey("de-dos"), "recovered from backup");
            Check(Directory.GetFiles(folder, "jugador.danado-*.json").Length == 1, "damaged copy kept");
            File.WriteAllText(path + ".bak", "también roto");
            var f = new Guardado.Almacen(path, false); f.Cargar();
            Check(f.Nuevo && f.Aviso != null && !f.HasKey("de-dos"), "fresh start when both are damaged");

            // US 163, CA4: a newer format is not overwritten.
            File.WriteAllText(path, "{\"version\":99,\"valores\":[{\"clave\":\"x\",\"tipo\":2,\"valor\":\"nuevo\"}]}");
            var g = new Guardado.Almacen(path, false); g.Cargar();
            g.SetString("x", "viejo");
            Check(g.SoloLectura && !g.Guardar() && File.ReadAllText(path).Contains("nuevo"), "newer format untouched");

            // Lazy migration from PlayerPrefs: read once, then written to the file.
            PlayerPrefs.SetInt(prefsKey, 7);
            string migrated = Path.Combine(folder, "migracion.json");
            var h = new Guardado.Almacen(migrated, true); h.Cargar();
            Check(h.GetInt(prefsKey, 0) == 7 && h.Pendiente, "migrated from PlayerPrefs");
            h.Guardar();
            Check(File.ReadAllText(migrated).Contains(prefsKey), "migrated value written");

            // US 159: totals merge with the maximum and the best reaction with the minimum.
            var mine = new EstadisticasMinijuego.Totales { disparos = 10, puntos = 300, mejorReaccionMs = 900 };
            mine.aciertos[0] = 4;
            var other = new EstadisticasMinijuego.Totales { disparos = 7, puntos = 500, mejorReaccionMs = 700 };
            other.aciertos[2] = 1;
            Check(EstadisticasMinijuego.Unir(mine, other) && mine.disparos == 10 && mine.puntos == 500 && mine.aciertos[0] == 4 &&
                  mine.aciertos[2] == 1 && mine.mejorReaccionMs == 700, "minigame merge");
            Check(!EstadisticasMinijuego.Unir(mine, other), "minigame merge idempotent");
            Check(EstadisticasMinijuego.Leer("basura").SinDatos && EstadisticasMinijuego.Leer("").SinDatos, "minigame bad data starts empty");

            // Achievements merge through the save file keys.
            string merged = LogrosCometas.JuntarJson("{\"version\":1,\"aciertos\":[12,0,0],\"fechas\":[\"a\"]}", "{\"version\":1,\"aciertos\":[3,9,0],\"fechas\":[]}");
            var p = JsonUtility.FromJson<LogrosCometas.Progreso>(merged);
            Check(p.aciertos[0] == 12 && p.aciertos[1] == 9, "achievements merge");

            // Cloud document survives JsonUtility (what LogrosOnline sends and reads back).
            var doc = new ProgresoNube.Documento();
            doc.contadores.Add(new ProgresoNube.Numero { clave = "minijuego.disparos", valor = 12 });
            doc.conjuntos.Add(new ProgresoNube.Conjunto { clave = "perfil.obtenidos", elementos = new[] { "av:a" } });
            doc.valores.Add(new ProgresoNube.Texto { clave = "perfil.titulo", valor = "" });
            var back = JsonUtility.FromJson<ProgresoNube.Documento>(JsonUtility.ToJson(doc));
            Check(back.contadores[0].valor == 12 && back.conjuntos[0].elementos[0] == "av:a" && back.valores[0].valor == "", "cloud document roundtrip");

            return checks + " checks passed. Real player data untouched.";
        }
        finally
        {
            PlayerPrefs.DeleteKey(prefsKey);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
