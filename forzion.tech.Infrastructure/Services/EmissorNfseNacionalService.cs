using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Xml;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Services;

public class EmissorNfseNacionalService : IEmissorNfseService, IDisposable
{
    private const string Namespace = "http://www.sped.fazenda.gov.br/nfse";
    private const string VersaoLayout = "1.01";
    private const string VersaoAplicativo = "forzion.tech-1.0";
    private const string CodigoEventoCancelamento = "101101";
    private const string CodigoMotivoCancelamento = "9";

    private readonly HttpClient _httpClient;
    private readonly NfseSettings _settings;
    private readonly ILogger<EmissorNfseNacionalService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Lazy<X509Certificate2> _certificado;

    public EmissorNfseNacionalService(
        HttpClient httpClient,
        IOptions<NfseSettings> settings,
        ILogger<EmissorNfseNacionalService> logger,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _timeProvider = timeProvider;
        _certificado = new Lazy<X509Certificate2>(CarregarCertificadoAssinatura);
    }

    protected virtual X509Certificate2 CarregarCertificadoAssinatura() =>
        new(_settings.CertificadoPath, _settings.CertificadoSenha, X509KeyStorageFlags.EphemeralKeySet);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _certificado.IsValueCreated)
            _certificado.Value.Dispose();
    }

    public async Task<NfseResultado> EmitirAsync(DpsInput input, CancellationToken cancellationToken = default)
    {
        var dpsXmlGZipB64 = MontarAssinarCompactar(input);
        _logger.LogInformation("Transmitindo DPS {NumeroDps} ao Sistema Nacional NFS-e.", input.NumeroDpsEstavel);

        var payload = JsonSerializer.Serialize(new { dpsXmlGZipB64 });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resposta = await _httpClient.PostAsync(Endpoint("nfse"), content, cancellationToken).ConfigureAwait(false);
        var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (EhTransiente(resposta.StatusCode))
        {
            _logger.LogError("Falha {Status} ao transmitir DPS {NumeroDps}.", (int)resposta.StatusCode, input.NumeroDpsEstavel);
            throw new HttpRequestException($"Sistema Nacional NFS-e respondeu {(int)resposta.StatusCode}.");
        }

        if (resposta.IsSuccessStatusCode)
        {
            var (chave, numero, data, danfse) = LerEmissao(corpo);
            _logger.LogInformation("NFS-e autorizada para DPS {NumeroDps}: {ChaveAcesso}.", input.NumeroDpsEstavel, chave);
            return new NfseResultado(true, chave, numero, data, danfse, null, null);
        }

        var (codigo, motivo) = LerErro(corpo, resposta.StatusCode);
        _logger.LogWarning("DPS {NumeroDps} rejeitada: {Codigo} - {Motivo}.", input.NumeroDpsEstavel, codigo, motivo);
        return new NfseResultado(false, null, null, null, null, codigo, motivo);
    }

    public async Task<NfseStatus> ConsultarAsync(string chaveAcesso, CancellationToken cancellationToken = default)
    {
        using var resposta = await _httpClient
            .GetAsync(Endpoint($"nfse/{Uri.EscapeDataString(chaveAcesso)}"), cancellationToken)
            .ConfigureAwait(false);

        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return new NfseStatus(NfseSituacao.NaoEncontrada, null, null, null, null, null);
        }

        if (EhTransiente(resposta.StatusCode))
        {
            throw new HttpRequestException($"Sistema Nacional NFS-e respondeu {(int)resposta.StatusCode}.");
        }

        var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (resposta.IsSuccessStatusCode)
        {
            var (_, numero, data, danfse) = LerEmissao(corpo);
            return new NfseStatus(NfseSituacao.Autorizada, numero, data, danfse, null, null);
        }

        var (codigo, motivo) = LerErro(corpo, resposta.StatusCode);
        return new NfseStatus(NfseSituacao.Rejeitada, null, null, null, codigo, motivo);
    }

    public async Task<NfseResultado> CancelarAsync(string chaveAcesso, string motivo, CancellationToken cancellationToken = default)
    {
        var eventoXmlGZipB64 = MontarAssinarCompactarEvento(chaveAcesso, motivo);
        _logger.LogInformation("Transmitindo cancelamento da NFS-e {ChaveAcesso} ao Sistema Nacional.", chaveAcesso);

        var payload = JsonSerializer.Serialize(new { pedidoRegistroEventoXmlGZipB64 = eventoXmlGZipB64 });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resposta = await _httpClient
            .PostAsync(Endpoint($"nfse/{Uri.EscapeDataString(chaveAcesso)}/eventos"), content, cancellationToken)
            .ConfigureAwait(false);
        var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (EhTransiente(resposta.StatusCode))
        {
            _logger.LogError("Falha {Status} ao cancelar NFS-e {ChaveAcesso}.", (int)resposta.StatusCode, chaveAcesso);
            throw new HttpRequestException($"Sistema Nacional NFS-e respondeu {(int)resposta.StatusCode}.");
        }

        if (resposta.IsSuccessStatusCode)
        {
            _logger.LogInformation("Cancelamento da NFS-e {ChaveAcesso} aceito.", chaveAcesso);
            return new NfseResultado(true, chaveAcesso, null, _timeProvider.GetUtcNow().UtcDateTime, null, null, null);
        }

        var (codigo, motivoErro) = LerErro(corpo, resposta.StatusCode);
        _logger.LogWarning("Cancelamento da NFS-e {ChaveAcesso} rejeitado: {Codigo} - {Motivo}.", chaveAcesso, codigo, motivoErro);
        return new NfseResultado(false, null, null, null, null, codigo, motivoErro);
    }

    private string MontarAssinarCompactarEvento(string chaveAcesso, string motivo)
    {
        var doc = MontarEventoCancelamento(chaveAcesso, motivo, out var idInfPedReg);
        Assinar(doc, idInfPedReg);
        return Compactar(doc);
    }

    private XmlDocument MontarEventoCancelamento(string chaveAcesso, string motivo, out string idInfPedReg)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };

        XmlElement E(string nome, string valor)
        {
            var e = doc.CreateElement(nome, Namespace);
            e.InnerText = valor;
            return e;
        }

        XmlElement G(string nome) => doc.CreateElement(nome, Namespace);

        var raiz = G("pedRegEvento");
        raiz.SetAttribute("versao", VersaoLayout);
        doc.AppendChild(raiz);

        idInfPedReg = $"PRE{Digitos.Apenas(chaveAcesso)}{CodigoEventoCancelamento}001";
        var inf = G("infPedReg");
        inf.SetAttribute("Id", idInfPedReg);
        raiz.AppendChild(inf);

        inf.AppendChild(E("tpAmb", _settings.Ambiente == NfseAmbiente.Producao ? "1" : "2"));
        inf.AppendChild(E("verAplic", VersaoAplicativo));
        inf.AppendChild(E("dhEvento", new DateTimeOffset(_timeProvider.GetUtcNow().UtcDateTime, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)));
        inf.AppendChild(E("CNPJAutor", Digitos.Apenas(_settings.CnpjPrestador)));
        inf.AppendChild(E("chNFSe", chaveAcesso));
        inf.AppendChild(E("nPedRegEvento", "1"));

        var evento = G("e101101");
        evento.AppendChild(E("xDesc", "Cancelamento"));
        evento.AppendChild(E("cMotivo", CodigoMotivoCancelamento));
        evento.AppendChild(E("xMotivo", motivo));
        inf.AppendChild(evento);

        return doc;
    }

    private string MontarAssinarCompactar(DpsInput input)
    {
        var doc = MontarDps(input, out var idInfDps);
        Assinar(doc, idInfDps);
        return Compactar(doc);
    }

    private XmlDocument MontarDps(DpsInput input, out string idInfDps)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };

        XmlElement E(string nome, string valor)
        {
            var e = doc.CreateElement(nome, Namespace);
            e.InnerText = valor;
            return e;
        }

        XmlElement G(string nome) => doc.CreateElement(nome, Namespace);

        var raiz = G("DPS");
        raiz.SetAttribute("versao", VersaoLayout);
        doc.AppendChild(raiz);

        idInfDps = MontarId(input);
        var inf = G("infDPS");
        inf.SetAttribute("Id", idInfDps);
        raiz.AppendChild(inf);

        inf.AppendChild(E("tpAmb", _settings.Ambiente == NfseAmbiente.Producao ? "1" : "2"));
        inf.AppendChild(E("dhEmi", new DateTimeOffset(_timeProvider.GetUtcNow().UtcDateTime, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)));
        inf.AppendChild(E("verAplic", VersaoAplicativo));
        inf.AppendChild(E("serie", SerieDps()));
        inf.AppendChild(E("nDPS", NumeroDps(input)));
        inf.AppendChild(E("dCompet", input.Competencia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        inf.AppendChild(E("tpEmit", "1"));
        inf.AppendChild(E("cLocEmi", input.Prestador.CodigoMunicipioIbge));

        var prest = G("prest");
        prest.AppendChild(E("CNPJ", Digitos.Apenas(input.Prestador.Cnpj)));
        if (!string.IsNullOrWhiteSpace(input.Prestador.InscricaoMunicipal))
        {
            prest.AppendChild(E("IM", input.Prestador.InscricaoMunicipal));
        }

        var regTrib = G("regTrib");
        regTrib.AppendChild(E("opSimpNac", MapearOpSimpNac(input.Prestador.RegimeTributario)));
        regTrib.AppendChild(E("regEspTrib", "0"));
        prest.AppendChild(regTrib);
        inf.AppendChild(prest);

        inf.AppendChild(MontarTomador(input.Tomador, E, G));

        var serv = G("serv");
        var locPrest = G("locPrest");
        locPrest.AppendChild(E("cLocPrestacao", input.Prestador.CodigoMunicipioIbge));
        serv.AppendChild(locPrest);
        var cServ = G("cServ");
        cServ.AppendChild(E("cTribNac", CodigoTributacaoNacional(input.CodigoServico)));
        cServ.AppendChild(E("xDescServ", $"Prestação de serviço {input.CodigoServico}"));
        serv.AppendChild(cServ);
        inf.AppendChild(serv);

        var valores = G("valores");
        var vServPrest = G("vServPrest");
        vServPrest.AppendChild(E("vServ", input.Valor.ToString("0.00", CultureInfo.InvariantCulture)));
        valores.AppendChild(vServPrest);
        var trib = G("trib");
        var tribMun = G("tribMun");
        tribMun.AppendChild(E("tribISSQN", _settings.TribISSQN));
        tribMun.AppendChild(E("tpRetISSQN", _settings.TpRetISSQN));
        tribMun.AppendChild(E("pAliq", input.Aliquota.ToString("0.00", CultureInfo.InvariantCulture)));
        trib.AppendChild(tribMun);
        var totTrib = G("totTrib");
        totTrib.AppendChild(E("indTotTrib", "0"));
        trib.AppendChild(totTrib);
        valores.AppendChild(trib);
        inf.AppendChild(valores);

        return doc;
    }

    private static XmlElement MontarTomador(DadosFiscais tomador, Func<string, string, XmlElement> e, Func<string, XmlElement> g)
    {
        var toma = g("toma");
        var tagDoc = tomador.TipoDocumento == TipoDocumentoFiscal.Cnpj ? "CNPJ" : "CPF";
        toma.AppendChild(e(tagDoc, Digitos.Apenas(tomador.Documento)));
        if (!string.IsNullOrWhiteSpace(tomador.InscricaoMunicipal))
        {
            toma.AppendChild(e("IM", tomador.InscricaoMunicipal));
        }

        toma.AppendChild(e("xNome", tomador.RazaoSocial));

        var end = g("end");
        var endNac = g("endNac");
        endNac.AppendChild(e("cMun", tomador.Endereco.CodigoMunicipioIbge));
        endNac.AppendChild(e("CEP", Digitos.Apenas(tomador.Endereco.Cep)));
        end.AppendChild(endNac);
        end.AppendChild(e("xLgr", tomador.Endereco.Logradouro));
        end.AppendChild(e("nro", tomador.Endereco.Numero));
        if (!string.IsNullOrWhiteSpace(tomador.Endereco.Complemento))
        {
            end.AppendChild(e("xCpl", tomador.Endereco.Complemento!));
        }

        end.AppendChild(e("xBairro", tomador.Endereco.Bairro));
        toma.AppendChild(end);
        return toma;
    }

    // Padrão SPED exige RSA-SHA1 + C14N. São o default do SignedXml quando a chave é RSA e
    // SignatureMethod não é setado — não trocar para SHA256 sem o gov passar a aceitar.
    private void Assinar(XmlDocument doc, string idInfDps)
    {
        var certificado = _certificado.Value;
        var signedXml = new SignedXml(doc) { SigningKey = certificado.GetRSAPrivateKey() };

        var referencia = new Reference("#" + idInfDps);
        referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        referencia.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(referencia);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificado));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        var assinatura = signedXml.GetXml();
        doc.DocumentElement!.AppendChild(doc.ImportNode(assinatura, true));
    }

    private static string Compactar(XmlDocument doc)
    {
        var bytes = Encoding.UTF8.GetBytes(doc.OuterXml);
        using var saida = new MemoryStream();
        using (var gzip = new GZipStream(saida, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(saida.ToArray());
    }

    private string Endpoint(string caminho) => $"{_settings.UrlBase.TrimEnd('/')}/{caminho}";

    private string MontarId(DpsInput input)
    {
        var cMun = Digitos.Apenas(input.Prestador.CodigoMunicipioIbge).PadLeft(7, '0');
        var inscFed = Digitos.Apenas(input.Prestador.Cnpj).PadLeft(14, '0');
        var serie = SerieDps().PadLeft(5, '0');
        var numero = NumeroDps(input).PadLeft(15, '0');
        return $"DPS{cMun}2{inscFed}{serie}{numero}";
    }

    private string SerieDps()
    {
        var serie = Digitos.Apenas(_settings.SerieDps).TrimStart('0');
        return serie.Length == 0 ? "1" : serie;
    }

    private static string NumeroDps(DpsInput input)
    {
        var numero = Digitos.Apenas(input.NumeroDpsEstavel).TrimStart('0');
        return numero.Length == 0 ? "1" : numero;
    }

    private static string CodigoTributacaoNacional(string codigoServico)
    {
        var digitos = Digitos.Apenas(codigoServico);
        return digitos.Length >= 6 ? digitos[..6] : digitos.PadRight(6, '0');
    }

    private static string MapearOpSimpNac(string regime) => (regime ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "mei" => "2",
        var r when r.Contains("simples", StringComparison.Ordinal) => "3",
        _ => "1",
    };

    private static bool EhTransiente(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;

    private static (string? Chave, string? Numero, DateTime? Data, string? Danfse) LerEmissao(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return (null, null, null, null);
        }

        try
        {
            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;
            return (
                Texto(raiz, "chaveAcesso"),
                Texto(raiz, "numeroNfse") ?? Texto(raiz, "nNFSe"),
                DataHora(raiz, "dataHoraProcessamento") ?? DataHora(raiz, "dhProc"),
                Texto(raiz, "danfseUrl") ?? Texto(raiz, "linkDanfse"));
        }
        catch (JsonException)
        {
            return (null, null, null, null);
        }
    }

    private static (string Codigo, string Motivo) LerErro(string corpo, HttpStatusCode status)
    {
        var fallback = ($"HTTP_{(int)status}", $"Sistema Nacional NFS-e respondeu {(int)status}.");
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return fallback;
        }

        try
        {
            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;
            if (raiz.TryGetProperty("erros", out var erros) && erros.ValueKind == JsonValueKind.Array && erros.GetArrayLength() > 0)
            {
                var primeiro = erros[0];
                var codigo = Texto(primeiro, "codigo") ?? fallback.Item1;
                var descricao = Texto(primeiro, "descricao") ?? Texto(primeiro, "mensagem") ?? fallback.Item2;
                var complemento = Texto(primeiro, "complemento");
                var motivo = string.IsNullOrWhiteSpace(complemento) ? descricao : $"{descricao} ({complemento})";
                return (codigo, motivo);
            }

            return fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string? Texto(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static DateTime? DataHora(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var prop)
        && prop.ValueKind == JsonValueKind.String
        && DateTime.TryParse(prop.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var data)
            ? data
            : null;
}
