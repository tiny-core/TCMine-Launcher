# Publica e empacota o TCMine Launcher com o Velopack (vpk).
# Correr a partir da raiz do repositório:  ./tools/pack-launcher.ps1
# Requisitos: .NET SDK, vpk (dotnet tool install -g vpk) e o TCMine-Launcher/Client.props
# (com o MicrosoftClientId). Os artefactos ficam em ./releases — depois faz upload em
# /admin -> Releases no servidor.

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
