using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NotchFlow.App.Features.LaunchAtLogin;
using NotchFlow.App.Features.Notch;
using NotchFlow.App.Features.Tray;
using NotchFlow.Core.Logging;
using NotchFlow.Core.Media;
using NotchFlow.Core.Notifications;
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
    /// <summary>Quanto tempo a ilha fica aberta quando abre sozinha por uma notificação.</summary>
    private static readonly TimeSpan NotificationFlashDuration = TimeSpan.FromSeconds(5);

    private AppSettings? _settings;
    private SystemMediaService? _mediaService;
    private MediaCoordinator? _media;
    private SystemNotificationService? _notificationService;
    private NotificationCoordinator? _notifications;
    private NotchWindowController? _controller;
    private TrayIconService? _tray;
    private LaunchAtLoginService? _launchAtLogin;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        void Dispatch(Action action) => queue.TryEnqueue(() => action());

        _settings = new AppSettings();
        _launchAtLogin = new LaunchAtLoginService();

        // Os eventos do SMTC e da Central de Ações chegam em threads de background;
        // o estado é consumido por XAML.
        _mediaService = new SystemMediaService();
        _media = new MediaCoordinator(_mediaService, Dispatch);

        _notificationService = new SystemNotificationService();
        _notifications = new NotificationCoordinator(_notificationService, _settings, Dispatch);
        _notifications.NotificationArrived += OnNotificationArrived;

        _controller = new NotchWindowController(_media, _notifications, _settings, queue);
        _controller.Start();

        _settings.Changed += (_, _) => _controller?.ApplySettings();
        _settings.NotificationsEnabledChanged += OnNotificationsEnabledChanged;

        _tray = new TrayIconService(BuildTrayMenu, () => _controller?.ToggleUnderPointer());
        _tray.SetVisible(_settings.ShowTrayIcon);
        _settings.TrayIconVisibilityChanged += (_, visible) => _tray?.SetVisible(visible);

        _ = _media.StartAsync();
        _ = _notifications.StartAsync();

        AppLog.Lifecycle.Info("NotchFlow iniciado");
    }

    /// <summary>
    /// A ilha só abre sozinha se o usuário pediu. Por padrão a notificação aparece como um
    /// indicador discreto, porque o Windows já mostra um toast para o mesmo evento e abrir
    /// a ilha por cima duplicaria o aviso.
    /// </summary>
    private void OnNotificationArrived(object? sender, Core.Models.NotificationItem item)
    {
        if (_settings?.AutoExpandOnNotification == true)
        {
            _controller?.FlashForNotification(NotificationFlashDuration);
        }
    }

    private void OnNotificationsEnabledChanged(object? sender, bool enabled)
    {
        _controller?.ApplySettings();

        if (!enabled)
        {
            _notifications?.Stop();
            return;
        }

        // Ligar dispara o pedido de permissão do Windows na primeira vez.
        _ = StartNotificationsAsync();
    }

    private async Task StartNotificationsAsync()
    {
        if (_notifications is null)
        {
            return;
        }

        await _notifications.StartAsync();

        if (_notifications.Access == NotificationAccess.Denied)
        {
            AppLog.Notifications.Info("Acesso negado; abrindo as configurações do Windows");
            OpenNotificationSettings();
        }
    }

    /// <summary>Leva direto à página onde o acesso é concedido, em vez de deixar o usuário
    /// procurar. Sem isso o recurso simplesmente não funcionaria, sem explicação.</summary>
    private static void OpenNotificationSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:privacy-notifications",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Notifications.Error($"Falha ao abrir as configurações: {ex.GetType().Name}");
        }
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

        var items = new List<TrayMenuItem>
        {
            new("Abrir a ilha", () => _controller?.ToggleUnderPointer()),
            new("Atualizar mídia", () => _ = _media?.RefreshAsync()),
            TrayMenuItem.Separator(),
            new("Mostrar em todas as telas",
                () => settings.ShowOnAllDisplays = !settings.ShowOnAllDisplays)
            {
                IsChecked = settings.ShowOnAllDisplays
            },
            new("Reduzir nas telas secundárias",
                () => settings.MinimizeOnSecondaryDisplays = !settings.MinimizeOnSecondaryDisplays)
            {
                IsChecked = settings.MinimizeOnSecondaryDisplays
            },
            TrayMenuItem.Separator(),
            new("Notificações na ilha",
                () => settings.NotificationsEnabled = !settings.NotificationsEnabled)
            {
                IsChecked = settings.NotificationsEnabled
            }
        };

        // A abertura automática só faz sentido com as notificações ligadas.
        if (settings.NotificationsEnabled)
        {
            items.Add(new TrayMenuItem(
                "    Abrir a ilha ao receber",
                () => settings.AutoExpandOnNotification = !settings.AutoExpandOnNotification)
            {
                IsChecked = settings.AutoExpandOnNotification
            });

            if (_notifications?.Access is NotificationAccess.Denied or NotificationAccess.Unavailable)
            {
                items.Add(new TrayMenuItem("    Permitir acesso às notificações…", OpenNotificationSettings));
            }
        }

        items.Add(TrayMenuItem.Separator());
        items.Add(new TrayMenuItem("Abrir com o Windows", () => launchAtLogin.Toggle())
        {
            IsChecked = launchAtLogin.IsEnabled
        });
        items.Add(TrayMenuItem.Separator());
        items.Add(new TrayMenuItem("Encerrar o NotchFlow", Shutdown));

        return items;
    }

    internal void Shutdown()
    {
        _tray?.Dispose();
        _controller?.Dispose();
        _notifications?.Dispose();
        _media?.Dispose();
        AppLog.Lifecycle.Info("NotchFlow encerrado");
        Exit();
    }
}
