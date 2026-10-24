using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextAZ.Harness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRouteBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_route_binding_events",
                schema: "harness",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    operation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    agent_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    agent_version = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_route_binding_events", x => x.event_id);
                    table.CheckConstraint("ck_agent_route_events_actor", "btrim(actor) <> ''");
                    table.CheckConstraint("ck_agent_route_events_agent_id", "agent_id ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$'");
                    table.CheckConstraint("ck_agent_route_events_agent_version", "agent_version > 0");
                    table.CheckConstraint("ck_agent_route_events_channel", "channel ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
                    table.CheckConstraint("ck_agent_route_events_operation", "operation ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
                    table.CheckConstraint("ck_agent_route_events_revision", "revision > 0");
                    table.CheckConstraint("ck_agent_route_events_tenant", "tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                });

            migrationBuilder.CreateTable(
                name: "agent_route_bindings",
                schema: "harness",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    operation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    agent_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    agent_version = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_route_bindings", x => new { x.tenant_id, x.channel, x.operation });
                    table.CheckConstraint("ck_agent_route_bindings_agent_id", "agent_id ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$'");
                    table.CheckConstraint("ck_agent_route_bindings_agent_version", "agent_version > 0");
                    table.CheckConstraint("ck_agent_route_bindings_channel", "channel ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
                    table.CheckConstraint("ck_agent_route_bindings_operation", "operation ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
                    table.CheckConstraint("ck_agent_route_bindings_revision", "revision > 0");
                    table.CheckConstraint("ck_agent_route_bindings_tenant", "tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_agent_route_bindings_updated_by", "btrim(updated_by) <> ''");
                });

            migrationBuilder.CreateIndex(
                name: "ux_agent_route_events_tenant_route_revision",
                schema: "harness",
                table: "agent_route_binding_events",
                columns: new[] { "tenant_id", "channel", "operation", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_route_binding_events",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "agent_route_bindings",
                schema: "harness");
        }
    }
}
