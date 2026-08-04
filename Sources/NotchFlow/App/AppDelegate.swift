import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let viewModel = NotchFlowViewModel()
    private var notchWindowController: NotchWindowController?
    private var statusItem: NSStatusItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApplication.shared.setActivationPolicy(.accessory)
        viewModel.start()

        let controller = NotchWindowController(viewModel: viewModel)
        notchWindowController = controller
        controller.show()

        viewModel.settings.showMenuBarIconDidChange = { [weak self] isVisible in
            self?.setMenuBarIconVisible(isVisible)
        }
        setMenuBarIconVisible(viewModel.settings.showMenuBarIcon)

        AppLog.lifecycle.info("NotchFlow iniciado")
    }

    func applicationWillTerminate(_ notification: Notification) {
        notchWindowController?.stop()
        viewModel.stop()
    }

    func toggleNotch() {
        notchWindowController?.toggleExpanded()
    }

    func showNotch() {
        notchWindowController?.show()
    }

    /// Reaplica preferências que mudam o tamanho ou a posição das ilhas.
    func refreshLayout() {
        notchWindowController?.applySettings()
    }

    private func setMenuBarIconVisible(_ isVisible: Bool) {
        if isVisible {
            guard statusItem == nil else { return }

            let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
            item.button?.image = NSImage(
                systemSymbolName: "waveform",
                accessibilityDescription: "NotchFlow"
            )
            item.menu = makeStatusMenu()
            statusItem = item
        } else if let statusItem {
            NSStatusBar.system.removeStatusItem(statusItem)
            self.statusItem = nil
        }
    }

    private func makeStatusMenu() -> NSMenu {
        let menu = NSMenu(title: "NotchFlow")
        menu.addItem(withTitle: "Abrir Notch", action: #selector(openNotchFromMenu), keyEquivalent: "")
        menu.addItem(
            withTitle: "Atualizar mídia e calendário",
            action: #selector(refreshFromMenu),
            keyEquivalent: ""
        )
        menu.addItem(.separator())
        menu.addItem(withTitle: "Ajustes…", action: #selector(openSettingsFromMenu), keyEquivalent: ",")
        menu.addItem(withTitle: "Ocultar ícone", action: #selector(hideStatusItem), keyEquivalent: "")
        menu.addItem(.separator())
        menu.addItem(withTitle: "Sair do NotchFlow", action: #selector(quitFromMenu), keyEquivalent: "q")

        for item in menu.items {
            item.target = self
        }
        return menu
    }

    @objc
    private func openNotchFromMenu() {
        toggleNotch()
    }

    @objc
    private func refreshFromMenu() {
        Task {
            viewModel.media.resetBrowserDiagnostics()
            await viewModel.media.refreshAll()
            await viewModel.calendar.refresh()
        }
    }

    @objc
    private func openSettingsFromMenu() {
        NSApplication.shared.activate(ignoringOtherApps: true)
        NSApplication.shared.sendAction(
            Selector(("showSettingsWindow:")),
            to: nil,
            from: nil
        )
    }

    @objc
    private func hideStatusItem() {
        viewModel.settings.showMenuBarIcon = false
    }

    @objc
    private func quitFromMenu() {
        NSApplication.shared.terminate(nil)
    }
}
