import Foundation

/// Monta os scripts enviados aos navegadores. Nenhum texto do usuário entra nos scripts:
/// apenas identificadores de bundle, índices numéricos e JavaScript fixo do aplicativo.
enum BrowserAppleScript {
    static let fieldSeparator = "|"
    static let errorPrefix = "ERROR "
    static let maximumProbes = 6

    static func scan(
        browser: BrowserApplication,
        hosts: [String],
        probeJavaScript: String,
        maximumProbes: Int = maximumProbes
    ) -> String {
        let bundle = AppleScriptLiteral.quoted(browser.bundleIdentifier)
        let hostList = AppleScriptLiteral.list(hosts)
        let separator = AppleScriptLiteral.quoted(fieldSeparator)
        let prefix = AppleScriptLiteral.quoted(errorPrefix)
        let probeStatement = statement(for: browser, javaScript: probeJavaScript, variable: "probeText")

        return """
        set candidateHosts to \(hostList)
        set outputText to ""
        set probeCount to 0
        tell application id \(bundle)
            set rawLists to {}
            try
                set rawLists to (URL of tabs of windows)
            end try
            if (count of rawLists) is 0 then
                set windowTotal to 0
                try
                    set windowTotal to (count of windows)
                end try
                repeat with w from 1 to windowTotal
                    set rowList to {}
                    try
                        set rowList to (URL of tabs of window w)
                    end try
                    set end of rawLists to rowList
                end repeat
            end if
            set urlLists to {}
            if (count of rawLists) > 0 then
                if class of (item 1 of rawLists) is list then
                    set urlLists to rawLists
                else
                    set urlLists to {rawLists}
                end if
            end if
            repeat with w from 1 to (count of urlLists)
                set rowList to item w of urlLists
                repeat with t from 1 to (count of rowList)
                    if probeCount < \(maximumProbes) then
                        set tabURL to ""
                        try
                            set tabURL to (item t of rowList) as text
                        end try
                        if tabURL is not "" then
                            set isCandidate to false
                            repeat with h in candidateHosts
                                if tabURL contains (contents of h) then set isCandidate to true
                            end repeat
                            if isCandidate then
                                set probeCount to probeCount + 1
                                set probeText to ""
                                try
                                    \(probeStatement)
                                on error errorText
                                    set probeText to \(prefix) & errorText
                                end try
                                if probeText is not "" then
                                    set outputText to outputText & (w as text) & \(separator) & (t as text) & \(separator) & probeText & linefeed
                                end if
                            end if
                        end if
                    end if
                end repeat
            end repeat
        end tell
        return outputText
        """
    }

    static func command(
        browser: BrowserApplication,
        windowIndex: Int,
        tabIndex: Int,
        javaScript: String
    ) -> String {
        let bundle = AppleScriptLiteral.quoted(browser.bundleIdentifier)
        let body = statement(
            for: browser,
            javaScript: javaScript,
            variable: "resultText",
            windowIndex: windowIndex,
            tabIndex: tabIndex
        )

        return """
        set resultText to ""
        tell application id \(bundle)
            \(body)
        end tell
        return resultText
        """
    }

    private static func statement(
        for browser: BrowserApplication,
        javaScript: String,
        variable: String,
        windowIndex: Int? = nil,
        tabIndex: Int? = nil
    ) -> String {
        let script = AppleScriptLiteral.quoted(javaScript)
        let windowReference = windowIndex.map(String.init) ?? "w"
        let tabReference = tabIndex.map(String.init) ?? "t"

        // A indentação do AppleScript gerado não importa; o que importa é uma instrução por linha.
        switch browser.engine {
        case .chromium:
            return [
                "tell tab \(tabReference) of window \(windowReference)",
                "set \(variable) to (execute javascript \(script)) as text",
                "end tell"
            ].joined(separator: "\n")
        case .webKit:
            return "set \(variable) to (do JavaScript \(script) in tab \(tabReference) of window \(windowReference)) as text"
        }
    }
}
