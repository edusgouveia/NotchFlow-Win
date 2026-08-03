import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let viewModel = NotchFlowViewModel()
    private var notchWindowController: NotchWindowController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApplication.shared.setActivationPolicy(.accessory)
        viewModel.start()

        let controller = NotchWindowController(viewModel: viewModel)
        notchWindowController = controller
        controller.show()

        AppLog.lifecycle.info("NotchFlow iniciado")
    }

    func applicationWillTerminate(_ notification: Notification) {
        viewModel.stop()
    }

    func toggleNotch() {
        notchWindowController?.toggleExpanded()
    }

    func showNotch() {
        notchWindowController?.show()
    }
}
