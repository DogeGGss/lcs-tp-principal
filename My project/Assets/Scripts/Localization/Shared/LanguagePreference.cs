using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Riftwalker.Localization
{
    /// <summary>A separate preference shared with the launcher; does not rewrite player saves.</summary>
    public static class LanguagePreference
    {
        public const string FileName = "idioma.json";
        public static string Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return TranslationCatalog.DefaultLanguage;
                var match = Regex.Match(File.ReadAllText(filePath), "\"language\"\\s*:\\s*\"([A-Za-z0-9-]+)\"");
                return match.Success ? match.Groups[1].Value.ToLowerInvariant() : TranslationCatalog.DefaultLanguage;
            }
            catch (IOException) { return TranslationCatalog.DefaultLanguage; }
            catch (UnauthorizedAccessException) { return TranslationCatalog.DefaultLanguage; }
        }

        public static void Save(string filePath, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code, "^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$"))
                throw new ArgumentException("Invalid locale code.", nameof(code));
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, "{\"language\":\"" + code.ToLowerInvariant() + "\"}", new UTF8Encoding(false));
            if (File.Exists(filePath)) File.Replace(temporary, filePath, null);
            else File.Move(temporary, filePath);
        }
    }
}
