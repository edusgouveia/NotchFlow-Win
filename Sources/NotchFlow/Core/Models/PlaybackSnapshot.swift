import Foundation

enum PlayerSource: String, CaseIterable, Identifiable, Sendable {
    case appleMusic
    case spotify

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .appleMusic: "Apple Music"
        case .spotify: "Spotify"
        }
    }

    var symbolName: String {
        switch self {
        case .appleMusic: "music.note"
        case .spotify: "waveform"
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

    var isPlaying: Bool { status == .playing }
    var hasTrack: Bool { !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

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
}
