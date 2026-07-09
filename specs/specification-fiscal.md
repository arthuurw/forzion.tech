# specification-fiscal — dados fiscais do treinador (forzion.tech)

DOC PARA AGENTES. Formato denso, agent-oriented.

## STATUS: EMISSÃO DE NFS-e REMOVIDA
A emissão de NFS-e (SEFIN Nacional, cert A1/mTLS, máquina de estado da nota, crons de comissão/reconciliação, telas de nota, e-mails DANFSe/bloqueio) foi REMOVIDA do backend pela feature `remocao-emissao-nfse`. A emissão passa a ser responsabilidade de **software fiscal terceiro**. NÃO existe mais no código: `NotaFiscal`, `IEmissorNfseService`/`EmissorNfseNacionalService`/`NullEmissorNfseService`, `NfseSettings`/`NfseMtlsCertificate`, `INotaFiscalRepository`, enums `NotaFiscalStatus`/`TipoNotaFiscal`, eventos `NotaFiscalEmitidaEvent`/`NotaFiscalBloqueadaDadosFiscaisEvent`, `NotaFiscalErrors`, handlers de emissão/comissão/cancelamento/reconciliação/e-mail, endpoints de nota, workflows `gerar-nfse-comissao.yml`/`reconciliar-nfse.yml`, tabela `notas_fiscais` (dropada pela migration `RemoverNotasFiscais`). A query de comissão (`ListarComissaoPorTreinadorNoPeriodoAsync`/`ComissaoTreinadorPeriodo`) também saiu (YAGNI). O histórico completo do que existia está no git antes de `remocao-emissao-nfse`.

## RETIDO: coleta de dados fiscais do treinador (handoff futuro ao terceiro)
A coleta dos dados fiscais do treinador PERMANECE (decisão do usuário: o software terceiro pode precisar dos dados do tomador). Receita/fee da plataforma (`ApplicationFeeAmount` Stripe) é independente da emissão e ficou intacta.

- `DadosFiscais` (Domain/ValueObjects) — VO owned por `Treinador`. `Criar(tipoDoc, documento, razaoSocial, EnderecoFiscal, inscricaoMunicipal?)` valida dígito CPF/CNPJ, CEP, código IBGE, UF. `enum TipoDocumentoFiscal {Cpf=0, Cnpj=1}`. `EnderecoFiscal{Logradouro, Numero, Complemento?, Bairro, CodigoMunicipioIbge, Uf, Cep}`. `Treinador.DadosFiscais? DadosFiscais` (nullable até preenchido) + `Result DefinirDadosFiscais(...)`. Persistido owned na tabela `treinadores` (colunas `dados_fiscais_*`, sem join) — [specification-db].
- `DefinirDadosFiscaisTreinadorHandler` (Application): persiste os dados fiscais e comita; NÃO desbloqueia/reenfileira nota (essa lógica saiu com a emissão). `TipoAcaoAprovacao.DefinicaoDadosFiscaisTreinador (=11)` audita a gravação.
- Endpoints (auth `IUserContext` próprio, exceto CEP): `PUT /treinador/dados-fiscais` (grava; CPF/CNPJ inválido → 422); `GET /treinador/dados-fiscais` (lê); `GET /treinador/cep/{cep}` (group `Treinador`, RL `read`; autofill).

### Autofill de endereço (CEP → endereço)
- Objetivo UX: ao digitar o CEP no form de dados fiscais, preencher `Logradouro/Complemento/Bairro/Localidade/Uf/CodigoMunicipioIbge` — reduz erro de digitação no IBGE/UF que reprova `DadosFiscais.Criar`. NÃO é fonte fiscal autoritativa; treinador confere/edita; `PUT /treinador/dados-fiscais` revalida tudo.
- `IConsultaCepService` (Application/Interfaces): `ConsultarAsync(cep, ct) → Result<ConsultaCepResultado>`. `ConsultaCepResultado(Logradouro, Complemento, Bairro, Localidade, Uf, CodigoMunicipioIbge)`. Erros `ConsultaCepErrors`: `CepInvalido` (Validation→400, ≠8 dígitos), `CepNaoEncontrado` (NotFound→404, ViaCEP `{erro:true}`), `ServicoIndisponivel` (**ExternalService→502**, não-2xx/timeout/HttpRequestException/InvalidOperationException/null).
- Impl `ViaCepConsultaCepService` (Infrastructure/Services): normaliza CEP a 8 dígitos via `Domain.Shared.Digitos.Apenas`; `GET {ViaCep:UrlBase}{cep}/json/`. ViaCEP retorna 200 mesmo p/ CEP inexistente (`{erro:true}`) → `BoolTolerante` (`JsonConverter<bool>`) aceita `erro` como bool OU string. Sem retry (best-effort, não outbox). GOTCHA: `InvalidOperationException` (`BaseAddress` ausente quando `ViaCep:UrlBase` em branco) está no catch → degrada p/ `ServicoIndisponivel` (502), nunca 500.
- Config `ViaCep:*` (sem classe Options; lido direto): `UrlBase` (`https://viacep.com.br/ws/`), `TimeoutSegundos` (default 4). `HttpClient` nomeado `"viacep"`; `IConsultaCepService` Scoped. SEM gate Habilitado/Null — ViaCEP é público e sem credencial; indisponibilidade degrada via 502 (form usável manualmente).

### Frontend + banner
- `dados-fiscais/page.tsx` → `nfseApi.getDadosFiscais`/`salvarDadosFiscais`/`consultarCep` (`lib/api/nfse.ts`, arquivo MANTIDO com só essas 3 chamadas + tipos de dados fiscais/CEP). Autofill dispara quando o CEP atinge 8 dígitos; `AbortController` cancela busca anterior. Popula campos só quando não-vazios (não apaga valor já digitado). Falha: 404 → "CEP não encontrado…"; 502/rede → "…preencha o endereço manualmente."
- Banner proativo `dadosFiscaisPendentes` no `GET /treinador/dashboard`: `true` quando dados fiscais ausentes E (plano pago, tier `!= Free`, OU `ModoPagamentoAluno.Plataforma`); dashboard renderiza banner com link `/treinador/dados-fiscais`.

### LGPD
- Dados fiscais retidos seguem a guarda fiscal (~5 anos pós-cancelamento) — [specification-lgpd].

## HANDOFF FUTURO (fora de escopo)
Export/integração dos dados fiscais ao software fiscal terceiro é feature nova futura; esta base só RETÉM a coleta, não integra.
