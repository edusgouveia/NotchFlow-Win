import AppKit
import Foundation

@MainActor
final class SpotifyService: MusicService {
    let source: PlayerSource = .spotify
    let bundleIdentifier = "com.spotify.client"

    private var cachedArtworkURL: URL?
    private var cachedArtworkData: Data?

    func isRunning() -> Bool {
        !NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier).isEmpty
    }

    func fetchSnapshot() async throws -> PlaybackSnapshot? {
        guard isRunning() else { return nil }

        let descriptor = try AppleScriptRunner.execute(
            """
            tell application id "com.spotify.client"
                if player state is stopped then
                    return {"stopped", "", "", "", 0, 0, ""}
                end if

                set activeTrack to current track
                return {(player state as text), (name of activeTrack), (artist of activeTrack), (album of activeTrack), (duration of activeTrack), player position, (artwork url of activeTrack)}
            end tell
            """
        )

        guard descriptor.numberOfItems >= 7 else {
            throw MusicServiceError.malformedResponse(source)
        }

        let status = playbackStatus(from: descriptor.atIndex(1)?.stringValue)
        let title = descriptor.atIndex(2)?.stringValue ?? ""
        let artist = descriptor.atIndex(3)?.stringValue ?? ""
        let album = descriptor.atIndex(4)?.stringValue ?? ""
        let durationMilliseconds = max(descriptor.atIndex(5)?.doubleValue ?? 0, 0)
        let position = max(descriptor.atIndex(6)?.doubleValue ?? 0, 0)
        let artworkURL = descriptor.atIndex(7)?.stringValue.flatMap(URL.init(string:))
        let artworkData = await artwork(for: artworkURL)

        return PlaybackSnapshot(
            source: source,
            status: status,
            title: title,
            artist: artist,
            album: album,
            artworkData: artworkData,
            duration: durationMilliseconds / 1_000,
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
        case .seek(let position):
            let target = min(max(position, 0), currentSnapshot.duration)
            statement = "set player position to \(target)"
        }

        try AppleScriptRunner.executeVoid(
            """
            tell application id "com.spotify.client"
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

    private func artwork(for url: URL?) async -> Data? {
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
            AppLog.media.error("Falha ao carregar capa do Spotify")
            return nil
        }
    }
}
