# Infraestrutura DNS — forzion.tech + Cloudflare

## Visão geral

```
Domínio forzion.tech (registrador)
  └── nameservers → Cloudflare (DNS gratuito)
        ├── A → IP da VM OCI          (hospedagem)
        ├── TXT SPF → Resend          (autoriza envio de e-mail)
        └── TXT DKIM x2 → Resend     (assina e-mail criptograficamente)
```

Custo: **$0** (Cloudflare DNS é gratuito). Único custo é o domínio (~$10–15/ano).

---

## 1. Criar conta Cloudflare e adicionar o domínio

1. Acesse [cloudflare.com](https://cloudflare.com) → criar conta gratuita
2. **Add a Site** → informar `forzion.tech`
3. Selecionar plano **Free**
4. Cloudflare importa automaticamente os DNS records existentes (se houver)
5. Cloudflare exibe dois nameservers, ex:
   ```
   aria.ns.cloudflare.com
   bob.ns.cloudflare.com
   ```

---

## 2. Apontar nameservers do domínio para Cloudflare

No painel do registrador onde `forzion.tech` foi comprado, substitua os nameservers pelos fornecidos pelo Cloudflare no passo anterior.

> Propagação: de alguns minutos a 48h. Cloudflare avisa por e-mail quando confirmar.

---

## 3. Apontar domínio para a VM OCI

No painel Cloudflare → **DNS** → **Add record**:

| Tipo | Nome | Conteúdo | Proxy |
|------|------|----------|-------|
| `A` | `forzion.tech` | `<IP público da VM OCI>` | Ativado (nuvem laranja) |
| `A` | `www` | `<IP público da VM OCI>` | Ativado |
| `A` | `homolog` | `<IP público da VM OCI>` | Ativado |

> O IP público da VM OCI está em: Console OCI → Compute → Instances → sua VM → **Public IP**.

---

## 4. Verificar domínio no Resend e obter records DNS

1. Acesse [resend.com](https://resend.com) → criar conta gratuita
2. **Domains** → **Add Domain** → informar `forzion.tech`
3. Resend exibe os records a adicionar. Exemplo:

| Tipo | Nome (Host) | Valor |
|------|-------------|-------|
| `TXT` | `forzion.tech` | `v=spf1 include:amazonses.com ~all` |
| `TXT` | `resend._domainkey.forzion.tech` | `p=MIIBIjANBgkq...` (chave DKIM 1) |
| `TXT` | `resend2._domainkey.forzion.tech` | `p=MIIBIjANBgkq...` (chave DKIM 2) |

> Os valores exatos são gerados pelo Resend para sua conta — use os do painel, não os do exemplo acima.

---

## 5. Adicionar records do Resend no Cloudflare

No painel Cloudflare → **DNS** → **Add record** — adicionar cada record fornecido pelo Resend:

**SPF:**
| Campo | Valor |
|-------|-------|
| Tipo | `TXT` |
| Nome | `forzion.tech` (ou `@`) |
| Conteúdo | valor SPF do Resend |
| Proxy | **Desativado** (nuvem cinza) |

**DKIM 1 e DKIM 2** (repetir para cada um):
| Campo | Valor |
|-------|-------|
| Tipo | `TXT` |
| Nome | nome fornecido pelo Resend (ex: `resend._domainkey`) |
| Conteúdo | valor DKIM do Resend |
| Proxy | **Desativado** (nuvem cinza) |

> Records de e-mail (SPF/DKIM) **nunca** devem ter proxy ativado.

---

## 6. Verificar no Resend

No painel Resend → **Domains** → clicar em `forzion.tech` → **Verify**.

Status esperado: `Verified` ✅

Se falhar, aguardar propagação DNS (pode levar até 1h) e tentar novamente.

---

## 7. Obter API Key do Resend

1. Resend → **API Keys** → **Create API Key**
2. Nome: `forzion-prod` (ou `forzion-homolog`)
3. Permission: **Sending access**
4. Copiar a chave (`re_...`) — aparece apenas uma vez

Configurar via secret:

```bash
# Desenvolvimento local
dotnet user-secrets set "Resend:ApiKey" "re_..." --project forzion.tech.Api

# Docker / produção — variável de ambiente
Resend__ApiKey=re_...
```

---

## 8. Estado final dos DNS records

| Tipo | Nome | Destino | Proxy |
|------|------|---------|-------|
| `A` | `forzion.tech` | IP VM OCI | ✅ ativado |
| `A` | `www` | IP VM OCI | ✅ ativado |
| `A` | `homolog` | IP VM OCI | ✅ ativado |
| `TXT` | `forzion.tech` | SPF Resend | ❌ desativado |
| `TXT` | `resend._domainkey` | DKIM 1 Resend | ❌ desativado |
| `TXT` | `resend2._domainkey` | DKIM 2 Resend | ❌ desativado |

---

## Checklist de go-live

- [ ] Conta Cloudflare criada e domínio adicionado
- [ ] Nameservers do registrador apontando para Cloudflare
- [ ] Records A configurados com IP da VM OCI
- [ ] Conta Resend criada e domínio adicionado
- [ ] Records SPF e DKIM adicionados no Cloudflare
- [ ] Domínio verificado no Resend (status `Verified`)
- [ ] API Key do Resend configurada via secret na aplicação
- [ ] Nginx configurado com o domínio correto (`forzion.tech`)
- [ ] SSL obtido via Certbot (`scripts/init-ssl.sh forzion.tech seu@email.com`)
