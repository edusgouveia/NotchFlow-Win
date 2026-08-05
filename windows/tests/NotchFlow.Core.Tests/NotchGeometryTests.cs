using NotchFlow.Core.Models;
using Xunit;

namespace NotchFlow.Core.Tests;

/// <summary>Porte dos NotchGeometryTests do NotchFlow para macOS.</summary>
public class NotchGeometryTests
{
    [Fact]
    public void TelaPrincipalUsaIlhaCompleta()
    {
        var geometry = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);

        Assert.Equal(ClosedStyle.Notch, geometry.ClosedStyle);
        Assert.False(geometry.IsMinimized);
    }

    [Fact]
    public void TelaSecundariaViraTiraQuandoAPreferenciaEstaLigada()
    {
        var geometry = NotchGeometry.Make(DisplayKind.Secondary, minimizeOnSecondaryDisplays: true);

        Assert.Equal(ClosedStyle.Sliver, geometry.ClosedStyle);
        Assert.True(geometry.IsMinimized);
        Assert.Equal(NotchGeometry.SliverWidth, geometry.ClosedWidthValue);
        Assert.Equal(NotchGeometry.SliverHeight, geometry.ClosedHeightValue);
    }

    [Fact]
    public void TelaSecundariaMantemAIlhaQuandoAPreferenciaEstaDesligada()
    {
        var geometry = NotchGeometry.Make(DisplayKind.Secondary, minimizeOnSecondaryDisplays: false);

        Assert.Equal(ClosedStyle.Notch, geometry.ClosedStyle);
        Assert.False(geometry.IsMinimized);
    }

    [Fact]
    public void AreaDeInteracaoDaTiraEMaiorQueOVisual()
    {
        var geometry = NotchGeometry.Make(DisplayKind.Secondary, minimizeOnSecondaryDisplays: true);

        // A tira tem 9 pixels de altura: sem uma área de toque maior seria quase
        // impossível acertá-la na borda superior da tela.
        Assert.True(geometry.ClosedInteractionHeight > geometry.ClosedHeightValue);
        Assert.True(geometry.ClosedInteractionWidth > geometry.ClosedWidthValue);
        Assert.Equal(NotchGeometry.SliverInteractionHeight, geometry.ClosedInteractionHeight);
    }

    [Fact]
    public void AreaDeInteracaoDaIlhaCompletaAcompanhaOVisual()
    {
        var geometry = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);

        Assert.Equal(geometry.ClosedWidthValue, geometry.ClosedInteractionWidth);
        Assert.Equal(geometry.ClosedHeightValue, geometry.ClosedInteractionHeight);
    }

    [Fact]
    public void SemCalendarioOPainelExpandidoTemSoAColunaDeMidia()
    {
        var geometry = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesCalendar: false);

        var esperado = NotchGeometry.HorizontalPadding * 2 + NotchGeometry.MediaColumnWidth;
        Assert.Equal(esperado, geometry.ExpandedWidth);
    }

    [Fact]
    public void ComCalendarioOPainelExpandidoAbreAColunaExtra()
    {
        var comCalendario = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesCalendar: true);
        var semCalendario = NotchGeometry.Make(
            DisplayKind.Primary, minimizeOnSecondaryDisplays: true, includesCalendar: false);

        var diferenca = comCalendario.ExpandedWidth - semCalendario.ExpandedWidth;
        var esperado = NotchGeometry.ColumnSpacing * 2
            + NotchGeometry.DividerWidth
            + NotchGeometry.CalendarColumnWidth;

        Assert.Equal(esperado, diferenca);
    }

    [Fact]
    public void OPainelExpandidoENasDuasDimensoesMaiorQueORecolhido()
    {
        var geometry = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);

        Assert.True(geometry.ExpandedWidth > geometry.ClosedWidthValue);
        Assert.True(geometry.ExpandedHeight > geometry.ClosedHeightValue);
    }

    [Fact]
    public void ATiraTemCantoMenosArredondadoQueAIlha()
    {
        var ilha = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);
        var tira = NotchGeometry.Make(DisplayKind.Secondary, minimizeOnSecondaryDisplays: true);

        Assert.True(tira.ClosedCornerRadius < ilha.ClosedCornerRadius);
    }
}
