using System.Runtime.CompilerServices;
using VerifyTests;
using VerifyXunit;

namespace forzion.tech.Tests.Api.Snapshots;

/// <summary>
/// Configuracao global do Verify para os snapshots de contrato de saida.
/// Os arquivos .verified.* ficam co-locados em Api/Snapshots/Snapshots/ e sao versionados;
/// os .received.* sao ignorados pelo .gitignore.
/// </summary>
internal static class VerifyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Snapshots gerados com valores deterministicos: nao queremos que o Verify
        // mascare Guid/DateTime — o objetivo e travar o shape e os valores do contrato.
        VerifierSettings.DontScrubGuids();
        VerifierSettings.DontScrubDateTimes();

        // Co-loca os .verified num subdiretorio Snapshots da pasta do teste.
        Verifier.DerivePathInfo(
            (sourceFile, projectDirectory, type, method) => new PathInfo(
                directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                typeName: type.Name,
                methodName: method.Name));
    }
}
