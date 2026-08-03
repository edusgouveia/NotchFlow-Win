import Foundation
import ServiceManagement

@MainActor
final class LaunchAtLoginService: ObservableObject {
    @Published private(set) var isEnabled = false
    @Published private(set) var requiresApproval = false
    @Published private(set) var errorMessage: String?

    var canBeConfigured: Bool {
        Bundle.main.bundleURL.pathExtension.lowercased() == "app"
    }

    init() {
        refreshStatus()
    }

    func setEnabled(_ enabled: Bool) {
        guard canBeConfigured else {
            errorMessage = "Disponível após gerar e instalar o NotchFlow.app."
            refreshStatus()
            return
        }

        do {
            if enabled {
                switch SMAppService.mainApp.status {
                case .enabled:
                    break
                case .requiresApproval:
                    SMAppService.openSystemSettingsLoginItems()
                case .notRegistered, .notFound:
                    try SMAppService.mainApp.register()
                @unknown default:
                    try SMAppService.mainApp.register()
                }
            } else if SMAppService.mainApp.status != .notRegistered {
                try SMAppService.mainApp.unregister()
            }
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
            AppLog.lifecycle.error("Falha ao alterar início automático: \(error.localizedDescription, privacy: .public)")
        }

        refreshStatus()
    }

    func refreshStatus() {
        guard canBeConfigured else {
            isEnabled = false
            requiresApproval = false
            return
        }

        let status = SMAppService.mainApp.status
        isEnabled = status == .enabled
        requiresApproval = status == .requiresApproval
    }

    func openSystemSettings() {
        SMAppService.openSystemSettingsLoginItems()
    }
}
