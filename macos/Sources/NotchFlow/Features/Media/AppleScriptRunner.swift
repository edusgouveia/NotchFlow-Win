import AppKit

/// Erro devolvido pelo AppleScript, com o número original para decidir o tratamento.
struct AppleScriptError: LocalizedError, Equatable {
    let code: Int
    let message: String

    var errorDescription: String? { message }

    /// -1743 e -1744 indicam que a automação não foi autorizada pelo usuário.
    var isPermissionDenied: Bool { code == -1743 || code == -1744 }

    /// Navegadores recusam scripts quando "Permitir JavaScript de Apple Events" está desligado.
    var isJavaScriptBlocked: Bool {
        guard !isPermissionDenied else { return false }
        let lowered = message.lowercased()
        return lowered.contains("javascript")
    }

    /// O aplicativo não está disponível para receber o evento.
    var isApplicationUnavailable: Bool { code == -600 || code == -609 || code == -1728 }
}

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
            let code = (errorInfo[NSAppleScript.errorNumber] as? NSNumber)?.intValue ?? 0
            throw AppleScriptError(code: code, message: message)
        }

        return result
    }

    static func executeVoid(_ source: String) throws {
        _ = try execute(source)
    }

    static func executeReturningString(_ source: String) throws -> String {
        try execute(source).stringValue ?? ""
    }
}

/// Converte textos para literais de AppleScript, evitando qualquer escape manual espalhado no código.
enum AppleScriptLiteral {
    static func quoted(_ value: String) -> String {
        var escaped = value.replacingOccurrences(of: "\\", with: "\\\\")
        escaped = escaped.replacingOccurrences(of: "\"", with: "\\\"")
        return "\"" + escaped + "\""
    }

    static func list(_ values: [String]) -> String {
        "{" + values.map(quoted).joined(separator: ", ") + "}"
    }
}
