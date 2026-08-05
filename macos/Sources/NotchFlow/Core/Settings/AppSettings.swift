import Combine
import Foundation

/// Preferências do usuário. Guarda apenas interruptores locais no UserDefaults.
@MainActor
final class AppSettings: ObservableObject {
    static let shared = AppSettings()

    private enum Key {
        static let browserIntegration = "media.browserIntegrationEnabled"
        static let minimizeOnExternalDisplays = "notch.minimizeOnExternalDisplays"
        static let showMenuBarIcon = "app.showMenuBarIcon"
        static let calendarAccessGrantedOnce = "calendar.accessGrantedOnce"
    }

    private let defaults: UserDefaults

    /// Usado pelo AppDelegate para mostrar ou remover o NSStatusItem nativo sem
    /// reconstruir as cenas do SwiftUI.
    var showMenuBarIconDidChange: ((Bool) -> Void)?

    @Published var browserIntegrationEnabled: Bool {
        didSet { defaults.set(browserIntegrationEnabled, forKey: Key.browserIntegration) }
    }

    @Published var minimizeOnExternalDisplays: Bool {
        didSet { defaults.set(minimizeOnExternalDisplays, forKey: Key.minimizeOnExternalDisplays) }
    }

    /// O ícone da barra de menus fica desligado por padrão: a própria ilha traz o menu de contexto.
    @Published var showMenuBarIcon: Bool {
        didSet {
            defaults.set(showMenuBarIcon, forKey: Key.showMenuBarIcon)
            showMenuBarIconDidChange?(showMenuBarIcon)
        }
    }

    /// Registra que o calendário já foi autorizado alguma vez, para explicar quando o macOS pede de novo.
    @Published var calendarAccessGrantedOnce: Bool {
        didSet { defaults.set(calendarAccessGrantedOnce, forKey: Key.calendarAccessGrantedOnce) }
    }

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        browserIntegrationEnabled = defaults.object(forKey: Key.browserIntegration) as? Bool ?? true
        minimizeOnExternalDisplays = defaults.object(forKey: Key.minimizeOnExternalDisplays) as? Bool ?? true
        showMenuBarIcon = defaults.object(forKey: Key.showMenuBarIcon) as? Bool ?? false
        calendarAccessGrantedOnce = defaults.object(forKey: Key.calendarAccessGrantedOnce) as? Bool ?? false
    }
}
