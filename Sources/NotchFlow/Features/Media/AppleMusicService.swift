import AppKit
import Foundation

@MainActor
final class AppleMusicService: MusicService {
    let source: PlayerSource = .appleMusic
    let bundleIdentifier = "com.apple.Music"

    func isRunning() -> Bool {
        !NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier).isEmpty
    }

    func fetchSnapshot() async throws -> PlaybackSnapshot? {
        guard isRunning() else { return nil }

        let descriptor = try AppleScriptRunner.execute(
            """
            tell application id "com.apple.Music"
                if player state is stopped then
                    return {"stopped", "", "", "", 0, 0, missing value}
                end if

                set activeTrack to current track
                set artworkBytes to missing value
                try
                    set artworkBytes to raw data of artwork 1 of activeTrack
                end try

                return {(player state as text), (name of activeTrack), (artist of activeTrack), (album of activeTrack), (duration of activeTrack), player position, artworkBytes}
            end tell
            """
        )

        guard descriptor.numberOfItems >= 6 else {
            throw MusicServiceError.malformedResponse(source)
        }

        let status = playbackStatus(from: descriptor.atIndex(1)?.stringValue)
        let title = descriptor.atIndex(2)?.stringValue ?? ""
        let artist = descriptor.atIndex(3)?.stringValue ?? ""
        let album = descriptor.atIndex(4)?.stringValue ?? ""
        let duration = max(descriptor.atIndex(5)?.doubleValue ?? 0, 0)
        let position = max(descriptor.atIndex(6)?.doubleValue ?? 0, 0)
        let artworkData = descriptor.atIndex(7)?.data

        return PlaybackSnapshot(
            source: source,
            status: status,
            title: title,
            artist: artist,
            album: album,
            artworkData: artworkData,
            duration: duration,
            position: position,
            capturedAt: Date()
        )
    }

    func perform(_ command: PlayerCommand, currentSnapshot: PlaybackSnapshot) async throws {
        guard isRunning() else { return }

        let statement: String
        switch command {
        case .togglePlayPause:
            statement = "playpause"
        case .previousTrack:
            statement = "previous track"
        case .nextTrack:
            statement = "next track"
        case .skip(let seconds):
            let target = min(max(currentSnapshot.effectivePosition() + seconds, 0), currentSnapshot.duration)
            statement = "set player position to \(target)"
        }

        try AppleScriptRunner.executeVoid(
            """
            tell application id "com.apple.Music"
                \(statement)
            end tell
            """
        )
    }

    private func playbackStatus(from value: String?) -> PlaybackStatus {
        switch value?.lowercased() {
        case "playing": .playing
        case "paused": .paused
        default: .stopped
        }
    }
}
