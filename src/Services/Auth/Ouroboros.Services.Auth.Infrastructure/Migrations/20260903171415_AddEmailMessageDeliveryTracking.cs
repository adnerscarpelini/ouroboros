using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ouroboros.Services.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailMessageDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                schema: "common",
                table: "email_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_attempt_at",
                schema: "common",
                table: "email_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                schema: "common",
                table: "email_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempt_count",
                schema: "common",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "last_attempt_at",
                schema: "common",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "last_error",
                schema: "common",
                table: "email_messages");
        }
    }
}
