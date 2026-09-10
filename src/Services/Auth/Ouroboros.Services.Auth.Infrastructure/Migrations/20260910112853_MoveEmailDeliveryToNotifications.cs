using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ouroboros.Services.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveEmailDeliveryToNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_messages",
                schema: "common");

            migrationBuilder.DropColumn(
                name: "email_message_id",
                schema: "auth",
                table: "tokens");

            migrationBuilder.AddColumn<Guid>(
                name: "notification_request_id",
                schema: "auth",
                table: "tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    message_type = table.Column<string>(type: "text", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    producer = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_external_id",
                schema: "common",
                table: "outbox_messages",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at",
                schema: "common",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "common");

            migrationBuilder.DropColumn(
                name: "notification_request_id",
                schema: "auth",
                table: "tokens");

            migrationBuilder.AddColumn<long>(
                name: "email_message_id",
                schema: "auth",
                table: "tokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "email_messages",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    recipient = table.Column<string>(type: "text", nullable: false),
                    sent = table.Column<bool>(type: "boolean", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subject = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_external_id",
                schema: "common",
                table: "email_messages",
                column: "external_id",
                unique: true);
        }
    }
}
