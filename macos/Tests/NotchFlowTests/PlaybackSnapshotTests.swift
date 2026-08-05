import Foundation
import Testing
@testable import NotchFlow

@Suite("Playback snapshot")
struct PlaybackSnapshotTests {
    @Test("A fonte define a cor de marca do indicador recolhido")
    func sourceDefinesPlaybackBrand() {
        #expect(PlaybackSnapshot.empty(source: .spotify).playbackBrand == .spotify)
        #expect(PlaybackSnapshot.empty(source: .appleMusic).playbackBrand == .appleMusic)

        var youtube = PlaybackSnapshot.empty(source: .browser)
        youtube.sourceDetail = "YouTube Music"
        #expect(youtube.playbackBrand == .youtube)

        var otherBrowser = PlaybackSnapshot.empty(source: .browser)
        otherBrowser.sourceDetail = "SoundCloud"
        #expect(otherBrowser.playbackBrand == .neutral)
    }

    @Test("Playing position advances and is clamped to duration")
    func effectivePositionAdvances() {
        let capturedAt = Date(timeIntervalSinceReferenceDate: 1_000)
        let snapshot = PlaybackSnapshot(
            source: .appleMusic,
            status: .playing,
            title: "Track",
            artist: "Artist",
            album: "Album",
            artworkData: nil,
            duration: 30,
            position: 25,
            capturedAt: capturedAt
        )

        #expect(snapshot.effectivePosition(at: capturedAt.addingTimeInterval(2)) == 27)
        #expect(snapshot.effectivePosition(at: capturedAt.addingTimeInterval(10)) == 30)
    }

    @Test("Paused position remains unchanged")
    func pausedPositionDoesNotAdvance() {
        let capturedAt = Date(timeIntervalSinceReferenceDate: 1_000)
        var snapshot = PlaybackSnapshot.empty(source: .spotify)
        snapshot.status = .paused
        snapshot.duration = 120
        snapshot.position = 42
        snapshot.capturedAt = capturedAt

        #expect(snapshot.effectivePosition(at: capturedAt.addingTimeInterval(30)) == 42)
    }
}
