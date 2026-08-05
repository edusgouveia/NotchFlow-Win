using NotchFlow.Core.Media;
using NotchFlow.Core.Models;
using Xunit;

namespace NotchFlow.Core.Tests;

/// <summary>Porte dos PlaybackSelectorTests do NotchFlow para macOS.</summary>
public class PlaybackSelectorTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static PlaybackSnapshot Snapshot(
        string sessionId,
        PlaybackStatus status,
        int capturedOffsetSeconds = 0,
        string title = "Faixa") => new()
        {
            SessionId = sessionId,
            SourceLabel = sessionId,
            Brand = PlaybackBrand.Neutral,
            Status = status,
            Title = title,
            Artist = string.Empty,
            Album = string.Empty,
            CapturedAt = Base.AddSeconds(capturedOffsetSeconds)
        };

    [Fact]
    public void SemSessoesNaoHaEscolha()
    {
        Assert.Null(PlaybackSelector.Select([], preferredSessionId: null));
    }

    [Fact]
    public void SessoesSemTituloSaoIgnoradas()
    {
        var snapshots = new[] { Snapshot("a", PlaybackStatus.Playing, title: "") };

        Assert.Null(PlaybackSelector.Select(snapshots, preferredSessionId: null));
    }

    [Fact]
    public void QuemEstaTocandoGanhaDeQuemEstaEmPausa()
    {
        var snapshots = new[]
        {
            Snapshot("pausada", PlaybackStatus.Paused, capturedOffsetSeconds: 100),
            Snapshot("tocando", PlaybackStatus.Playing)
        };

        var escolhida = PlaybackSelector.Select(snapshots, preferredSessionId: null);

        Assert.Equal("tocando", escolhida?.SessionId);
    }

    [Fact]
    public void EntreDuasTocandoAPreferidaGanha()
    {
        var snapshots = new[]
        {
            Snapshot("outra", PlaybackStatus.Playing, capturedOffsetSeconds: 100),
            Snapshot("preferida", PlaybackStatus.Playing)
        };

        var escolhida = PlaybackSelector.Select(snapshots, preferredSessionId: "preferida");

        Assert.Equal("preferida", escolhida?.SessionId);
    }

    [Fact]
    public void SemPreferidaTocandoVenceALeituraMaisRecente()
    {
        var snapshots = new[]
        {
            Snapshot("antiga", PlaybackStatus.Playing),
            Snapshot("recente", PlaybackStatus.Playing, capturedOffsetSeconds: 50)
        };

        var escolhida = PlaybackSelector.Select(snapshots, preferredSessionId: "inexistente");

        Assert.Equal("recente", escolhida?.SessionId);
    }

    [Fact]
    public void ComTodasEmPausaAPreferidaAindaGanha()
    {
        var snapshots = new[]
        {
            Snapshot("outra", PlaybackStatus.Paused, capturedOffsetSeconds: 100),
            Snapshot("preferida", PlaybackStatus.Paused)
        };

        var escolhida = PlaybackSelector.Select(snapshots, preferredSessionId: "preferida");

        Assert.Equal("preferida", escolhida?.SessionId);
    }

    [Fact]
    public void ComTodasEmPausaESemPreferidaVenceAMaisRecente()
    {
        var snapshots = new[]
        {
            Snapshot("antiga", PlaybackStatus.Paused),
            Snapshot("recente", PlaybackStatus.Paused, capturedOffsetSeconds: 50)
        };

        var escolhida = PlaybackSelector.Select(snapshots, preferredSessionId: null);

        Assert.Equal("recente", escolhida?.SessionId);
    }
}
