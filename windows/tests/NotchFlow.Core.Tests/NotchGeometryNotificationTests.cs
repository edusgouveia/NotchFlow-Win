using NotchFlow.Core.Models;
using Xunit;

namespace NotchFlow.Core.Tests;

/// <summary>Geometria da coluna de notificações, que só existe quando há algo a mostrar.</summary>
public class NotchGeometryNotificationTests
{
    [Fact]
    public void SemNotificacoesAIlhaAbreNoTamanhoDeSempre()
    {
        var comuns = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);
        var esperado = NotchGeometry.HorizontalPadding * 2 + NotchGeometry.MediaColumnWidth;

        Assert.Equal(esperado, comuns.ExpandedWidth);
        Assert.False(comuns.IncludesNotifications);
    }

    [Fact]
    public void ComNotificacoesAIlhaAbreAColunaExtra()
    {
        var com = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesNotifications: true);
        var sem = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesNotifications: false);

        var esperado = NotchGeometry.ColumnSpacing * 2
            + NotchGeometry.DividerWidth
            + NotchGeometry.NotificationColumnWidth;

        Assert.Equal(esperado, com.ExpandedWidth - sem.ExpandedWidth);
    }

    [Fact]
    public void AsDuasColunasExtrasSomamSemSeAtropelar()
    {
        var ambas = NotchGeometry.Make(
            DisplayKind.Primary,
            minimizeOnSecondaryDisplays: true,
            includesCalendar: true,
            includesNotifications: true);

        var soNotificacoes = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesNotifications: true);

        var soCalendario = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesCalendar: true);

        var nenhuma = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);

        // Quando o calendário existir, as duas colunas precisam conviver.
        var somaSeparada = (soNotificacoes.ExpandedWidth - nenhuma.ExpandedWidth)
            + (soCalendario.ExpandedWidth - nenhuma.ExpandedWidth);

        Assert.Equal(somaSeparada, ambas.ExpandedWidth - nenhuma.ExpandedWidth);
    }

    [Fact]
    public void AColunaDeNotificacoesNaoMudaAAltura()
    {
        var com = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesNotifications: true);
        var sem = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);

        // A ilha cresce para o lado, não para baixo: a animação só precisa acompanhar a largura.
        Assert.Equal(sem.ExpandedHeight, com.ExpandedHeight);
    }

    [Fact]
    public void ATiraDaTelaSecundariaTambemPodeAbrirAColuna()
    {
        var tira = NotchGeometry.Make(
            DisplayKind.Secondary, minimizeOnSecondaryDisplays: true, includesNotifications: true);

        Assert.True(tira.IsMinimized);
        Assert.True(tira.IncludesNotifications);
        Assert.True(tira.ExpandedWidth > NotchGeometry.MediaColumnWidth);
    }
}
