namespace NotchFlow.Core.Models;

/// <summary>Marca visual da fonte, usada para colorir o indicador recolhido.</summary>
public enum PlaybackBrand
{
    Spotify,
    YouTube,
    AppleMusic,
    Neutral
}

public enum PlaybackStatus
{
    Playing,
    Paused,
    Stopped
}

/// <summary>
/// Estado de reprodução em um instante. É imutável: o painel sempre recebe um valor novo
/// em vez de observar um objeto que muda por baixo.
/// </summary>
public sealed record PlaybackSnapshot
{
    /// <summary>Identificador da sessão no SMTC (AppUserModelId), usado para diferenciar players.</summary>
    public required string SessionId { get; init; }

    public required string SourceLabel { get; init; }
    public required PlaybackBrand Brand { get; init; }
    public required PlaybackStatus Status { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public required string Album { get; init; }

    /// <summary>Capa já decodificada. Vem do stream do SMTC, então não há download HTTP.</summary>
    public byte[]? ArtworkData { get; init; }

    public TimeSpan Duration { get; init; }
    public TimeSpan Position { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }

    public bool SupportsTrackSkip { get; init; } = true;
    public bool SupportsSeek { get; init; } = true;
    public bool SupportsPlayPause { get; init; } = true;

    public bool IsPlaying => Status == PlaybackStatus.Playing;

    public bool HasTrack => !string.IsNullOrWhiteSpace(Title);

    /// <summary>
    /// Posição projetada para o instante pedido. O SMTC só atualiza a timeline em eventos,
    /// então a barra de progresso avança sozinha entre uma leitura e a próxima.
    /// </summary>
    public TimeSpan EffectivePosition(DateTimeOffset at)
    {
        var elapsed = IsPlaying ? at - CapturedAt : TimeSpan.Zero;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var projected = Position + elapsed;
        var ceiling = Duration > TimeSpan.Zero ? Duration : TimeSpan.Zero;

        if (projected < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return projected > ceiling ? ceiling : projected;
    }

    /// <summary>Progresso de 0 a 1, ou 0 quando a duração é desconhecida.</summary>
    public double Progress(DateTimeOffset at)
    {
        if (Duration <= TimeSpan.Zero)
        {
            return 0;
        }

        return EffectivePosition(at).TotalSeconds / Duration.TotalSeconds;
    }
}

public enum PlayerCommandKind
{
    TogglePlayPause,
    PreviousTrack,
    NextTrack,
    Skip,
    Seek
}

/// <summary>Comando tipado enviado ao player. Nada de string interpolada em caminho de execução.</summary>
public readonly record struct PlayerCommand(PlayerCommandKind Kind, TimeSpan Value)
{
    public static PlayerCommand TogglePlayPause() => new(PlayerCommandKind.TogglePlayPause, TimeSpan.Zero);

    public static PlayerCommand PreviousTrack() => new(PlayerCommandKind.PreviousTrack, TimeSpan.Zero);

    public static PlayerCommand NextTrack() => new(PlayerCommandKind.NextTrack, TimeSpan.Zero);

    /// <summary>Avanço ou retorno relativo, usado pelos botões de 15 segundos.</summary>
    public static PlayerCommand Skip(TimeSpan offset) => new(PlayerCommandKind.Skip, offset);

    /// <summary>Posição absoluta, usada quando o usuário arrasta a barra de progresso.</summary>
    public static PlayerCommand Seek(TimeSpan position) => new(PlayerCommandKind.Seek, position);
}
