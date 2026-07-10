using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    // ai_token_usage: telemetria fora do domain model (sem entity/DbSet) — SQL cru só p/ o migrate
    // provisionar o schema (dono = forzion_api, sem grant anon herdado). IF NOT EXISTS porque
    // homolog/develop/public já a tinham (criada antes por CREATE TABLE LIKE) → no-op nos legados.
    // lint-migrations:allow — NOT NULL/UNIQUE recaem sobre tabela recém-criada vazia, não sobre dado pré-existente.
    public partial class AdicionarAiTokenUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ai_token_usage (
                    id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    agent_type character varying NOT NULL,
                    date date NOT NULL,
                    token_count integer NOT NULL,
                    CONSTRAINT pk_ai_token_usage PRIMARY KEY (id)
                );");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_ai_token_usage_user_agent_date
                    ON ai_token_usage (user_id, agent_type, date);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ai_token_usage;");
        }
    }
}
