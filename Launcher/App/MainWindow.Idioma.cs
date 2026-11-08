using System.Windows;
using System.Windows.Controls;
namespace Riftwalker.Launcher;
public partial class MainWindow
{
    private sealed record LanguageChoice(string Code,string Label);
    private bool populatingLanguages;
    private void InitializeLanguages()
    {
        populatingLanguages=true;
        LanguagePicker.ItemsSource=LauncherText.Language.Locales.Select(l=>new LanguageChoice(l.code,l.name)).ToArray();
        LanguagePicker.SelectedValuePath=nameof(LanguageChoice.Code);
        LanguagePicker.SelectedValue=LauncherText.Language.Code;
        populatingLanguages=false;
        LauncherText.Language.Changed+=RefreshWindowLanguage;
        Closed+=(_,_)=>LauncherText.Language.Changed-=RefreshWindowLanguage;
    }
    private void Language_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(populatingLanguages||LanguagePicker.SelectedItem is not LanguageChoice selected)return;
        try{LauncherText.Language.Select(selected.Code,!App.Preview);}
        catch(Exception ex)
        {
            LauncherText.BindFormat(SettingsError,$"No se pudo guardar el idioma: {ex.Message}");
            populatingLanguages=true;LanguagePicker.SelectedValue=LauncherText.Language.Code;populatingLanguages=false;
        }
    }
    private void RefreshWindowLanguage()
    {
        if(!busy&&!App.Preview)Idle();
        if(releases.Count>0)
        {
            string? selectedTag=(ReleasePicker.SelectedItem as NoteChoice)?.Release.Tag;
            ShowReleases();
            if(selectedTag!=null&&ReleasePicker.ItemsSource is IEnumerable<NoteChoice> choices)ReleasePicker.SelectedItem=choices.FirstOrDefault(c=>c.Release.Tag==selectedTag)??choices.FirstOrDefault();
        }
    }
}