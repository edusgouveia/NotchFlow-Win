using NotchFlow.Core.Models;

namespace NotchFlow.Core.Media;

/// <summary>
/// Escolhe qual sessão aparece na ilha quando há mais de um player ativo.
/// Porte direto do PlaybackSelector do NotchFlow para macOS: quem está tocando ganha de quem
/// está em pausa, e a sessão preferida ganha o desempate.
/// </summary>
public static class PlaybackSelector
{
    public static PlaybackSnapshot? Select(
        IReadOnlyCollection<PlaybackSnapshot> snapshots,
        string? preferredSessionId)
    {
        var playable = snapshots.Where(s => s.HasTrack).ToList();
        if (playable.Count == 0)
        {
            return null;
        }

        var playing = playable.Where(s => s.IsPlaying).ToList();

        if (preferredSessionId is not null)
        {
            var preferredPlaying = playing.FirstOrDefault(s => s.SessionId == preferredSessionId);
            if (preferredPlaying is not null)
            {
                return preferredPlaying;
            }
        }

        if (playing.Count > 0)
        {
            return MostRecent(playing);
        }

        if (preferredSessionId is not null)
        {
            var preferred = playable.FirstOrDefault(s => s.SessionId == preferredSessionId);
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return MostRecent(playable);
    }

    private static PlaybackSnapshot MostRecent(List<PlaybackSnapshot> candidates)
    {
        var best = candidates[0];
        foreach (var candidate in candidates)
        {
            if (candidate.CapturedAt > best.CapturedAt)
            {
                best = candidate;
            }
        }

        return best;
    }
}
