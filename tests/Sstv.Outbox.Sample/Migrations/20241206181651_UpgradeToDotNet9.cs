using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sstv.Outbox.Sample.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeToDotNet9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_partitioned_ef_outbox_items_status",
                table: "partitioned_ef_outbox_items",
                column: "status",
                filter: "status <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_partitioned_ef_outbox_items_status",
                table: "partitioned_ef_outbox_items");
        }
    }
}
