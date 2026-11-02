using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextAZ.Harness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProviderRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_connections",
                schema: "harness",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    encrypted_api_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    api_key_nonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    api_key_tag = table.Column<byte[]>(type: "bytea", nullable: false),
                    encrypted_data_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    key_encryption_key_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    encryption_algorithm = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_connections", x => new { x.tenant_id, x.provider_connection_id });
                    table.CheckConstraint("ck_provider_connections_encrypted_credential", "octet_length(encrypted_api_key) > 0 AND octet_length(api_key_nonce) = 12 AND octet_length(api_key_tag) = 16 AND octet_length(encrypted_data_key) > 0 AND octet_length(encrypted_api_key) <= 16384 AND octet_length(encrypted_data_key) <= 1024");
                    table.CheckConstraint("ck_provider_connections_endpoint", "endpoint ~ '^https://[^[:space:]]+$'");
                    table.CheckConstraint("ck_provider_connections_key", "connection_key ~ '^[a-z0-9][a-z0-9._-]{1,79}$'");
                    table.CheckConstraint("ck_provider_connections_key_encryption", "key_encryption_key_id ~ '^https://[^[:space:]]+/keys/[^/[:space:]]+/[^/[:space:]]+$' AND encryption_algorithm = 'RSA-OAEP-256'");
                    table.CheckConstraint("ck_provider_connections_text", "btrim(provider_id) <> '' AND btrim(display_name) <> '' AND btrim(endpoint) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "agent_model_routes",
                schema: "harness",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    agent_version = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    model_policy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_model_routes", x => new { x.tenant_id, x.agent_id, x.agent_version, x.model_policy, x.priority });
                    table.CheckConstraint("ck_agent_model_routes_values", "agent_version > 0 AND priority BETWEEN 1 AND 4 AND timeout_seconds BETWEEN 1 AND 300 AND agent_id ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$' AND length(model_policy) BETWEEN 1 AND 100 AND btrim(model_policy) <> '' AND length(model_id) BETWEEN 1 AND 200 AND btrim(model_id) <> ''");
                    table.ForeignKey(
                        name: "fk_agent_model_routes_provider_connection",
                        columns: x => new { x.tenant_id, x.provider_connection_id },
                        principalSchema: "harness",
                        principalTable: "provider_connections",
                        principalColumns: new[] { "tenant_id", "provider_connection_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_model_routes_tenant_provider_connection",
                schema: "harness",
                table: "agent_model_routes",
                columns: new[] { "tenant_id", "provider_connection_id" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_connections_tenant_provider_enabled",
                schema: "harness",
                table: "provider_connections",
                columns: new[] { "tenant_id", "provider_id", "enabled" });

            migrationBuilder.CreateIndex(
                name: "ux_provider_connections_tenant_key",
                schema: "harness",
                table: "provider_connections",
                columns: new[] { "tenant_id", "connection_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_model_routes",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "provider_connections",
                schema: "harness");
        }
    }
}
