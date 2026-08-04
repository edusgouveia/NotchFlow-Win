import AppKit
import SwiftUI

@main
@MainActor
struct NotchFlowApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    /// Mesma chave usada por `AppSettings`, para o ícone entrar e sair sem reiniciar o aplicativo.
    @AppStorage("app.showMenuBarIcon") private var showMenuBarIcon = false

    var body: some Scene {
        MenuBarExtra(
            "NotchFlow",
            systemImage: "waveform",
            isInserted: $showMenuBarIcon
        ) {
            Button("Abrir Notch") {
                appDelegate.toggleNotch()
            }

            Button("Atualizar mídia e calendário") {
                Task {
                    appDelegate.viewModel.media.resetBrowserDiagnostics()
                    await appDelegate.viewModel.media.refreshAll()
                    await appDelegate.viewModel.calendar.refresh()
                }
            }

            Divider()

            // Agrupado porque o ViewBuilder aceita no máximo dez filhos diretos.
            Group {
                BrowserIntegrationMenuItem(
                    settings: appDelegate.viewModel.settings,
                    media: appDelegate.viewModel.media
                )

                ExternalDisplayMenuItem(settings: appDelegate.viewModel.settings) {
                    appDelegate.refreshLayout()
                }

                MenuBarIconMenuItem(settings: appDelegate.viewModel.settings)

                LaunchAtLoginMenuItem(service: appDelegate.viewModel.launchAtLogin)
            }

            Divider()

            SettingsLink {
                Text("Ajustes…")
            }

            Divider()

            Button("Sair do NotchFlow", role: .destructive) {
                NSApplication.shared.terminate(nil)
            }
            .keyboardShortcut("q")
        }

        Settings {
            SettingsView(viewModel: appDelegate.viewModel) {
                appDelegate.refreshLayout()
            }
        }
    }
}

private struct LaunchAtLoginMenuItem: View {
    @ObservedObject var service: LaunchAtLoginService

    var body: some View {
        Toggle(
            "Abrir ao iniciar o Mac",
            isOn: Binding(
                get: { service.isEnabled },
                set: { service.setEnabled($0) }
            )
        )
        .disabled(!service.canBeConfigured)
        .onAppear { service.refreshStatus() }
    }
}

private struct BrowserIntegrationMenuItem: View {
    @ObservedObject var settings: AppSettings
    @ObservedObject var media: MediaCoordinator

    var body: some View {
        Toggle(
            "Controlar mídia do navegador",
            isOn: Binding(
                get: { settings.browserIntegrationEnabled },
                set: { newValue in
                    settings.browserIntegrationEnabled = newValue
                    Task {
                        media.resetBrowserDiagnostics()
                        await media.refreshAll()
                    }
                }
            )
        )
    }
}

private struct MenuBarIconMenuItem: View {
    @ObservedObject var settings: AppSettings

    var body: some View {
        Toggle(
            "Mostrar ícone na barra de menus",
            isOn: Binding(
                get: { settings.showMenuBarIcon },
                set: { settings.showMenuBarIcon = $0 }
            )
        )
    }
}

private struct ExternalDisplayMenuItem: View {
    @ObservedObject var settings: AppSettings
    let onChange: () -> Void

    var body: some View {
        Toggle(
            "Reduzir em telas externas",
            isOn: Binding(
                get: { settings.minimizeOnExternalDisplays },
                set: { newValue in
                    settings.minimizeOnExternalDisplays = newValue
                    onChange()
                }
            )
        )
    }
}
