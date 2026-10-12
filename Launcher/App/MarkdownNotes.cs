using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Riftwalker.Launcher;

public static class MarkdownNotes
{
    public static FlowDocument Render(string text)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(0), FontSize = 15, Foreground = Brush("#D6DAE0"),
            FontFamily = (FontFamily)Application.Current.Resources["BodyFont"], LineHeight = 23 };
        foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var p = new Paragraph { Margin = new Thickness(0, 3, 0, 9) };
            var heading = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (heading.Success)
            {
                line = heading.Groups[2].Value;
                p.FontFamily = (FontFamily)Application.Current.Resources["DisplayFont"];
                p.FontWeight = FontWeights.Bold; p.FontSize = heading.Groups[1].Length <= 2 ? 24 : 20;
                p.Foreground = Brush("#F3F4F6"); p.Margin = new Thickness(0, 12, 0, 7);
            }
            else if (Regex.IsMatch(line, @"^\s*[-*+]\s+")) line = "•  " + Regex.Replace(line, @"^\s*[-*+]\s+", "");
            int pos = 0;
            foreach (Match m in Regex.Matches(line, @"\*\*(.+?)\*\*|\[([^\]]+)\]\((https?://[^\s)]+)\)"))
            {
                if (m.Index > pos) p.Inlines.Add(new Run(line[pos..m.Index]));
                if (m.Groups[1].Success) p.Inlines.Add(new Bold(new Run(m.Groups[1].Value)));
                else
                {
                    var link = new Hyperlink(new Run(m.Groups[2].Value)) { NavigateUri = new Uri(m.Groups[3].Value), Foreground = Brush("#F29A38") };
                    link.RequestNavigate += (_, e) => { try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { } e.Handled = true; };
                    p.Inlines.Add(link);
                }
                pos = m.Index + m.Length;
            }
            p.Inlines.Add(new Run(line[pos..])); doc.Blocks.Add(p);
        }
        return doc;
    }
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}
