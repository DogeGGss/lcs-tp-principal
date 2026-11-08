using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
namespace Riftwalker.Launcher;

public static class LauncherText
{
    public static LauncherLanguage Language { get; private set; } = null!;
    private static readonly Revision revision=new();
    private sealed class ContentState { public Func<string> Render { get; set; }=()=>""; }
    private static readonly ConditionalWeakTable<ContentControl,ContentState> contentStates=new();
    public static void Initialize(string[] args)
    {
        Language=LauncherLanguage.FromResources(System.IO.Path.Combine(Cuentas.CarpetaDelJuego(),Riftwalker.Localization.LanguagePreference.FileName));
        int index=Array.IndexOf(args,"--language");
        if(index>=0&&index+1<args.Length)Language.Select(args[index+1],false);
        Language.Changed+=ApplyResources;ApplyResources();
    }
    private static void ApplyResources()
    {
        foreach(var entry in Language.Resources)Application.Current.Resources["loc."+entry.Key]=entry.Value;
        CultureInfo culture;try{culture=CultureInfo.GetCultureInfo(Language.Code);}catch(CultureNotFoundException){culture=CultureInfo.InvariantCulture;}
        CultureInfo.CurrentCulture=culture;CultureInfo.CurrentUICulture=culture;revision.Notify();
    }
    public static string Source(string? source)=>Language==null?source??"":Language.Source(source);
    public static string F(FormattableString source)=>Language==null?source.ToString(CultureInfo.CurrentCulture):Language.Format(source);
    public static TextBlock Bind(TextBlock target,string source){BindProperty(target,TextBlock.TextProperty,source);return target;}
    public static TextBlock BindFormat(TextBlock target,FormattableString source){BindComputedProperty(target,TextBlock.TextProperty,()=>F(source));return target;}
    public static TextBlock BindValue(TextBlock target,Func<string> value){BindComputedProperty(target,TextBlock.TextProperty,value);return target;}
    public static void BindContent(ContentControl target,string source)=>BindValueContent(target,()=>Source(source));
    public static void BindFormatContent(ContentControl target,FormattableString source)=>BindValueContent(target,()=>F(source));
    public static void BindValueContent(ContentControl target,Func<string> value)
    {
        contentStates.GetValue(target,_=>new ContentState()).Render=value;
        BindComputedProperty(target,ContentControl.ContentProperty,value);
    }
    public static Action CaptureContent(ContentControl target)
    {
        if(contentStates.TryGetValue(target,out var state)){var original=state.Render;return()=>BindValueContent(target,original);}
        object originalValue=target.Content;return()=>target.Content=originalValue;
    }
    public static void BindName(FrameworkElement target,string source)=>BindProperty(target,System.Windows.Automation.AutomationProperties.NameProperty,source);
    public static Run BindRun(string source){var run=new Run();BindComputedProperty(run,Run.TextProperty,()=>Source(source));return run;}
    public static void BindProperty(FrameworkElement target,DependencyProperty property,string source)
    {
        if(Language.TrySourceKey(source,out var key))target.SetResourceReference(property,"loc."+key);
        else BindComputedProperty(target,property,()=>Source(source));
    }
    private static void BindComputedProperty(DependencyObject target,DependencyProperty property,Func<string> value)
    {
        BindingOperations.SetBinding(target,property,new Binding(nameof(Revision.Value)){Source=revision,Mode=BindingMode.OneWay,Converter=new TextConverter(value)});
    }
    // This traversal is used only for authored account presentation, never identity fields or patch notes.
    public static void Localize(UIElement element)
    {
        void Walk(DependencyObject node)
        {
            if(node is TextBox or PasswordBox)return;
            if(node is TextBlock text&&!string.IsNullOrEmpty(text.Text))Bind(text,text.Text);
            if(node is ContentControl content&&content.Content is string label)BindContent(content,label);
            foreach(object child in LogicalTreeHelper.GetChildren(node))if(child is DependencyObject dependency)Walk(dependency);
        }
        Walk(element);
    }
    private sealed class Revision:INotifyPropertyChanged
    {
        public int Value{get;private set;}
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Notify(){Value++;PropertyChanged?.Invoke(this,new(nameof(Value)));}
    }
    private sealed class TextConverter(Func<string> value):IValueConverter
    {
        public object Convert(object ignored,Type type,object parameter,CultureInfo culture)=>value();
        public object ConvertBack(object ignored,Type type,object parameter,CultureInfo culture)=>throw new NotSupportedException();
    }
}
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension:MarkupExtension
{
    public string Source{get;set;}="";
    public TrExtension(){}
    public TrExtension(string source)=>Source=source;
    public override object ProvideValue(IServiceProvider services)=>LauncherText.Language!=null&&LauncherText.Language.TrySourceKey(Source,out var key)?new DynamicResourceExtension("loc."+key).ProvideValue(services):Source;
}