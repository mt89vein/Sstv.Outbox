using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Sstv.Outbox.Sample.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ef_outbox_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: true),
                    retry_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    headers = table.Column<byte[]>(type: "bytea", nullable: true),
                    data = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ef_outbox_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ef_outbox_items_created_at_retry_after_status",
                table: "ef_outbox_items",
                columns: new[] { "created_at", "retry_after", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ef_outbox_items");
        }
    }
}