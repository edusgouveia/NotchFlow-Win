import AppKit

@MainActor
enum AppleScriptRunner {
    static func execute(_ source: String) throws -> NSAppleEventDescriptor {
        guard let script = NSAppleScript(source: source) else {
            throw MusicServiceError.automationFailed("Não foi possível preparar a automação.")
        }

        var errorInfo: NSDictionary?
        let result = script.executeAndReturnError(&errorInfo)

        if let errorInfo {
            let message = errorInfo[NSAppleScript.errorMessage] as? String
                ?? "O macOS recusou a automação."
            throw MusicServiceError.automationFailed(message)
        }

        return result
    }

    static func executeVoid(_ source: String) throws {
        _ = try execute(source)
    }
}
