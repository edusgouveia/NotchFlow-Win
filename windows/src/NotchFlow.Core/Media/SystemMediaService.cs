using NotchFlow.Core.Logging;
using NotchFlow.Core.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NotchFlow.Core.Media;

/// <summary>
/// Lê e controla a mídia do sistema pelo SMTC (<c>GlobalSystemMediaTransportControlsSessionManager</c>).
///
/// Substitui, sozinho, toda a pilha de Apple Events do NotchFlow para macOS: AppleMusicService,
/// SpotifyService, BrowserMediaService, AppleScriptRunner, BrowserAppleScript, BrowserProbe e
/// BrowserScript. Em troca ganha-se atualização por evento em vez de polling de 5 segundos, e
/// desaparece a exigência de o usuário liberar permissões de automação e JavaScript.
/// </summary>
public sealed class SystemMediaService : ISystemMediaService
{
    /// <summary>Teto para a capa, herdado da ArtworkDownloadPolicy do projeto Swift. Aqui o dado
    /// vem do próprio sistema, mas um limite ainda protege contra um player devolver algo absurdo.</summary>
    private const uint MaxArtworkBytes = 8 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly List<GlobalSystemMediaTransportControlsSession> _tracked = [];

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private bool _disposed;

    public event EventHandler? SessionsChanged;

    public async Task StartAsync()
    {
        if (_manager is not null)
        {
            return;
        }

        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += OnSessionsChanged;
        _manager.CurrentSessionChanged += OnSessionsChanged;
        AttachSessionHandlers();

        AppLog.Media.Info("SMTC iniciado");
    }

    public async Task<IReadOnlyList<PlaybackSnapshot>> ReadSnapshotsAsync()
    {
        var manager = _manager;
        if (manager is null)
        {
            return [];
        }

        var sessions = manager.GetSessions();
        var result = new List<PlaybackSnapshot>(sessions.Count);

        // Enumerar o IReadOnlyList projetado pelo CsWinRT lança InvalidCastException.
        // O acesso por índice é o caminho suportado.
        for (var i = 0; i < sessions.Count; i++)
        {
            var snapshot = await TryReadAsync(sessions[i]);
            if (snapshot is not null)
            {
                result.Add(snapshot);
            }
        }

        return result;
    }

    private async Task<PlaybackSnapshot?> TryReadAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var title = properties.Title ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var artist = properties.Artist ?? string.Empty;
            var album = properties.AlbumTitle ?? string.Empty;
            var sessionId = session.SourceAppUserModelId ?? string.Empty;

            var playback = session.GetPlaybackInfo();
            var controls = playback.Controls;
            var timeline = session.GetTimelineProperties();

            var duration = timeline.EndTime - timeline.StartTime;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            // LastUpdatedTime diz exatamente quando a posição foi medida, o que deixa a
            // projeção da barra de progresso mais precisa do que usar a hora atual.
            var capturedAt = timeline.LastUpdatedTime == default
                ? DateTimeOffset.Now
                : timeline.LastUpdatedTime;

            var (displayName, brand) = MediaSourceCatalog.Describe(sessionId, title, artist, album);

            return new PlaybackSnapshot
            {
                SessionId = sessionId,
                SourceLabel = displayName,
                Brand = brand,
                Status = MapStatus(playback.PlaybackStatus),
                Title = title,
                Artist = artist,
                Album = album,
                ArtworkData = await TryReadArtworkAsync(properties.Thumbnail),
                Duration = duration,
                Position = timeline.Position < TimeSpan.Zero ? TimeSpan.Zero : timeline.Position,
                CapturedAt = capturedAt,
                SupportsPlayPause = controls.IsPlayEnabled || controls.IsPauseEnabled
                    || controls.IsPlayPauseToggleEnabled,
                SupportsTrackSkip = controls.IsNextEnabled || controls.IsPreviousEnabled,
                SupportsSeek = controls.IsPlaybackPositionEnabled && duration > TimeSpan.Zero
            };
        }
        catch (Exception ex)
        {
            // Uma sessão pode desaparecer entre a listagem e a leitura.
            AppLog.Media.Error($"Falha ao ler sessão de mídia: {ex.GetType().Name}");
            return null;
        }
    }

    private static async Task<byte[]?> TryReadArtworkAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > MaxArtworkBytes)
            {
                return null;
            }

            var size = (uint)stream.Size;
            using var reader = new DataReader(stream);
            await reader.LoadAsync(size);

            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            AppLog.Media.Error($"Falha ao ler capa: {ex.GetType().Name}");
            return null;
        }
    }

    public async Task<bool> PerformAsync(string sessionId, PlayerCommand command, PlaybackSnapshot current)
    {
        var session = FindSession(sessionId);
        if (session is null)
        {
            return false;
        }

        try
        {
            switch (command.Kind)
            {
                case PlayerCommandKind.TogglePlayPause:
                    return await session.TryTogglePlayPauseAsync();

                case PlayerCommandKind.NextTrack:
                    return await session.TrySkipNextAsync();

                case PlayerCommandKind.PreviousTrack:
                    return await session.TrySkipPreviousAsync();

                case PlayerCommandKind.Skip:
                {
                    var target = Clamp(current.EffectivePosition(DateTimeOffset.Now) + command.Value, current.Duration);
                    return await session.TryChangePlaybackPositionAsync(target.Ticks);
                }

                case PlayerCommandKind.Seek:
                {
                    var target = Clamp(command.Value, current.Duration);
                    return await session.TryChangePlaybackPositionAsync(target.Ticks);
                }

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            AppLog.Media.Error($"Comando {command.Kind} falhou: {ex.GetType().Name}");
            return false;
        }
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan duration)
    {
        if (value < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return duration > TimeSpan.Zero && value > duration ? duration : value;
    }

    private GlobalSystemMediaTransportControlsSession? FindSession(string sessionId)
    {
        var manager = _manager;
        if (manager is null)
        {
            return null;
        }

        var sessions = manager.GetSessions();
        for (var i = 0; i < sessions.Count; i++)
        {
            if (sessions[i].SourceAppUserModelId == sessionId)
            {
                return sessions[i];
            }
        }

        return null;
    }

    private void OnSessionsChanged(object? sender, object args)
    {
        AttachSessionHandlers();
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Assina os eventos de cada sessão viva. Sem isso só saberíamos que a lista de sessões
    /// mudou, não que a faixa ou o estado de reprodução mudaram dentro de uma sessão.
    /// </summary>
    private void AttachSessionHandlers()
    {
        var manager = _manager;
        if (manager is null)
        {
            return;
        }

        lock (_gate)
        {
            foreach (var previous in _tracked)
            {
                previous.MediaPropertiesChanged -= OnSessionDetailChanged;
                previous.PlaybackInfoChanged -= OnSessionDetailChanged;
                previous.TimelinePropertiesChanged -= OnSessionDetailChanged;
            }

            _tracked.Clear();

            var sessions = manager.GetSessions();
            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                session.MediaPropertiesChanged += OnSessionDetailChanged;
                session.PlaybackInfoChanged += OnSessionDetailChanged;
                session.TimelinePropertiesChanged += OnSessionDetailChanged;
                _tracked.Add(session);
            }
        }
    }

    private void OnSessionDetailChanged(object? sender, object args)
        => SessionsChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_gate)
        {
            foreach (var session in _tracked)
            {
                session.MediaPropertiesChanged -= OnSessionDetailChanged;
                session.PlaybackInfoChanged -= OnSessionDetailChanged;
                session.TimelinePropertiesChanged -= OnSessionDetailChanged;
            }

            _tracked.Clear();
        }

        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager.CurrentSessionChanged -= OnSessionsChanged;
            _manager = null;
        }
    }

    private static PlaybackStatus MapStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
        => status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
            _ => PlaybackStatus.Stopped
        };
}
