using NotchFlow.Core.Logging;
using NotchFlow.Core.Models;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace NotchFlow.Core.Notifications;

/// <summary>
/// Lê a Central de Ações do Windows pela <c>UserNotificationListener</c>.
///
/// A API funciona sem identidade de pacote, o que mantém o NotchFlow distribuível como um
/// .exe solto, sem MSIX nem assinatura. Exige do usuário uma autorização única em
/// Configurações, Privacidade, Notificações.
///
/// A API entrega as notificações de todos os aplicativos. O filtro do
/// <see cref="NotificationSourceCatalog"/> é aplicado <b>durante</b> a leitura: o que não
/// for de uma fonte acompanhada é descartado antes de o texto ser sequer extraído.
/// </summary>
public sealed class SystemNotificationService : INotificationService
{
    /// <summary>Teto de itens processados por leitura. A Central pode acumular dezenas, e a
    /// ilha mostra só os mais recentes.</summary>
    private const int MaxItems = 40;

    private UserNotificationListener? _listener;
    private bool _subscribed;
    private bool _disposed;

    public event EventHandler? NotificationsChanged;

    public NotificationAccess Access { get; private set; } = NotificationAccess.Unknown;

    public async Task<NotificationAccess> RequestAccessAsync()
    {
        try
        {
            _listener = UserNotificationListener.Current;
        }
        catch (Exception ex)
        {
            AppLog.Notifications.Error($"UserNotificationListener indisponível: {ex.GetType().Name}");
            Access = NotificationAccess.Unavailable;
            return Access;
        }

        try
        {
            var status = await _listener.RequestAccessAsync();
            Access = status switch
            {
                UserNotificationListenerAccessStatus.Allowed => NotificationAccess.Allowed,
                UserNotificationListenerAccessStatus.Denied => NotificationAccess.Denied,
                _ => NotificationAccess.Unknown
            };
        }
        catch (Exception ex)
        {
            AppLog.Notifications.Error($"Falha ao pedir acesso às notificações: {ex.GetType().Name}");
            Access = NotificationAccess.Unavailable;
            return Access;
        }

        if (Access == NotificationAccess.Allowed && !_subscribed)
        {
            try
            {
                _listener.NotificationChanged += OnNotificationChanged;
                _subscribed = true;
            }
            catch (Exception ex)
            {
                // Sem o evento o coordenador ainda funciona pela consulta periódica.
                AppLog.Notifications.Error($"Evento de notificações indisponível: {ex.GetType().Name}");
            }
        }

        AppLog.Notifications.Info($"Acesso às notificações: {Access}");
        return Access;
    }

    public async Task<IReadOnlyList<NotificationItem>> ReadAsync()
    {
        var listener = _listener;
        if (listener is null || Access != NotificationAccess.Allowed)
        {
            return [];
        }

        IReadOnlyList<UserNotification> notifications;
        try
        {
            notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
        }
        catch (Exception ex)
        {
            AppLog.Notifications.Error($"Falha ao ler a Central de Ações: {ex.GetType().Name}");
            return [];
        }

        var items = new List<NotificationItem>();

        // Enumerar o IReadOnlyList projetado pelo WinRT lança InvalidCastException.
        var count = Math.Min(notifications.Count, MaxItems);
        for (var i = 0; i < count; i++)
        {
            var item = TryConvert(notifications[i]);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        items.Sort((a, b) => b.ReceivedAt.CompareTo(a.ReceivedAt));
        return items;
    }

    private static NotificationItem? TryConvert(UserNotification notification)
    {
        try
        {
            var appId = notification.AppInfo?.AppUserModelId;
            var appName = notification.AppInfo?.DisplayInfo?.DisplayName;

            // O filtro vem antes de qualquer leitura de conteúdo.
            var source = NotificationSourceCatalog.Match(appId, appName);
            if (source is null)
            {
                return null;
            }

            var binding = notification.Notification?.Visual?.GetBinding(
                KnownNotificationBindings.ToastGeneric);
            if (binding is null)
            {
                return null;
            }

            var texts = binding.GetTextElements();
            var title = texts.Count > 0 ? texts[0]?.Text ?? string.Empty : string.Empty;

            // O corpo costuma ser a segunda linha, mas alguns aplicativos quebram a
            // mensagem em várias. Juntar o restante evita mostrar só um pedaço.
            var body = string.Empty;
            if (texts.Count > 1)
            {
                var parts = new List<string>();
                for (var i = 1; i < texts.Count; i++)
                {
                    var text = texts[i]?.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(text.Trim());
                    }
                }

                body = string.Join(" · ", parts);
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return new NotificationItem
            {
                Id = notification.Id,
                AppId = appId ?? string.Empty,
                Source = source.DisplayName,
                Accent = source.Accent,
                Title = Collapse(title),
                Body = Collapse(body),
                ReceivedAt = notification.CreationTime
            };
        }
        catch (Exception ex)
        {
            // Uma notificação pode ser removida entre a listagem e a leitura.
            AppLog.Notifications.Error($"Notificação ilegível: {ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>Junta quebras de linha em espaços: a ilha tem uma linha por campo.</summary>
    private static string Collapse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var collapsed = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();

        while (collapsed.Contains("  ", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("  ", " ", StringComparison.Ordinal);
        }

        return collapsed;
    }

    private void OnNotificationChanged(
        UserNotificationListener sender, UserNotificationChangedEventArgs args)
        => NotificationsChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_listener is not null && _subscribed)
        {
            try
            {
                _listener.NotificationChanged -= OnNotificationChanged;
            }
            catch (Exception)
            {
                // Desinscrever pode falhar se o listener já foi derrubado pelo sistema.
            }
        }

        _listener = null;
    }
}
