import Foundation

/// Navegador compatível com automação por Apple Events.
struct BrowserApplication: Equatable, Sendable, Identifiable {
    enum Engine: String, Equatable, Sendable {
        case chromium
        case webKit
    }

    let bundleIdentifier: String
    let displayName: String
    let engine: Engine

    var id: String { bundleIdentifier }
}

/// Serviço de mídia reconhecido dentro de uma aba.
struct BrowserMediaProvider: Equatable, Sendable {
    let host: String
    let displayName: String
    let supportsTrackSkip: Bool
}

enum BrowserCatalog {
    static let browsers: [BrowserApplication] = [
        BrowserApplication(bundleIdentifier: "com.google.Chrome", displayName: "Google Chrome", engine: .chromium),
        BrowserApplication(bundleIdentifier: "com.google.Chrome.beta", displayName: "Google Chrome Beta", engine: .chromium),
        BrowserApplication(bundleIdentifier: "com.brave.Browser", displayName: "Brave", engine: .chromium),
        BrowserApplication(bundleIdentifier: "com.microsoft.edgemac", displayName: "Microsoft Edge", engine: .chromium),
        BrowserApplication(bundleIdentifier: "company.thebrowser.Browser", displayName: "Arc", engine: .chromium),
        BrowserApplication(bundleIdentifier: "com.vivaldi.Vivaldi", displayName: "Vivaldi", engine: .chromium),
        BrowserApplication(bundleIdentifier: "com.operasoftware.Opera", displayName: "Opera", engine: .chromium),
        BrowserApplication(bundleIdentifier: "com.apple.Safari", displayName: "Safari", engine: .webKit)
    ]

    static let providers: [BrowserMediaProvider] = [
        BrowserMediaProvider(host: "music.youtube.com", displayName: "YouTube Music", supportsTrackSkip: true),
        BrowserMediaProvider(host: "youtube.com", displayName: "YouTube", supportsTrackSkip: true),
        BrowserMediaProvider(host: "youtu.be", displayName: "YouTube", supportsTrackSkip: true),
        BrowserMediaProvider(host: "soundcloud.com", displayName: "SoundCloud", supportsTrackSkip: true),
        BrowserMediaProvider(host: "open.spotify.com", displayName: "Spotify Web", supportsTrackSkip: true),
        BrowserMediaProvider(host: "music.apple.com", displayName: "Apple Music Web", supportsTrackSkip: true),
        BrowserMediaProvider(host: "deezer.com", displayName: "Deezer", supportsTrackSkip: true),
        BrowserMediaProvider(host: "tidal.com", displayName: "TIDAL", supportsTrackSkip: true),
        BrowserMediaProvider(host: "bandcamp.com", displayName: "Bandcamp", supportsTrackSkip: true),
        BrowserMediaProvider(host: "mixcloud.com", displayName: "Mixcloud", supportsTrackSkip: true),
        BrowserMediaProvider(host: "twitch.tv", displayName: "Twitch", supportsTrackSkip: false),
        BrowserMediaProvider(host: "vimeo.com", displayName: "Vimeo", supportsTrackSkip: false),
        BrowserMediaProvider(host: "netflix.com", displayName: "Netflix", supportsTrackSkip: false),
        BrowserMediaProvider(host: "primevideo.com", displayName: "Prime Video", supportsTrackSkip: false),
        BrowserMediaProvider(host: "disneyplus.com", displayName: "Disney+", supportsTrackSkip: false),
        BrowserMediaProvider(host: "max.com", displayName: "Max", supportsTrackSkip: false),
        BrowserMediaProvider(host: "globoplay.globo.com", displayName: "Globoplay", supportsTrackSkip: false),
        BrowserMediaProvider(host: "crunchyroll.com", displayName: "Crunchyroll", supportsTrackSkip: false),
        BrowserMediaProvider(host: "udemy.com", displayName: "Udemy", supportsTrackSkip: false),
        BrowserMediaProvider(host: "coursera.org", displayName: "Coursera", supportsTrackSkip: false),
        BrowserMediaProvider(host: "alura.com.br", displayName: "Alura", supportsTrackSkip: false)
    ]

    /// Fragmentos usados para filtrar abas antes de executar qualquer JavaScript.
    static var candidateHosts: [String] {
        var seen = Set<String>()
        var result: [String] = []
        for provider in providers where !seen.contains(provider.host) {
            seen.insert(provider.host)
            result.append(provider.host)
        }
        return result
    }

    static func provider(forHost host: String) -> BrowserMediaProvider? {
        let normalized = host.lowercased()
        guard !normalized.isEmpty else { return nil }

        return providers
            .filter { normalized == $0.host || normalized.hasSuffix("." + $0.host) }
            .max { $0.host.count < $1.host.count }
    }

    static func browser(withBundleIdentifier identifier: String) -> BrowserApplication? {
        browsers.first { $0.bundleIdentifier == identifier }
    }
}
