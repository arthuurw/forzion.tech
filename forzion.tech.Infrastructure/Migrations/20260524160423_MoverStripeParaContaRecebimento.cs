using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoverStripeParaContaRecebimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conta_recebimento",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_connect_account_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    onboarding_completo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conta_recebimento", x => x.id);
                    table.ForeignKey(
                        name: "fk_conta_recebimento_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalSchema: "homolog",
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conta_recebimento_stripe_connect_account_id",
                schema: "homolog",
                table: "conta_recebimento",
                column: "stripe_connect_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_conta_recebimento_treinador_id",
                schema: "homolog",
                table: "conta_recebimento",
                column: "treinador_id",
                unique: true);

            // Move o estado de Stripe Connect que vivia em treinadores para a nova
            // projeção de billing. Só treinadores que já têm conta Stripe geram linha.
            migrationBuilder.Sql(@"
                INSERT INTO homolog.conta_recebimento
                    (id, treinador_id, stripe_connect_account_id, onboarding_completo, created_at)
                SELECT gen_random_uuid(), id, stripe_connect_account_id, stripe_onboarding_completo, now()
                FROM homolog.treinadores
                WHERE stripe_connect_account_id IS NOT NULL;");

            migrationBuilder.DropIndex(
                name: "ix_treinadores_stripe_connect_account_id",
                schema: "homolog",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "stripe_connect_account_id",
                schema: "homolog",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "stripe_onboarding_completo",
                schema: "homolog",
                table: "treinadores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "stripe_connect_account_id",
                schema: "homolog",
                table: "treinadores",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "stripe_onboarding_completo",
                schema: "homolog",
                table: "treinadores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Restaura o estado de Stripe Connect de volta para treinadores.
            migrationBuilder.Sql(@"
                UPDATE homolog.treinadores t
                SET stripe_connect_account_id = c.stripe_connect_account_id,
                    stripe_onboarding_completo = c.onboarding_completo
                FROM homolog.conta_recebimento c
                WHERE c.treinador_id = t.id;");

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_stripe_connect_account_id",
                schema: "homolog",
                table: "treinadores",
                column: "stripe_connect_account_id");

            migrationBuilder.DropTable(
                name: "conta_recebimento",
                schema: "homolog");
        }
    }
}
