using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable S1481 // Remove unused local variables
#pragma warning disable S2325 // Methods that don't access instance data should be static
#pragma warning disable S3400 // Methods should not return constants

namespace forzion.tech.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicioDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration is schema-agnostic and works for any PostgreSQL schema
            // The schema name is read from the __EFMigrationsHistory table location

            var schemaName = migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL" 
                ? GetCurrentSchema() 
                : "dbo";

            // Cleanup legacy tables if they exist
            migrationBuilder.Sql($@"
                DO $$ 
                DECLARE
                    v_schema TEXT := current_schema();
                BEGIN
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.execucoes_exercicio CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.treino_exercicios CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.treino_alunos CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.execucoes_treino CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.vinculos_treinador_aluno CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.pacotes_aluno CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.treinos CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.exercicios CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.treinadores CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.system_users CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.alunos CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.planos_treinador CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.logs_aprovacao CASCADE', v_schema);
                    EXECUTE FORMAT('DROP TABLE IF EXISTS %I.contas CASCADE', v_schema);
                END $$;
            ");

            // Create the default schema if needed (will be public or homolog based on context)
            migrationBuilder.Sql($@"
                CREATE SCHEMA IF NOT EXISTS public;
            ");

            // All tables created via EF Core migration builder
            // The schema will be applied from the DbContext configuration
            migrationBuilder.CreateTable(
                name: "contas",
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
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_users",
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
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treinadores",
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
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treinadores_planos_treinador_plano_treinador_id",
                        column: x => x.plano_treinador_id,
                        principalTable: "planos_treinador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercicios",
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
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pacotes_aluno",
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
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treinos",
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
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculos_treinador_aluno",
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
                        principalTable: "alunos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vinculos_treinador_aluno_pacotes_aluno_pacote_aluno_id",
                        column: x => x.pacote_aluno_id,
                        principalTable: "pacotes_aluno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vinculos_treinador_aluno_treinadores_treinador_id",
                        column: x => x.treinador_id,
                        principalTable: "treinadores",
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
                        name: "fk_execucoes_exercicio_treino_exercicios_treino_exercicio_id",
                        column: x => x.treino_exercicio_id,
                        principalTable: "treino_exercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create indices
            migrationBuilder.CreateIndex(
                name: "ix_alunos_conta_id",
                table: "alunos",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_contas_email",
                table: "contas",
                column: "email",
                unique: true);

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
                name: "ix_execucoes_treino_treino_id",
                table: "execucoes_treino",
                column: "treino_id");

            migrationBuilder.CreateIndex(
                name: "ix_exercicios_treinador_id",
                table: "exercicios",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_logs_aprovacao_entidade_id",
                table: "logs_aprovacao",
                column: "entidade_id");

            migrationBuilder.CreateIndex(
                name: "ix_logs_aprovacao_realizado_por_id",
                table: "logs_aprovacao",
                column: "realizado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_pacotes_aluno_treinador_id",
                table: "pacotes_aluno",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_users_conta_id",
                table: "system_users",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_conta_id",
                table: "treinadores",
                column: "conta_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_treinadores_plano_treinador_id",
                table: "treinadores",
                column: "plano_treinador_id");

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
                name: "ix_treinos_treinador_id",
                table: "treinos",
                column: "treinador_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_treinador_aluno_aluno_id",
                table: "vinculos_treinador_aluno",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_treinador_aluno_pacote_aluno_id",
                table: "vinculos_treinador_aluno",
                column: "pacote_aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_treinador_aluno_treinador_id_aluno_id",
                table: "vinculos_treinador_aluno",
                columns: new[] { "treinador_id", "aluno_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execucoes_exercicio");

            migrationBuilder.DropTable(
                name: "logs_aprovacao");

            migrationBuilder.DropTable(
                name: "system_users");

            migrationBuilder.DropTable(
                name: "treino_alunos");

            migrationBuilder.DropTable(
                name: "vinculos_treinador_aluno");

            migrationBuilder.DropTable(
                name: "execucoes_treino");

            migrationBuilder.DropTable(
                name: "treino_exercicios");

            migrationBuilder.DropTable(
                name: "pacotes_aluno");

            migrationBuilder.DropTable(
                name: "alunos");

            migrationBuilder.DropTable(
                name: "exercicios");

            migrationBuilder.DropTable(
                name: "treinos");

            migrationBuilder.DropTable(
                name: "treinadores");

            migrationBuilder.DropTable(
                name: "contas");
        }

        private string GetCurrentSchema()
        {
            // Placeholder - in actual execution, EF Core will handle schema from context
            return "public";
        }
    }
}
