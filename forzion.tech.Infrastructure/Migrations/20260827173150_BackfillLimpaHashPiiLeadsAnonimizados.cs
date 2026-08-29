using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLimpaHashPiiLeadsAnonimizados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE leads
                SET argumentos_hash = NULL, idempotency_key = NULL
                WHERE anonimizado = true;
                """);
        }

        /// <inheritdoc />
        // Forward-only (specification-design-review §5): o hash original não é recuperável a
        // partir do dado já anonimizado, então Down não reconstrói o valor pré-migration.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
