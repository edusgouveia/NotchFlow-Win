import OSLog

enum AppLog {
    static let lifecycle = Logger(subsystem: "com.thiagoalves.NotchFlow", category: "lifecycle")
    static let media = Logger(subsystem: "com.thiagoalves.NotchFlow", category: "media")
    static let calendar = Logger(subsystem: "com.thiagoalves.NotchFlow", category: "calendar")
    static let window = Logger(subsystem: "com.thiagoalves.NotchFlow", category: "window")
}
