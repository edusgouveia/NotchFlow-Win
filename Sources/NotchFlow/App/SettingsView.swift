import SwiftUI

struct SettingsView: View {
    let viewModel: NotchFlowViewModel

    var body: some View {
        TabView {
            GeneralSettingsView(launchAtLogin: viewModel.launchAtLogin)
                .tabItem { Label("Geral", systemImage: "gearshape") }

            MediaSettingsView(media: viewModel.media)
                .tabItem { Label("Mídia", systemImage: "music.note") }

            CalendarSettingsView(calendar: viewModel.calendar)
                .tabItem { Label("Calendário", systemImage: "calendar") }
        }
        .padding(20)
        .frame(width: 480, height: 260)
    }
}

private struct GeneralSettingsView: View {
    @ObservedObject var launchAtLogin: LaunchAtLoginService

    var body: some View {
        Form {
            Section {
                Toggle(
                    "Abrir o NotchFlow ao iniciar o Mac",
                    isOn: Binding(
                        get: { launchAtLogin.isEnabled },
                        set: { launchAtLogin.setEnabled($0) }
                    )
                )
                .disabled(!launchAtLogin.canBeConfigured)

                if !launchAtLogin.canBeConfigured {
                    Text("Essa opção estará disponível no NotchFlow.app instalado.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                } else if launchAtLogin.requiresApproval {
                    Text("O macOS precisa da sua aprovação para abrir o NotchFlow automaticamente.")
                        .font(.caption)
                        .foregroundStyle(.orange)

                    Button("Abrir Itens de Início") {
                        launchAtLogin.openSystemSettings()
                    }
                }

                if let error = launchAtLogin.errorMessage {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                }
            } header: {
                Text("Inicialização")
            }
        }
        .formStyle(.grouped)
        .onAppear { launchAtLogin.refreshStatus() }
    }
}

private struct MediaSettingsView: View {
    @ObservedObject var media: MediaCoordinator

    var body: some View {
        Form {
            LabeledContent("Fonte atual") {
                Text(media.currentSnapshot?.source.displayName ?? "Nenhuma")
            }

            if let error = media.errorMessage {
                Text(error)
                    .foregroundStyle(.red)
            }

            Button("Atualizar agora") {
                Task { await media.refreshAll() }
            }
        }
        .formStyle(.grouped)
    }
}

private struct CalendarSettingsView: View {
    @ObservedObject var calendar: CalendarService

    var body: some View {
        Form {
            LabeledContent("Permissão") {
                Text(calendar.accessState.displayText)
            }

            switch calendar.accessState {
            case .notDetermined:
                Button("Permitir acesso") {
                    Task { await calendar.requestAccess() }
                }
            case .denied:
                Button("Abrir Ajustes do Sistema") {
                    calendar.openSystemSettings()
                }
            case .authorized:
                Button("Atualizar eventos") {
                    Task { await calendar.refresh() }
                }
            }
        }
        .formStyle(.grouped)
    }
}
