using NotchFlow.Core.Models;

namespace NotchFlow.Core.Notifications;

public enum NotificationAccess
{
    /// <summary>Ainda não foi pedido.</summary>
    Unknown,

    Allowed,

    /// <summary>O usuário negou, ou a política do sistema não permite.</summary>
    Denied,

    /// <summary>A API não existe nesta versão do Windows.</summary>
    Unavailable
}

/// <summary>
/// Fonte de notificações do sistema. Existe como interface para o coordenador poder ser
/// testado sem a Central de Ações real.
/// </summary>
public interface INotificationService : IDisposable
{
    /// <summary>Disparado quando a Central de Ações muda. Pode chegar em thread de background.</summary>
    event EventHandler? NotificationsChanged;

    NotificationAccess Access { get; }

    Task<NotificationAccess> RequestAccessAsync();

    /// <summary>Notificações das fontes acompanhadas, das mais recentes para as mais antigas.
    /// O que não estiver no catálogo é descartado antes de virar um item.</summary>
    Task<IReadOnlyList<NotificationItem>> ReadAsync();
}
