using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ouroboros.Modules.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTokensAndTokenTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "token_types",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    token_type_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    email_message_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validated = table.Column<bool>(type: "boolean", nullable: false),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_tokens_token_types_token_type_id",
                        column: x => x.token_type_id,
                        principalSchema: "auth",
                        principalTable: "token_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "token_types",
                columns: new[] { "id", "created_at", "external_id", "name", "updated_at" },
                values: new object[] { 1L, new DateTime(2026, 8, 31, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), "UserCreationValidation", null });

            migrationBuilder.CreateIndex(
                name: "ix_token_types_external_id",
                schema: "auth",
                table: "token_types",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_types_name",
                schema: "auth",
                table: "token_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_external_id",
                schema: "auth",
                table: "tokens",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_token_hash",
                schema: "auth",
                table: "tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_token_type_id",
                schema: "auth",
                table: "tokens",
                column: "token_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_user_id",
                schema: "auth",
                table: "tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tokens",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "token_types",
                schema: "auth");
        }
    }
}
