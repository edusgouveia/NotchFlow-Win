using NotchFlow.Core.Models;
using Xunit;

namespace NotchFlow.Core.Tests;

public class NotificationItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 14, 30, 0, TimeSpan.Zero);

    private static NotificationItem Item(int minutesAgo = 0, string title = "Ualas Caixeta") => new()
    {
        Id = 1,
        AppId = "MSTeams_8wekyb3d8bbwe!MSTeams",
        Source = "Teams",
        Accent = 0xFF6264A7,
        Title = title,
        Body = "Bom dia, conseguimos falar sobre os contratos?",
        ReceivedAt = Now.AddMinutes(-minutesAgo)
    };

    [Fact]
    public void ChegadaRecenteApareceComoAgora()
    {
        Assert.Equal("agora", Item(minutesAgo: 0).RelativeTime(Now));
    }

    [Fact]
    public void AteUmaHoraMostraOsMinutos()
    {
        Assert.Equal("há 5 min", Item(minutesAgo: 5).RelativeTime(Now));
        Assert.Equal("há 59 min", Item(minutesAgo: 59).RelativeTime(Now));
    }

    [Fact]
    public void AcimaDeUmaHoraMostraAHora()
    {
        // "há 3 h" não ajuda a se localizar; a hora do relógio ajuda.
        var texto = Item(minutesAgo: 180).RelativeTime(Now);

        Assert.Contains(":", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("há", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void UmRelogioAtrasadoNaoProduzTempoNegativo()
    {
        Assert.Equal("agora", Item(minutesAgo: -10).RelativeTime(Now));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Ualas Caixeta", true)]
    public void SoContaComoTituloQuandoHaTexto(string title, bool esperado)
    {
        Assert.Equal(esperado, Item(title: title).HasTitle);
    }
}
