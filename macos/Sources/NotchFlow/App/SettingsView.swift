import SwiftUI

struct SettingsView: View {
    let viewModel: NotchFlowViewModel
    let onLayoutChange: () -> Void

    var body: some View {
        TabView {
            GeneralSettingsView(
                launchAtLogin: viewModel.launchAtLogin,
                settings: viewModel.settings,
                onLayoutChange: onLayoutChange
            )
            .tabItem { Label("Geral", systemImage: "gearshape") }

            MediaSettingsView(media: viewModel.media, settings: viewModel.settings)
                .tabItem { Label("Mídia", systemImage: "music.note") }

            CalendarSettingsView(calendar: viewModel.calendar)
                .tabItem { Label("Calendário", systemImage: "calendar") }
        }
        .padding(20)
        .frame(width: 500, height: 330)
    }
}

private struct GeneralSettingsView: View {
    @ObservedObject var launchAtLogin: LaunchAtLoginService
    @ObservedObject var settings: AppSettings
    let onLayoutChange: () -> Void

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

            Section {
                Toggle(
                    "Mostrar ícone na barra de menus",
                    isOn: Binding(
                        get: { settings.showMenuBarIcon },
                        set: { settings.showMenuBarIcon = $0 }
                    )
                )

                Text("Com o ícone oculto, todas as opções continuam no menu que abre ao clicar com o botão direito na ilha.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } header: {
                Text("Barra de menus")
            }

            Section {
                Toggle(
                    "Reduzir a ilha em telas externas",
                    isOn: Binding(
                        get: { settings.minimizeOnExternalDisplays },
                        set: { newValue in
                            settings.minimizeOnExternalDisplays = newValue
                            onLayoutChange()
                        }
                    )
                )

                Text("Em monitores sem notch fica apenas uma tira fina no topo, que abre o painel ao passar o mouse.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } header: {
                Text("Telas")
            }
        }
        .formStyle(.grouped)
        .onAppear { launchAtLogin.refreshStatus() }
    }
}

private struct MediaSettingsView: View {
    @ObservedObject var media: MediaCoordinator
    @ObservedObject var settings: AppSettings

    var body: some View {
        Form {
            Section {
                LabeledContent("Fonte atual") {
                    Text(media.currentSnapshot?.sourceLabel ?? "Nenhuma")
                }

                if let error = media.errorMessage {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                }

                Button("Atualizar agora") {
                    Task {
                        media.resetBrowserDiagnostics()
                        await media.refreshAll()
                    }
                }
            } header: {
                Text("Reprodução")
            }

            Section {
                Toggle(
                    "Controlar a mídia do navegador",
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

                if let hint = media.browserHint {
                    Text(hint)
                        .font(.caption)
                        .foregroundStyle(.orange)
                } else {
                    Text("Funciona com YouTube, YouTube Music, SoundCloud, Spotify Web e outros sites, no Chrome, Brave, Edge, Arc, Vivaldi, Opera e Safari.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Text("O navegador precisa permitir JavaScript por Apple Events: no Chrome e derivados em Visualizar, Desenvolvedor; no Safari em Desenvolvedor.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } header: {
                Text("Navegador")
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

                if calendar.accessWasGrantedBefore {
                    Text("Você já autorizou o calendário antes. O macOS volta a pedir quando o aplicativo é assinado de novo, o que acontece a cada build com assinatura ad hoc. Assinar com um certificado fixo resolve de forma definitiva.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
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
