using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class ValidateAchievements {
 static int checks;
 static void Check(bool ok,string message) { checks++; if(!ok)throw new Exception(message); }
 public static string Main() {
 var p=new LogrosCometas.Progreso();
 Check(LogrosCometas.Catalogo.Length==9,"catalog count");
 Check(LogrosCometas.Aplicar(p,0,9,"a").Count==0,"early unlock");
 Check(LogrosCometas.Aplicar(p,0,10,"b").Count==1,"first threshold");
 Check(LogrosCometas.Aplicar(p,0,10,"c").Count==0 && p.fechas[0]=="b","duplicate unlock");
 Check(LogrosCometas.Aplicar(p,0,100,"d").Count==2,"jump over thresholds");
 Check(LogrosCometas.Aplicar(p,0,2,"e").Count==0 && p.aciertos[0]==100,"regression");
 Check(p.aciertos[1]==0 && p.aciertos[2]==0 && p.fechas[3]==null,"color isolation");
 Check(LogrosCometas.Aplicar(p,1,50,"f").Count==2,"green thresholds");
 Check(LogrosCometas.Aplicar(p,2,100,"g").Count==3,"gold thresholds");
 Check(LogrosCometas.Aplicar(p,1,100,"h").Count==1,"last threshold");
 var copy=JsonUtility.FromJson<LogrosCometas.Progreso>(JsonUtility.ToJson(p));
 Check(copy.aciertos[2]==100 && copy.fechas[8]=="g","JSON roundtrip");
 // Saves from a version with fewer or more achievements still load, resized to the current catalog.
 var older=LogrosCometas.Validar(JsonUtility.FromJson<LogrosCometas.Progreso>("{\"version\":1,\"aciertos\":[12,0,3],\"fechas\":[\"x\",\"\"]}"));
 Check(older.fechas.Length==LogrosCometas.Catalogo.Length && older.fechas[0]=="x" && older.aciertos[0]==12,"older save resized");
 var newer=LogrosCometas.Validar(JsonUtility.FromJson<LogrosCometas.Progreso>("{\"version\":1,\"aciertos\":[1,2,3],\"fechas\":[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"j\",\"k\"]}"));
 Check(newer.fechas.Length==LogrosCometas.Catalogo.Length && newer.fechas[8]=="i","newer save resized");
 bool rejected=false; try { LogrosCometas.Validar(JsonUtility.FromJson<LogrosCometas.Progreso>("{\"version\":1,\"aciertos\":[1,-2,3]}")); } catch(InvalidDataException) { rejected=true; }
 Check(rejected,"negative counter rejected");
 // Two games on the same PC: saving merges with the file (maximum per color, keeps dates) instead of overwriting.
 var mine=new LogrosCometas.Progreso(); mine.aciertos[0]=15; mine.aciertos[1]=2;
 var theirs=new LogrosCometas.Progreso(); theirs.aciertos[0]=12; theirs.aciertos[1]=9; theirs.fechas[0]="t";
 Check(LogrosCometas.Juntar(mine,theirs) && mine.aciertos[0]==15 && mine.aciertos[1]==9 && mine.fechas[0]=="t","merge keeps maximum");
 Check(!LogrosCometas.Juntar(mine,theirs),"merge is idempotent");
 var folder=Path.Combine(Path.GetTempPath(),"riftwalker-achievements-"+Guid.NewGuid());
 try { var path=Path.Combine(folder,"test.json"); Guardado.EscribirAtomico(path,"first"); Guardado.EscribirAtomico(path,"second"); Check(File.ReadAllText(path)=="second" && File.ReadAllText(path+".bak")=="first","atomic backup"); }
 finally { if(Directory.Exists(folder))Directory.Delete(folder,true); }
 var config=ScriptableObject.CreateInstance<LogrosOnlineConfig>(); config.habilitado=true;config.url="https://example.supabase.co";config.clavePublica="sb_secret_fake";
 Check(!config.Valido,"secret key rejected");config.clavePublica="eyJ.legacy.jwt";Check(!config.Valido,"unverified JWT rejected");config.clavePublica="sb_publishable_example";Check(config.Valido,"publishable accepted");UnityEngine.Object.DestroyImmediate(config);
 const string asset="Assets/Resources/LogrosOnline.asset";
 if(AssetDatabase.LoadAssetAtPath<LogrosOnlineConfig>(asset)==null){Directory.CreateDirectory("Assets/Resources");AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LogrosOnlineConfig>(),asset);AssetDatabase.SaveAssets();}
 return checks+" checks passed. Configuration asset ready; real player data untouched.";
 }
}
