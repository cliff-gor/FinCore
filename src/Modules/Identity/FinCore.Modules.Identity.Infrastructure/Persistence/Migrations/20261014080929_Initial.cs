using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinCore.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    operation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    maker_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_checkers = table.Column<int>(type: "integer", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outcome_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "identity",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => new { x.message_id, x.handler_name });
                });

            migrationBuilder.CreateTable(
                name: "institutions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_institutions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    trace_parent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    dead_lettered_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    home_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_decisions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    checker_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_decisions_approval_requests_approval_request_id",
                        column: x => x.approval_request_id,
                        principalSchema: "identity",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_limits",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    max_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_limits", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_limits_staff_users_staff_user_id",
                        column: x => x.staff_user_id,
                        principalSchema: "identity",
                        principalTable: "staff_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    granted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_roles_staff_users_staff_user_id",
                        column: x => x.staff_user_id,
                        principalSchema: "identity",
                        principalTable: "staff_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_decisions_approval_request_id_checker_staff_id",
                schema: "identity",
                table: "approval_decisions",
                columns: new[] { "approval_request_id", "checker_staff_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_limits_staff_user_id_type",
                schema: "identity",
                table: "approval_limits",
                columns: new[] { "staff_user_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_maker_staff_id",
                schema: "identity",
                table: "approval_requests",
                column: "maker_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_module_operation_type_entity_id",
                schema: "identity",
                table: "approval_requests",
                columns: new[] { "module", "operation_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_status_expires_at_utc",
                schema: "identity",
                table: "approval_requests",
                columns: new[] { "status", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_status_institution_id",
                schema: "identity",
                table: "approval_requests",
                columns: new[] { "status", "institution_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc_processed_on_utc",
                schema: "identity",
                table: "outbox_messages",
                columns: new[] { "occurred_on_utc", "processed_on_utc" },
                filter: "processed_on_utc IS NULL AND dead_lettered_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_staff_roles_staff_user_id_role",
                schema: "identity",
                table: "staff_roles",
                columns: new[] { "staff_user_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_email",
                schema: "identity",
                table: "staff_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_external_subject_id",
                schema: "identity",
                table: "staff_users",
                column: "external_subject_id",
                unique: true,
                filter: "external_subject_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_institution_id_home_branch_id",
                schema: "identity",
                table: "staff_users",
                columns: new[] { "institution_id", "home_branch_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_decisions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "approval_limits",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "institutions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "staff_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "staff_users",
                schema: "identity");
        }
    }
}
