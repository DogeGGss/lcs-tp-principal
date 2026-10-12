#nullable disable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Riftwalker.Releases
{
    // Shared by the launcher and the game: SemVer precedence, including exp.2 < exp.10.
    public sealed class ReleaseVersion : IComparable<ReleaseVersion>
    {
        public readonly int Major, Minor, Patch;
        public readonly string Prerelease;
        public bool IsExperimental { get { return Prerelease.Length != 0; } }
        private ReleaseVersion(int major, int minor, int patch, string prerelease)
        { Major = major; Minor = minor; Patch = patch; Prerelease = prerelease; }

        public static bool TryParse(string text, out ReleaseVersion result)
        {
            result = null;
            var m = Regex.Match(text ?? "", @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$");
            int a, b, c;
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out a) || !int.TryParse(m.Groups[2].Value, out b) || !int.TryParse(m.Groups[3].Value, out c)) return false;
            string pre = m.Groups[4].Value;
            foreach (string part in pre.Split('.'))
                if (part.Length > 1 && part[0] == '0' && Regex.IsMatch(part, @"^\d+$")) return false;
            result = new ReleaseVersion(a, b, c, pre);
            return true;
        }
        public static ReleaseVersion Parse(string text)
        {
            ReleaseVersion result;
            if (!TryParse(text, out result)) throw new FormatException("Versión inválida: " + text);
            return result;
        }
        public int CompareTo(ReleaseVersion other)
        {
            if (other == null) return 1;
            int n = Major.CompareTo(other.Major); if (n != 0) return n;
            n = Minor.CompareTo(other.Minor); if (n != 0) return n;
            n = Patch.CompareTo(other.Patch); if (n != 0) return n;
            if (!IsExperimental || !other.IsExperimental) return IsExperimental == other.IsExperimental ? 0 : IsExperimental ? -1 : 1;
            string[] a = Prerelease.Split('.'), b = other.Prerelease.Split('.');
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                bool an = Regex.IsMatch(a[i], @"^\d+$"), bn = Regex.IsMatch(b[i], @"^\d+$");
                if (an && bn) { n = a[i].Length.CompareTo(b[i].Length); if (n == 0) n = string.CompareOrdinal(a[i], b[i]); }
                else if (an != bn) n = an ? -1 : 1;
                else n = string.CompareOrdinal(a[i], b[i]);
                if (n != 0) return n;
            }
            return a.Length.CompareTo(b.Length);
        }
        public override string ToString() { return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}{3}", Major, Minor, Patch, IsExperimental ? "-" + Prerelease : ""); }
    }
}
