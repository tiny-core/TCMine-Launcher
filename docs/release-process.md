# Processo de release (auto-update com Velopack)

O launcher atualiza-se sozinho via **Velopack**. O feed de releases é servido pelo
servidor TCMine em **`{ServerUrl}/updates`** (pasta `UPDATES_DIR`, default
`TCMine-Server/updates/`). Em desenvolvimento (não instalado) o update é ignorado.

## 1. Instalar a ferramenta `vpk` (uma vez)

```bash
dotnet tool install -g vpk
```

## 2. Publicar e empacotar uma nova versão

Sobe a `<Version>` no `TCMine-Launcher/TCMine-Launcher.csproj` (ex.: `1.1.0`) e:

```powershell
# 1. Publica a app (com a tua Client.props para o MicrosoftClientId)
dotnet publish TCMine-Launcher/TCMine-Launcher.csproj -c Release -r win-x64 `
    --self-contained -o publish

# 2. Empacota com Velopack (gera Setup.exe + ficheiros de release)
vpk pack --packId TCMine.Launcher --packVersion 1.1.0 `
    --packDir publish --mainExe TCMine-Launcher.exe --outputDir releases
```

Isto cria, em `releases/`:
- `TCMine.Launcher-win-Setup.exe` — o **instalador** (primeira instalação).
- `*-full.nupkg` / `*-delta.nupkg` — os pacotes de atualização.
- `releases.win.json` — o índice que o launcher lê.

## 3. Publicar o feed no servidor

Copia **todo** o conteúdo de `releases/` para a pasta `UPDATES_DIR` do servidor
(servida em `/updates`). Ex. com Docker, monta um volume:

```yaml
# compose.yaml
environment:
  - UPDATES_DIR=/data/updates
volumes:
  - ./releases:/data/updates
```

Distribui o `Setup.exe` aos utilizadores na primeira vez. A partir daí, sempre que
publicares uma versão nova nesta pasta, o launcher deteta, mostra **"⬆ Atualizar"**,
descarrega e reinicia automaticamente.

> Para o auto-update funcionar, o launcher tem de ter sido **instalado pelo `Setup.exe`**
> do Velopack (não basta correr o `.exe` solto). O `VelopackApp.Build().Run()` no
> `Program.cs` trata dos hooks de instalação.
