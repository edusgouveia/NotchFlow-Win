using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NotchFlow.App.Features.LaunchAtLogin;
using NotchFlow.App.Features.Notch;
using NotchFlow.App.Features.Tray;
using NotchFlow.Core.Logging;
using NotchFlow.Core.Media;
using NotchFlow.Core.Settings;

namespace NotchFlow.App;

/// <summary>
/// Host do aplicativo. Equivale ao AppDelegate da versão macOS: sobe os serviços
/// compartilhados, cria o controlador das ilhas e os desliga na saída.
///
/// O NotchFlow não tem janela principal. Ele vive nas ilhas e no ícone da bandeja.
/// </summary>
public partial class App : Application
{
    private AppSettings? _settings;
    private SystemMediaService? _mediaService;
    private MediaCoordinator? _media;
    private NotchWindowController? _controller;
    private TrayIconService? _tray;
    private LaunchAtLoginService? _launchAtLogin;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var queue = DispatcherQueue.GetForCurrentThread();

        _settings = new AppSettings();
        _launchAtLogin = new LaunchAtLoginService();
        _mediaService = new SystemMediaService();

        // Os eventos do SMTC chegam em threads de background; o estado é consumido por XAML.
        _media = new MediaCoordinator(_mediaService, action => queue.TryEnqueue(() => action()));

        _controller = new NotchWindowController(_media, _settings, queue);
        _controller.Start();

        _settings.Changed += (_, _) => _controller?.ApplySettings();

        _tray = new TrayIconService(BuildTrayMenu, () => _controller?.ToggleUnderPointer());
        _tray.SetVisible(_settings.ShowTrayIcon);
        _settings.TrayIconVisibilityChanged += (_, visible) => _tray?.SetVisible(visible);

        _ = _media.StartAsync();

        AppLog.Lifecycle.Info("NotchFlow iniciado");
    }

    /// <summary>
    /// Reconstruído a cada abertura para as marcas de seleção refletirem o estado atual.
    /// </summary>
    private IReadOnlyList<TrayMenuItem> BuildTrayMenu()
    {
        var settings = _settings;
        var launchAtLogin = _launchAtLogin;

        if (settings is null || launchAtLogin is null)
        {
            return [];
        }

        return
        [
            new TrayMenuItem("Abrir a ilha", () => _controller?.ToggleUnderPointer()),
            new TrayMenuItem("Atualizar mídia", () => _ = _media?.RefreshAsync()),
            TrayMenuItem.Separator(),
            new TrayMenuItem(
                "Mostrar em todas as telas",
                () => settings.ShowOnAllDisplays = !settings.ShowOnAllDisplays)
            {
                IsChecked = settings.ShowOnAllDisplays
            },
            new TrayMenuItem(
                "Reduzir nas telas secundárias",
                () => settings.MinimizeOnSecondaryDisplays = !settings.MinimizeOnSecondaryDisplays)
            {
                IsChecked = settings.MinimizeOnSecondaryDisplays
            },
            new TrayMenuItem("Abrir com o Windows", () => launchAtLogin.Toggle())
            {
                IsChecked = launchAtLogin.IsEnabled
            },
            TrayMenuItem.Separator(),
            new TrayMenuItem("Encerrar o NotchFlow", Shutdown)
        ];
    }

    internal void Shutdown()
    {
        _tray?.Dispose();
        _controller?.Dispose();
        _media?.Dispose();
        AppLog.Lifecycle.Info("NotchFlow encerrado");
        Exit();
    }
}
