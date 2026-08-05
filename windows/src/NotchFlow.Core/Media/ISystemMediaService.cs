using NotchFlow.Core.Models;

namespace NotchFlow.Core.Media;

/// <summary>
/// Fonte de sessões de mídia do sistema. Existe como interface para o coordenador poder
/// ser testado sem o SMTC real.
/// </summary>
public interface ISystemMediaService : IDisposable
{
    /// <summary>Disparado quando qualquer sessão muda: metadados, estado ou timeline.
    /// Pode chegar em thread de background.</summary>
    event EventHandler? SessionsChanged;

    Task StartAsync();

    Task<IReadOnlyList<PlaybackSnapshot>> ReadSnapshotsAsync();

    Task<bool> PerformAsync(string sessionId, PlayerCommand command, PlaybackSnapshot current);
}
