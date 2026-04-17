using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TriggerSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "homolog");

            migrationBuilder.CreateTable(
                name: "contas",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    tipo_conta = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "logs_aprovacao",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_acao = table.Column<string>(type: "text", nullable: false),
                    realizado_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidade_tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_logs_aprovacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "planos_treinador",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    max_alunos = table.Column<int>(type: "integer", nullable: false),
                    preco = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    is_ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planos_treinador", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alunos",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alunos", x => x.id);
                    table.ForeignKey(
                        name: "fk_alunos_contas_conta_id",
                        column: x => x.conta_id,
                        principalSchema: "homolog",
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_users",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_users_contas_conta_id",
                        column: x => x.conta_id,
                        principalSchema: "homolog",
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treinadores",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plano_treinador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    aprovado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aprovado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treinadores", x => x.id);
                    table.ForeignKey(
                        name: "fk_treinadores_contas_conta_id",
                        column: x => x.conta_id,
                        principalSchema: "homolog",
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treinadores_planos_treinador_plano_treinador_id",
                        column: x => x.plano_treinador_id,
                        principalSchema: "homolog",
                        principalTable: "planos_treinador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercicios",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    grupo_muscular = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exercicios", x => x.id);
                    table.ForeignKey(
                        name: "fk_exercicios_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalSchema: "homolog",
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pacotes_aluno",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    max_fichas = table.Column<int>(type: "integer", nullable: false),
                    preco = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    is_ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pacotes_aluno", x => x.id);
                    table.ForeignKey(
                        name: "fk_pacotes_aluno_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalSchema: "homolog",
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treinos",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    objetivo = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treinos", x => x.id);
                    table.ForeignKey(
                        name: "fk_treinos_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalSchema: "homolog",
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculos_treinador_aluno",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treinador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pacote_aluno_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    aprovado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aprovado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculos_treinador_aluno", x => x.id);
                    table.ForeignKey(
                        name: "fk_vinculos_treinador_aluno_alunos_aluno_id",
                        column: x => x.aluno_id,
                        principalSchema: "homolog",
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vinculos_treinador_aluno_pacotes_aluno_pacote_aluno_id",
                        column: x => x.pacote_aluno_id,
                        principalSchema: "homolog",
                        principalTable: "pacotes_aluno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vinculos_treinador_aluno_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalSchema: "homolog",
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "execucoes_treino",
                schema: "homolog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                        principalSchema: "homolog",
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_execucoes_treino_treinos_treino_id",
                        column: x => x.treino_id,
                        principalSchema: "homolog",
                        principalTable: "treinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treino_alunos",
                schema: "homolog",
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
                        principalSchema: "homolog",
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treino_alunos_treinos_treino_id",
                        column: x => x.treino_id,
                        principalSchema: "homolog",
                        principalTable: "treinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treino_exercicios",
                schema: "homolog",
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
                        principalSchema: "homolog",
                        principalTable: "exercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treino_exercicios_treinos_treino_id",
                        column: x => x.treino_id,
                        principalSchema: "homolog",
                        principalTable: "treinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "execucoes_exercicio",
                schema: "homolog",
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
                        principalSchema: "homolog",
                        principalTable: "execucoes_treino",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_execucoes_exercicio_treino_exercicios_treino_exercicio_id",
                        column: x => x.treino_exercicio_id,
                        principalSchema: "homolog",
                        principalTable: "treino_exercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alunos_conta_id",
                schema: "homolog",
                table: "alunos",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_contas_email",
                schema: "homolog",
                table: "contas",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_exercicio_execucao_treino_id",
                schema: "homolog",
                table: "execucoes_exercicio",
                column: "execucao_treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_exercicio_treino_exercicio_id",
                schema: "homolog",
                table: "execucoes_exercicio",
                column: "treino_exercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_aluno_id",
                schema: "homolog",
                table: "execucoes_treino",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_execucoes_treino_treino_id",
                schema: "homolog",
                table: "execucoes_treino",
                column: "treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_exercicios_treinador_id",
                schema: "homolog",
                table: "exercicios",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_logs_aprovacao_entidade_id",
                schema: "homolog",
                table: "logs_aprovacao",
                column: "entidade_id");

            migrationBuilder.CreateIndex(
                name: "ix_logs_aprovacao_realizado_por_id",
                schema: "homolog",
                table: "logs_aprovacao",
                column: "realizado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_pacotes_aluno_treinador_id",
                schema: "homolog",
                table: "pacotes_aluno",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_users_conta_id",
                schema: "homolog",
                table: "system_users",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_conta_id",
                schema: "homolog",
                table: "treinadores",
                column: "conta_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_plano_treinador_id",
                schema: "homolog",
                table: "treinadores",
                column: "plano_treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_aluno_id",
                schema: "homolog",
                table: "treino_alunos",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_alunos_treino_id",
                schema: "homolog",
                table: "treino_alunos",
                column: "treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_exercicios_exercicio_id",
                schema: "homolog",
                table: "treino_exercicios",
                column: "exercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_treino_exercicios_treino_id",
                schema: "homolog",
                table: "treino_exercicios",
                column: "treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_treinos_treinador_id",
                schema: "homolog",
                table: "treinos",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_treinador_aluno_aluno_id",
                schema: "homolog",
                table: "vinculos_treinador_aluno",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_treinador_aluno_pacote_aluno_id",
                schema: "homolog",
                table: "vinculos_treinador_aluno",
                column: "pacote_aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_treinador_aluno_treinador_id_aluno_id",
                schema: "homolog",
                table: "vinculos_treinador_aluno",
                columns: new[] { "treinador_id", "aluno_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execucoes_exercicio",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "logs_aprovacao",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "system_users",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "treino_alunos",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "vinculos_treinador_aluno",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "execucoes_treino",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "treino_exercicios",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "pacotes_aluno",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "alunos",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "exercicios",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "treinos",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "treinadores",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "contas",
                schema: "homolog");

            migrationBuilder.DropTable(
                name: "planos_treinador",
                schema: "homolog");
        }
    }
}
