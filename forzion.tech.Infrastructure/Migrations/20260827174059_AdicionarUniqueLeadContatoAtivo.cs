using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarUniqueLeadContatoAtivo : Migration
    {
        /// <inheritdoc />
        // lint-migrations:allow — dedup de leads ativos duplicados precede o CREATE UNIQUE INDEX.
        // Índice não é declarado via HasIndex/LeadConfiguration: EF Core só suporta índice
        // composto owner+owned via lambda para ComplexProperty (EF Core 11+), não para OwnsOne
        // (table splitting) — confirmado no design-time (`migrations add` falha com "cannot be
        // added... no corresponding CLR property"). O índice fica gerenciado só pelo Postgres.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE leads t
                SET status = 'Descartado', motivo_descarte = 'Duplicado', updated_at = now()
                WHERE anonimizado = false
                  AND status IN ('Novo', 'EmContato')
                  AND id <> (
                    SELECT id FROM leads t2
                    WHERE t2.treinador_id = t.treinador_id
                      AND t2.contato_valor = t.contato_valor
                      AND t2.anonimizado = false
                      AND t2.status IN ('Novo', 'EmContato')
                    ORDER BY created_at DESC, id DESC
                    LIMIT 1
                  );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_leads_treinador_id_contato_valor_ativo
                ON leads (treinador_id, contato_valor)
                WHERE anonimizado = false AND status IN ('Novo', 'EmContato');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_leads_treinador_id_contato_valor_ativo;");
        }
    }
}
