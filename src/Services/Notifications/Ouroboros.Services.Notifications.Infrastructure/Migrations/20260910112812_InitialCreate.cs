using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ouroboros.Services.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.EnsureSchema(
                name: "common");

            migrationBuilder.CreateTable(
                name: "email_deliveries",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    producer = table.Column<string>(type: "text", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient = table.Column<string>(type: "text", nullable: false),
                    template_key = table.Column<string>(type: "text", nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    content_fingerprint = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_deliveries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "error_logs",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    exception_type = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    stack_trace = table.Column<string>(type: "text", nullable: true),
                    request_path = table.Column<string>(type: "text", nullable: true),
                    trace_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_logs", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "email_delivery_attempts",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    email_delivery_id = table.Column<long>(type: "bigint", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    succeeded = table.Column<bool>(type: "boolean", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_delivery_attempts_email_deliveries_email_delivery_id",
                        column: x => x.email_delivery_id,
                        principalSchema: "notifications",
                        principalTable: "email_deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_deliveries_external_id",
                schema: "notifications",
                table: "email_deliveries",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_deliveries_producer_request_id",
                schema: "notifications",
                table: "email_deliveries",
                columns: new[] { "producer", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_deliveries_status_next_attempt_at",
                schema: "notifications",
                table: "email_deliveries",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_email_delivery_attempts_email_delivery_id_attempt_number",
                schema: "notifications",
                table: "email_delivery_attempts",
                columns: new[] { "email_delivery_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_delivery_attempts_external_id",
                schema: "notifications",
                table: "email_delivery_attempts",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_error_logs_external_id",
                schema: "common",
                table: "error_logs",
                column: "external_id",
                unique: true);

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
                name: "email_delivery_attempts",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "error_logs",
                schema: "common");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "common");

            migrationBuilder.DropTable(
                name: "email_deliveries",
                schema: "notifications");
        }
    }
}
