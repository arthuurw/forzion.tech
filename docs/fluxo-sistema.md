# Forzion.tech — Fluxo do Sistema
### Linguagem Ubíqua para Treinadores

---

## Os Participantes

A plataforma opera com três tipos de participantes:

- **Treinador** — o profissional de educação física. Cria fichas, prescreve treinos e acompanha seus alunos.
- **Aluno** — o cliente do treinador. Recebe fichas, executa treinos e registra seu histórico.
- **Plataforma (Admin)** — a equipe da Forzion, responsável por aprovar treinadores e configurar limites de capacidade.

---

## 1. O Treinador Entra na Plataforma

O treinador realiza seu **cadastro** informando nome, e-mail e senha. A partir desse momento, sua **conta** existe, mas seu acesso ainda não está liberado — ele entra em estado de **Aguardando Aprovação**.

A plataforma revisa o cadastro. Se aprovado, o treinador recebe o status **Ativo** e pode acessar todas as funcionalidades. Se reprovado, o acesso é negado e o motivo pode ser registrado.

> **Por que existe aprovação?**
> Para garantir que apenas profissionais qualificados operam na plataforma, protegendo a experiência dos alunos.

---

## 2. O Plano do Treinador

Após a aprovação, a plataforma atribui ao treinador um **Plano**. O Plano define a **capacidade máxima de alunos simultâneos** que aquele treinador pode ter ativos.

Por exemplo: um Plano "Starter" pode permitir até 10 alunos. Um Plano "Pro" pode permitir até 50.

O treinador não escolhe seu próprio Plano — ele é definido e atribuído pela plataforma conforme o contrato estabelecido.

> **Impacto prático:** se o treinador já atingiu seu limite de alunos, ele não consegue aprovar novos vínculos até que um dos alunos ativos seja desvinculado, ou até que a plataforma eleve seu Plano.

---

## 3. O Treinador Prepara sua Estrutura

Antes de receber alunos, o treinador configura dois elementos fundamentais:

### 3.1 Pacotes

O **Pacote** é uma configuração criada pelo treinador que define os termos do atendimento oferecido a um aluno. Cada Pacote tem um nome, uma descrição livre e um preço.

Exemplos:
- **Pacote Básico** — "Acompanhamento mensal com 1 treino" — R$ 150/mês
- **Pacote Intermediário** — "Acompanhamento mensal com até 3 treinos" — R$ 250/mês
- **Pacote Premium** — "Acompanhamento completo, treinos ilimitados" — R$ 400/mês

O treinador pode criar quantos Pacotes quiser. Quando um aluno se cadastra na plataforma, ele escolhe o Pacote daquele treinador.

### 3.2 Biblioteca de Exercícios

O treinador monta sua **Biblioteca de Exercícios** — uma coleção de movimentos que ele usará para compor as fichas. Cada exercício tem nome, grupo muscular e uma descrição opcional.

A plataforma também oferece uma **Biblioteca Global** com exercícios pré-cadastrados. O treinador pode copiar qualquer exercício global para sua própria biblioteca e utilizá-lo livremente.

---

## 4. O Aluno Entra na Plataforma

O aluno não se cadastra de forma independente — ele se cadastra **vinculado a um treinador**. No momento do cadastro, o aluno:

1. Informa seus dados pessoais (nome, e-mail, senha e, opcionalmente, telefone).
2. Escolhe o **Treinador** com quem quer trabalhar.
3. Escolhe o **Pacote** daquele treinador.

Ao concluir o cadastro, o aluno e seu **Vínculo** com o treinador entram em estado de **Aguardando Aprovação**. O aluno ainda não tem acesso às fichas de treino.

---

## 5. O Treinador Aprova o Vínculo

O treinador vê em seu painel os vínculos pendentes de aprovação. Ao aprovar um vínculo:

- O **Vínculo** passa para o status **Ativo**.
- A data de início do acompanhamento é registrada.
- O Pacote escolhido pelo aluno é confirmado.

Antes de aprovar, o sistema valida duas condições:
- O aluno não pode já estar ativo com outro treinador.
- O treinador não pode ter ultrapassado o limite de alunos do seu Plano.

Se qualquer uma dessas condições não for atendida, a aprovação é bloqueada.

> A partir deste momento, o aluno está **ativo** na carteira do treinador e pode receber fichas de treino.

O treinador também pode **desvincular** um aluno a qualquer momento, registrando opcionalmente uma observação. O Vínculo passa para **Inativo** e a data de encerramento é registrada. O histórico de execuções do aluno é preservado.

> **Efeito em cascata:** ao desvincular um aluno, todas as fichas ativas desse aluno com o treinador são automaticamente inativadas.

---

## 5.1 Reativando um Aluno

Se o vínculo de um aluno foi encerrado, o treinador pode **reativar** esse aluno a qualquer momento — desde que o treinador ainda tenha capacidade disponível no seu Plano.

Ao reativar, o treinador escolhe um Pacote (o mesmo ou um diferente do anterior). Um novo Vínculo é criado e aprovado diretamente, sem a etapa de aprovação manual. O histórico anterior do aluno é preservado integralmente.

> O treinador não é obrigado a ter tido um vínculo anterior com o aluno para reativá-lo — qualquer aluno inativo pode ser reconectado dessa forma.

---

## 6. O Treinador Cria e Prescreve Fichas

### 6.1 Criando uma Ficha

Uma **Ficha de Treino** é o protocolo de exercícios prescrito pelo treinador. Cada Ficha tem um nome e um **Objetivo** (Hipertrofia, Força, Resistência, Emagrecimento, Flexibilidade ou Condicionamento).

Após criar a Ficha, o treinador a compõe adicionando exercícios de sua Biblioteca. Para cada exercício, ele define:

- **Séries** — quantas séries o aluno deve realizar.
- **Repetições** — quantas repetições por série.
- **Carga** — peso em quilogramas (opcional).
- **Descanso** — tempo de descanso entre séries, em segundos (opcional).

O treinador pode reordenar, adicionar e remover exercícios a qualquer momento. Uma Ficha também pode ser **duplicada** — gerando uma cópia idêntica para ser adaptada.

### 6.2 Vinculando a Ficha ao Aluno

Quando a Ficha está pronta, o treinador a vincula a um aluno específico. A partir deste momento, o aluno pode visualizá-la e executá-la.

Antes de vincular, o sistema valida:
- O aluno tem um Vínculo ativo com o treinador.

A mesma Ficha pode ser vinculada a múltiplos alunos — cada vínculo é tratado de forma independente.

---

## 7. O Aluno Executa o Treino

O aluno acessa sua lista de **Fichas Ativas** e seleciona a que deseja executar. A tela de execução mostra, exercício por exercício, os parâmetros prescritos pelo treinador (séries, repetições, carga, descanso).

Ao concluir a sessão, o aluno registra uma **Execução**, informando:

- A **data** em que o treino foi realizado.
- Para cada exercício: quantas séries, repetições e qual carga efetivamente executou.
- Uma **observação** livre (opcional) — pode ser a sensação durante o treino, alguma limitação, etc.

Os valores executados podem ser diferentes dos prescritos. O registro captura o que realmente aconteceu, não apenas o que foi planejado.

---

## 8. O Histórico de Evolução

Cada Execução registrada forma o **Histórico** do aluno. O histórico é cumulativo — mesmo que uma Ficha seja inativada, o aluno seja desvinculado ou troque de treinador, as execuções passadas permanecem registradas para sempre.

O histórico permite que o aluno acompanhe sua própria evolução ao longo do tempo.

---

## 9. Troca de Treinador

O aluno pode, a qualquer momento, solicitar a troca para outro treinador disponível na plataforma.

O processo funciona assim:

1. O aluno acessa seu perfil e escolhe o **novo treinador** e o **Pacote** desejado.
2. Um novo Vínculo é criado com o novo treinador, em estado de **Aguardando Aprovação**.
3. O vínculo com o treinador atual **permanece ativo** enquanto a solicitação não for aprovada — o aluno continua recebendo atendimento normalmente.
4. Quando o novo treinador **aprova** o vínculo, a transição acontece automaticamente:
   - O vínculo anterior é **encerrado**.
   - Todas as fichas ativas do treinador anterior são **inativadas**.
   - O aluno passa a ser atendido pelo novo treinador.

> O aluno não pode ter duas solicitações de troca pendentes ao mesmo tempo. Enquanto aguarda a aprovação do novo treinador, o botão de solicitar nova troca fica desabilitado.

> O histórico de execuções com o treinador anterior é preservado integralmente após a troca.

---

## Resumo do Fluxo Completo

```
PLATAFORMA
  └─ Aprova Treinador
  └─ Atribui Plano ao Treinador (define limite de alunos)

TREINADOR (após aprovado)
  └─ Cria Pacotes (define limite de fichas por aluno)
  └─ Monta Biblioteca de Exercícios
  └─ Cria Fichas de Treino (compõe com exercícios)

ALUNO
  └─ Cadastra-se escolhendo Treinador + Pacote
     └─ Vínculo criado → Aguardando Aprovação

TREINADOR
  └─ Aprova o Vínculo
     └─ Aluno fica Ativo na carteira
  └─ Vincula Fichas ao Aluno
     └─ Respeita o limite do Pacote do Aluno

ALUNO (após ativo e com fichas)
  └─ Visualiza Fichas Ativas
  └─ Executa e Registra cada sessão
     └─ Histórico cresce ao longo do tempo

── Ciclo de vida do Vínculo ──────────────────────────────

  Desvínculo pelo Treinador
    └─ Vínculo → Inativo
    └─ Fichas ativas do par → Inativas (cascata)
    └─ Histórico preservado

  Reativação pelo Treinador
    └─ Novo Vínculo criado → Ativo imediatamente
    └─ Treinador escolhe Pacote
    └─ Sujeito ao limite do Plano

  Troca de Treinador pelo Aluno
    └─ Vínculo pendente criado com novo treinador
    └─ Vínculo atual permanece ativo durante espera
    └─ Novo treinador aprova →
         └─ Vínculo anterior → Inativo
         └─ Fichas do treinador anterior → Inativas (cascata)
         └─ Histórico preservado
```

---

## Glossário

| Termo | Significado |
|---|---|
| **Conta** | Credencial de acesso à plataforma (e-mail + senha) |
| **Treinador** | Profissional cadastrado e aprovado pela plataforma |
| **Aluno** | Cliente do treinador, cadastrado na plataforma |
| **Vínculo** | Relação formal e rastreável entre um Treinador e um Aluno |
| **Plano** | Limite de alunos simultâneos de um Treinador, definido pela plataforma |
| **Pacote** | Configuração do Treinador que define nome, descrição e preço do atendimento oferecido ao Aluno |
| **Ficha de Treino** | Protocolo de exercícios prescrito pelo Treinador |
| **Exercício** | Movimento individual (ex: Supino, Agachamento) com parâmetros |
| **Execução** | Registro de uma sessão de treino realizada pelo Aluno |
| **Histórico** | Conjunto de todas as Execuções de um Aluno ao longo do tempo |
| **Aprovação** | Ato de liberar um cadastro ou vínculo para operação |
| **Aguardando Aprovação** | Estado intermediário antes da liberação |
| **Ativo** | Estado operacional — pode usar as funcionalidades correspondentes |
| **Inativo** | Estado encerrado — sem acesso às funcionalidades, histórico preservado |
| **Reativação** | Ato do treinador de reconectar um aluno inativo, criando um novo vínculo ativo |
| **Troca de Treinador** | Solicitação do aluno para migrar para outro treinador, com aprovação do novo |
| **Cascata** | Efeito automático: ao encerrar um vínculo, todas as fichas ativas do par são inativadas |
