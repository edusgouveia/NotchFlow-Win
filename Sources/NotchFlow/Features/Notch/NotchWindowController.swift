import AppKit
import SwiftUI

@MainActor
final class NotchWindowController: NSObject {
    @MainActor
    private final class DisplayContext {
        let panel: NotchPanel
        let viewModel: NotchFlowViewModel
        private var pointerTask: Task<Void, Never>?
        private var isPointerInside = false

        init(panel: NotchPanel, viewModel: NotchFlowViewModel) {
            self.panel = panel
            self.viewModel = viewModel
        }

        func updatePointerState(_ isInside: Bool) {
            guard isInside != isPointerInside else { return }

            isPointerInside = isInside
            pointerTask?.cancel()
            pointerTask = Task { @MainActor [weak self] in
                let delay: Duration = isInside ? .milliseconds(55) : .milliseconds(300)
                try? await Task.sleep(for: delay)
                guard !Task.isCancelled, let self, self.isPointerInside == isInside else { return }
                self.viewModel.setExpanded(isInside)
            }
        }

        func stopPointerTracking() {
            pointerTask?.cancel()
            pointerTask = nil
        }
    }

    private let windowSize = NotchGeometry.windowSize
    private let sharedViewModel: NotchFlowViewModel
    private var displays: [CGDirectDisplayID: DisplayContext] = [:]
    private var screenObserver: NSObjectProtocol?
    private var pointerMonitorTimer: Timer?

    init(viewModel: NotchFlowViewModel) {
        sharedViewModel = viewModel
        super.init()
        synchronizeDisplays()
        startPointerTracking()

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

    /// Reaplica as preferências, por exemplo quando o usuário muda o modo das telas externas.
    func applySettings() {
        synchronizeDisplays()
    }

    func stop() {
        pointerMonitorTimer?.invalidate()
        pointerMonitorTimer = nil

        if let screenObserver {
            NotificationCenter.default.removeObserver(screenObserver)
            self.screenObserver = nil
        }

        displays.values.forEach { $0.stopPointerTracking() }
    }

    private func synchronizeDisplays() {
        let availableScreens = NSScreen.screens.compactMap { screen -> (CGDirectDisplayID, NSScreen)? in
            guard let identifier = displayIdentifier(for: screen) else { return nil }
            return (identifier, screen)
        }
        let availableIdentifiers = Set(availableScreens.map(\.0))

        for identifier in Array(displays.keys) where !availableIdentifiers.contains(identifier) {
            guard let removed = displays.removeValue(forKey: identifier) else { continue }
            removed.stopPointerTracking()
            removed.panel.orderOut(nil)
            removed.panel.close()
            AppLog.window.info("Janela removida do monitor \(identifier)")
        }

        for (identifier, screen) in availableScreens {
            let kind = displayKind(for: identifier)
            let context: DisplayContext
            if let existing = displays[identifier] {
                context = existing
            } else {
                context = makeDisplayContext(for: screen, displayKind: kind)
                displays[identifier] = context
                context.panel.orderFrontRegardless()
                AppLog.window.info("Janela criada para o monitor \(identifier)")
            }

            context.viewModel.updateScreenMetrics(for: screen, displayKind: kind)
            position(context.panel, on: screen)
        }
    }

    private func makeDisplayContext(
        for screen: NSScreen,
        displayKind: NotchGeometry.DisplayKind
    ) -> DisplayContext {
        let displayViewModel = NotchFlowViewModel(
            media: sharedViewModel.media,
            calendar: sharedViewModel.calendar,
            launchAtLogin: sharedViewModel.launchAtLogin,
            settings: sharedViewModel.settings
        )
        displayViewModel.updateScreenMetrics(for: screen, displayKind: displayKind)

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

    /// O painel é transparente e não ativa o aplicativo. Nessas condições o `onHover`
    /// do SwiftUI pode não receber eventos em algumas combinações de monitor e macOS.
    /// Consultar a posição global do ponteiro evita permissões extras e funciona em
    /// qualquer tela, inclusive quando outro aplicativo está em primeiro plano.
    private func startPointerTracking() {
        pointerMonitorTimer?.invalidate()
        AppLog.window.info("Monitor nativo do ponteiro iniciado")
        let timer = Timer(
            timeInterval: 0.06,
            target: self,
            selector: #selector(handlePointerTimer(_:)),
            userInfo: nil,
            repeats: true
        )
        RunLoop.main.add(timer, forMode: .common)
        pointerMonitorTimer = timer
    }

    @objc
    private func handlePointerTimer(_ timer: Timer) {
        updatePointerInteractions()
    }

    private func updatePointerInteractions() {
        let pointerLocation = NSEvent.mouseLocation

        for context in displays.values {
            let geometry = context.viewModel.geometry
            let interactionSize = context.viewModel.isExpanded
                ? geometry.expandedSize
                : geometry.closedInteractionSize
            let interactionRect = NotchGeometry.interactionRect(
                in: context.panel.frame,
                size: interactionSize
            )
            context.updatePointerState(interactionRect.contains(pointerLocation))
        }
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

    private func displayKind(for identifier: CGDirectDisplayID) -> NotchGeometry.DisplayKind {
        CGDisplayIsBuiltin(identifier) != 0 ? .builtIn : .external
    }
}
