import Foundation

struct CalendarColor: Equatable, Sendable {
    let red: Double
    let green: Double
    let blue: Double
}

struct CalendarEventItem: Identifiable, Equatable, Sendable {
    let id: String
    let title: String
    let startDate: Date
    let endDate: Date
    let isAllDay: Bool
    let calendarTitle: String
    let color: CalendarColor
    let hasRecurrenceRules: Bool

    var isInProgress: Bool {
        let now = Date()
        return startDate <= now && endDate > now
    }

    static func relevantEvents(from events: [CalendarEventItem], now: Date = Date(), limit: Int = 4) -> [CalendarEventItem] {
        Array(
            events
                .filter { $0.isAllDay || $0.endDate > now }
                .sorted {
                    if $0.isAllDay != $1.isAllDay { return $0.isAllDay }
                    return $0.startDate < $1.startDate
                }
                .prefix(limit)
        )
    }
}
