using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Riftwalker.Releases;

namespace Riftwalker.Launcher;

public partial class MainWindow : Window
{
    private readonly HttpClient http = ReleaseClient.CreateHttpClient();
    private readonly string[] args;
    private string root;
    private GameInstaller installer;
    private LauncherSettings settings = new();
    private List<GitHubRelease> releases = [];
    private GitHubRelease? target;
    private CancellationTokenSource? operation;
    private bool busy, online, installedLauncher, setup, closeAfterCancel;
    private string StatePath => Path.Combine(root, ".riftwalker", "settings.json");
    private string NotesPath => Path.Combine(root, ".riftwalker", "notes.json");
    private bool NeedsUpdate => target != null && (installer.Installed == null
        || (!settings.Experimental && ReleaseVersion.Parse(installer.Installed.Version).IsExperimental)
        || target.Version.CompareTo(ReleaseVersion.Parse(installer.Installed.Version)) > 0);

    public MainWindow(string root, string[] args)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => PermitirMinimizar();
        this.root = root; this.args = args; installer = new(root);
        installedLauncher = File.Exists(Path.Combine(root, ".riftwalker", "installation.json"));
        if (installedLauncher)
        {
            settings = Storage.Read<LauncherSettings>(StatePath) ?? new();
            installer.Recover();
        }
        int pedido = Array.IndexOf(args, "--tamano"); // vista previa de un tamaño: --tamano chica|normal|grande
        AplicarTamano(pedido >= 0 && pedido + 1 < args.Length ? args[pedido + 1] : settings.Size, false);
        LauncherVersionText.Text = $"PROJECT RIFTWALKER / LAUNCHER {App.LauncherVersion}";
        Notes.Document = MarkdownNotes.Render("## Preparando las novedades\nLas notas del parche aparecerán acá.");
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += (_, _) => { operation?.Cancel(); http.Dispose(); };
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Raiz.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
        if (App.Preview)
        {
            StateTitle.Text = "TU EQUIPO TE ESPERA"; InstalledText.Text = "Vista previa del diseño";
            StatusText.Text = "Todo en un lugar. Actualizá el juego, leé las novedades y entrá a la próxima partida.";
            MainAction.Content = "JUGAR  →"; MainAction.IsEnabled = true;
            Progress.IsIndeterminate = false; Progress.Value = 100; ProgressText.Text = "Vista previa · no instala ni abre el juego";
            ConnectionText.Text = "VISTA PREVIA"; Retry.Visibility = Visibility.Collapsed;
            ReleasePicker.Items.Add(new { Label = "Vista previa" }); ReleasePicker.SelectedIndex = 0;
            Notes.Document = MarkdownNotes.Render("## El próximo cruce empieza acá\nUn punto de encuentro para preparar tu próxima partida.\n### Tu juego, al día\n- Descargá las nuevas versiones desde este launcher.\n- Conservá tus opciones y tu progreso.\n- Consultá qué cambió antes de jugar.\n### Elegí tu andén\n**Táctico · Deathmatch · Zombie**\n\nEsta es una vista previa de la interfaz, no las notas de una versión publicada.");
            if (args.Contains("--settings-preview")) OpenSettings(true);
            if (args.Contains("--render"))
            {
                await Task.Delay(700);
                Raiz.BeginAnimation(OpacityProperty, null); Raiz.Opacity = 1;
                UpdateLayout();
                int index = Array.IndexOf(args, "--render");
                string path = index + 1 < args.Length ? args[index + 1] : "launcher-preview.png";
                // The whole frame, with its shadow and transparent corners (PNG with alpha).
                var bitmap = new RenderTargetBitmap((int)Raiz.ActualWidth, (int)Raiz.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(Raiz);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(path)) encoder.Save(stream);
                Close();
            }
            return;
        }
        if (args.Contains("--updated")) File.WriteAllText(Storage.Child(root, ".riftwalker/update-ok"), App.LauncherVersion);
        if (!installedLauncher) { OpenSettings(true); Idle(); return; }
        await RefreshAsync();
    }
    private void Begin(string text, bool cancellable)
    {
        busy = true; operation?.Dispose(); operation = new();
        StatusText.Text = text; MainAction.IsEnabled = false; MainAction.Content = text;
        Progress.IsIndeterminate = true; Retry.Visibility = Visibility.Collapsed;
        Cancel.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Idle(string? message = null)
    {
        busy = false; Progress.IsIndeterminate = false; Cancel.Visibility = Visibility.Collapsed;
        Retry.Visibility = installedLauncher ? Visibility.Visible : Visibility.Collapsed;
        MainAction.IsEnabled = true;
        var local = installedLauncher ? installer.Installed : null;
        InstalledText.Text = local == null ? "Juego sin instalar" : $"Versión {local.Version} instalada";
        bool returnStable = local != null && !settings.Experimental && ReleaseVersion.Parse(local.Version).IsExperimental;
        MainAction.Content = !installedLauncher ? "CONFIGURAR  →" : NeedsUpdate ? local == null ? "INSTALAR  ↓" : returnStable ? "VOLVER A LA ESTABLE  ↓" : "ACTUALIZAR  ↓" : local != null ? "JUGAR  →" : "REINTENTAR  ↻";
        StateTitle.Text = !installedLauncher ? "BIENVENIDO AL ANDÉN" : local == null ? "PRIMERA PARADA" : NeedsUpdate ? "HAY ALGO NUEVO" : "LISTO PARA ENTRAR";
        if (message != null) StatusText.Text = message;
        else if (target != null && NeedsUpdate) StatusText.Text = $"{(target.Prerelease ? "Experimental" : "Nueva versión")} {target.Version}. Instalá esta versión para jugar desde el launcher.";
        else if (local != null) StatusText.Text = online ? $"Versión {local.Version} · Actualizado" : "No se pudo buscar actualizaciones. Podés jugar con la versión instalada.";
        else StatusText.Text = "Configurá la instalación para comenzar.";
        ChannelBadge.Text = settings.Experimental ? "EXPERIMENTAL" : "ESTABLE";
        ChannelBadge.Foreground = (Brush)FindResource(settings.Experimental ? "Accent" : "Ink");
        if (closeAfterCancel) Close();
    }
    private async Task RefreshAsync()
    {
        if (busy || App.Preview) return;
        Begin("Buscando actualizaciones…", false);
        ProgressText.Text = "Consultando las versiones publicadas";
        try
        {
            var client = new ReleaseClient(http, Path.Combine(root, ".riftwalker"));
            bool fullList = true;
            try
            {
                releases = await client.FetchAsync(operation!.Token);
                Storage.Write(NotesPath, releases);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The API answered with an error (for example, GitHub's limit for a shared network). The latest stable
                // version still comes from its release.json; the notes are the ones saved last time. A timeout is not
                // retried this way: past 10 s it counts as no connection.
                Log(ex);
                fullList = false;
                releases = Storage.Read<List<GitHubRelease>>(NotesPath) ?? [];
                if (await client.LatestStableAsync(operation!.Token) is { } latest && !releases.Any(r => r.Tag == latest.Release.Tag))
                    releases.Insert(0, latest.Release with { Body = "## Notas no disponibles por ahora\nGitHub no devolvió la lista de versiones. Las notas de esta versión están en [GitHub Releases](" + ReleaseClient.ReleasesUrl + ")." });
                releases = releases.OrderByDescending(r => r.Version).ToList();
            }
            target = ReleaseClient.Select(releases, settings.Experimental); online = true;
            ConnectionText.Text = fullList ? "CONECTADO A GITHUB" : "CONECTADO · NOTAS GUARDADAS";
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(61, 220, 151));
            ShowReleases();
            if (!args.Contains("--skip-self-update"))
            {
                try
                {
                    // The launcher updates itself from its own release, apart from the game versions.
                    if (await client.LauncherAsync(operation!.Token) is { } launcher
                        && ReleaseVersion.Parse(launcher.Version).CompareTo(ReleaseVersion.Parse(App.LauncherVersion)) > 0)
                    {
                        StatusText.Text = "Actualizando el launcher…"; MainAction.Content = "ACTUALIZANDO LAUNCHER…";
                        await SelfUpdate.StartAsync(root, ReleaseClient.LauncherDownloadUrl, launcher, http, DownloadProgress(), operation.Token);
                        ((App)Application.Current).ReleaseLock();
                        busy = false; Close(); return;
                    }
                }
                catch (Exception ex) when (!operation.Token.IsCancellationRequested)
                {
                    // Launcher update failure must not discard the successfully fetched game version.
                    Log(ex); ProgressText.Text = "El launcher no pudo actualizarse. Lo intentará la próxima vez.";
                    Idle(); return;
                }
            }
            ProgressText.Text = target == null ? "Todavía no hay un release compatible con este launcher." : $"Canal {(settings.Experimental ? "experimental" : "estable")} · v{target.Version}";
            Idle(target == null ? "El equipo todavía no publicó una versión con el nuevo formato. Volvé a comprobar más tarde." : null);
        }
        catch (Exception ex)
        {
            target = null; online = false;
            releases = Storage.Read<List<GitHubRelease>>(NotesPath) ?? [];
            ShowReleases(); ConnectionText.Text = "SIN CONEXIÓN / DATOS GUARDADOS";
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(142, 150, 163));
            ProgressText.Text = "Podés volver a intentarlo cuando tengas conexión.";
            Log(ex); Idle("No se pudo buscar actualizaciones. " + (installer.Installed != null ? "Podés jugar con la versión instalada." : "Reintentá para instalar el juego."));
        }
    }
    private sealed record NoteChoice(GitHubRelease Release, string Label);
    private void ShowReleases()
    {
        var local = installer.Installed;
        var choices = releases.Where(r => settings.Experimental || !r.Prerelease).Select(r => new NoteChoice(r,
            $"v{r.Version}" + (local != null && r.Version.CompareTo(ReleaseVersion.Parse(local.Version)) > 0 ? " · NUEVO" : ""))).ToList();
        ReleasePicker.ItemsSource = choices;
        if (choices.Count > 0) ReleasePicker.SelectedIndex = 0;
        else Notes.Document = MarkdownNotes.Render(online ? "## El andén está listo\nLas notas aparecerán cuando el equipo publique la primera versión compatible.\n\nPodés consultar las versiones anteriores en [GitHub Releases](" + ReleaseClient.ReleasesUrl + ")." : "## Sin notas guardadas\nNo se pudieron cargar las notas del parche. Volvé a intentarlo cuando tengas conexión.");
    }
    private void Release_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ReleasePicker.SelectedItem is not NoteChoice choice) return;
        string kind = choice.Release.Prerelease ? "Experimental" : "Versión estable";
        NotesSubtitle.Text = choice.Release.Published == default ? kind : $"{choice.Release.Published:dd/MM/yyyy} · {kind}";
        string notas = ReleaseClient.NotesForLauncher(choice.Release.Body);
        Notes.Document = MarkdownNotes.Render(string.IsNullOrWhiteSpace(notas) ? "Sin notas para esta versión." : notas);
    }
    private Progress<TransferProgress> DownloadProgress() => new(p =>
    {
        Progress.IsIndeterminate = false; Progress.Value = p.Percent;
        string eta = double.IsFinite(p.SecondsLeft) ? $" · {TimeSpan.FromSeconds(Math.Clamp(p.SecondsLeft, 0, 86399)):mm\\:ss} restantes" : "";
        ProgressText.Text = $"{p.Percent:0}% · {p.Downloaded / 1048576d:N1} / {p.Total / 1048576d:N1} MB{eta}";
    });
    private async void Main_Click(object sender, RoutedEventArgs e)
    {
        if (App.Preview || busy) return;
        if (!installedLauncher) { OpenSettings(true); return; }
        if (target == null && installer.Installed == null) { await RefreshAsync(); return; }
        if (!NeedsUpdate)
        {
            try { installer.Launch(); Close(); } catch (Exception ex) { Log(ex); Idle(ex.Message); }
            return;
        }
        if (installer.IsRunning()) { Idle("Cerrá el juego para actualizar. Después tocá Actualizar."); return; }
        Begin("Descargando…", true);
        try
        {
            var release = target!;
            var manifest = await new ReleaseClient(http).ManifestAsync(release, operation!.Token);
            installer.CheckSpace(manifest.Game);
            string zip = installer.DownloadPath(manifest.Game);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    await new Downloader(http).DownloadAsync(release.Assets.Single(a => a.Name == manifest.Game.Asset).Url,
                        manifest.Game, zip, DownloadProgress(), operation.Token);
                    await installer.InstallAsync(zip, manifest, new Progress<string>(s => { StatusText.Text = s; Progress.IsIndeterminate = true; }), operation.Token);
                    break;
                }
                catch (InvalidDataException) when (attempt == 0)
                {
                    if (File.Exists(zip)) File.Delete(zip);
                    StatusText.Text = "El archivo estaba dañado. Descargando otra vez…";
                }
            }
            Progress.Value = 100; ProgressText.Text = "Instalación completada";
            ShowReleases(); Idle($"Versión {manifest.Version} instalada. Ya podés jugar.");
        }
        catch (OperationCanceledException) when (operation!.IsCancellationRequested) { Idle("Descarga cancelada. La instalación anterior se conserva."); }
        catch (Exception ex) { Log(ex); Idle(Cortada(ex) ? "Se interrumpió la descarga. Tocá Actualizar o Instalar para continuar desde donde quedó." : ex.Message); }
    }
    // A dropped connection: a timeout, an HTTP error, or the socket closing in the middle of the download.
    private static bool Cortada(Exception ex) => ex is OperationCanceledException or HttpRequestException or HttpIOException
        || ex is IOException { InnerException: System.Net.Sockets.SocketException };
    private async void Retry_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private void Cancel_Click(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void Guide_Click(object sender, RoutedEventArgs e)
    { try { Process.Start(new ProcessStartInfo("https://dogeggss.github.io/lcs-tp-principal/") { UseShellExecute = true }); } catch (Exception ex) { StatusText.Text = ex.Message; } }
    private void Settings_Click(object sender, RoutedEventArgs e) { if (!busy) OpenSettings(!installedLauncher); }

    // ---------- Own frame (no Windows title bar) ----------

    // The top bar and the banner move the window. The buttons on them handle their own clicks.
    private void Arrastrar(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }
    private void Minimizar_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close(); // OnClosing cancels a download first

    // A window without Windows' frame loses the minimize style: clicking its taskbar icon would not minimize it.
    private void PermitirMinimizar()
    {
        IntPtr ventana = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SetWindowLong(ventana, EstiloVentana, GetWindowLong(ventana, EstiloVentana) | ConMinimizar | ConMenuDeSistema);
    }
    private const int EstiloVentana = -16, ConMinimizar = 0x20000, ConMenuDeSistema = 0x80000;

    // ---------- Window size (Ajustes) ----------

    private const double Sombra = 12; // transparent margin around the panel, for its shadow

    // Chica: the normal panel at 80 %. Normal: 1120 x 700. Grande: 16:9 at the same height, scaled up to almost the
    // whole work area of the screen (92 %). The whole window is scaled, so the design is the same at every size.
    private void AplicarTamano(string tamano, bool centrar)
    {
        Rect area = SystemParameters.WorkArea;
        double ancho = 1120, alto = 700, escala = 1;
        if (tamano == "chica") escala = 0.8;
        else if (tamano == "grande")
        {
            ancho = Math.Round(alto * 16 / 9);
            escala = Math.Min(0.92 * area.Width / (ancho + 2 * Sombra), 0.92 * area.Height / (alto + 2 * Sombra));
        }
        Marco.Width = ancho; Marco.Height = alto;
        Escala.ScaleX = Escala.ScaleY = escala;
        ArmarMarco(ancho, alto);
        if (!centrar) return; // the first time, WindowStartupLocation centers it
        UpdateLayout();
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + (area.Height - ActualHeight) / 2;
    }

    // The panel's shape (also the content's clip), its outline and the corner strokes, for a logical size.
    private void ArmarMarco(double w, double h)
    {
        Geometry forma = Poligono(w, h, 0);
        Fondo.Data = forma; Shell.Clip = forma;
        Contorno.Data = Poligono(w, h, 0.5);
        AcentoAbajo.Data = Geometry.Parse(FormattableString.Invariant($"M {w - 0.5},{h - 118} L {w - 0.5},{h - 30.5} L {w - 30.5},{h - 0.5} L {w - 236},{h - 0.5}"));
        Canvas.SetLeft(PuntaAbajo, w - 241); Canvas.SetTop(PuntaAbajo, h - 3);
        Canvas.SetLeft(PuntaDerecha, w - 3); Canvas.SetTop(PuntaDerecha, h - 123);
        AcentoCeleste.Data = Geometry.Parse(FormattableString.Invariant($"M {w - 56},0.5 L {w - 12.5},0.5 L {w - 0.5},12.5 L {w - 0.5},46"));
        AcentoGris.Data = Geometry.Parse(FormattableString.Invariant($"M 0.5,{h - 48} L 0.5,{h - 12.5} L 12.5,{h - 0.5} L 48,{h - 0.5}"));
    }

    // Cut corners: 30 at top-left and bottom-right, 12 at the other two. "d" moves the edge inwards (0.5: crisp outline).
    private static Geometry Poligono(double w, double h, double d) => Geometry.Parse(FormattableString.Invariant(
        $"M {30 + d},{d} L {w - 12 - d},{d} L {w - d},{12 + d} L {w - d},{h - 30 - d} L {w - 30 - d},{h - d} L {12 + d},{h - d} L {d},{h - 12 - d} L {d},{30 + d} Z"));
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private void OpenSettings(bool first)
    {
        setup = first; SettingsPanel.Visibility = Visibility.Visible;
        InstallPath.Text = first ? DesktopInstall.DefaultRoot : root; InstallPath.IsReadOnly = !first; Browse.IsEnabled = first;
        Experimental.IsChecked = settings.Experimental;
        TamanoChica.IsChecked = settings.Size == "chica"; TamanoGrande.IsChecked = settings.Size == "grande";
        TamanoNormal.IsChecked = settings.Size != "chica" && settings.Size != "grande";
        SettingsTitle.Text = first ? "TU PRIMERA PARADA" : "TU ANDÉN, A TU MANERA";
        SaveSettings.Content = first ? "Instalar launcher" : "Guardar";
        SetupHint.Text = first ? "Instalá el launcher y sus accesos directos. Después podrás descargar el juego." : "La carpeta contiene el launcher y el juego. Tus partidas guardadas se conservan por separado.";
        SettingsError.Text = "";
    }
    private void CloseSettings_Click(object sender, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Collapsed;
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Elegí la carpeta para Project Riftwalker" };
        if (dialog.ShowDialog(this) == true) InstallPath.Text = Path.Combine(dialog.FolderName, "Project Riftwalker");
    }
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        string tamano = TamanoChica.IsChecked == true ? "chica" : TamanoGrande.IsChecked == true ? "grande" : "normal";
        // The preview tries the size without saving anything.
        if (App.Preview) { SettingsPanel.Visibility = Visibility.Collapsed; AplicarTamano(tamano, true); return; }
        try
        {
            bool canal = settings.Experimental != (Experimental.IsChecked == true);
            settings = new(Experimental.IsChecked == true, tamano);
            if (setup)
            {
                root = DesktopInstall.Install(InstallPath.Text);
                Storage.Write(StatePath, settings);
                Process.Start(new ProcessStartInfo(Path.Combine(root, "Riftwalker-Launcher.exe")) { WorkingDirectory = root, UseShellExecute = true });
                Close(); return;
            }
            Storage.Write(StatePath, settings); SettingsPanel.Visibility = Visibility.Collapsed;
            AplicarTamano(tamano, true);
            if (canal) await RefreshAsync(); // only a channel change needs to ask GitHub again
        }
        catch (Exception ex) { SettingsError.Text = "No se pudo guardar: " + ex.Message; }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!busy) return;
        e.Cancel = true; closeAfterCancel = true; operation?.Cancel();
    }
    private void Log(Exception ex)
    {
        try { if (installedLauncher) File.AppendAllText(Storage.Child(root, ".riftwalker/launcher.log"), $"{DateTimeOffset.Now:u} {ex}\n"); } catch (IOException) { }
    }
}
