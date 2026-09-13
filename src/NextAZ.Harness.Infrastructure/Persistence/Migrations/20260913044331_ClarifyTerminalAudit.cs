using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextAZ.Harness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClarifyTerminalAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_pending_created_at",
                schema: "harness",
                table: "outbox_events");

            migrationBuilder.DropColumn(
                name: "published_at",
                schema: "harness",
                table: "outbox_events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at",
                schema: "harness",
                table: "outbox_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending_created_at",
                schema: "harness",
                table: "outbox_events",
                column: "created_at",
                filter: "published_at IS NULL");
        }
    }
}
