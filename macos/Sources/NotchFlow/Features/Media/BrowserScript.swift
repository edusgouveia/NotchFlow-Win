import Foundation

/// JavaScript executado nas abas. Cada trecho fica em uma única linha porque será
/// embutido em um literal de AppleScript, e nenhum deles usa aspas duplas ou barra invertida.
enum BrowserScript {
    private static let picker =
        "var nfPick=function(){"
        + "var l=[].slice.call(document.querySelectorAll('video,audio'));"
        + "l=l.filter(function(e){return isFinite(e.duration)&&e.duration>0});"
        + "if(!l.length){return null}"
        + "var p=l.filter(function(e){return !e.paused});"
        + "if(p.length){p.sort(function(a,b){return b.duration-a.duration});return p[0]}"
        + "l.sort(function(a,b){return b.duration-a.duration});"
        + "return l[0]};"

    static let probe =
        "(function(){try{"
        + picker
        + "var v=nfPick();if(!v){return ''}"
        + "var m=(navigator.mediaSession&&navigator.mediaSession.metadata)||null;"
        + "var art='';"
        + "if(m&&m.artwork&&m.artwork.length){var b=m.artwork[m.artwork.length-1];if(b&&b.src){art=b.src}}"
        + "var o={title:(m&&m.title)||document.title||'',"
        + "artist:(m&&m.artist)||'',"
        + "album:(m&&m.album)||'',"
        + "artwork:art,"
        + "duration:v.duration||0,"
        + "position:v.currentTime||0,"
        + "playing:!v.paused,"
        + "host:location.hostname};"
        + "return JSON.stringify(o)}catch(e){return ''}})()"

    static let togglePlayPause =
        "(function(){try{"
        + picker
        + "var v=nfPick();if(!v){return 'none'}"
        + "if(v.paused){v.play()}else{v.pause()}"
        + "return 'ok'}catch(e){return 'error'}})()"

    static let nextTrack = click(selectors: [
        "ytmusic-player-bar .next-button",
        ".ytp-next-button",
        ".skipControl__next",
        "[data-testid=control-button-skip-forward]",
        "button[aria-label*=Next i]"
    ])

    static let previousTrack = click(selectors: [
        "ytmusic-player-bar .previous-button",
        ".ytp-prev-button",
        ".skipControl__previous",
        "[data-testid=control-button-skip-back]",
        "button[aria-label*=Previous i]"
    ])

    static func seek(toMilliseconds milliseconds: Int) -> String {
        "(function(){try{"
        + picker
        + "var v=nfPick();if(!v){return 'none'}"
        + "var t=(\(milliseconds)/1000);"
        + "if(t<0){t=0}"
        + "if(isFinite(v.duration)&&t>v.duration-0.5){t=Math.max(0,v.duration-0.5)}"
        + "v.currentTime=t;"
        + "return 'ok'}catch(e){return 'error'}})()"
    }

    static func seek(byMilliseconds milliseconds: Int) -> String {
        "(function(){try{"
        + picker
        + "var v=nfPick();if(!v){return 'none'}"
        + "var t=v.currentTime+(\(milliseconds)/1000);"
        + "if(t<0){t=0}"
        + "if(isFinite(v.duration)&&t>v.duration-0.5){t=Math.max(0,v.duration-0.5)}"
        + "v.currentTime=t;"
        + "return 'ok'}catch(e){return 'error'}})()"
    }

    private static func click(selectors: [String]) -> String {
        let list = selectors.map { "'" + $0 + "'" }.joined(separator: ",")
        return "(function(){try{var s=[" + list + "];"
            + "for(var i=0;i<s.length;i++){var e=null;"
            + "try{e=document.querySelector(s[i])}catch(x){e=null}"
            + "if(e&&!e.disabled&&e.getAttribute('aria-disabled')!=='true'){e.click();return 'ok'}}"
            + "return 'none'}catch(e){return 'error'}})()"
    }
}
