import AppKit
import Combine

@MainActor
final class NotchFlowViewModel: ObservableObject {
    @Published var isExpanded = false
    @Published private(set) var closedSize = CGSize(width: 156, height: 28)

    let media: MediaCoordinator
    let calendar: CalendarService
    let launchAtLogin: LaunchAtLoginService

    init(
        media: MediaCoordinator = MediaCoordinator(),
        calendar: CalendarService = CalendarService(),
        launchAtLogin: LaunchAtLoginService = LaunchAtLoginService()
    ) {
        self.media = media
        self.calendar = calendar
        self.launchAtLogin = launchAtLogin
    }

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
        isExpanded.toggle()
    }

    func updateScreenMetrics(for screen: NSScreen? = NSScreen.main) {
        guard let screen else { return }

        var width: CGFloat = 156
        if let leftArea = screen.auxiliaryTopLeftArea,
           let rightArea = screen.auxiliaryTopRightArea {
            let hardwareNotchWidth = screen.frame.width - leftArea.width - rightArea.width
            width = min(max(hardwareNotchWidth, 150), 172)
        }

        let height: CGFloat
        if screen.safeAreaInsets.top > 0 {
            height = min(max(screen.safeAreaInsets.top, 28), 32)
        } else {
            height = min(max(screen.frame.maxY - screen.visibleFrame.maxY, 28), 32)
        }

        closedSize = CGSize(width: width, height: height)
    }
}
