using NotchFlow.Core.Models;
using Xunit;

namespace NotchFlow.Core.Tests;

/// <summary>Porte dos PlaybackSnapshotTests do NotchFlow para macOS.</summary>
public class PlaybackSnapshotTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static PlaybackSnapshot Snapshot(
        PlaybackStatus status = PlaybackStatus.Playing,
        double positionSeconds = 30,
        double durationSeconds = 180,
        string title = "Aquarela do Brasil") => new()
        {
            SessionId = "teste",
            SourceLabel = "Teste",
            Brand = PlaybackBrand.Neutral,
            Status = status,
            Title = title,
            Artist = "Ary Barroso",
            Album = "Álbum",
            Duration = TimeSpan.FromSeconds(durationSeconds),
            Position = TimeSpan.FromSeconds(positionSeconds),
            CapturedAt = Base
        };

    [Fact]
    public void TocandoAPosicaoAvancaComORelogio()
    {
        var snapshot = Snapshot(positionSeconds: 30);

        var position = snapshot.EffectivePosition(Base.AddSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(40), position);
    }

    [Fact]
    public void EmPausaAPosicaoNaoAvanca()
    {
        var snapshot = Snapshot(PlaybackStatus.Paused, positionSeconds: 30);

        var position = snapshot.EffectivePosition(Base.AddSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(30), position);
    }

    [Fact]
    public void APosicaoProjetadaNaoPassaDaDuracao()
    {
        var snapshot = Snapshot(positionSeconds: 170, durationSeconds: 180);

        // Passaram 60 segundos, mas faltavam só 10 para o fim.
        var position = snapshot.EffectivePosition(Base.AddSeconds(60));

        Assert.Equal(TimeSpan.FromSeconds(180), position);
    }

    [Fact]
    public void UmRelogioAtrasadoNaoProduzPosicaoNegativa()
    {
        var snapshot = Snapshot(positionSeconds: 30);

        var position = snapshot.EffectivePosition(Base.AddSeconds(-120));

        Assert.Equal(TimeSpan.FromSeconds(30), position);
    }

    [Fact]
    public void SemDuracaoOProgressoEZero()
    {
        var snapshot = Snapshot(durationSeconds: 0);

        Assert.Equal(0, snapshot.Progress(Base));
    }

    [Fact]
    public void OProgressoEAFracaoDaDuracao()
    {
        var snapshot = Snapshot(positionSeconds: 45, durationSeconds: 180);

        Assert.Equal(0.25, snapshot.Progress(Base), precision: 6);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Aquarela", true)]
    public void SoContaComoFaixaQuandoHaTitulo(string title, bool esperado)
    {
        var snapshot = Snapshot(title: title);

        Assert.Equal(esperado, snapshot.HasTrack);
    }

    [Fact]
    public void SomenteOEstadoTocandoContaComoReproducao()
    {
        Assert.True(Snapshot(PlaybackStatus.Playing).IsPlaying);
        Assert.False(Snapshot(PlaybackStatus.Paused).IsPlaying);
        Assert.False(Snapshot(PlaybackStatus.Stopped).IsPlaying);
    }
}
