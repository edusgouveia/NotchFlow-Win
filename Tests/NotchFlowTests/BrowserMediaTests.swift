import Foundation
import Testing
@testable import NotchFlow

@Suite("Mídia do navegador")
struct BrowserMediaTests {
    @Test("O parser lê as abas com mídia e separa os erros")
    func parsesProbeOutput() throws {
        let raw = """
        1|3|{"title":"(2) Música - YouTube","artist":"Banda","album":"Álbum","artwork":"https://img.example/a.jpg","duration":210.5,"position":12.25,"playing":true,"host":"www.youtube.com"}
        2|1|ERROR Executing JavaScript through AppleScript is turned off.
        2|2|
        3|4|{"title":"Outra","duration":90,"playing":false,"host":"soundcloud.com"}
        """

        let output = BrowserProbeParser.parse(raw)

        #expect(output.results.count == 2)
        #expect(output.errorMessages.count == 1)

        let first = try #require(output.results.first)
        #expect(first.windowIndex == 1)
        #expect(first.tabIndex == 3)
        #expect(first.payload.playing)
        #expect(first.payload.duration == 210.5)
        #expect(first.payload.host == "www.youtube.com")

        let second = try #require(output.results.last)
        #expect(second.payload.artist.isEmpty)
        #expect(second.payload.playing == false)
    }

    @Test("Abas tocando vêm antes das pausadas")
    func ranksPlayingFirst() throws {
        let paused = BrowserProbeResult(
            windowIndex: 1,
            tabIndex: 1,
            payload: BrowserMediaPayload(title: "Pausada", duration: 600, playing: false, host: "youtube.com")
        )
        let playing = BrowserProbeResult(
            windowIndex: 2,
            tabIndex: 7,
            payload: BrowserMediaPayload(title: "Tocando", duration: 120, playing: true, host: "youtube.com")
        )

        let ranked = BrowserProbeParser.ranked([paused, playing])
        let first = try #require(ranked.first)

        #expect(first.payload.title == "Tocando")
        #expect(first.tabIndex == 7)
    }

    @Test("Títulos perdem o contador e o nome do site")
    func cleansTitles() {
        #expect(BrowserTitleCleaner.clean("(3) Meu Vídeo - YouTube") == "Meu Vídeo")
        #expect(BrowserTitleCleaner.clean("Faixa - YouTube Music") == "Faixa")
        #expect(BrowserTitleCleaner.clean("Set ao vivo | SoundCloud") == "Set ao vivo")
        #expect(BrowserTitleCleaner.clean("Sem sufixo") == "Sem sufixo")
        #expect(BrowserTitleCleaner.clean("(nao numero) Título") == "(nao numero) Título")
    }

    @Test("O host escolhe o serviço mais específico")
    func matchesProviders() {
        #expect(BrowserCatalog.provider(forHost: "music.youtube.com")?.displayName == "YouTube Music")
        #expect(BrowserCatalog.provider(forHost: "www.youtube.com")?.displayName == "YouTube")
        #expect(BrowserCatalog.provider(forHost: "WWW.NETFLIX.COM")?.supportsTrackSkip == false)
        #expect(BrowserCatalog.provider(forHost: "exemplo.com") == nil)
        #expect(BrowserCatalog.provider(forHost: "") == nil)
    }

    @Test("O JavaScript embutido não quebra o literal de AppleScript")
    func javaScriptIsSafeForAppleScript() {
        let scripts = [
            BrowserScript.probe,
            BrowserScript.togglePlayPause,
            BrowserScript.nextTrack,
            BrowserScript.previousTrack,
            BrowserScript.seek(byMilliseconds: -15_000),
            BrowserScript.seek(toMilliseconds: 92_500)
        ]

        for script in scripts {
            #expect(!script.contains("\""))
            #expect(!script.contains("\\"))
            #expect(!script.contains("\n"))
            #expect(!script.isEmpty)
        }
    }

    @Test("O script do Chrome aponta para a aba escolhida")
    func chromeCommandScript() throws {
        let chrome = try #require(BrowserCatalog.browser(withBundleIdentifier: "com.google.Chrome"))
        let script = BrowserAppleScript.command(
            browser: chrome,
            windowIndex: 2,
            tabIndex: 5,
            javaScript: BrowserScript.togglePlayPause
        )

        #expect(script.contains("tell application id \"com.google.Chrome\""))
        #expect(script.contains("tell tab 5 of window 2"))
        #expect(script.contains("execute javascript"))
        #expect(script.contains("return resultText"))
    }

    @Test("O script do Safari usa do JavaScript")
    func safariCommandScript() throws {
        let safari = try #require(BrowserCatalog.browser(withBundleIdentifier: "com.apple.Safari"))
        let script = BrowserAppleScript.command(
            browser: safari,
            windowIndex: 1,
            tabIndex: 4,
            javaScript: BrowserScript.probe
        )

        #expect(script.contains("do JavaScript"))
        #expect(script.contains("in tab 4 of window 1"))
    }

    @Test("A varredura limita o número de abas consultadas")
    func scanScriptLimitsProbes() throws {
        let chrome = try #require(BrowserCatalog.browser(withBundleIdentifier: "com.google.Chrome"))
        let script = BrowserAppleScript.scan(
            browser: chrome,
            hosts: ["youtube.com"],
            probeJavaScript: BrowserScript.probe,
            maximumProbes: 4
        )

        #expect(script.contains("if probeCount < 4 then"))
        #expect(script.contains("set candidateHosts to {\"youtube.com\"}"))
        #expect(script.contains("URL of tabs of windows"))
    }

    @Test("Textos viram literais seguros de AppleScript")
    func quotesLiterals() {
        #expect(AppleScriptLiteral.quoted("simples") == "\"simples\"")
        #expect(AppleScriptLiteral.quoted("com \"aspas\"") == "\"com \\\"aspas\\\"\"")
        #expect(AppleScriptLiteral.list(["a", "b"]) == "{\"a\", \"b\"}")
    }

    @Test("O rótulo da fonte mostra o serviço do navegador")
    func sourceLabelUsesDetail() {
        var snapshot = PlaybackSnapshot.empty(source: .browser)
        snapshot.sourceDetail = "YouTube Music"

        #expect(snapshot.sourceLabel == "YouTube Music")
        #expect(PlaybackSnapshot.empty(source: .spotify).sourceLabel == "Spotify")
    }
}
