import Foundation
import Testing
@testable import NotchFlow

@Suite("Playback selection")
struct PlaybackSelectorTests {
    @Test("Playing source wins over a preferred paused source")
    func playingWins() {
        let apple = snapshot(source: .appleMusic, status: .paused, capturedAt: 10)
        let spotify = snapshot(source: .spotify, status: .playing, capturedAt: 5)

        let selected = PlaybackSelector.select(
            from: [.appleMusic: apple, .spotify: spotify],
            preferredSource: .appleMusic
        )

        #expect(selected?.source == .spotify)
    }

    @Test("Preferred source wins when both are playing")
    func preferredPlayingWins() {
        let apple = snapshot(source: .appleMusic, status: .playing, capturedAt: 10)
        let spotify = snapshot(source: .spotify, status: .playing, capturedAt: 20)

        let selected = PlaybackSelector.select(
            from: [.appleMusic: apple, .spotify: spotify],
            preferredSource: .appleMusic
        )

        #expect(selected?.source == .appleMusic)
    }

    private func snapshot(
        source: PlayerSource,
        status: PlaybackStatus,
        capturedAt: TimeInterval
    ) -> PlaybackSnapshot {
        PlaybackSnapshot(
            source: source,
            status: status,
            title: "Track",
            artist: "Artist",
            album: "Album",
            artworkData: nil,
            duration: 180,
            position: 10,
            capturedAt: Date(timeIntervalSinceReferenceDate: capturedAt)
        )
    }
}
