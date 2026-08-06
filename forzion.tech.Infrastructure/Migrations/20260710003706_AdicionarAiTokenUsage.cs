using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    // ai_token_usage: telemetria fora do domain model (sem entity/DbSet). IF NOT EXISTS porque
    // homolog/develop/public já a tinham com dado real (CREATE TABLE LIKE prévio) → no-op nos legados.
    // lint-migrations:allow — UNIQUE tratado defensivamente no próprio SQL (ver Up()).
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

            // Duplicatas pré-existentes nos legados dariam 23505 e abortariam o migrate; degrada pra RAISE NOTICE.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    CREATE UNIQUE INDEX IF NOT EXISTS ix_ai_token_usage_user_agent_date
                        ON ai_token_usage (user_id, agent_type, date);
                EXCEPTION
                    WHEN unique_violation THEN
                        RAISE NOTICE 'ai_token_usage: duplicatas pré-existentes em (user_id, agent_type, date) — unique index NAO criado; dedup manual necessario.';
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: Up() é no-op nos legados (tabela já existia com dado real) — esta
            // migration nunca é dona dos dados; DROP TABLE aqui destruiria telemetria que não criou.
        }
    }
}
