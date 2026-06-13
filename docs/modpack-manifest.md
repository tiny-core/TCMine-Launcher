# Manifesto de modpack oficial

Os modpacks oficiais são geridos na **administração** (`/admin` → Modpacks) e guardados
numa base de dados SQLite. O servidor serve-os no formato JSON consumido pelo launcher.

## Endpoints

```
GET /modpacks               → array de resumos (sem a lista de mods, só "modCount"/"serverCount")
GET /modpacks/{id}          → manifesto completo (mods + servidores)
GET /modpacks/{id}/overrides → zip de overrides (configs/resourcepacks/options), se existir
```

## Formato de `GET /modpacks/{id}`

```json
{
  "id": "tcmine-official",
  "name": "TCMine Modpack",
  "version": "1.0.0",
  "minecraft": "1.21.1",
  "neoforge": "21.1.172",
  "description": "O modpack oficial do servidor TCMine.",
  "hasOverrides": true,
  "recommendedRamMb": 8192,
  "mods": [
    {
      "modId": 238222,
      "fileId": 5798904,
      "name": "Just Enough Items (JEI)",
      "fileName": "jei-1.21.1-neoforge-19.21.0.247.jar",
      "downloadUrl": "https://edge.forgecdn.net/files/5798/904/jei-...jar",
      "target": "mod"
    }
  ],
  "servers": [
    { "name": "TCMine Survival", "address": "play.tcmine.net", "port": 25565 }
  ]
}
```

| Campo | Descrição |
|---|---|
| `id` | Identificador único (slug). Estável — o launcher usa-o para atualizar em vez de duplicar. |
| `version` | Versão do modpack. Mudá-la sinaliza atualização (e reaplica os overrides). |
| `minecraft` / `neoforge` | Versões usadas pela instância criada. |
| `hasOverrides` | Se `true`, há um bundle de overrides em `/modpacks/{id}/overrides`. |
| `recommendedRamMb` | RAM (MB) aplicada à instância ao instalar (opcional). |
| `mods[]` | Mesma forma que o `ModEntry` do launcher (ver `target`). |
| `mods[].target` | Pasta de destino: `mod` → `mods/`, `resourcepack` → `resourcepacks/`, `shaderpack` → `shaderpacks/`. |
| `servers[]` | Escritos no `servers.dat`; o 1.º é usado para ligação direta ao arrancar. |

## Gestão na administração

- **Importar do CurseForge** — pesquisa um modpack; o servidor descarrega-o, lê o
  `manifest.json`, resolve cada projeto pela sua classe (`mod`/`resourcepack`/`shaderpack`)
  e captura a pasta `overrides/` num bundle zip. Funciona ao criar e ao editar (com aviso
  de substituição).
- **Adicionar mods** — pesquisa no CurseForge e preenche `fileId`/`fileName`/`downloadUrl`
  automaticamente (escolhe o ficheiro mais recente para a versão MC + NeoForge).
- **RAM recomendada** e **publicação** definem-se no editor.

## Comportamento no launcher

1. **Aba Modpacks** lista `GET /modpacks`.
2. **Instalar** vai buscar `GET /modpacks/{id}`, cria/atualiza a instância oficial com os
   mods e servidores, aplica a `recommendedRamMb` e seleciona-a.
3. No **Jogar**, cada ficheiro é descarregado para a pasta certa pelo `target`; o bundle de
   **overrides** é aplicado por cima (uma vez por versão) — trazendo configs, resource packs
   e `options.txt` (que os ativa). Os servidores são escritos no `servers.dat`.
