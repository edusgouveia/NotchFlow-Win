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
    // Teams novo e clássico.
    [InlineData("MSTeams_8wekyb3d8bbwe!MSTeams", "Microsoft Teams", "Teams")]
    [InlineData("com.squirrel.Teams.Teams", "Microsoft Teams", "Teams")]
    // Teams pelo navegador, que é o caminho que funciona quando o app desktop
    // desenha a própria notificação em vez de entregá-la ao Windows.
    [InlineData("Microsoft.MicrosoftEdge.Stable_8wekyb3d8bbwe!https://teams.microsoft.com/", "", "Teams")]
    // Outlook novo e clássico.
    [InlineData("Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows", "Outlook", "Outlook")]
    [InlineData("Microsoft.Office.OUTLOOK.EXE.15", "Outlook", "Outlook")]
    [InlineData("Microsoft.Office.OUTLOOK.EXE.16", "", "Outlook")]
    // WhatsApp da Store.
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", "WhatsApp", "WhatsApp")]
    public void FontesAcompanhadasSaoReconhecidas(string appId, string appName, string esperado)
    {
        var source = NotificationSourceCatalog.Match(appId, appName);

        Assert.NotNull(source);
        Assert.Equal(esperado, source.DisplayName);
    }

    [Fact]
    public void ReconheceApenasPeloNomeQuandoOIdentificadorNaoAjuda()
    {
        var source = NotificationSourceCatalog.Match("id-sem-pista", "WhatsApp");

        Assert.NotNull(source);
        Assert.Equal("WhatsApp", source.DisplayName);
    }

    [Theory]
    [InlineData("DellInc.DellCommandUpdate_htrsf667h5kn2!x", "Dell Command | Update")]
    [InlineData("Claude_pzs8sxrjxfjjc!Claude", "Claude")]
    [InlineData("Microsoft.PowerToysWin32", "PowerToys")]
    [InlineData("algum.banco", "Banco")]
    [InlineData("Microsoft.ScreenSketch_8wekyb3d8bbwe!App", "Ferramenta de Captura")]
    public void OutrosAplicativosSaoDescartados(string appId, string appName)
    {
        Assert.Null(NotificationSourceCatalog.Match(appId, appName));
    }

    [Fact]
    public void OTeamViewerNaoEConfundidoComOTeams()
    {
        // "TeamViewer.TeamViewer" contém "team", mas não "teams".
        Assert.Null(NotificationSourceCatalog.Match("TeamViewer.TeamViewer", "TeamViewer"));
    }

    [Fact]
    public void SemIdentificacaoNadaEAcompanhado()
    {
        Assert.Null(NotificationSourceCatalog.Match(null, null));
        Assert.Null(NotificationSourceCatalog.Match("", ""));
    }

    [Fact]
    public void CadaFonteTemCorOpacaEDistinta()
    {
        var cores = NotificationSourceCatalog.All.Select(s => s.Accent).ToList();

        // O canal alfa precisa estar cheio, senão o indicador sai transparente na ilha.
        Assert.All(cores, cor => Assert.Equal(0xFFu, (cor >> 24) & 0xFF));

        // Cores repetidas tornariam o ponto de origem inútil na lista misturada.
        Assert.Equal(cores.Count, cores.Distinct().Count());
    }
}
