using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsTreinosEExercicios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE exercicios ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON exercicios
                    USING (tenant_id::text = current_setting('app.current_tenant_id', true));

                ALTER TABLE treinos ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON treinos
                    USING (tenant_id::text = current_setting('app.current_tenant_id', true));

                ALTER TABLE execucoes_treino ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON execucoes_treino
                    USING (tenant_id::text = current_setting('app.current_tenant_id', true));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON execucoes_treino;
                ALTER TABLE execucoes_treino DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON treinos;
                ALTER TABLE treinos DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON exercicios;
                ALTER TABLE exercicios DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
