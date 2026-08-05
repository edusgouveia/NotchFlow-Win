import SwiftUI

@main
@MainActor
struct NotchFlowApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        Settings {
            SettingsView(viewModel: appDelegate.viewModel) {
                appDelegate.refreshLayout()
            }
        }
    }
}
