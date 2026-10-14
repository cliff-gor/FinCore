using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinCore.BuildingBlocks.Infrastructure.Idempotency.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditSessionsAndKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bff_sessions",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    protected_tokens = table.Column<string>(type: "text", nullable: false),
                    access_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bff_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_institution_id_occurred_at_utc",
                schema: "platform",
                table: "audit_log",
                columns: new[] { "institution_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_user_id_occurred_at_utc",
                schema: "platform",
                table: "audit_log",
                columns: new[] { "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_bff_sessions_absolute_expires_at_utc",
                schema: "platform",
                table: "bff_sessions",
                column: "absolute_expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_bff_sessions_subject",
                schema: "platform",
                table: "bff_sessions",
                column: "subject");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "bff_sessions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "data_protection_keys",
                schema: "platform");
        }
    }
}
