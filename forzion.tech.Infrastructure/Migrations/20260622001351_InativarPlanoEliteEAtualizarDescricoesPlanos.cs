using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InativarPlanoEliteEAtualizarDescricoesPlanos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE planos_plataforma SET is_ativo = false, updated_at = now() WHERE tier = 'Elite' AND is_ativo = true;");

            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Ideal para começar e testar sem compromisso. Acesso à plataforma para até 10 alunos.', updated_at = now() WHERE tier = 'Free' AND descricao IS NULL;");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Acesso completo à plataforma de treinos. R$2 por aluno/mês na lotação.', updated_at = now() WHERE tier = 'Basic' AND descricao = 'Acesso somente à plataforma';");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Tudo do Basic + notificações por e-mail que mantêm seus alunos engajados entre as sessões.', updated_at = now() WHERE tier = 'Pro' AND descricao = 'Basic + e-mail';");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Tudo do Pro + WhatsApp integrado: seus alunos recebem tudo onde já estão.', updated_at = now() WHERE tier = 'ProPlus' AND descricao = 'Pro + WhatsApp';");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'O plano mais completo: tudo do Pro Plus somado a IA para personalizar e otimizar cada treino.', updated_at = now() WHERE tier = 'Elite' AND descricao = 'Pro Plus + IA';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = NULL, updated_at = now() WHERE tier = 'Free' AND descricao = 'Ideal para começar e testar sem compromisso. Acesso à plataforma para até 10 alunos.';");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Acesso somente à plataforma', updated_at = now() WHERE tier = 'Basic' AND descricao = 'Acesso completo à plataforma de treinos. R$2 por aluno/mês na lotação.';");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Basic + e-mail', updated_at = now() WHERE tier = 'Pro' AND descricao = 'Tudo do Basic + notificações por e-mail que mantêm seus alunos engajados entre as sessões.';");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Pro + WhatsApp', updated_at = now() WHERE tier = 'ProPlus' AND descricao = 'Tudo do Pro + WhatsApp integrado: seus alunos recebem tudo onde já estão.';");
            migrationBuilder.Sql("UPDATE planos_plataforma SET descricao = 'Pro Plus + IA', updated_at = now() WHERE tier = 'Elite' AND descricao = 'O plano mais completo: tudo do Pro Plus somado a IA para personalizar e otimizar cada treino.';");

            migrationBuilder.Sql("UPDATE planos_plataforma SET is_ativo = true, updated_at = now() WHERE tier = 'Elite' AND is_ativo = false;");
        }
    }
}
