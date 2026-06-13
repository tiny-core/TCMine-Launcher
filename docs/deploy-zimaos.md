# Publicar o TCMine-Server no ZimaOS

O ZimaOS (baseado em CasaOS) corre apps em **Docker** e **puxa imagens de um registry**
— não compila a partir do código. O fluxo é: **publicar a imagem no Docker Hub** e depois
**instalar uma app personalizada** no ZimaOS a apontar para essa imagem.

> Substitui `SEU-USUARIO` pelo teu utilizador do Docker Hub em todo o documento.

## 1. Publicar a imagem no Docker Hub

### Opção A — automático (GitHub Actions, recomendado)

O workflow [`.github/workflows/server-image.yml`](../.github/workflows/server-image.yml)
constrói e envia a imagem a cada tag `v*`.

1. Em **Settings → Secrets and variables → Actions**, cria:
    - `DOCKERHUB_USERNAME` — o teu utilizador do Docker Hub.
    - `DOCKERHUB_TOKEN` — um *Access Token* (Docker Hub → Account Settings → Personal access tokens),
      com permissão **Read & Write**.
2. Lança uma release:
   ```bash
   git tag v1.0.0
   git push --tags
   ```
   A imagem fica em `docker.io/SEU-USUARIO/tcmine-server:1.0.0` e `:latest`.

### Opção B — manual (a partir do teu PC com Docker)

A partir da **raiz do repositório**:

```bash
docker build -t SEU-USUARIO/tcmine-server:1.0.0 -f TCMine-Server/Dockerfile .
docker tag  SEU-USUARIO/tcmine-server:1.0.0 SEU-USUARIO/tcmine-server:latest

docker login -u SEU-USUARIO
docker push SEU-USUARIO/tcmine-server:1.0.0
docker push SEU-USUARIO/tcmine-server:latest
```

## 2. Instalar no ZimaOS

App Store → **"+" → Install a customized app**. Podes preencher o formulário ou
**importar este compose**:

```yaml
name: tcmine-server
services:
  tcmine-server:
    image: SEU-USUARIO/tcmine-server:latest
    container_name: tcmine-server
    ports:
      - "8080:8080"
    environment:
      - CF_API_KEY=a-tua-key-curseforge
      - ADMIN_PASSWORD=uma-senha-forte
      - DB_PATH=/data/tcmine.db
      - UPDATES_DIR=/data/updates
      - OVERRIDES_DIR=/data/overrides
      # - CF_ALLOWED_ORIGINS=https://o-teu-dominio   # opcional, se exposto publicamente
    volumes:
      - /DATA/AppData/tcmine:/data    # persiste BD + updates + overrides
    restart: unless-stopped
    x-casaos:
      webui_port: 8080
      scheme: http
```

Notas:

- **Porta** — o container escuta em `8080` (imagem ASP.NET); mapeado para `8080` no host.
- **Volume `/data`** — guarda a BD SQLite, o feed de updates e os overrides. A pasta já é
  criada e fica gravável pelo utilizador não-root da imagem.
- **Obrigatórias** — `CF_API_KEY` (proxy de mods) e `ADMIN_PASSWORD` (login do `/admin`).

## 3. Usar

- Administração: `http://<ip-do-zima>:8080/admin` (entra com a `ADMIN_PASSWORD`).
- No launcher: **Definições → URL do servidor TCMine** = `http://<ip-do-zima>:8080`.
- Publica o(s) modpack(s) no admin e, para o auto-update, faz upload da release do
  launcher em **/admin → Releases**.

## 4. Atualizar a versão

1. `git tag v1.0.1 && git push --tags` (ou rebuild/push manual).
2. No ZimaOS, na app → **Atualizar / Pull** a imagem (ou muda a tag de `:latest` para a
   nova versão e recria). Os dados persistem no volume `/data`.

## Acesso externo / HTTPS

Para usar fora da rede local, coloca o serviço atrás de um reverse proxy com HTTPS
(o ZimaOS tem opções de rede, ou usa Caddy/nginx/Cloudflare Tunnel) e usa esse URL
público no launcher. Considera restringir `CF_ALLOWED_ORIGINS`.
