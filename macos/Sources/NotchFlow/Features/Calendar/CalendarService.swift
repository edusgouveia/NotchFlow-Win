import AppKit
import Combine
@preconcurrency import EventKit
import Foundation

enum CalendarAccessState: Equatable {
    case notDetermined
    case authorized
    case denied

    var displayText: String {
        switch self {
        case .notDetermined: "Acesso ainda não solicitado"
        case .authorized: "Calendário autorizado"
        case .denied: "Acesso ao calendário negado"
        }
    }
}

@MainActor
final class CalendarService: ObservableObject {
    @Published private(set) var events: [CalendarEventItem] = []
    @Published private(set) var accessState: CalendarAccessState = .notDetermined
    @Published private(set) var errorMessage: String?
    @Published private(set) var displayedMonth: Date

    private let eventStore: EKEventStore
    private let settings: AppSettings
    private var storeObserver: NSObjectProtocol?
    private var hasRequestedAccessThisLaunch = false

    init(eventStore: EKEventStore = EKEventStore(), settings: AppSettings = .shared) {
        self.eventStore = eventStore
        self.settings = settings
        self.displayedMonth = Calendar.current.dateInterval(of: .month, for: Date())?.start ?? Date()
        updateAccessState()
    }

    /// O acesso já foi concedido alguma vez neste Mac. Serve para explicar um novo pedido do sistema.
    var accessWasGrantedBefore: Bool { settings.calendarAccessGrantedOnce }

    func start() {
        guard storeObserver == nil else { return }

        storeObserver = NotificationCenter.default.addObserver(
            forName: .EKEventStoreChanged,
            object: eventStore,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                await self?.refresh()
            }
        }

        Task {
            await requestAccessIfNeeded()
            await refresh()
        }
    }

    func stop() {
        if let storeObserver {
            NotificationCenter.default.removeObserver(storeObserver)
            self.storeObserver = nil
        }
    }

    /// Pede a permissão uma única vez por sessão, sem exigir um clique no painel.
    private func requestAccessIfNeeded() async {
        updateAccessState()
        guard accessState == .notDetermined, !hasRequestedAccessThisLaunch else { return }
        hasRequestedAccessThisLaunch = true
        await requestAccess()
    }

    func requestAccess() async {
        do {
            let granted = try await eventStore.requestFullAccessToEvents()
            accessState = granted ? .authorized : .denied
            if granted {
                settings.calendarAccessGrantedOnce = true
            }
            if granted {
                eventStore.reset()
                await refresh()
            }
        } catch {
            accessState = .denied
            errorMessage = error.localizedDescription
            AppLog.calendar.error("Falha ao solicitar acesso ao calendário")
        }
    }

    func refresh() async {
        updateAccessState()
        guard accessState == .authorized else {
            events = []
            return
        }

        let calendar = Calendar.current
        guard let monthInterval = calendar.dateInterval(of: .month, for: displayedMonth) else { return }

        let predicate = eventStore.predicateForEvents(
            withStart: monthInterval.start,
            end: monthInterval.end,
            calendars: nil
        )
        let fetched = eventStore.events(matching: predicate)
        events = fetched.compactMap(Self.mapEvent).sorted { lhs, rhs in
            if lhs.isAllDay != rhs.isAllDay { return lhs.isAllDay }
            return lhs.startDate < rhs.startDate
        }
        errorMessage = nil
    }

    func moveMonth(by value: Int) async {
        guard let month = Calendar.current.date(byAdding: .month, value: value, to: displayedMonth) else {
            return
        }
        displayedMonth = Calendar.current.dateInterval(of: .month, for: month)?.start ?? month
        await refresh()
    }

    func returnToCurrentMonth() async {
        displayedMonth = Calendar.current.dateInterval(of: .month, for: Date())?.start ?? Date()
        await refresh()
    }

    func open(_ event: CalendarEventItem) {
        guard let escapedID = event.id.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) else {
            openCalendarApplication()
            return
        }

        let dateSuffix: String
        if event.hasRecurrenceRules {
            let formatter = ISO8601DateFormatter()
            dateSuffix = "/\(formatter.string(from: event.startDate))"
        } else {
            dateSuffix = ""
        }

        if let url = URL(string: "ical://ekevent\(dateSuffix)/\(escapedID)?method=show&options=more") {
            NSWorkspace.shared.open(url)
        } else {
            openCalendarApplication()
        }
    }

    func openSystemSettings() {
        guard let url = URL(
            string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Calendars"
        ) else { return }
        NSWorkspace.shared.open(url)
    }

    private func updateAccessState() {
        switch EKEventStore.authorizationStatus(for: .event) {
        case .fullAccess, .authorized:
            accessState = .authorized
            if !settings.calendarAccessGrantedOnce {
                settings.calendarAccessGrantedOnce = true
            }
        case .notDetermined:
            accessState = .notDetermined
        case .denied, .restricted, .writeOnly:
            accessState = .denied
        @unknown default:
            accessState = .denied
        }
    }

    private func openCalendarApplication() {
        guard let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.apple.iCal") else {
            return
        }
        NSWorkspace.shared.openApplication(at: url, configuration: .init())
    }

    private static func mapEvent(_ event: EKEvent) -> CalendarEventItem? {
        let identifier = event.calendarItemIdentifier
        guard let startDate = event.startDate,
              let endDate = event.endDate,
              let eventCalendar = event.calendar else { return nil }

        let nsColor = NSColor(cgColor: eventCalendar.cgColor) ?? .systemBlue
        let rgbColor = nsColor.usingColorSpace(.sRGB) ?? .systemBlue

        return CalendarEventItem(
            id: identifier,
            title: event.title?.isEmpty == false ? event.title : "Evento sem título",
            startDate: startDate,
            endDate: endDate,
            isAllDay: event.isAllDay,
            calendarTitle: eventCalendar.title,
            color: CalendarColor(
                red: Double(rgbColor.redComponent),
                green: Double(rgbColor.greenComponent),
                blue: Double(rgbColor.blueComponent)
            ),
            hasRecurrenceRules: !(event.recurrenceRules?.isEmpty ?? true)
        )
    }
}
