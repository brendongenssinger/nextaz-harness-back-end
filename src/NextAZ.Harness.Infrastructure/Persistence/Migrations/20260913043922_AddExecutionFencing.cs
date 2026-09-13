using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextAZ.Harness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionFencing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at",
                schema: "harness",
                table: "executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lease_owner",
                schema: "harness",
                table: "executions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                schema: "harness",
                table: "executions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_fingerprint",
                schema: "harness",
                table: "executions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE harness.executions SET request_fingerprint = 'legacy:' || replace(execution_id::text, '-', '') WHERE request_fingerprint IS NULL");

            migrationBuilder.Sql(
                "UPDATE harness.executions SET lease_owner = 'legacy-migration', lease_token = execution_id, lease_expires_at = CURRENT_TIMESTAMP WHERE status = 'Running'");

            migrationBuilder.AlterColumn<string>(
                name: "request_fingerprint",
                schema: "harness",
                table: "executions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_executions_lease_state",
                schema: "harness",
                table: "executions",
                sql: "(status = 'Running' AND lease_owner IS NOT NULL AND lease_token IS NOT NULL AND lease_expires_at IS NOT NULL) OR (status <> 'Running' AND lease_owner IS NULL AND lease_token IS NULL AND lease_expires_at IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_executions_lease_state",
                schema: "harness",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                schema: "harness",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "lease_owner",
                schema: "harness",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "lease_token",
                schema: "harness",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "request_fingerprint",
                schema: "harness",
                table: "executions");
        }
    }
}
