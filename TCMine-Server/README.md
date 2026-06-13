# TCMine — Servidor

Servidor ASP.NET Core com:

1. **Proxy CurseForge** — reencaminha `/v1/*` para `api.curseforge.com` injetando a
   `x-api-key`. A key fica **só no servidor**, nunca no launcher.
2. **Conteúdo** — novidades (`/news`) e modpacks oficiais (`/modpacks`, `/modpacks/{id}`)
   servidos a partir de uma **base de dados SQLite** (EF Core).
3. **Updates do launcher** — feed Velopack servido em `/updates`.
4. **Interface de administração** — em **`/admin`** (Blazor Server, protegida por senha)
   para gerir novidades, modpacks e releases sem editar ficheiros à mão.

O esquema da BD é criado automaticamente no arranque; todo o conteúdo (novidades,
modpacks, releases) é gerido na administração web.

Contratos (inalterados): [`../docs/curseforge-proxy.md`](../docs/curseforge-proxy.md) (mods) e
[`../docs/modpack-manifest.md`](../docs/modpack-manifest.md) (modpacks).

## 1. Obter uma API key do CurseForge

Pede uma key (Eternal API) em <https://console.curseforge.com/> → *API Keys*.

## 2. Correr localmente (dotnet)

Define a key como variável de ambiente e arranca:

```powershell
# Windows (PowerShell)
$env:CF_API_KEY = "a-tua-key"
$env:ADMIN_PASSWORD = "uma-senha"   # necessária para entrar em /admin
dotnet run --project TCMine-Server
```

```bash
# Linux / macOS
export CF_API_KEY="a-tua-key"
export ADMIN_PASSWORD="uma-senha"   # necessária para entrar em /admin
dotnet run --project TCMine-Server
```

A BD (`tcmine.db`) é criada na pasta do projeto. Abre a administração em
`http://localhost:5062/admin` e entra com a `ADMIN_PASSWORD`.

> **Em vez de variáveis de ambiente** podes criar `TCMine-Server/appsettings.local.json`
> (ignorado pelo git — ver `appsettings.local.json.example`) com `CF_API_KEY` e
> `ADMIN_PASSWORD`. É lido em runtime; as env vars, quando existem, têm prioridade.

Fica em `http://localhost:5062`. Testa:

```
GET http://localhost:5062/                → { service, configured: true }
GET http://localhost:5062/v1/mods/search?gameId=432&gameVersion=1.21.1&modLoaderType=6&searchFilter=jei
```

## 3. Correr com Docker

A partir da **raiz da solução** (onde está o `compose.yaml`):

```bash
# a key vem do ambiente do host (ou de um ficheiro .env ao lado do compose.yaml)
CF_API_KEY="a-tua-key" docker compose up --build
```

Fica em `http://localhost:8080` (porta mapeada no `compose.yaml`).

## 4. Ligar no launcher

No launcher: **Definições → CurseForge → URL do proxy** = o root do servidor
(`http://localhost:5062` com dotnet, ou `http://localhost:8080` com Docker), **sem** `/v1`.
A partir daí a pesquisa e instalação de mods funcionam.

## Variáveis de ambiente

| Variável | Obrigatória | Descrição |
|---|---|---|
| `CF_API_KEY` | **sim** (mods) | API key do CurseForge (Eternal). |
| `ADMIN_PASSWORD` | **sim** (admin) | Senha da interface `/admin`. Vazia = login sempre recusado. |
| `DB_PATH` | não | Caminho do ficheiro SQLite (default `./tcmine.db`). |
| `CF_CACHE_MINUTES` | não | TTL da cache em memória (default `5`). |
| `CF_ALLOWED_ORIGINS` | não | Lista de origens CORS separadas por vírgula. Vazio = qualquer origem. |
| `UPDATES_DIR` | não | Pasta do feed de updates do launcher (Velopack), servida em `/updates` (default `./updates`). |
| `OVERRIDES_DIR` | não | Pasta dos bundles de overrides dos modpacks (default `./overrides`). |

## Administração (`/admin`)

Interface web (Blazor Server) para gerir tudo sem editar ficheiros:

- **Novidades** — criar/editar/eliminar/publicar (servidas em `/news`).
- **Modpacks** — criar/editar, com as listas de mods e servidores (servidos em
  `/modpacks` e `/modpacks/{id}`). Podes **importar do CurseForge** (pesquisa →
  resolve mods, resource packs e shaders pela classe do projeto e captura o bundle
  de **overrides** — configs/options); o launcher aplica os overrides na instância.
  Os overrides são servidos em `/modpacks/{id}/overrides`.
- **Releases** — fazer upload dos artefactos do `vpk pack` (ver abaixo).

Entra em `/admin` com a `ADMIN_PASSWORD`. Os endpoints públicos mantêm o contrato:

```
GET /news             → novidades publicadas
GET /modpacks         → lista (resumo de cada modpack)
GET /modpacks/{id}    → manifesto completo (mods + servidores)
```

O launcher continua a consumir estes endpoints tal como antes — **não muda nada do lado dele**.

## Auto-update do launcher (Velopack)

O servidor serve o feed de releases do launcher em **`/updates`** (pasta `UPDATES_DIR`).
O launcher (Velopack) lê esse feed, descarrega e aplica a atualização sozinho.
Em vez de copiar ficheiros à mão, faz **upload dos artefactos do `vpk pack` em
`/admin` → Releases**. Ver [`../docs/release-process.md`](../docs/release-process.md).

## Deploy

É um app ASP.NET normal. Com Docker, coloca-o atrás de um reverse proxy (nginx/Caddy)
com HTTPS e usa esse URL público no campo das Definições do launcher. Considera
restringir `CF_ALLOWED_ORIGINS`.

> **Nunca** comites a `CF_API_KEY`. Usa variáveis de ambiente / secrets do teu host.
