import AppKit
import SwiftUI

@MainActor
final class NotchWindowController {
    private final class DisplayContext {
        let panel: NotchPanel
        let viewModel: NotchFlowViewModel

        init(panel: NotchPanel, viewModel: NotchFlowViewModel) {
            self.panel = panel
            self.viewModel = viewModel
        }
    }

    private let windowSize = CGSize(width: 628, height: 252)
    private let sharedViewModel: NotchFlowViewModel
    private var displays: [CGDirectDisplayID: DisplayContext] = [:]
    private var screenObserver: NSObjectProtocol?

    init(viewModel: NotchFlowViewModel) {
        sharedViewModel = viewModel
        synchronizeDisplays()

        screenObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.synchronizeDisplays()
            }
        }
    }

    func show() {
        synchronizeDisplays()
        displays.values.forEach { $0.panel.orderFrontRegardless() }
    }

    func toggleExpanded() {
        guard let context = contextForPointerOrMainScreen() else { return }
        context.viewModel.toggleExpanded()
        context.panel.orderFrontRegardless()
    }

    func reposition() {
        synchronizeDisplays()
    }

    private func synchronizeDisplays() {
        let availableScreens = NSScreen.screens.compactMap { screen -> (CGDirectDisplayID, NSScreen)? in
            guard let identifier = displayIdentifier(for: screen) else { return nil }
            return (identifier, screen)
        }
        let availableIdentifiers = Set(availableScreens.map(\.0))

        for identifier in Array(displays.keys) where !availableIdentifiers.contains(identifier) {
            guard let removed = displays.removeValue(forKey: identifier) else { continue }
            removed.panel.orderOut(nil)
            removed.panel.close()
            AppLog.window.info("Janela removida do monitor \(identifier)")
        }

        for (identifier, screen) in availableScreens {
            let context: DisplayContext
            if let existing = displays[identifier] {
                context = existing
            } else {
                context = makeDisplayContext(for: screen)
                displays[identifier] = context
                context.panel.orderFrontRegardless()
                AppLog.window.info("Janela criada para o monitor \(identifier)")
            }

            context.viewModel.updateScreenMetrics(for: screen)
            position(context.panel, on: screen)
        }
    }

    private func makeDisplayContext(for screen: NSScreen) -> DisplayContext {
        let displayViewModel = NotchFlowViewModel(
            media: sharedViewModel.media,
            calendar: sharedViewModel.calendar,
            launchAtLogin: sharedViewModel.launchAtLogin
        )
        displayViewModel.updateScreenMetrics(for: screen)

        let panel = NotchPanel(
            contentRect: NSRect(origin: .zero, size: windowSize),
            styleMask: [.borderless, .nonactivatingPanel, .utilityWindow, .hudWindow],
            backing: .buffered,
            defer: false
        )
        configure(panel)
        panel.contentView = NSHostingView(rootView: NotchView(viewModel: displayViewModel))

        return DisplayContext(panel: panel, viewModel: displayViewModel)
    }

    private func configure(_ panel: NotchPanel) {
        panel.isFloatingPanel = true
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.hidesOnDeactivate = false
        panel.isMovable = false
        panel.isReleasedWhenClosed = false
        panel.level = .mainMenu + 2
        panel.appearance = NSAppearance(named: .darkAqua)
        panel.collectionBehavior = [
            .canJoinAllSpaces,
            .fullScreenAuxiliary,
            .stationary,
            .ignoresCycle
        ]
    }

    private func position(_ panel: NSPanel, on screen: NSScreen) {
        let origin = NSPoint(
            x: screen.frame.midX - windowSize.width / 2,
            y: screen.frame.maxY - windowSize.height
        )
        panel.setFrameOrigin(origin)
    }

    private func contextForPointerOrMainScreen() -> DisplayContext? {
        let pointerLocation = NSEvent.mouseLocation
        let targetScreen = NSScreen.screens.first(where: { $0.frame.contains(pointerLocation) })
            ?? NSScreen.main
            ?? NSScreen.screens.first

        guard let targetScreen,
              let identifier = displayIdentifier(for: targetScreen) else { return nil }
        return displays[identifier]
    }

    private func displayIdentifier(for screen: NSScreen) -> CGDirectDisplayID? {
        let key = NSDeviceDescriptionKey("NSScreenNumber")
        guard let number = screen.deviceDescription[key] as? NSNumber else { return nil }
        return CGDirectDisplayID(number.uint32Value)
    }
}
