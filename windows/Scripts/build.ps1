<#
.SYNOPSIS
    Compila o NotchFlow para Windows e reúne a distribuição em dist/.

.DESCRIPTION
    Equivalente ao Scripts/build-app.sh da versão macOS: roda os testes antes de
    empacotar e publica o SHA-256 do resultado.
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,

    # Embute o Windows App SDK no pacote, dispensando o runtime na máquina de destino.
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'

# A raiz do projeto Windows é windows/, um nível acima de windows/Scripts/.
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'NotchFlow.sln'
$appProject = Join-Path $root 'src\NotchFlow.App\NotchFlow.App.csproj'
$dist = Join-Path $root 'dist'

Write-Host 'Compilando...' -ForegroundColor Cyan
dotnet build $solution -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }

if (-not $SkipTests) {
    Write-Host 'Executando os testes...' -ForegroundColor Cyan
    dotnet test $solution -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Os testes falharam. O pacote não foi gerado.' }
}

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

Write-Host 'Publicando...' -ForegroundColor Cyan
$publishArgs = @(
    'publish', $appProject,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'false',
    '-o', (Join-Path $dist 'NotchFlow'),
    '--nologo'
)
if ($SelfContained) {
    $publishArgs += '-p:WindowsAppSDKSelfContained=true'
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'Falha na publicação.' }

$zip = Join-Path $dist 'NotchFlow-windows-x64.zip'
Compress-Archive -Path (Join-Path $dist 'NotchFlow\*') -DestinationPath $zip

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Set-Content -Path "$zip.sha256" -Value "$hash  $(Split-Path $zip -Leaf)"

Write-Host ''
Write-Host "Pacote: $zip" -ForegroundColor Green
Write-Host "SHA-256: $hash" -ForegroundColor Green
