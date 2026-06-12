# Proxy CurseForge — especificação para o servidor TCMine

O launcher **não** fala diretamente com o CurseForge. Em vez disso chama um **proxy**
no teu servidor, que injeta a `x-api-key` e reencaminha para `api.curseforge.com`.
Assim a API key **nunca sai do servidor** (não é extraível do binário).

O URL base do proxy configura-se no launcher em **Definições → CurseForge → URL do proxy**
(persistido em `settings.json` como `CurseForgeProxyUrl`).

## Contrato

O proxy reencaminha as rotas `/v1/*` para `https://api.curseforge.com/v1/*`,
**1:1** (mesmos query params, mesmo JSON de resposta), apenas acrescentando o header
`x-api-key: <A_TUA_KEY>`. O launcher usa duas rotas:

### 1. Pesquisa de mods

```
GET {BASE}/v1/mods/search
      ?gameId=432               # Minecraft
      &gameVersion=1.21.1       # versão da instância
      &modLoaderType=6          # 6 = NeoForge
      &searchFilter=<texto>
      &sortField=2&sortOrder=desc
      &pageSize=20&index=0
```

Resposta (subconjunto usado pelo launcher):

```json
{
  "data": [
    {
      "id": 238222,
      "name": "Just Enough Items (JEI)",
      "summary": "View items and recipes",
      "logo": { "thumbnailUrl": "https://..." }
    }
  ]
}
```

### 2. Ficheiros de um mod (escolha do .jar compatível)

```
GET {BASE}/v1/mods/{modId}/files
      ?gameVersion=1.21.1
      &modLoaderType=6
      &pageSize=20
```

Resposta:

```json
{
  "data": [
    {
      "id": 5101618,
      "fileName": "jei-1.21.1-neoforge-19.21.0.247.jar",
      "displayName": "JEI 19.21.0.247",
      "downloadUrl": "https://edge.forgecdn.net/files/5101/618/jei-...jar"
    }
  ]
}
```

O launcher escolhe o **primeiro ficheiro com `downloadUrl` não-nulo**.

## Download dos .jar

O download é feito **diretamente** a partir do `downloadUrl` (CDN público
`forgecdn.net`), sem passar pelo proxy nem pela key. Quando `downloadUrl` é `null`
(distribuição por terceiros proibida pelo autor), esse mod é ignorado — idealmente
o proxy/launcher deveria encaminhar o utilizador para a página do mod (melhoria futura).

## Implementação

O proxy está implementado em **`TCMine-Server/`** (ASP.NET Core Minimal API), incluído
na solução. Ver [`../TCMine-Server/README.md`](../TCMine-Server/README.md) para correr
(dotnet ou Docker). A `CF_API_KEY` lê-se da variável de ambiente — nunca do código.

## Notas

- `modLoaderType`: 1=Forge, 4=Fabric, 5=Quilt, **6=NeoForge**.
- `gameId` do Minecraft = **432**.
- Considera cachear as respostas de pesquisa/ficheiros para poupar quota da API.
- Restringe o CORS/origem ao launcher se expuseres o proxy publicamente.
