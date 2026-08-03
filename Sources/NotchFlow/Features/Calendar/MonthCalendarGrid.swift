import Foundation

enum MonthCalendarGrid {
    static func days(in month: Date, calendar: Calendar = .current) -> [Date?] {
        guard let monthInterval = calendar.dateInterval(of: .month, for: month),
              let dayRange = calendar.range(of: .day, in: .month, for: month) else {
            return []
        }

        let firstDay = monthInterval.start
        let weekday = calendar.component(.weekday, from: firstDay)
        let leadingEmptyDays = (weekday - calendar.firstWeekday + 7) % 7

        var result = Array<Date?>(repeating: nil, count: leadingEmptyDays)
        result.append(contentsOf: dayRange.compactMap { day in
            calendar.date(byAdding: .day, value: day - 1, to: firstDay)
        })

        let visibleCellCount = max(35, Int(ceil(Double(result.count) / 7.0)) * 7)
        result.append(contentsOf: Array(repeating: nil, count: visibleCellCount - result.count))
        return result
    }

    static func events(
        on date: Date,
        from events: [CalendarEventItem],
        calendar: Calendar = .current
    ) -> [CalendarEventItem] {
        let start = calendar.startOfDay(for: date)
        guard let end = calendar.date(byAdding: .day, value: 1, to: start) else { return [] }

        return events
            .filter { $0.startDate < end && $0.endDate > start }
            .sorted { lhs, rhs in
                if lhs.isAllDay != rhs.isAllDay { return lhs.isAllDay }
                return lhs.startDate < rhs.startDate
            }
    }

    static func weekdaySymbols(calendar: Calendar = .current) -> [String] {
        let symbols = calendar.veryShortStandaloneWeekdaySymbols
        guard symbols.count == 7 else { return symbols }
        let firstIndex = max(0, min(6, calendar.firstWeekday - 1))
        return Array(symbols[firstIndex...]) + Array(symbols[..<firstIndex])
    }
}
