using System.Text.Json;
using Riftwalker.Localization;
namespace Riftwalker.Launcher;

public sealed class LauncherLanguage
{
    private readonly TranslationCatalog catalog = new();
    private readonly Dictionary<string,Dictionary<string,string>> values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,string> sourceKeys = new(StringComparer.Ordinal);
    private readonly string preferencePath;
    public IReadOnlyList<LocaleDefinition> Locales { get; }
    public string Code { get; private set; } = TranslationCatalog.DefaultLanguage;
    public event Action? Changed;
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    public LauncherLanguage(string preferencePath,IEnumerable<LocaleDefinition> locales,IEnumerable<(string Locale,TranslationFile File)> files)
    {
        this.preferencePath=preferencePath;Locales=locales.ToArray();
        if(!Locales.Any(l=>l.code==TranslationCatalog.DefaultLanguage))throw new InvalidDataException("The Spanish source locale is required.");
        foreach(var (locale,file) in files)
        {
            catalog.Add(locale,file.entries);
            if(!values.TryGetValue(locale,out var table))values.Add(locale,table=new(StringComparer.Ordinal));
            foreach(var entry in file.entries??[])table.Add(entry.key,entry.text);
        }
        if(values.TryGetValue(TranslationCatalog.DefaultLanguage,out var sources))foreach(var entry in sources)sourceKeys.TryAdd(entry.Value,entry.Key);
        var saved=LanguagePreference.Load(preferencePath);
        Code=Locales.FirstOrDefault(l=>string.Equals(l.code,saved,StringComparison.OrdinalIgnoreCase))?.code??TranslationCatalog.DefaultLanguage;
    }
    public static LauncherLanguage FromResources(string preferencePath)
    {
        var assembly=typeof(LauncherLanguage).Assembly;
        var resources=assembly.GetManifestResourceNames().ToDictionary(n=>n.Replace('\\','/'),n=>n,StringComparer.Ordinal);
        T Read<T>(string name) where T:class
        {
            if(!resources.TryGetValue(name,out var actual))throw new InvalidDataException("Missing localization resource: "+name);
            using var stream=assembly.GetManifestResourceStream(actual)!;
            return JsonSerializer.Deserialize<T>(stream,Json)??throw new InvalidDataException("Invalid localization resource: "+name);
        }
        var manifest=Read<LocaleManifest>("Localization/locales.json");
        var files=new List<(string,TranslationFile)>();
        foreach(var locale in manifest.locales)files.Add((locale.code,Read<TranslationFile>("Localization/"+locale.code+"/launcher.json")));
        return new LauncherLanguage(preferencePath,manifest.locales,files);
    }
    public string T(string key,params object[] arguments)=>catalog.Format(Code,key,arguments);
    public string Source(string? source)
    {
        if(source==null)return string.Empty;
        if(sourceKeys.TryGetValue(source,out var key))return catalog.GetText(Code,key);
        var trimmed=source.Trim();
        if(trimmed.Length>0&&sourceKeys.TryGetValue(trimmed,out key))
        {
            int leading=source.Length-source.TrimStart().Length,trailing=source.Length-source.TrimEnd().Length;
            return source[..leading]+catalog.GetText(Code,key)+(trailing==0?"":source[^trailing..]);
        }
        return source;
    }
    public string Format(FormattableString source)=>TranslationCatalog.FormatText(Code,Source(source.Format),source.GetArguments());
    public bool TrySourceKey(string source,out string key)=>sourceKeys.TryGetValue(source,out key!);
    public IEnumerable<KeyValuePair<string,string>> Resources=>values[TranslationCatalog.DefaultLanguage].Keys.Select(key=>new KeyValuePair<string,string>(key,catalog.GetText(Code,key)));
    public void Select(string code,bool persist=true)
    {
        var locale=Locales.FirstOrDefault(l=>string.Equals(l.code,code,StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("Unsupported language: "+code,nameof(code));
        if(persist)LanguagePreference.Save(preferencePath,locale.code);
        if(Code==locale.code)return;Code=locale.code;Changed?.Invoke();
    }
}