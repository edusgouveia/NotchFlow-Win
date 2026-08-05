#!/bin/zsh

set -euo pipefail

cd "${0:A:h:h}"

files=("${(@f)$(git ls-files --cached --others --exclude-standard)}")
scan_files=()
for file in "${files[@]}"; do
    [[ -f "${file}" && "${file}" != "Scripts/security-check.sh" ]] && scan_files+=("${file}")
done

forbidden_files='(^|/)(\.env($|\.)|[^/]+\.(p12|pfx|pem|key|mobileprovision|sqlite|sqlite3|dmg|zip))$'
if print -rl -- "${files[@]}" | grep -En "${forbidden_files}"; then
    print -u2 "Arquivo potencialmente sensível ou artefato de distribuição está versionado."
    exit 1
fi

secret_pattern='AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9_]{20,}|BEGIN (RSA |OPENSSH |EC |DSA )?PRIVATE KEY|Authorization:[[:space:]]*Bearer[[:space:]]+[A-Za-z0-9._-]+|/Users/[^/]+/'
if (( ${#scan_files[@]} > 0 )) && grep -nIE -e "${secret_pattern}" -- "${scan_files[@]}"; then
    print -u2 "Possível segredo ou caminho pessoal encontrado nos arquivos versionados."
    exit 1
fi

reference_pattern='BillStack|Boring[ -]?Notch|NotchNook'
if (( ${#scan_files[@]} > 0 )) && grep -nIEi -e "${reference_pattern}" -- "${scan_files[@]}"; then
    print -u2 "Referência externa não desejada encontrada na documentação ou no código."
    exit 1
fi

dangerous_configuration='NSAllowsArbitraryLoads|com\.apple\.security\.get-task-allow|com\.apple\.security\.cs\.disable-library-validation'
if grep -RnIE -e "${dangerous_configuration}" -- Configuration; then
    print -u2 "Configuração de segurança perigosa encontrada."
    exit 1
fi

if grep -RnIE -e 'http://' -- Sources; then
    print -u2 "URL HTTP sem criptografia encontrada no código da aplicação."
    exit 1
fi

plutil -lint Configuration/Info.plist Configuration/NotchFlow.entitlements >/dev/null

print "Verificação de segurança concluída."
