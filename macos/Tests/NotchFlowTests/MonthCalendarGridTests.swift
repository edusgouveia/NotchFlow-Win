import Foundation
import Testing
@testable import NotchFlow

@Suite("Month calendar grid")
struct MonthCalendarGridTests {
    @Test("Grid includes every day and complete weeks")
    func completeMonthGrid() throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try #require(TimeZone(secondsFromGMT: 0))
        calendar.firstWeekday = 1
        let month = try #require(calendar.date(from: DateComponents(year: 2026, month: 8, day: 1)))

        let days = MonthCalendarGrid.days(in: month, calendar: calendar)
        let actualDates = days.compactMap { $0 }

        #expect(days.count.isMultiple(of: 7))
        #expect(actualDates.count == 31)
        #expect(calendar.component(.day, from: try #require(actualDates.first)) == 1)
        #expect(calendar.component(.day, from: try #require(actualDates.last)) == 31)
    }

    @Test("Events are grouped by overlapping day")
    func eventsForDay() throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try #require(TimeZone(secondsFromGMT: 0))
        let day = try #require(calendar.date(from: DateComponents(year: 2026, month: 8, day: 3)))
        let matching = makeEvent(
            id: "matching",
            start: day.addingTimeInterval(3_600),
            end: day.addingTimeInterval(7_200)
        )
        let other = makeEvent(
            id: "other",
            start: day.addingTimeInterval(90_000),
            end: day.addingTimeInterval(93_600)
        )

        let result = MonthCalendarGrid.events(on: day, from: [other, matching], calendar: calendar)

        #expect(result.map(\.id) == ["matching"])
    }

    private func makeEvent(id: String, start: Date, end: Date) -> CalendarEventItem {
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
