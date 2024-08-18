using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sstv.Outbox.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitionedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 CREATE TABLE public.partitioned_ef_outbox_items (
                                 	id uuid NOT NULL,
                                 	created_at timestamptz NOT NULL,
                                 	status int4 NOT NULL,
                                 	retry_count int4 NULL,
                                 	retry_after timestamptz NULL,
                                 	headers bytea NULL,
                                 	"data" bytea NULL,
                                 	CONSTRAINT pk_partitioned_ef_outbox_items PRIMARY KEY (id)
                                 )
                                 PARTITION BY RANGE(id);
                                 """);

            migrationBuilder.Sql("""
                                 CREATE TABLE public.partitioned_outbox_items (
                                 	id uuid NOT NULL,
                                 	created_at timestamptz NOT NULL,
                                 	status int4 NOT NULL,
                                 	retry_count int4 NULL,
                                 	retry_after timestamptz NULL,
                                 	headers bytea NULL,
                                 	"data" bytea NULL,
                                 	CONSTRAINT pk_partitioned_outbox_items PRIMARY KEY (id)
                                 )
                                 PARTITION BY RANGE(id);
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "partitioned_ef_outbox_items");
            migrationBuilder.DropTable(name: "partitioned_outbox_items");
        }
    }
}
