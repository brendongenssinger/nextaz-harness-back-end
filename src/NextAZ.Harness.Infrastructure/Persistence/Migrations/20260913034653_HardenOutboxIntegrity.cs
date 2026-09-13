using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextAZ.Harness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenOutboxIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_pending_created_at",
                schema: "harness",
                table: "outbox_events");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending_created_at",
                schema: "harness",
                table: "outbox_events",
                column: "created_at",
                filter: "published_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_outbox_events_executions",
                schema: "harness",
                table: "outbox_events",
                columns: new[] { "tenant_id", "execution_id" },
                principalSchema: "harness",
                principalTable: "executions",
                principalColumns: new[] { "tenant_id", "execution_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_outbox_events_executions",
                schema: "harness",
                table: "outbox_events");

            migrationBuilder.DropIndex(
                name: "ix_outbox_pending_created_at",
                schema: "harness",
                table: "outbox_events");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending_created_at",
                schema: "harness",
                table: "outbox_events",
                columns: new[] { "published_at", "created_at" });
        }
    }
}
