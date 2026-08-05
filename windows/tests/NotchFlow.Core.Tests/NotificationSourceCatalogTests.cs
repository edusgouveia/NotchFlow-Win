using NotchFlow.Core.Notifications;
using Xunit;

namespace NotchFlow.Core.Tests;

/// <summary>
/// O catálogo é o filtro de privacidade do recurso: a API do Windows entrega as notificações
/// de todos os aplicativos, e só o que casa aqui vira um item em memória.
/// </summary>
public class NotificationSourceCatalogTests
{
    [Theory]
    [InlineData("MSTeams_8wekyb3d8bbwe!MSTeams", "Microsoft Teams")]
    [InlineData("com.squirrel.Teams.Teams", "Microsoft Teams")]
    [InlineData("", "Microsoft Teams")]
    [InlineData("MSTeams_8wekyb3d8bbwe!MSTeams", "")]
    public void OTeamsEReconhecidoPeloIdentificadorOuPeloNome(string appId, string appName)
    {
        var source = NotificationSourceCatalog.Match(appId, appName);

        Assert.NotNull(source);
        Assert.Equal("Teams", source.DisplayName);
    }

    [Theory]
    [InlineData("Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows", "Outlook")]
    [InlineData("DellInc.DellCommandUpdate_htrsf667h5kn2!x", "Dell Command | Update")]
    [InlineData("Claude_pzs8sxrjxfjjc!Claude", "Claude")]
    [InlineData("algum.banco", "Banco")]
    public void OutrosAplicativosSaoDescartados(string appId, string appName)
    {
        Assert.Null(NotificationSourceCatalog.Match(appId, appName));
    }

    [Fact]
    public void SemIdentificacaoNadaEAcompanhado()
    {
        Assert.Null(NotificationSourceCatalog.Match(null, null));
        Assert.Null(NotificationSourceCatalog.Match("", ""));
    }

    [Fact]
    public void ACorDaFonteEOpaca()
    {
        var source = NotificationSourceCatalog.Match("MSTeams_8wekyb3d8bbwe!MSTeams", "Teams");

        Assert.NotNull(source);
        // O canal alfa precisa estar cheio, senão o indicador sai transparente na ilha.
        Assert.Equal(0xFFu, (source.Accent >> 24) & 0xFF);
    }
}
