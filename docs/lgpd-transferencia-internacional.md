# LGPD — Transferência Internacional de Dados: Anthropic API

**Versão:** 1.0  
**Data:** 2026-05-16  
**Responsável:** A definir (DPO ou responsável técnico)  
**Decisão relacionada:** DC-001 (`.agent-security-memory.md`)

---

## 1. Contexto

O Forzion Tech IA utiliza a Anthropic API (Claude Haiku 4.5) como provedor LLM para os assistentes AlunoAssistant e TreinadorAssistant. A Anthropic opera servidores nos EUA. Dados de usuários brasileiros são enviados nas requisições ao modelo.

Esta transferência está sujeita ao **Art. 33 da LGPD** (Lei 13.709/2018), que regula transferência internacional de dados pessoais.

---

## 2. Dados Enviados ao LLM

### 2.1 Dados permitidos (enviados atualmente)

| Dado | Natureza | Justificativa |
|------|----------|---------------|
| Histórico de execuções (data, nome do treino, séries, cargas) | Dado de atividade física | Necessário para funcionalidade do assistente |
| Fichas de treino (nome, objetivo, dificuldade, exercícios) | Dado de saúde (art. 11 LGPD — atividade física) | Necessário para funcionalidade do assistente |
| Progresso (número de execuções, frequência) | Dado de atividade | Necessário para funcionalidade do assistente |
| Mensagem do usuário (linguagem natural) | Dado fornecido voluntariamente | Conteúdo da requisição |

### 2.2 Dados PROIBIDOS (nunca enviar ao LLM)

| Dado | Categoria LGPD | Razão da proibição |
|------|----------------|--------------------|
| CPF | Dado pessoal identificador | Identificação direta — sem necessidade funcional |
| Nome completo | Dado pessoal | Sem necessidade — IDs internos são suficientes |
| Email / telefone | Dado pessoal | Sem necessidade funcional |
| Dados clínicos (diagnósticos, medicações) | Dado sensível (art. 11) | Alto risco; não há base legal adequada |
| Dados financeiros (pagamento, cartão) | Dado pessoal | Sem necessidade funcional; risco elevado |
| Endereço residencial | Dado pessoal | Sem necessidade funcional |

**Controle técnico:** `OutputScanner` bloqueia CPF, CNPJ e cartão no output. Dados proibidos não devem estar no contexto enviado. Ferramentas das tools não incluem campos identificadores diretos nos dados retornados.

---

## 3. Base Legal para Transferência (Art. 33 LGPD)

A LGPD permite transferência internacional quando o país receptor oferece grau de proteção adequado ou mediante garantias adequadas. Em relação à Anthropic:

### 3.1 Adequação do receptor

A Anthropic, Inc. é empresa norte-americana sujeita ao **GDPR** (dados de usuários europeus) e às regulamentações da **California Consumer Privacy Act (CCPA)**. Embora o EUA não possua decisão de adequação formal pela ANPD, a Anthropic possui:

- **Data Processing Agreement (DPA):** disponível para clientes API. **Ação obrigatória:** verificar e assinar DPA antes de qualquer deploy em produção
- **Política de privacidade para API:** Anthropic afirma que dados enviados via API não são usados para treino de modelos por padrão (verificar nos termos vigentes antes do deploy)
- **SOC 2 Type II:** auditoria de segurança anual

### 3.2 Base legal aplicável

**Art. 33, inciso II — Garantias adequadas:**
- Contrato com cláusulas de proteção de dados (DPA Anthropic)
- Limitação de finalidade: dados usados exclusivamente para geração de resposta, não para treino
- Retenção: Anthropic afirma não reter dados de API além do necessário para geração da resposta

**Art. 33, inciso VI — Consentimento do titular:**
- Complementar: colher consentimento informado dos usuários sobre o uso de IA com processamento externo
- **Ação:** incluir cláusula específica nos Termos de Uso e Política de Privacidade do Forzion Tech

---

## 4. Checklist Obrigatório Antes do Deploy em Produção

- [ ] Verificar e baixar DPA Anthropic atual: [anthropic.com/legal/data-processing-addendum](https://www.anthropic.com/legal/data-processing-addendum)
- [ ] Confirmar que API usage não é usada para treino (verificar termos vigentes)
- [ ] Assinar DPA com Anthropic (requer conta API)
- [ ] Atualizar Política de Privacidade do Forzion Tech: informar usuários sobre uso de LLM externo para processamento de dados de treino
- [ ] Atualizar Termos de Uso: cláusula de consentimento para processamento por IA
- [ ] Registrar esta transferência no RIPD (Relatório de Impacto à Proteção de Dados) se mantido
- [ ] Definir responsável (DPO ou equivalente) para monitorar mudanças nos termos da Anthropic

---

## 5. Mitigações Técnicas em Vigor

| Controle | Implementação |
|----------|---------------|
| Dados mínimos (minimização) | Tools retornam apenas campos necessários — sem CPF, nome completo, dados clínicos |
| Pseudonimização | Alunos identificados por `alunoId` (GUID) no contexto do LLM, não por nome completo |
| Output scan | `OutputScanner` bloqueia CPF/CNPJ/cartão/API keys no output |
| Auditoria | Todas as chamadas ao LLM logadas com `AgentRun AlunoId/TreinadorId Tokens` |

---

## 6. Revisão

Esta documentação deve ser revisada:
- Trimestralmente (próxima revisão: 2026-08-16)
- Sempre que os Termos de Serviço da Anthropic forem atualizados
- Sempre que novos tipos de dados forem adicionados ao contexto do LLM
