# TCMine — Proxy CurseForge

Servidor mínimo (ASP.NET Core) que reencaminha os pedidos do launcher para a API do
CurseForge, injetando a `x-api-key`. A key fica **só no servidor**, nunca no launcher.

Contrato consumido pelo launcher: [`../docs/curseforge-proxy.md`](../docs/curseforge-proxy.md).

## 1. Obter uma API key do CurseForge

Pede uma key (Eternal API) em <https://console.curseforge.com/> → *API Keys*.

## 2. Correr localmente (dotnet)

Define a key como variável de ambiente e arranca:

```powershell
# Windows (PowerShell)
$env:CF_API_KEY = "a-tua-key"
dotnet run --project TCMine-Server
```

```bash
# Linux / macOS
export CF_API_KEY="a-tua-key"
dotnet run --project TCMine-Server
```

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
| `CF_API_KEY` | **sim** | API key do CurseForge (Eternal). |
| `CF_CACHE_MINUTES` | não | TTL da cache em memória (default `5`). |
| `CF_ALLOWED_ORIGINS` | não | Lista de origens CORS separadas por vírgula. Vazio = qualquer origem. |

## Deploy

É um app ASP.NET normal. Com Docker, coloca-o atrás de um reverse proxy (nginx/Caddy)
com HTTPS e usa esse URL público no campo das Definições do launcher. Considera
restringir `CF_ALLOWED_ORIGINS`.

> **Nunca** comites a `CF_API_KEY`. Usa variáveis de ambiente / secrets do teu host.
