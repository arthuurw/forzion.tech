using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record DadosFiscais
{
    public TipoDocumentoFiscal TipoDocumento { get; }
    public string Documento { get; }
    public string RazaoSocial { get; }
    public EnderecoFiscal Endereco { get; }
    public string? InscricaoMunicipal { get; }

    private DadosFiscais()
    {
        Documento = null!;
        RazaoSocial = null!;
        Endereco = null!;
    }

    private DadosFiscais(TipoDocumentoFiscal tipoDocumento, string documento, string razaoSocial, EnderecoFiscal endereco, string? inscricaoMunicipal)
    {
        TipoDocumento = tipoDocumento;
        Documento = documento;
        RazaoSocial = razaoSocial;
        Endereco = endereco;
        InscricaoMunicipal = inscricaoMunicipal;
    }

    public static Result<DadosFiscais> Criar(
        TipoDocumentoFiscal tipoDocumento,
        string documento,
        string razaoSocial,
        EnderecoFiscal endereco,
        string? inscricaoMunicipal = null)
    {
        if (endereco is null)
            return Result.Failure<DadosFiscais>(DadosFiscaisErrors.EnderecoObrigatorio);
        if (string.IsNullOrWhiteSpace(razaoSocial))
            return Result.Failure<DadosFiscais>(DadosFiscaisErrors.RazaoSocialObrigatoria);
        var razao = razaoSocial.Trim();
        if (razao.Length > 150)
            return Result.Failure<DadosFiscais>(DadosFiscaisErrors.RazaoSocialMuitoLonga);

        var docDigitos = Digitos.Apenas(documento);
        var documentoValido = tipoDocumento switch
        {
            TipoDocumentoFiscal.Cpf => CpfValido(docDigitos),
            TipoDocumentoFiscal.Cnpj => CnpjValido(docDigitos),
            _ => false
        };
        if (tipoDocumento is not (TipoDocumentoFiscal.Cpf or TipoDocumentoFiscal.Cnpj))
            return Result.Failure<DadosFiscais>(DadosFiscaisErrors.TipoDocumentoInvalido);
        if (!documentoValido)
            return Result.Failure<DadosFiscais>(DadosFiscaisErrors.DocumentoInvalido);

        var inscricao = string.IsNullOrWhiteSpace(inscricaoMunicipal) ? null : Digitos.Apenas(inscricaoMunicipal);

        return Result.Success(new DadosFiscais(tipoDocumento, docDigitos, razao, endereco, inscricao));
    }

    private static bool CpfValido(string cpf)
    {
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1)
            return false;

        var digitos = cpf.Select(c => c - '0').ToArray();
        var d1 = DigitoVerificador(digitos, 9, 10);
        var d2 = DigitoVerificador(digitos, 10, 11);
        return digitos[9] == d1 && digitos[10] == d2;
    }

    private static bool CnpjValido(string cnpj)
    {
        if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1)
            return false;

        var digitos = cnpj.Select(c => c - '0').ToArray();
        var pesos1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var pesos2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var d1 = DigitoVerificadorPonderado(digitos, pesos1);
        var d2 = DigitoVerificadorPonderado(digitos, pesos2);
        return digitos[12] == d1 && digitos[13] == d2;
    }

    private static int DigitoVerificador(int[] digitos, int quantidade, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
            soma += digitos[i] * (pesoInicial - i);

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int DigitoVerificadorPonderado(int[] digitos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += digitos[i] * pesos[i];

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
