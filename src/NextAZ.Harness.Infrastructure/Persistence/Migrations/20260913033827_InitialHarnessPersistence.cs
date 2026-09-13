using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextAZ.Harness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialHarnessPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "harness");

            migrationBuilder.CreateTable(
                name: "executions",
                schema: "harness",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    agent_version = table.Column<int>(type: "integer", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    context_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    steps = table.Column<string>(type: "jsonb", nullable: false),
                    response_content = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_executions", x => new { x.tenant_id, x.execution_id });
                    table.CheckConstraint("ck_executions_token_counts", "input_tokens >= 0 AND output_tokens >= 0");
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                schema: "harness",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    agent_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    agent_version = table.Column<int>(type: "integer", nullable: false),
                    channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_events", x => x.message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_executions_tenant_agent_started_at",
                schema: "harness",
                table: "executions",
                columns: new[] { "tenant_id", "agent_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ux_executions_tenant_operation_correlation",
                schema: "harness",
                table: "executions",
                columns: new[] { "tenant_id", "operation", "correlation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending_created_at",
                schema: "harness",
                table: "outbox_events",
                columns: new[] { "published_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_tenant_execution",
                schema: "harness",
                table: "outbox_events",
                columns: new[] { "tenant_id", "execution_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "executions",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "outbox_events",
                schema: "harness");
        }
    }
}
