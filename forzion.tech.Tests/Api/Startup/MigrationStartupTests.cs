using forzion.tech.Api.Startup;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Startup;

// R1 (deploy-safety): garante que o migrate é desacoplado do boot. O ponto crítico do incidente
// 2026-06-11 foi DDL no startup em Homolog; aqui asserimos que só Development auto-migra e que a
// flag de CLI é reconhecida para o step one-shot pré-deploy.
public class MigrationStartupTests
{
    [Theory]
    [InlineData("migrate")]
    [InlineData("--migrate")]
    public void IsMigrateCommand_ReconheceFlag(string arg) =>
        Assert.True(MigrationStartup.IsMigrateCommand([arg]));

    [Fact]
    public void IsMigrateCommand_BootNormal_Falso()
    {
        Assert.False(MigrationStartup.IsMigrateCommand([]));
        Assert.False(MigrationStartup.IsMigrateCommand(["serve"]));
    }

    [Fact]
    public void ShouldAutoMigrateOnBoot_Development_True() =>
        Assert.True(MigrationStartup.ShouldAutoMigrateOnBoot(Env("Development")));

    [Theory]
    [InlineData("Homolog")]
    [InlineData("Production")]
    [InlineData("Test")]
    public void ShouldAutoMigrateOnBoot_NaoDev_Falso(string environmentName) =>
        Assert.False(MigrationStartup.ShouldAutoMigrateOnBoot(Env(environmentName)));

    private static IHostEnvironment Env(string name) =>
        Mock.Of<IHostEnvironment>(e => e.EnvironmentName == name);
}
