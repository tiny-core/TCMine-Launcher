# Manifesto de modpack oficial

O servidor TCMine serve os modpacks oficiais a partir de ficheiros JSON na pasta
`MODPACKS_DIR` (default `TCMine-Server/modpacks/`). Cada ficheiro é `<id>.json`.

## Endpoints

```
GET /modpacks         → array de resumos (sem a lista de mods, só "modCount")
GET /modpacks/{id}    → o ficheiro <id>.json completo (mods + servidores)
```

## Formato do ficheiro `<id>.json`

```json
{
  "id": "tcmine-official",
  "name": "TCMine Modpack",
  "version": "1.0.0",
  "minecraft": "1.21.1",
  "neoforge": "21.1.172",
  "description": "O modpack oficial do servidor TCMine.",
  "mods": [
    {
      "modId": 238222,
      "fileId": 5798904,
      "name": "Just Enough Items (JEI)",
      "fileName": "jei-1.21.1-neoforge-19.21.0.247.jar",
      "downloadUrl": "https://edge.forgecdn.net/files/5798/904/jei-...jar"
    }
  ],
  "servers": [
    { "name": "TCMine Survival", "address": "play.tcmine.net", "port": 25565 }
  ]
}
```

| Campo | Descrição |
|---|---|
| `id` | Identificador único = nome do ficheiro. Estável (o launcher usa-o para atualizar em vez de duplicar). |
| `version` | Versão do modpack. Mudá-la sinaliza atualização ao reinstalar. |
| `minecraft` / `neoforge` | Versões usadas pela instância criada. |
| `mods[]` | Mesma forma que o `ModEntry` do launcher. Obtém `modId`/`fileId`/`downloadUrl` via CurseForge (o teu proxy `/v1/mods/{id}/files`). |
| `servers[]` | Escritos no `servers.dat`; o 1º é usado para ligação direta ao arrancar. |

## Comportamento no launcher

1. **Aba Modpacks** lista o que vem de `GET /modpacks`.
2. **Instalar** vai buscar `GET /modpacks/{id}`, cria (ou atualiza) uma instância
   oficial com os mods e servidores, e seleciona-a.
3. No **Instalar/Jogar**, os mods são descarregados e os servidores escritos no
   `servers.dat` — aparecem na lista multijogador. O jogo arranca a ligar-se
   diretamente ao primeiro servidor.

> Os campos `fileName`/`downloadUrl` dos mods podem ser obtidos do CurseForge; mantém-nos
> coerentes com a versão de Minecraft/NeoForge do modpack.
