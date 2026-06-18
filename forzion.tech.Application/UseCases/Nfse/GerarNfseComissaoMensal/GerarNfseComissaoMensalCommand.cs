namespace forzion.tech.Application.UseCases.Nfse.GerarNfseComissaoMensal;

public sealed record GerarNfseComissaoMensalCommand(DateOnly CompetenciaInicio, DateOnly CompetenciaFim);

public sealed record GerarNfseComissaoMensalResultado(int Geradas, int Puladas);
