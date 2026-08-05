import SwiftUI

struct CalendarPanelView: View {
    @ObservedObject var calendar: CalendarService
    @State private var selectedDate = Calendar.current.startOfDay(for: Date())

    private static let dayCellHeight: CGFloat = 14
    private static let dayCellSpacing: CGFloat = 1

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
        VStack(spacing: 2) {
            monthHeader
            weekdayHeader
            monthGrid
            Spacer(minLength: 0)
            selectedDaySummary
        }
    }

    private var monthHeader: some View {
        HStack(spacing: 3) {
            monthButton(symbol: "chevron.left", label: "Mês anterior") {
                await calendar.moveMonth(by: -1)
            }

            Button {
                Task { await calendar.returnToCurrentMonth() }
            } label: {
                Text(calendar.displayedMonth.formatted(.dateTime.month(.wide).year()))
                    .font(.system(size: 9.5, weight: .semibold))
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.plain)
            .help("Voltar ao mês atual")

            monthButton(symbol: "chevron.right", label: "Próximo mês") {
                await calendar.moveMonth(by: 1)
            }
        }
        .frame(height: 15)
    }

    private var weekdayHeader: some View {
        HStack(spacing: Self.dayCellSpacing) {
            ForEach(Array(MonthCalendarGrid.weekdaySymbols().enumerated()), id: \.offset) { _, symbol in
                Text(symbol.uppercased())
                    .font(.system(size: 7, weight: .semibold))
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity)
            }
        }
        .frame(height: 8)
    }

    private var monthGrid: some View {
        let days = MonthCalendarGrid.days(in: calendar.displayedMonth)
        let columns = Array(
            repeating: GridItem(.flexible(), spacing: Self.dayCellSpacing),
            count: 7
        )

        return LazyVGrid(columns: columns, spacing: Self.dayCellSpacing) {
            ForEach(Array(days.enumerated()), id: \.offset) { _, date in
                if let date {
                    MonthDayButton(
                        date: date,
                        events: MonthCalendarGrid.events(on: date, from: calendar.events),
                        isSelected: Calendar.current.isDate(date, inSameDayAs: selectedDate),
                        height: Self.dayCellHeight
                    ) {
                        selectedDate = date
                    }
                } else {
                    Color.clear.frame(height: Self.dayCellHeight)
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
                HStack(spacing: 5) {
                    Circle()
                        .fill(color(for: event))
                        .frame(width: 4, height: 4)

                    Text(event.isAllDay ? "Dia todo" : event.startDate.formatted(date: .omitted, time: .shortened))
                        .font(.system(size: 8.5, weight: .semibold).monospacedDigit())
                        .foregroundStyle(.secondary)

                    Text(event.title)
                        .font(.system(size: 9.5, weight: .medium))
                        .lineLimit(1)

                    Spacer(minLength: 0)

                    if events.count > 1 {
                        Text("+\(events.count - 1)")
                            .font(.system(size: 8.5, weight: .semibold))
                            .foregroundStyle(.secondary)
                    }
                }
                .padding(.horizontal, 6)
                .frame(height: 19)
                .background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 6))
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .help("Abrir no Calendário")
        } else {
            HStack(spacing: 4) {
                Image(systemName: "checkmark")
                    .font(.system(size: 7, weight: .bold))
                    .foregroundStyle(.green)
                Text("Sem eventos neste dia")
                    .font(.system(size: 9))
                    .foregroundStyle(.secondary)
                Spacer(minLength: 0)
            }
            .padding(.horizontal, 6)
            .frame(height: 19)
            .background(.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 6))
        }
    }

    private var permissionState: some View {
        VStack(alignment: .leading, spacing: 7) {
            Label("Calendário", systemImage: "calendar")
                .font(.system(size: 11, weight: .semibold))
            Text(
                calendar.accessWasGrantedBefore
                    ? "O macOS pediu a permissão de novo porque o aplicativo foi assinado outra vez."
                    : "Veja o mês e seus compromissos direto no notch."
            )
            .font(.system(size: 9.5))
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
        VStack(alignment: .leading, spacing: 7) {
            Label("Calendário", systemImage: "calendar.badge.exclamationmark")
                .font(.system(size: 11, weight: .semibold))
            Text("O acesso ao calendário está desativado.")
                .font(.system(size: 9.5))
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
                .font(.system(size: 8, weight: .bold))
                .frame(width: 15, height: 15)
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
    let height: CGFloat
    let action: () -> Void

    private var isToday: Bool { Calendar.current.isDateInToday(date) }

    var body: some View {
        Button(action: action) {
            ZStack {
                Circle()
                    .fill(isSelected ? Color.white : Color.clear)
                    .frame(width: height, height: height)

                VStack(spacing: 0) {
                    Text(date.formatted(.dateTime.day()))
                        .font(.system(size: 8.5, weight: isToday || isSelected ? .bold : .regular).monospacedDigit())
                        .foregroundStyle(isSelected ? .black : (isToday ? .green : .white))
                        .frame(height: 10)

                    HStack(spacing: 1) {
                        ForEach(Array(events.prefix(3).enumerated()), id: \.offset) { _, event in
                            Circle()
                                .fill(Color(
                                    red: event.color.red,
                                    green: event.color.green,
                                    blue: event.color.blue
                                ))
                                .frame(width: 2, height: 2)
                        }
                    }
                    .frame(height: 3)
                }
            }
            .frame(maxWidth: .infinity, minHeight: height, maxHeight: height)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(date.formatted(date: .complete, time: .omitted))
        .accessibilityValue(events.isEmpty ? "Sem eventos" : "\(events.count) eventos")
    }
}
