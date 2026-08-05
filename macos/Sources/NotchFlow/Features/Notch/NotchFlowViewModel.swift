import AppKit
import Combine
import SwiftUI

/// Tempos usados na abertura e no fechamento da ilha.
enum NotchAnimation {
    static let shape = Animation.smooth(duration: 0.28, extraBounce: 0)
    static let contentIn = Animation.easeOut(duration: 0.12)
    static let contentOut = Animation.easeIn(duration: 0.07)

    /// Espera até a forma estar praticamente aberta antes de mostrar o conteúdo.
    static let contentInDelay: Duration = .milliseconds(110)

    /// Deixa o conteúdo desaparecer antes de a forma voltar ao tamanho fechado.
    static let shapeCloseDelay: Duration = .milliseconds(70)
}

@MainActor
final class NotchFlowViewModel: ObservableObject {
    private struct ScreenMetrics: Equatable {
        let displayKind: NotchGeometry.DisplayKind
        let hardwareNotchWidth: CGFloat?
        let menuBarHeight: CGFloat
    }

    @Published private(set) var isExpanded = false

    /// Controla o interior separadamente da forma, para as animações não se atropelarem.
    @Published private(set) var showsContent = false

    @Published private(set) var geometry: NotchGeometry

    let media: MediaCoordinator
    let calendar: CalendarService
    let launchAtLogin: LaunchAtLoginService
    let settings: AppSettings

    private var screenMetrics: ScreenMetrics?
    private var contentTask: Task<Void, Never>?

    init(
        media: MediaCoordinator = MediaCoordinator(),
        calendar: CalendarService = CalendarService(),
        launchAtLogin: LaunchAtLoginService = LaunchAtLoginService(),
        settings: AppSettings = .shared
    ) {
        self.media = media
        self.calendar = calendar
        self.launchAtLogin = launchAtLogin
        self.settings = settings
        geometry = NotchGeometry.make(
            displayKind: .builtIn,
            hardwareNotchWidth: nil,
            menuBarHeight: 30,
            minimizeOnExternalDisplays: settings.minimizeOnExternalDisplays
        )
    }

    var closedSize: CGSize { geometry.closedSize }
    var expandedSize: CGSize { geometry.expandedSize }

    func start() {
        media.start()
        calendar.start()
        updateScreenMetrics()
    }

    func stop() {
        media.stop()
        calendar.stop()
    }

    func toggleExpanded() {
        setExpanded(!isExpanded)
    }

    /// Abre em duas etapas: primeiro a forma cresce, depois o conteúdo aparece.
    /// Ao fechar, o conteúdo sai primeiro e a forma encolhe em seguida.
    func setExpanded(_ expanded: Bool) {
        contentTask?.cancel()

        guard expanded != isExpanded || expanded != showsContent else { return }

        if expanded {
            isExpanded = true
            contentTask = Task { @MainActor [weak self] in
                try? await Task.sleep(for: NotchAnimation.contentInDelay)
                guard !Task.isCancelled, let self else { return }
                withAnimation(NotchAnimation.contentIn) {
                    self.showsContent = true
                }
            }
        } else {
            withAnimation(NotchAnimation.contentOut) {
                showsContent = false
            }
            contentTask = Task { @MainActor [weak self] in
                try? await Task.sleep(for: NotchAnimation.shapeCloseDelay)
                guard !Task.isCancelled, let self else { return }
                self.isExpanded = false
            }
        }
    }

    func updateScreenMetrics(
        for screen: NSScreen? = NSScreen.main,
        displayKind: NotchGeometry.DisplayKind = .builtIn
    ) {
        guard let screen else { return }

        var hardwareNotchWidth: CGFloat?
        if let leftArea = screen.auxiliaryTopLeftArea,
           let rightArea = screen.auxiliaryTopRightArea {
            hardwareNotchWidth = screen.frame.width - leftArea.width - rightArea.width
        }

        let menuBarHeight: CGFloat
        if screen.safeAreaInsets.top > 0 {
            menuBarHeight = screen.safeAreaInsets.top
        } else {
            menuBarHeight = screen.frame.maxY - screen.visibleFrame.maxY
        }

        screenMetrics = ScreenMetrics(
            displayKind: displayKind,
            hardwareNotchWidth: hardwareNotchWidth,
            menuBarHeight: menuBarHeight
        )
        applyGeometry()
    }

    /// Recalcula a geometria depois de uma mudança nas preferências.
    func applyGeometry() {
        let metrics = screenMetrics ?? ScreenMetrics(
            displayKind: .builtIn,
            hardwareNotchWidth: nil,
            menuBarHeight: 30
        )

        let updated = NotchGeometry.make(
            displayKind: metrics.displayKind,
            hardwareNotchWidth: metrics.hardwareNotchWidth,
            menuBarHeight: metrics.menuBarHeight,
            minimizeOnExternalDisplays: settings.minimizeOnExternalDisplays
        )

        guard updated != geometry else { return }
        geometry = updated
        if updated.isMinimized {
            setExpanded(false)
        }
    }
}
