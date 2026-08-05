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
    private const uint TeamsPurple = 0xFF6264A7;
    private const uint OutlookBlue = 0xFF0F6CBD;
    private const uint WhatsAppGreen = 0xFF25D366;

    /// <summary>
    /// A comparação é por "contém", em minúsculas, contra o AppUserModelId e o nome exibido.
    /// Casar pelos dois campos cobre as variações de cada aplicativo sem listar cada AUMID:
    /// o Outlook aparece como <c>Microsoft.Office.OUTLOOK.EXE.15</c> na versão clássica e
    /// como <c>Microsoft.OutlookForWindows_...</c> na nova.
    ///
    /// O Teams continua aqui, mas fica registrado que na versão desktop atual ele desenha a
    /// própria notificação em uma WebView e nunca a entrega ao Windows, então não há o que
    /// ler. Pelo navegador funciona: o identificador do PWA contém "teams" e casa nesta lista.
    /// </summary>
    private static readonly NotificationSource[] Sources =
    [
        new("teams", "Teams", TeamsPurple),
        new("outlook", "Outlook", OutlookBlue),
        new("whatsapp", "WhatsApp", WhatsAppGreen)
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
