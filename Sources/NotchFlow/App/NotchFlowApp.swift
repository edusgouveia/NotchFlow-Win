import AppKit
import SwiftUI

@main
@MainActor
struct NotchFlowApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        MenuBarExtra("NotchFlow", systemImage: "waveform.badge.calendar") {
            Button("Abrir Notch") {
                appDelegate.toggleNotch()
            }

            Button("Atualizar mídia e calendário") {
                Task {
                    await appDelegate.viewModel.media.refreshAll()
                    await appDelegate.viewModel.calendar.refresh()
                }
            }

            LaunchAtLoginMenuItem(service: appDelegate.viewModel.launchAtLogin)

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
            SettingsView(viewModel: appDelegate.viewModel)
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
