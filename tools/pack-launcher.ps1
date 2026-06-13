# Publica e empacota o TCMine Launcher com o Velopack (vpk).
# Uso (a partir da raiz do repositório):
#   ./tools/pack-launcher.ps1                 → usa a <Version> atual do csproj
#   ./tools/pack-launcher.ps1 -Version 1.0.1  → escreve a versão no csproj e empacota
# Requisitos: .NET SDK, vpk (dotnet tool install -g vpk) e o TCMine-Launcher/Client.props
# (com o MicrosoftClientId). Os artefactos ficam em ./releases — depois faz upload em
# /admin -> Releases no servidor.
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$rid = "win-x64"
$proj = "TCMine-Launcher/TCMine-Launcher.csproj"
$icon = "TCMine-Launcher/Assets/icon.ico"     # ícone do Setup.exe + atalhos
$splash = "TCMine-Launcher/Assets/splash.png" # imagem mostrada durante a instalação
$title = "TCMine Launcher"
$authors = "Tiny Core"

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw "vpk não encontrado. Instala com: dotnet tool install -g vpk"
}

# Se for passada uma versão, valida-a e escreve no <Version> do csproj.
if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+([.-].+)?$') {
        throw "Versão inválida: '$Version' (usa semver, ex.: 1.0.1)."
    }
    $content = Get-Content $proj -Raw
    if ($content -notmatch '<Version>.*?</Version>') {
        throw "Não encontrei <Version> em $proj para atualizar."
    }
    $content = [regex]::Replace($content, '<Version>.*?</Version>', "<Version>$Version</Version>")
    [System.IO.File]::WriteAllText((Resolve-Path $proj), $content, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "==> <Version> atualizado para $Version em $proj" -ForegroundColor Yellow
}

# Versão lida do <Version> do csproj.
[xml]$xml = Get-Content $proj
$version = @($xml.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if (-not $version) { throw "Não encontrei <Version> em $proj." }

Write-Host "==> A empacotar TCMine Launcher v$version ($rid)" -ForegroundColor Cyan

dotnet publish $proj -c Release -r $rid --self-contained -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

# Notas de versão opcionais: se existir tools/release-notes.md, são incluídas.
$notesArg = @()
if (Test-Path "tools/release-notes.md") { $notesArg = @("--releaseNotes", "tools/release-notes.md") }

vpk pack --packId TCMine.Launcher --packVersion $version `
    --packDir publish --mainExe TCMine-Launcher.exe --outputDir releases `
    --packTitle $title --packAuthors $authors --icon $icon `
    --splashImage $splash --splashProgressColor "#F97316" @notesArg
if ($LASTEXITCODE -ne 0) { throw "vpk pack falhou." }

Write-Host "==> Pronto. Artefactos em ./releases" -ForegroundColor Green
Write-Host "    Faz upload do conteúdo em /admin -> Releases (ou copia para UPDATES_DIR)."
