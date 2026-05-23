# Pact Broker self-hosted (homolog)

Broker de contratos rodando na VM homolog via Docker, atrás do nginx em
`https://pact.homologacao.forzion.tech`. Postgres dedicado (volume `pact_pgdata`).

Serviços adicionados em `docker-compose.homolog.yml`: `pact-postgres`,
`pact-broker`. Rota nginx: bloco de servidor para o subdomínio `pact.`.

## Pré-requisitos (uma vez)

### 1. DNS

Criar registro **A** `pact.homologacao.forzion.tech` → IP da VM.

### 2. Variáveis de ambiente na VM (`/opt/forzion/.env`)

```dotenv
PACT_POSTGRES_USER=pact
PACT_POSTGRES_PASSWORD=<senha-forte-do-postgres-do-broker>
PACT_BROKER_USERNAME=<usuario-basic-auth>
PACT_BROKER_PASSWORD=<senha-basic-auth>
```

> Sem essas vars, o `pact-postgres` não sobe saudável (Postgres exige senha).
> O site principal **não** é afetado — nginx usa o cert existente e segue no ar.

### 3. Deploy

Push em `homolog` dispara o deploy (ou rodar na VM):

```bash
cd /opt/forzion/app
docker compose -f docker-compose.homolog.yml --env-file /opt/forzion/.env up -d
```

### 4. Expandir o certificado TLS para o subdomínio

Enquanto o cert não cobrir `pact.`, o nginx usa o cert do domínio principal
(sobe normal, mas o subdomínio serve cert inválido). Expandir:

```bash
docker compose -f docker-compose.homolog.yml run --rm --entrypoint certbot certbot \
  certonly --webroot -w /var/www/certbot \
  -d homologacao.forzion.tech -d pact.homologacao.forzion.tech --expand

docker compose -f docker-compose.homolog.yml restart nginx
```

> O `--entrypoint certbot` é **obrigatório**: o serviço `certbot` no compose tem
> um entrypoint que é o loop de renovação (`while :; do certbot renew; sleep 12h`).
> Sem sobrescrever, `compose run` roda o loop e **trava** em vez do `certonly`.

### 5. Criar o environment `homolog` no broker (para `can-i-deploy`)

```bash
docker run --rm \
  -e PACT_BROKER_BASE_URL=https://pact.homologacao.forzion.tech \
  -e PACT_BROKER_USERNAME=<user> -e PACT_BROKER_PASSWORD=<pass> \
  pactfoundation/pact-cli:latest \
  broker create-environment --name homolog --display-name Homolog
```

## GitHub (ativa o `contract.yml`)

Secrets do repositório (Settings › Secrets and variables › Actions):

| Secret | Valor |
|---|---|
| `PACT_BROKER_BASE_URL` | `https://pact.homologacao.forzion.tech` |
| `PACT_BROKER_USERNAME` | mesmo `PACT_BROKER_USERNAME` da VM |
| `PACT_BROKER_PASSWORD` | mesmo `PACT_BROKER_PASSWORD` da VM |

Com os secrets presentes, `contract.yml` passa a **publicar** o pact e rodar
`can-i-deploy` a cada PR/push. Sem eles, o contrato só é gerado/validado.

## Verificação

```bash
# Heartbeat (interno)
docker compose -f docker-compose.homolog.yml exec pact-broker \
  wget -qO- http://localhost:9292/diagnostic/status/heartbeat

# UI (após DNS + cert)
curl -u <user>:<pass> https://pact.homologacao.forzion.tech/
```

## Provider verification (backend .NET)

O contrato publicado é verificado pelo pipeline do **backend** com o
`PactNet` verifier, apontando para `PACT_BROKER_BASE_URL`. Fora deste repo de
deploy — tarefa do lado backend.
