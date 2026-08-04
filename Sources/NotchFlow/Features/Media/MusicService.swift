import Foundation

@MainActor
protocol MusicService: AnyObject {
    var source: PlayerSource { get }
    var bundleIdentifier: String { get }

    func isRunning() -> Bool
    func fetchSnapshot() async throws -> PlaybackSnapshot?
    func perform(_ command: PlayerCommand, currentSnapshot: PlaybackSnapshot) async throws
}

/// Serviços que precisam orientar o usuário sobre permissões ou ajustes do sistema.
@MainActor
protocol MusicServiceDiagnostics: AnyObject {
    var integrationHint: String? { get }

    func resetDiagnostics()
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
