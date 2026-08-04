import Foundation

enum PlayerSource: String, CaseIterable, Identifiable, Sendable {
    case appleMusic
    case spotify
    case browser

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .appleMusic: "Apple Music"
        case .spotify: "Spotify"
        case .browser: "Navegador"
        }
    }

    var symbolName: String {
        switch self {
        case .appleMusic: "music.note"
        case .spotify: "waveform"
        case .browser: "play.rectangle.fill"
        }
    }
}

enum PlaybackStatus: String, Sendable {
    case playing
    case paused
    case stopped
}

struct PlaybackSnapshot: Equatable, Sendable {
    let source: PlayerSource
    var status: PlaybackStatus
    var title: String
    var artist: String
    var album: String
    var artworkData: Data?
    var duration: TimeInterval
    var position: TimeInterval
    var capturedAt: Date

    /// Nome do serviço concreto quando a fonte é genérica, como "YouTube Music" para o navegador.
    var sourceDetail: String?

    /// Indica se a fonte aceita os comandos de faixa anterior e próxima.
    var supportsTrackSkip: Bool = true

    /// Indica se a fonte aceita avanço e retrocesso na linha do tempo.
    var supportsSeek: Bool = true

    var isPlaying: Bool { status == .playing }
    var hasTrack: Bool { !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    var sourceLabel: String {
        guard let sourceDetail, !sourceDetail.isEmpty else { return source.displayName }
        return sourceDetail
    }

    func effectivePosition(at date: Date = Date()) -> TimeInterval {
        let elapsed = isPlaying ? date.timeIntervalSince(capturedAt) : 0
        return min(max(position + elapsed, 0), max(duration, 0))
    }

    static func empty(source: PlayerSource) -> PlaybackSnapshot {
        PlaybackSnapshot(
            source: source,
            status: .stopped,
            title: "",
            artist: "",
            album: "",
            artworkData: nil,
            duration: 0,
            position: 0,
            capturedAt: Date()
        )
    }
}

enum PlayerCommand: Equatable, Sendable {
    case togglePlayPause
    case previousTrack
    case nextTrack
    case skip(seconds: TimeInterval)
    /// Posição absoluta, usada quando o usuário arrasta a barra de progresso.
    case seek(to: TimeInterval)
}
