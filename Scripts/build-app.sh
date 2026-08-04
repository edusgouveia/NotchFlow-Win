#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="${0:A:h}"
PROJECT_DIR="${SCRIPT_DIR:h}"
DIST_DIR="${PROJECT_DIR}/dist"
APP_PATH="${DIST_DIR}/NotchFlow.app"
ICON_SOURCE="${PROJECT_DIR}/Assets/AppIcon.png"
ICON_WORK_DIR="$(mktemp -d "${TMPDIR%/}/notchflow-icon.XXXXXX")"
SIGN_IDENTITY="${NOTCHFLOW_SIGN_IDENTITY:--}"
ICONSET_PATH="${ICON_WORK_DIR}/NotchFlow.iconset"
ICNS_PATH="${ICON_WORK_DIR}/NotchFlow.icns"

cleanup() {
    rm -rf "${ICON_WORK_DIR}"
}
trap cleanup EXIT

cd "${PROJECT_DIR}"

"${PROJECT_DIR}/Scripts/security-check.sh"
swift test
BUILD_ARCH="${NOTCHFLOW_BUILD_ARCH:-$(uname -m)}"
swift build -c release --arch "${BUILD_ARCH}"
BIN_DIR="$(swift build -c release --arch "${BUILD_ARCH}" --show-bin-path)"

if [[ ! -f "${ICON_SOURCE}" ]]; then
    print -u2 "Ícone não encontrado: ${ICON_SOURCE}"
    exit 1
fi

mkdir -p "${ICONSET_PATH}"
sips -z 16 16 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_16x16.png" >/dev/null
sips -z 32 32 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_16x16@2x.png" >/dev/null
sips -z 32 32 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_32x32.png" >/dev/null
sips -z 64 64 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_32x32@2x.png" >/dev/null
sips -z 128 128 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_128x128.png" >/dev/null
sips -z 256 256 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_128x128@2x.png" >/dev/null
sips -z 256 256 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_256x256.png" >/dev/null
sips -z 512 512 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_256x256@2x.png" >/dev/null
sips -z 512 512 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_512x512.png" >/dev/null
sips -z 1024 1024 "${ICON_SOURCE}" --out "${ICONSET_PATH}/icon_512x512@2x.png" >/dev/null
iconutil -c icns "${ICONSET_PATH}" -o "${ICNS_PATH}"

if [[ "${APP_PATH}" != "${PROJECT_DIR}/dist/NotchFlow.app" ]]; then
    print -u2 "Caminho de saída inesperado; build interrompido."
    exit 1
fi

rm -rf "${APP_PATH}"

mkdir -p "${APP_PATH}/Contents/MacOS"
mkdir -p "${APP_PATH}/Contents/Resources"

ditto "${BIN_DIR}/NotchFlow" "${APP_PATH}/Contents/MacOS/NotchFlow"
ditto "${PROJECT_DIR}/Configuration/Info.plist" "${APP_PATH}/Contents/Info.plist"
ditto "${ICNS_PATH}" "${APP_PATH}/Contents/Resources/NotchFlow.icns"

plutil -lint "${APP_PATH}/Contents/Info.plist"

codesign \
    --force \
    --deep \
    --options runtime \
    --timestamp=none \
    --sign "${SIGN_IDENTITY}" \
    --entitlements "${PROJECT_DIR}/Configuration/NotchFlow.entitlements" \
    "${APP_PATH}"

codesign --verify --deep --strict --verbose=2 "${APP_PATH}"

print ""
print "Build concluído: ${APP_PATH}"

if [[ "${SIGN_IDENTITY}" == "-" ]]; then
    print ""
    print "Assinatura ad hoc: o macOS trata cada build como um aplicativo novo e pede"
    print "as permissões de Calendário e Automação outra vez."
    print "Para preservar as permissões entre builds, use um certificado fixo:"
    print "  NOTCHFLOW_SIGN_IDENTITY=\"Nome do certificado\" ./Scripts/build-app.sh"
fi
