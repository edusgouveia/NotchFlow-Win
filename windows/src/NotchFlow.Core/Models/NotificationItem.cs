namespace NotchFlow.Core.Models;

/// <summary>
/// Uma notificação lida da Central de Ações do Windows.
///
/// É um valor puro e imutável, como <see cref="PlaybackSnapshot"/>: o painel sempre recebe
/// uma lista nova em vez de observar objetos que mudam por baixo.
/// </summary>
public sealed record NotificationItem
{
    /// <summary>Identificador atribuído pelo Windows. Serve para saber o que já foi visto.</summary>
    public required uint Id { get; init; }

    /// <summary>AppUserModelId da origem, usado para filtrar.</summary>
    public required string AppId { get; init; }

    /// <summary>Rótulo da fonte, como "Teams".</summary>
    public required string Source { get; init; }

    /// <summary>Cor da marca, no formato AARRGGBB.</summary>
    public required uint Accent { get; init; }

    /// <summary>Quem mandou: pessoa, grupo ou canal. É a primeira linha do toast.</summary>
    public required string Title { get; init; }

    /// <summary>Prévia da mensagem. É a segunda linha do toast.</summary>
    public required string Body { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    /// <summary>
    /// Descrição curta do momento, no estilo "agora", "há 5 min", "14:32".
    /// Notificações antigas ganham a hora porque "há 3 h" não ajuda a se localizar.
    /// </summary>
    public string RelativeTime(DateTimeOffset now)
    {
        var elapsed = now - ReceivedAt;

        if (elapsed < TimeSpan.Zero || elapsed < TimeSpan.FromMinutes(1))
        {
            return "agora";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"há {(int)elapsed.TotalMinutes} min";
        }

        return ReceivedAt.ToLocalTime().ToString("HH:mm");
    }
}
