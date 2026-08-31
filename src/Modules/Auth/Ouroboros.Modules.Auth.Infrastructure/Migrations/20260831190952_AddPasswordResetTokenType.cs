using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ouroboros.Modules.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetTokenType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "auth",
                table: "token_types",
                columns: new[] { "id", "created_at", "external_id", "name", "updated_at" },
                values: new object[] { 2L, new DateTime(2026, 8, 31, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000002"), "PasswordReset", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "auth",
                table: "token_types",
                keyColumn: "id",
                keyValue: 2L);
        }
    }
}
