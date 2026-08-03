import SwiftUI

struct CalendarPanelView: View {
    @ObservedObject var calendar: CalendarService
    @State private var selectedDate = Calendar.current.startOfDay(for: Date())

    var body: some View {
        Group {
            switch calendar.accessState {
            case .notDetermined:
                permissionState
            case .denied:
                deniedState
            case .authorized:
                monthView
            }
        }
        .onChange(of: calendar.displayedMonth) { _, month in
            selectedDate = Calendar.current.isDate(month, equalTo: Date(), toGranularity: .month)
                ? Calendar.current.startOfDay(for: Date())
                : month
        }
    }

    private var monthView: some View {
        VStack(spacing: 4) {
            monthHeader
            weekdayHeader
            monthGrid
            selectedDaySummary
        }
    }

    private var monthHeader: some View {
        HStack(spacing: 4) {
            monthButton(symbol: "chevron.left", label: "Mês anterior") {
                await calendar.moveMonth(by: -1)
            }

            Button {
                Task { await calendar.returnToCurrentMonth() }
            } label: {
                Text(calendar.displayedMonth.formatted(.dateTime.month(.wide).year()))
                    .font(.caption.weight(.semibold))
                    .lineLimit(1)
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.plain)
            .help("Voltar ao mês atual")

            monthButton(symbol: "chevron.right", label: "Próximo mês") {
                await calendar.moveMonth(by: 1)
            }
        }
        .frame(height: 18)
    }

    private var weekdayHeader: some View {
        HStack(spacing: 2) {
            ForEach(Array(MonthCalendarGrid.weekdaySymbols().enumerated()), id: \.offset) { _, symbol in
                Text(symbol.uppercased())
                    .font(.system(size: 8, weight: .semibold))
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity)
            }
        }
        .frame(height: 10)
    }

    private var monthGrid: some View {
        let days = MonthCalendarGrid.days(in: calendar.displayedMonth)
        let columns = Array(repeating: GridItem(.flexible(), spacing: 2), count: 7)

        return LazyVGrid(columns: columns, spacing: 2) {
            ForEach(Array(days.enumerated()), id: \.offset) { _, date in
                if let date {
                    MonthDayButton(
                        date: date,
                        events: MonthCalendarGrid.events(on: date, from: calendar.events),
                        isSelected: Calendar.current.isDate(date, inSameDayAs: selectedDate)
                    ) {
                        selectedDate = date
                    }
                } else {
                    Color.clear.frame(height: 16)
                }
            }
        }
    }

    @ViewBuilder
    private var selectedDaySummary: some View {
        let events = MonthCalendarGrid.events(on: selectedDate, from: calendar.events)

        if let event = events.first {
            Button {
                calendar.open(event)
            } label: {
                HStack(spacing: 6) {
                    Circle()
                        .fill(color(for: event))
                        .frame(width: 5, height: 5)

                    Text(event.isAllDay ? "Dia todo" : event.startDate.formatted(date: .omitted, time: .shortened))
                        .font(.system(size: 9, weight: .semibold).monospacedDigit())
                        .foregroundStyle(.secondary)

                    Text(event.title)
                        .font(.system(size: 10, weight: .medium))
                        .lineLimit(1)

                    Spacer(minLength: 0)

                    if events.count > 1 {
                        Text("+\(events.count - 1)")
                            .font(.system(size: 9, weight: .semibold))
                            .foregroundStyle(.secondary)
                    }
                }
                .padding(.horizontal, 7)
                .frame(height: 24)
                .background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 7))
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .help("Abrir no Calendário")
        } else {
            HStack(spacing: 5) {
                Image(systemName: "checkmark")
                    .font(.system(size: 8, weight: .bold))
                    .foregroundStyle(.green)
                Text("Sem eventos neste dia")
                    .font(.system(size: 10))
                    .foregroundStyle(.secondary)
                Spacer(minLength: 0)
            }
            .padding(.horizontal, 7)
            .frame(height: 24)
            .background(.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 7))
        }
    }

    private var permissionState: some View {
        VStack(alignment: .leading, spacing: 9) {
            Label("Calendário", systemImage: "calendar")
                .font(.headline)
            Text("Veja o mês e seus compromissos diretamente no notch.")
                .font(.caption)
                .foregroundStyle(.secondary)
            Button("Permitir calendário") {
                Task { await calendar.requestAccess() }
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var deniedState: some View {
        VStack(alignment: .leading, spacing: 9) {
            Label("Calendário", systemImage: "calendar.badge.exclamationmark")
                .font(.headline)
            Text("O acesso ao calendário está desativado.")
                .font(.caption)
                .foregroundStyle(.secondary)
            Button("Abrir Ajustes") {
                calendar.openSystemSettings()
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private func monthButton(
        symbol: String,
        label: String,
        action: @escaping @MainActor () async -> Void
    ) -> some View {
        Button {
            Task { await action() }
        } label: {
            Image(systemName: symbol)
                .font(.system(size: 9, weight: .bold))
                .frame(width: 18, height: 18)
                .background(.white.opacity(0.07), in: Circle())
        }
        .buttonStyle(.plain)
        .help(label)
    }

    private func color(for event: CalendarEventItem) -> Color {
        Color(red: event.color.red, green: event.color.green, blue: event.color.blue)
    }
}

private struct MonthDayButton: View {
    let date: Date
    let events: [CalendarEventItem]
    let isSelected: Bool
    let action: () -> Void

    private var isToday: Bool { Calendar.current.isDateInToday(date) }

    var body: some View {
        Button(action: action) {
            ZStack(alignment: .bottom) {
                Circle()
                    .fill(isSelected ? Color.white : Color.clear)
                    .frame(width: 16, height: 16)

                Text(date.formatted(.dateTime.day()))
                    .font(.system(size: 9, weight: isToday || isSelected ? .bold : .regular).monospacedDigit())
                    .foregroundStyle(isSelected ? .black : (isToday ? .green : .white))
                    .frame(width: 16, height: 14, alignment: .top)

                if !events.isEmpty {
                    HStack(spacing: 1) {
                        ForEach(Array(events.prefix(3))) { event in
                            Circle()
                                .fill(Color(
                                    red: event.color.red,
                                    green: event.color.green,
                                    blue: event.color.blue
                                ))
                                .frame(width: 2.5, height: 2.5)
                        }
                    }
                    .padding(.bottom, 1)
                }
            }
            .frame(maxWidth: .infinity, minHeight: 16, maxHeight: 16)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(date.formatted(date: .complete, time: .omitted))
        .accessibilityValue(events.isEmpty ? "Sem eventos" : "\(events.count) eventos")
    }
}
