using FluentAssertions;
using forzion.tech.Infrastructure.Notifications.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EmailTemplatesXssTests
{
    private const string Payload = "<script>alert(1)</script>\"";
    private const string Encoded = "&lt;script&gt;alert(1)&lt;/script&gt;&quot;";

    private static void DeveEstarEscapado(string html)
    {
        html.Should().Contain(Encoded);
        html.Should().NotContain("<script>");
    }

    [Fact]
    public void TreinadorAprovado_escapa_nome()
    {
        DeveEstarEscapado(EmailTemplates.TreinadorAprovado(Payload));
    }

    [Fact]
    public void TreinadorReprovado_escapa_nome()
    {
        DeveEstarEscapado(EmailTemplates.TreinadorReprovado(Payload));
    }

    [Fact]
    public void TreinadorInativado_escapa_nome()
    {
        DeveEstarEscapado(EmailTemplates.TreinadorInativado(Payload));
    }

    [Fact]
    public void VinculoAprovado_escapa_nomeAluno()
    {
        DeveEstarEscapado(EmailTemplates.VinculoAprovado(Payload, "Treinador"));
    }

    [Fact]
    public void VinculoAprovado_escapa_nomeTreinador()
    {
        DeveEstarEscapado(EmailTemplates.VinculoAprovado("Aluno", Payload));
    }

    [Fact]
    public void BemVindoAluno_escapa_nome()
    {
        DeveEstarEscapado(EmailTemplates.BemVindoAluno(Payload));
    }

    [Fact]
    public void AlunoInativado_escapa_nome()
    {
        DeveEstarEscapado(EmailTemplates.AlunoInativado(Payload));
    }

    [Fact]
    public void AssinaturaAlunoCriada_escapa_nomeAluno()
    {
        DeveEstarEscapado(EmailTemplates.AssinaturaAlunoCriada(Payload, "Treinador", "Pacote", 149.90m));
    }

    [Fact]
    public void AssinaturaAlunoCriada_escapa_nomeTreinador()
    {
        DeveEstarEscapado(EmailTemplates.AssinaturaAlunoCriada("Aluno", Payload, "Pacote", 149.90m));
    }

    [Fact]
    public void AssinaturaAlunoCriada_escapa_nomePacote()
    {
        DeveEstarEscapado(EmailTemplates.AssinaturaAlunoCriada("Aluno", "Treinador", Payload, 149.90m));
    }
}
