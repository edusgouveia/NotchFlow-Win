import Foundation
import Testing
@testable import NotchFlow

@Suite("Calendar events")
struct CalendarEventItemTests {
    @Test("Relevant events remove ended events and respect the limit")
    func relevantEvents() {
        let now = Date(timeIntervalSinceReferenceDate: 1_000)
        let ended = event(id: "ended", start: now.addingTimeInterval(-200), end: now.addingTimeInterval(-100))
        let first = event(id: "first", start: now.addingTimeInterval(100), end: now.addingTimeInterval(200))
        let second = event(id: "second", start: now.addingTimeInterval(300), end: now.addingTimeInterval(400))

        let result = CalendarEventItem.relevantEvents(
            from: [second, ended, first],
            now: now,
            limit: 1
        )

        #expect(result.map(\.id) == ["first"])
    }

    private func event(id: String, start: Date, end: Date) -> CalendarEventItem {
        CalendarEventItem(
            id: id,
            title: id,
            startDate: start,
            endDate: end,
            isAllDay: false,
            calendarTitle: "Test",
            color: CalendarColor(red: 0, green: 0, blue: 1),
            hasRecurrenceRules: false
        )
    }
}
