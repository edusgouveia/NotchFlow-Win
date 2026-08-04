import AppKit
import Foundation

/// Lê e controla a mídia que está tocando em uma aba do navegador.
///
/// A leitura usa `navigator.mediaSession` quando o site fornece metadados e cai para o
/// título do documento quando não fornece. Os comandos agem sobre o elemento `video`/`audio`
/// da aba, ou nos botões do próprio site no caso de faixa anterior e próxima.
@MainActor
final class BrowserMediaService: MusicService, MusicServiceDiagnostics {
    private struct Target: Equatable {
        let browser: BrowserApplication
        let windowIndex: Int
        let tabIndex: Int
    }

    let source: PlayerSource = .browser

    /// Intervalo mínimo entre varreduras completas, para não bloquear a interface com Apple Events.
    private static let minimumScanInterval: TimeInterval = 8

    private var target: Target?
    private var lastScanDate: Date?
    private var deniedBundleIdentifiers: Set<String> = []
    private var cachedArtworkURL: URL?
    private var cachedArtworkData: Data?

    private(set) var integrationHint: String?

    var bundleIdentifier: String { target?.browser.bundleIdentifier ?? "" }

    func isRunning() -> Bool {
        !runningBrowsers().isEmpty
    }

    func resetDiagnostics() {
        deniedBundleIdentifiers.removeAll()
        integrationHint = nil
        lastScanDate = nil
    }

    func fetchSnapshot() async throws -> PlaybackSnapshot? {
        let browsers = runningBrowsers()
        guard !browsers.isEmpty else {
            target = nil
            return nil
        }

        var fallback: (target: Target, payload: BrowserMediaPayload)?

        if let current = target, browsers.contains(current.browser) {
            if let payload = probe(current) {
                if payload.playing {
                    return await snapshot(for: payload)
                }
                fallback = (current, payload)
            }
        }

        // Com uma aba conhecida em pausa, evita varrer o navegador inteiro a cada atualização.
        if let fallback, let lastScanDate, Date().timeIntervalSince(lastScanDate) < Self.minimumScanInterval {
            target = fallback.target
            return await snapshot(for: fallback.payload)
        }

        lastScanDate = Date()

        for browser in browsers where !deniedBundleIdentifiers.contains(browser.bundleIdentifier) {
            guard let output = scan(browser) else { continue }

            for result in BrowserProbeParser.ranked(output.results) {
                guard BrowserCatalog.provider(forHost: result.payload.host) != nil else { continue }

                let candidate = Target(
                    browser: browser,
                    windowIndex: result.windowIndex,
                    tabIndex: result.tabIndex
                )

                if result.payload.playing {
                    target = candidate
                    return await snapshot(for: result.payload)
                }

                if fallback == nil {
                    fallback = (candidate, result.payload)
                }
            }
        }

        if let fallback {
            target = fallback.target
            return await snapshot(for: fallback.payload)
        }

        target = nil
        return nil
    }

    func perform(_ command: PlayerCommand, currentSnapshot: PlaybackSnapshot) async throws {
        guard let target else { return }

        let javaScript: String
        switch command {
        case .togglePlayPause:
            javaScript = BrowserScript.togglePlayPause
        case .nextTrack:
            guard currentSnapshot.supportsTrackSkip else { return }
            javaScript = BrowserScript.nextTrack
        case .previousTrack:
            guard currentSnapshot.supportsTrackSkip else { return }
            javaScript = BrowserScript.previousTrack
        case .skip(let seconds):
            javaScript = BrowserScript.seek(byMilliseconds: Int(seconds * 1_000))
        case .seek(let position):
            javaScript = BrowserScript.seek(toMilliseconds: Int(max(position, 0) * 1_000))
        }

        do {
            _ = try AppleScriptRunner.executeReturningString(
                BrowserAppleScript.command(
                    browser: target.browser,
                    windowIndex: target.windowIndex,
                    tabIndex: target.tabIndex,
                    javaScript: javaScript
                )
            )
        } catch let error as AppleScriptError {
            register(error, for: target.browser)
            throw error
        }
    }

    private func runningBrowsers() -> [BrowserApplication] {
        let frontmostIdentifier = NSWorkspace.shared.frontmostApplication?.bundleIdentifier
        let running = BrowserCatalog.browsers.filter { browser in
            !NSRunningApplication.runningApplications(withBundleIdentifier: browser.bundleIdentifier).isEmpty
        }

        guard let frontmostIdentifier,
              let frontIndex = running.firstIndex(where: { $0.bundleIdentifier == frontmostIdentifier }) else {
            return running
        }

        var ordered = running
        let frontmost = ordered.remove(at: frontIndex)
        ordered.insert(frontmost, at: 0)
        return ordered
    }

    private func scan(_ browser: BrowserApplication) -> BrowserProbeParser.Output? {
        do {
            let raw = try AppleScriptRunner.executeReturningString(
                BrowserAppleScript.scan(
                    browser: browser,
                    hosts: BrowserCatalog.candidateHosts,
                    probeJavaScript: BrowserScript.probe
                )
            )
            let output = BrowserProbeParser.parse(raw)
            register(errorMessages: output.errorMessages, for: browser)
            return output
        } catch let error as AppleScriptError {
            register(error, for: browser)
            return nil
        } catch {
            AppLog.media.error("Falha ao consultar \(browser.displayName, privacy: .public)")
            return nil
        }
    }

    private func probe(_ target: Target) -> BrowserMediaPayload? {
        do {
            let raw = try AppleScriptRunner.executeReturningString(
                BrowserAppleScript.command(
                    browser: target.browser,
                    windowIndex: target.windowIndex,
                    tabIndex: target.tabIndex,
                    javaScript: BrowserScript.probe
                )
            )
            guard let payload = BrowserProbeParser.decodePayload(raw),
                  BrowserCatalog.provider(forHost: payload.host) != nil else { return nil }
            return payload
        } catch let error as AppleScriptError {
            register(error, for: target.browser)
            return nil
        } catch {
            return nil
        }
    }

    private func snapshot(for payload: BrowserMediaPayload) async -> PlaybackSnapshot? {
        let provider = BrowserCatalog.provider(forHost: payload.host)
        let title = BrowserTitleCleaner.clean(payload.title)
        guard !title.isEmpty else { return nil }

        integrationHint = nil

        return PlaybackSnapshot(
            source: .browser,
            status: payload.playing ? .playing : .paused,
            title: title,
            artist: payload.artist,
            album: payload.album,
            artworkData: await downloadArtwork(from: URL(string: payload.artwork)),
            duration: max(payload.duration, 0),
            position: max(payload.position, 0),
            capturedAt: Date(),
            sourceDetail: provider?.displayName ?? payload.host,
            supportsTrackSkip: provider?.supportsTrackSkip ?? false,
            supportsSeek: true
        )
    }

    private func downloadArtwork(from url: URL?) async -> Data? {
        guard let url, ArtworkDownloadPolicy.allows(url) else {
            cachedArtworkURL = nil
            cachedArtworkData = nil
            return nil
        }

        if url == cachedArtworkURL {
            return cachedArtworkData
        }

        do {
            guard let data = try await ArtworkDownloader.download(from: url) else { return nil }

            cachedArtworkURL = url
            cachedArtworkData = data
            return data
        } catch {
            AppLog.media.error("Falha ao carregar capa do navegador")
            return nil
        }
    }

    private func register(_ error: AppleScriptError, for browser: BrowserApplication) {
        if error.isPermissionDenied {
            deniedBundleIdentifiers.insert(browser.bundleIdentifier)
            integrationHint = "Autorize o NotchFlow a controlar o \(browser.displayName) em Ajustes do Sistema, "
                + "em Privacidade e Segurança, Automação."
            return
        }

        if error.isJavaScriptBlocked {
            integrationHint = javaScriptHint(for: browser)
            return
        }

        guard !error.isApplicationUnavailable else { return }

        AppLog.media.error("Falha no navegador \(browser.displayName, privacy: .public), código \(error.code)")
    }

    private func register(errorMessages: [String], for browser: BrowserApplication) {
        guard let message = errorMessages.first else { return }

        if message.lowercased().contains("javascript") {
            integrationHint = javaScriptHint(for: browser)
            return
        }

        AppLog.media.error("Uma aba recusou a leitura no \(browser.displayName, privacy: .public)")
    }

    private func javaScriptHint(for browser: BrowserApplication) -> String {
        switch browser.engine {
        case .chromium:
            "No \(browser.displayName), ative Visualizar, Desenvolvedor, Permitir JavaScript de Apple Events."
        case .webKit:
            "No Safari, ative Desenvolvedor, Permitir JavaScript de Apple Events."
        }
    }
}
