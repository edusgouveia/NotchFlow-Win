namespace NotchFlow.Core.Notifications;

/// <summary>Aplicativo cujas notificações a ilha acompanha.</summary>
public sealed record NotificationSource(string Match, string DisplayName, uint Accent);

/// <summary>
/// Define quais notificações chegam à ilha.
///
/// A <c>UserNotificationListener</c> entrega as notificações de <b>todos</b> os aplicativos.
/// Esta lista é o filtro que impede o resto de sequer virar um objeto em memória: banco,
/// e-mail pessoal e mensageiros são descartados na leitura, não depois.
///
/// Acrescentar uma fonte é acrescentar uma linha aqui.
/// </summary>
public static class NotificationSourceCatalog
{
    /// <summary>Roxo da marca do Teams.</summary>
    private const uint TeamsPurple = 0xFF6264A7;

    /// <summary>
    /// A comparação é por "contém", em minúsculas, contra o AppUserModelId e o nome exibido.
    /// O Teams mudou de identificador entre a versão clássica e a nova, e casar pelos dois
    /// campos cobre as duas sem precisar listar cada AUMID.
    /// </summary>
    private static readonly NotificationSource[] Sources =
    [
        new("teams", "Teams", TeamsPurple)
    ];

    public static IReadOnlyList<NotificationSource> All => Sources;

    /// <summary>Devolve a fonte correspondente, ou nulo quando a notificação deve ser ignorada.</summary>
    public static NotificationSource? Match(string? appId, string? appDisplayName)
    {
        var id = (appId ?? string.Empty).ToLowerInvariant();
        var name = (appDisplayName ?? string.Empty).ToLowerInvariant();

        if (id.Length == 0 && name.Length == 0)
        {
            return null;
        }

        return Sources.FirstOrDefault(source =>
            id.Contains(source.Match, StringComparison.Ordinal)
            || name.Contains(source.Match, StringComparison.Ordinal));
    }
}
