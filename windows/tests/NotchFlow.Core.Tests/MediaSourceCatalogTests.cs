using NotchFlow.Core.Media;
using NotchFlow.Core.Models;
using Xunit;

namespace NotchFlow.Core.Tests;

/// <summary>
/// Cobre a tradução de AppUserModelId em nome e marca. Não há equivalente na versão macOS:
/// lá o navegador entregava o host exato da aba, aqui a origem é o aplicativo.
/// </summary>
public class MediaSourceCatalogTests
{
    [Theory]
    [InlineData("Spotify.exe", "Spotify", PlaybackBrand.Spotify)]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify", PlaybackBrand.Spotify)]
    [InlineData("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "Apple Music", PlaybackBrand.AppleMusic)]
    public void PlayersConhecidosGanhamNomeEMarca(string id, string nome, PlaybackBrand marca)
    {
        var (displayName, brand) = MediaSourceCatalog.Describe(id, "Faixa", "Artista", "Álbum");

        Assert.Equal(nome, displayName);
        Assert.Equal(marca, brand);
    }

    [Fact]
    public void NavegadorComMetadadosDeYouTubeViraYouTube()
    {
        var (displayName, brand) = MediaSourceCatalog.Describe(
            "msedge.exe", "Aquarela do Brasil", "Canal", "YouTube Music");

        Assert.Equal("YouTube", displayName);
        Assert.Equal(PlaybackBrand.YouTube, brand);
    }

    [Fact]
    public void NavegadorSemPistaFicaComONomeDoNavegador()
    {
        var (displayName, brand) = MediaSourceCatalog.Describe(
            "chrome.exe", "Uma aula qualquer", "Instrutor", "");

        Assert.Equal("Google Chrome", displayName);
        Assert.Equal(PlaybackBrand.Neutral, brand);
    }

    [Fact]
    public void OFirefoxEReconhecidoPeloIdentificadorEmGuid()
    {
        var (displayName, _) = MediaSourceCatalog.Describe(
            "308046B0AF4A39CB", "Faixa", "Artista", "Álbum");

        Assert.Equal("Firefox", displayName);
    }

    [Theory]
    [InlineData("algumplayer.exe", "algumplayer")]
    [InlineData("Editora.MeuApp_8wekyb3d8bbwe!App", "MeuApp")]
    [InlineData("", "Reproduzindo")]
    public void PlayerDesconhecidoRecebeUmNomeApresentavel(string id, string esperado)
    {
        var (displayName, brand) = MediaSourceCatalog.Describe(id, "Faixa", "", "");

        Assert.Equal(esperado, displayName);
        Assert.Equal(PlaybackBrand.Neutral, brand);
    }
}
