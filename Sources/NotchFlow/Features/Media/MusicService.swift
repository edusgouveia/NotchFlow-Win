import Foundation

@MainActor
protocol MusicService: AnyObject {
    var source: PlayerSource { get }
    var bundleIdentifier: String { get }

    func isRunning() -> Bool
    func fetchSnapshot() async throws -> PlaybackSnapshot?
    func perform(_ command: PlayerCommand, currentSnapshot: PlaybackSnapshot) async throws
}

enum MusicServiceError: LocalizedError {
    case automationFailed(String)
    case malformedResponse(PlayerSource)

    var errorDescription: String? {
        switch self {
        case .automationFailed(let message): message
        case .malformedResponse(let source): "Não foi possível ler os dados do \(source.displayName)."
        }
    }
}
