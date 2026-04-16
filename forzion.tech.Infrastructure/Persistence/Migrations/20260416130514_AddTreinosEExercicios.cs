using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTreinosEExercicios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercicios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    grupo_muscular = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exercicios", x => x.id);
                    table.ForeignKey(
                        name: "fk_exercicios_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treinos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    objetivo = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treinos", x => x.id);
                    table.ForeignKey(
                        name: "fk_treinos_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treinos_usuarios_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "execucoes_treino",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_execucao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_execucoes_treino", x => x.id);
                    table.ForeignKey(
                        name: "fk_execucoes_treino_alunos_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_execucoes_treino_treinos_treino_id",
                        column: x => x.treino_id,
                        principalTable: "treinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treino_alunos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treino_alunos", x => x.id);
                    table.ForeignKey(
                        name: "fk_treino_alunos_alunos_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treino_alunos_treinos_treino_id",
                        column: x => x.treino_id,
                        principalTable: "treinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treino_exercicios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series = table.Column<int>(type: "integer", nullable: false),
                    repeticoes = table.Column<int>(type: "integer", nullable: false),
                    carga = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    descanso = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treino_exercicios", x => x.id);
                    table.ForeignKey(
                        name: "fk_treino_exercicios_exercicios_exercicio_id",
                        column: x => x.exercicio_id,
                        principalTable: "exercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treino_exercicios_treinos_treino_id",
                        column: x => x.treino_id,
                        principalTable: "treinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "execucoes_exercicio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_treino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    treino_exercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_executadas = table.Column<int>(type: "integer", nullable: false),
                    repeticoes_executadas = table.Column<int>(type: "integer", nullable: false),
                    carga_executada = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_execucoes_exercicio", x => x.id);
                    table.ForeignKey(
                        name: "fk_execucoes_exercicio_execucoes_treino_execucao_treino_id",
                        column: x => x.execucao_treino_id,
                        principalTable: "execucoes_treino",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_execucoes_exercicio_treino_exercicio_treino_exercicio_id",
                        column: x => x.treino_exercicio_id,
                        principalTable: "treino_exercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_exercicio_execucao_treino_id",
                table: "execucoes_exercicio",
                column: "execucao_treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_exercicio_treino_exercicio_id",
                table: "execucoes_exercicio",
                column: "treino_exercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_aluno_id",
                table: "execucoes_treino",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_tenant_id",
                table: "execucoes_treino",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_treino_id",
                table: "execucoes_treino",
                column: "treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_exercicios_tenant_id",
                table: "exercicios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_aluno_id",
                table: "treino_alunos",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_treino_id",
                table: "treino_alunos",
                column: "treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_exercicios_exercicio_id",
                table: "treino_exercicios",
                column: "exercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_exercicios_treino_id",
                table: "treino_exercicios",
                column: "treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_treinos_tenant_id",
                table: "treinos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_treinos_treinador_id",
                table: "treinos",
                column: "treinador_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "execucoes_exercicio");
            migrationBuilder.DropTable(name: "treino_alunos");
            migrationBuilder.DropTable(name: "execucoes_treino");
            migrationBuilder.DropTable(name: "treino_exercicios");
            migrationBuilder.DropTable(name: "exercicios");
            migrationBuilder.DropTable(name: "treinos");
        }
    }
}
