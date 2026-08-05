using NotchFlow.Core.Models;
using Xunit;

namespace NotchFlow.Core.Tests;

/// <summary>
/// Largura da ilha recolhida. No macOS ela era fixa em 156 para imitar a notch de hardware;
/// no Windows não há notch para imitar, então ela mede o que está mostrando.
/// </summary>
public class NotchGeometryClosedWidthTests
{
    private static double Largura(ClosedContent content) => NotchGeometry
        .Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true, closedContent: content)
        .ClosedWidthValue;

    [Fact]
    public void OciosaFicaNoTamanhoDaCapsula()
    {
        var ociosa = Largura(ClosedContent.Idle);

        Assert.Equal(38, ociosa);
    }

    [Fact]
    public void SoOContadorEMaisEstreitoQueMidiaMaisContador()
    {
        var soContador = Largura(new ClosedContent(false, false, 3));
        var tudo = Largura(new ClosedContent(HasArtwork: true, HasMedia: true, NotificationCount: 3));

        Assert.True(soContador < tudo);
    }

    [Fact]
    public void CadaElementoVisivelAumentaALargura()
    {
        var nada = Largura(ClosedContent.Idle);
        var midia = Largura(new ClosedContent(false, true, 0));
        var midiaComCapa = Largura(new ClosedContent(true, true, 0));
        var tudo = Largura(new ClosedContent(true, true, 1));

        Assert.True(midia >= nada);
        Assert.True(midiaComCapa > midia);
        Assert.True(tudo > midiaComCapa);
    }

    [Fact]
    public void ContadorDeDoisDigitosAlargaAIlha()
    {
        var umDigito = Largura(new ClosedContent(true, true, 9));
        var doisDigitos = Largura(new ClosedContent(true, true, 10));

        // Acima de nove o texto vira "9+" e precisa de mais espaço.
        Assert.True(doisDigitos > umDigito);
    }

    [Fact]
    public void SoComOContadorALarguraFicaNoPiso()
    {
        // O contador sozinho é estreito demais: o piso evita uma ilha minúscula, e por isso
        // a diferença entre um e dois dígitos não aparece nesta combinação.
        Assert.Equal(38, Largura(new ClosedContent(false, false, 3)));
        Assert.Equal(38, Largura(new ClosedContent(false, false, 12)));
    }

    [Fact]
    public void NenhumaCombinacaoFicaMenorQueOPiso()
    {
        var combinacoes = new[]
        {
            ClosedContent.Idle,
            new ClosedContent(false, false, 1),
            new ClosedContent(false, true, 0),
            new ClosedContent(true, false, 0),
            new ClosedContent(true, true, 99)
        };

        // Uma ilha menor que isso viraria um ponto perdido no topo da tela.
        Assert.All(combinacoes, c => Assert.True(Largura(c) >= 38));
    }

    [Fact]
    public void AIlhaEncolheuEmRelacaoAoTamanhoHerdadoDoMac()
    {
        var comTudo = Largura(new ClosedContent(true, true, 3));

        // O valor antigo, fixo, era 156.
        Assert.True(comTudo < 156);
    }

    [Fact]
    public void AAreaDeInteracaoNaoEncolheJuntoComOVisual()
    {
        var geometry = NotchGeometry.Make(
            DisplayKind.Primary,
            minimizeOnSecondaryDisplays: true,
            closedContent: ClosedContent.Idle);

        // Mirar numa ilha de 38 pixels seria desconfortável.
        Assert.True(geometry.ClosedInteractionWidth > geometry.ClosedWidthValue);
        Assert.Equal(96, geometry.ClosedInteractionWidth);
    }

    [Fact]
    public void ATiraNaoEAfetadaPeloConteudo()
    {
        var vazia = NotchGeometry.Make(
            DisplayKind.Secondary, true, closedContent: ClosedContent.Idle);
        var cheia = NotchGeometry.Make(
            DisplayKind.Secondary, true, closedContent: new ClosedContent(true, true, 5));

        // Na tela secundária a ilha é sempre a mesma tira fina.
        Assert.Equal(vazia.ClosedWidthValue, cheia.ClosedWidthValue);
        Assert.Equal(NotchGeometry.SliverWidth, cheia.ClosedWidthValue);
    }

    [Fact]
    public void AFaixaSuperiorDoPainelAcompanhaAAlturaRecolhida()
    {
        var geometry = NotchGeometry.Make(DisplayKind.Primary, minimizeOnSecondaryDisplays: true);

        // O painel aberto reserva no topo o espaço onde a ilha recolhida estaria.
        Assert.Equal(geometry.ClosedHeightValue + 4, geometry.ExpandedTopPadding);
    }
}
