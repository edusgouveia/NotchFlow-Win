#!/bin/zsh

set -euo pipefail

cd "${0:A:h:h}"

forbidden_files='(^|/)(\.env($|\.)|[^/]+\.(p12|pfx|pem|key|mobileprovision|sqlite|sqlite3|dmg|zip))$'
if git ls-files | grep -E "${forbidden_files}"; then
    print -u2 "Arquivo potencialmente sensível ou artefato de distribuição está versionado."
    exit 1
fi

secret_pattern='AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9_]{20,}|BEGIN (RSA |OPENSSH |EC |DSA )?PRIVATE KEY|Authorization:[[:space:]]*Bearer[[:space:]]+[A-Za-z0-9._-]+|/Users/[^/]+/'
if git grep -nI -E "${secret_pattern}" -- . ':!Scripts/security-check.sh'; then
    print -u2 "Possível segredo ou caminho pessoal encontrado nos arquivos versionados."
    exit 1
fi

reference_pattern='BillStack|Boring[ -]?Notch|NotchNook'
if git grep -nI -i -E "${reference_pattern}" -- . ':!Scripts/security-check.sh'; then
    print -u2 "Referência externa não desejada encontrada na documentação ou no código."
    exit 1
fi

print "Verificação de segurança concluída."
