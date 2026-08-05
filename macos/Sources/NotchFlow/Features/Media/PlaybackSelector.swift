import Foundation

enum PlaybackSelector {
    static func select(
        from snapshots: [PlayerSource: PlaybackSnapshot],
        preferredSource: PlayerSource?
    ) -> PlaybackSnapshot? {
        let playable = snapshots.values.filter(\.hasTrack)
        guard !playable.isEmpty else { return nil }

        let playing = playable.filter(\.isPlaying)
        if let preferredSource,
           let preferredPlaying = playing.first(where: { $0.source == preferredSource }) {
            return preferredPlaying
        }

        if let mostRecentPlaying = playing.max(by: { $0.capturedAt < $1.capturedAt }) {
            return mostRecentPlaying
        }

        if let preferredSource,
           let preferred = playable.first(where: { $0.source == preferredSource }) {
            return preferred
        }

        return playable.max(by: { $0.capturedAt < $1.capturedAt })
    }
}
