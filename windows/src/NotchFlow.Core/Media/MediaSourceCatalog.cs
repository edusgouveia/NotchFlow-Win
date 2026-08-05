using NotchFlow.Core.Models;

namespace NotchFlow.Core.Media;

/// <summary>
/// Traduz o AppUserModelId de uma sessão do SMTC em nome legível e marca visual.
///
/// O NotchFlow para macOS sabia o host exato da aba do navegador, então distinguia
/// "YouTube Music" de "YouTube". O SMTC entrega o aplicativo, não a aba, então para
/// navegadores a marca é inferida dos metadados e pode cair em <see cref="PlaybackBrand.Neutral"/>.
/// Essa é a única perda de fidelidade em relação à versão do Mac.
/// </summary>
public static class MediaSourceCatalog
{
    private sealed record KnownSource(string Match, string DisplayName, PlaybackBrand Brand, bool IsBrowser);

    /// <summary>Comparação por "contém", em minúsculas, porque o mesmo player aparece com
    /// identificadores diferentes conforme a origem da instalação (Win32, MSIX ou Store).</summary>
    private static readonly KnownSource[] Sources =
    [
        new("spotify", "Spotify", PlaybackBrand.Spotify, IsBrowser: false),
        new("applemusic", "Apple Music", PlaybackBrand.AppleMusic, IsBrowser: false),
        new("itunes", "iTunes", PlaybackBrand.AppleMusic, IsBrowser: false),
        new("zunemusic", "Media Player", PlaybackBrand.Neutral, IsBrowser: false),
        new("microsoft.media.player", "Media Player", PlaybackBrand.Neutral, IsBrowser: false),
        new("vlc", "VLC", PlaybackBrand.Neutral, IsBrowser: false),
        new("foobar2000", "foobar2000", PlaybackBrand.Neutral, IsBrowser: false),
        new("musicbee", "MusicBee", PlaybackBrand.Neutral, IsBrowser: false),
        new("aimp", "AIMP", PlaybackBrand.Neutral, IsBrowser: false),
        new("deezer", "Deezer", PlaybackBrand.Neutral, IsBrowser: false),
        new("tidal", "TIDAL", PlaybackBrand.Neutral, IsBrowser: false),

        new("msedge", "Microsoft Edge", PlaybackBrand.Neutral, IsBrowser: true),
        new("chrome", "Google Chrome", PlaybackBrand.Neutral, IsBrowser: true),
        new("brave", "Brave", PlaybackBrand.Neutral, IsBrowser: true),
        new("vivaldi", "Vivaldi", PlaybackBrand.Neutral, IsBrowser: true),
        new("opera", "Opera", PlaybackBrand.Neutral, IsBrowser: true),
        new("firefox", "Firefox", PlaybackBrand.Neutral, IsBrowser: true),
        // O Firefox se identifica por este GUID em vez do nome do executável.
        new("308046b0af4a39cb", "Firefox", PlaybackBrand.Neutral, IsBrowser: true)
    ];

    /// <summary>Pistas de que a mídia de um navegador vem do YouTube. O YouTube Music preenche
    /// o álbum; o YouTube comum costuma deixá-lo vazio.</summary>
    private static readonly string[] YouTubeHints = ["youtube", "youtu.be", "yt music"];

    public static (string DisplayName, PlaybackBrand Brand) Describe(
        string? sessionId,
        string title,
        string artist,
        string album)
    {
        var id = sessionId ?? string.Empty;
        var normalized = id.ToLowerInvariant();

        var known = Sources.FirstOrDefault(s => normalized.Contains(s.Match, StringComparison.Ordinal));
        if (known is null)
        {
            return (FallbackName(id), PlaybackBrand.Neutral);
        }

        if (!known.IsBrowser)
        {
            return (known.DisplayName, known.Brand);
        }

        // Num navegador o rótulo útil é o serviço, não o navegador em si.
        var haystack = $"{title} {artist} {album}".ToLowerInvariant();
        if (YouTubeHints.Any(hint => haystack.Contains(hint, StringComparison.Ordinal)))
        {
            return ("YouTube", PlaybackBrand.YouTube);
        }

        if (haystack.Contains("spotify", StringComparison.Ordinal))
        {
            return ("Spotify Web", PlaybackBrand.Spotify);
        }

        return (known.DisplayName, PlaybackBrand.Neutral);
    }

    /// <summary>Limpa um AppUserModelId desconhecido para algo apresentável.</summary>
    private static string FallbackName(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return "Reproduzindo";
        }

        // AUMIDs de apps da Store têm a forma "Publisher.Nome_hash!Entrada".
        var value = sessionId;
        var bang = value.IndexOf('!');
        if (bang > 0)
        {
            value = value[..bang];
        }

        var underscore = value.IndexOf('_');
        if (underscore > 0)
        {
            value = value[..underscore];
        }

        var dot = value.LastIndexOf('.');
        if (dot > 0 && dot < value.Length - 1)
        {
            var tail = value[(dot + 1)..];
            // Descarta apenas a extensão de executável, não o último segmento de um namespace.
            value = tail.Equals("exe", StringComparison.OrdinalIgnoreCase) ? value[..dot] : tail;
        }

        return value.Length == 0 ? "Reproduzindo" : value;
    }
}
