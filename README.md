<div align="center">

# 🎮 TCMine Launcher

**Launcher de Minecraft personalizado para o servidor e o modpack NeoForge do TCMine.**

Login com a Microsoft, gestão de versões e um modpack próprio — tudo numa interface moderna.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Avalonia](https://img.shields.io/badge/Avalonia-12-8B44AC)
![Plataforma](https://img.shields.io/badge/plataforma-Linux%20%7C%20Windows-2A2A40)
![Licença](https://img.shields.io/badge/licen%C3%A7a-GPL--3.0-3DA639)
![Estado](https://img.shields.io/badge/estado-em%20desenvolvimento-F97316)

</div>

---

## ✨ Visão geral

O **TCMine Launcher** é um launcher desktop construído com **Avalonia UI** (.NET 10) que instala e lança o **modpack custom do servidor TCMine** sobre **NeoForge**. Foca-se numa experiência simples: entra com a tua conta Microsoft, escolhe o modpack e joga.

<div align="center">

![Ecrã principal](docs/screenshot-home.png)

</div>

## 🚀 Funcionalidades

- 🔐 **Login com a Microsoft** (navegador do sistema) — obtém o teu perfil, nome e UUID reais
- 👤 **Modo offline** para jogar/testar sem conta online
- 🧩 **Modpack oficial** com seleção de versão de Minecraft e NeoForge
- 🗂️ **Navegação por separadores**: Jogar, Modpacks, Novidades e Definições
- 📊 **Progresso e registo de launch** com consola integrada
- ⚙️ **Definições**: memória JVM, caminho do Java e gestão de conta
- 🎨 Interface escura moderna, janela sem decorações nativas e transições suaves

<div align="center">

![Tela de login](docs/screenshot-login.png)

</div>

## 🛠️ Stack

| Camada | Tecnologia |
|---|---|
| UI | [Avalonia UI 12](https://avaloniaui.net/) (XAML, tema Fluent) |
| Padrão | MVVM via [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) |
| Minecraft / NeoForge | [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) + `CmlLib.Core.Installer.NeoForge` |
| Autenticação | `CmlLib.Core.Auth.Microsoft` + `XboxAuthNet.Game.Msal` (MSAL) |
| Runtime | .NET 10 |

## 📦 Compilar e correr

**Pré-requisitos:** [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/tiny-core/TCMine-Launcher.git
cd TCMine-Launcher
dotnet run --project TCMine-Launcher
```

> Sem o login Microsoft configurado (ver abaixo), usa **"Continuar em modo offline"**.

## 🔑 Configurar o login com a Microsoft

A autenticação usa uma **app registada no Azure** (um *public client* — **não tem client secret**). O `Client ID` é embutido no binário em tempo de compilação e mantido **fora do git**.

1. Cria uma App Registration no [portal do Azure](https://portal.azure.com) com:
   - Redirect URI `http://localhost` (plataforma *Mobile and desktop applications*)
   - *Allow public client flows* = **Yes**
   - Contas Microsoft pessoais permitidas
2. Copia o template e coloca o teu Client ID:
   ```bash
   cp TCMine-Launcher/Client.props.example TCMine-Launcher/Client.props
   # edita <MicrosoftClientId> no Client.props
   ```
   Em CI/produção, em alternativa: `dotnet publish -p:MicrosoftClientId=<o-teu-id>`.

> ℹ️ Apps do Azure novas precisam de **aprovação para a API do Minecraft** ([formulário](https://aka.ms/mce-reviewappid)); sem ela, o login devolve `403`. O modo offline funciona sem aprovação.

## 🗂️ Estrutura

```
TCMine-Launcher/
├─ Models/        # dados puros (PlayerProfile, GameProfile, Modpack, ...) — sem UI
├─ ViewModels/    # MVVM: shell + páginas (Home, Modpacks, News, Settings)
├─ Views/         # AXAML + code-behind mínimo
├─ Services/      # AuthService (login MS), AppConfig
└─ Client.props   # Client ID do Azure (gitignored)
```

A separação MVVM é estrita: os **Models** não conhecem UI nem CmlLib; os **ViewModels** orquestram; as **Views** só fazem binding.

## 🗺️ Estado / Roadmap

- [x] Interface completa e navegação entre telas
- [x] Login Microsoft real (navegador do sistema) + modo offline
- [ ] Download e instalação do modpack via CmlLib
- [ ] Launch real do Minecraft (NeoForge)
- [ ] Skins reais e persistência de definições

## 📄 Licença

Distribuído sob a **GNU General Public License v3.0** (copyleft) — vê o ficheiro [`LICENSE`](LICENSE). Qualquer versão modificada e distribuída deve também disponibilizar o código-fonte sob a mesma licença.

Copyright © 2026 tiny-core
