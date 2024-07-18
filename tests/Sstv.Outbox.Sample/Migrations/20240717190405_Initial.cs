using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sstv.Outbox.Sample.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql("""
                                 CREATE TABLE public.one_more_outbox_items (
                                 	id uuid NOT NULL,
                                 	created_at timestamptz NOT NULL,
                                 	status int4 NOT NULL,
                                 	retry_count int4 NULL,
                                 	retry_after timestamptz NULL,
                                 	headers bytea NULL,
                                 	"data" bytea NULL,
                                 	CONSTRAINT pk_one_more_outbox_items PRIMARY KEY (id)
                                 );
                                 CREATE INDEX ix_one_more_outbox_items_created_at_retry_after_status ON public.one_more_outbox_items USING btree (created_at, retry_after, status);
                                 """);

            migrationBuilder.Sql("""
                                 CREATE TABLE public.my_outbox_items (
                                 	id uuid NOT NULL,
                                 	created_at timestamptz NOT NULL,
                                 	status int4 NOT NULL,
                                 	retry_count int4 NULL,
                                 	retry_after timestamptz NULL,
                                 	headers bytea NULL,
                                 	"data" bytea NULL,
                                 	CONSTRAINT pk_my_outbox_items PRIMARY KEY (id)
                                 );
                                 CREATE INDEX ix_my_outbox_items_created_at_retry_after_status ON public.my_outbox_items USING btree (created_at, retry_after, status);
                                 """);

            migrationBuilder.Sql("""
                                 CREATE TABLE public.strict_outbox_items (
                                 	id uuid NOT NULL,
                                 	created_at timestamptz NOT NULL,
                                 	status int4 NOT NULL,
                                 	retry_count int4 NULL,
                                 	retry_after timestamptz NULL,
                                 	headers bytea NULL,
                                 	"data" bytea NULL,
                                 	CONSTRAINT pk_strict_outbox_items PRIMARY KEY (id)
                                 );
                                 CREATE INDEX ix_strict_outbox_items_created_at_retry_after_status ON public.strict_outbox_items USING btree (created_at, retry_after, status);
                                 """);

            migrationBuilder.Sql("""
                                 CREATE TABLE public.kafka_npgsql_outbox_items (
                                 	id uuid NOT NULL,
                                 	"key" bytea NULL,
                                 	value bytea NULL,
                                 	topic text NULL,
                                 	headers json NULL,
                                 	status int4 NOT NULL,
                                 	retry_count int4 NULL,
                                 	retry_after timestamptz NULL,
                                 	CONSTRAINT pk_kafka_npgsql_outbox_items PRIMARY KEY (id)
                                 );
                                 CREATE INDEX ix_kafka_npgsql_outbox_items_retry_after_status ON public.kafka_npgsql_outbox_items USING btree (retry_after, status);
                                 """);

            migrationBuilder.Sql("""
                                 CREATE TABLE public.kafka_npgsql_outbox_item_with_priorities (
                                 	id uuid NOT NULL,
                                 	"key" bytea NULL,
                                 	value bytea NULL,
                                 	topic text NULL,
                                 	headers json NULL,
                                 	priority int4 NOT NULL,
                                 	status int4 NOT NULL,
                                 	retry_count int4 NULL,
                                 	retry_after timestamptz NULL,
                                 	CONSTRAINT pk_kafka_npgsql_outbox_item_with_priorities PRIMARY KEY (id)
                                 );
                                 CREATE INDEX ix_kafka_npgsql_outbox_item_with_priorities_priority_retry_after_status ON public.kafka_npgsql_outbox_item_with_priorities USING btree (priority, retry_after, status);
                                 """);

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

            migrationBuilder.CreateTable(
                name: "kafka_ef_outbox_item_with_priorities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<byte[]>(type: "bytea", nullable: true),
                    value = table.Column<byte[]>(type: "bytea", nullable: true),
                    topic = table.Column<string>(type: "text", nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    headers = table.Column<Dictionary<string, string>>(type: "json", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: true),
                    retry_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kafka_ef_outbox_item_with_priorities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kafka_ef_outbox_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<byte[]>(type: "bytea", nullable: true),
                    value = table.Column<byte[]>(type: "bytea", nullable: true),
                    topic = table.Column<string>(type: "text", nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    headers = table.Column<Dictionary<string, string>>(type: "json", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: true),
                    retry_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kafka_ef_outbox_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ef_outbox_items_created_at_retry_after_status",
                table: "ef_outbox_items",
                columns: new[] { "created_at", "retry_after", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_kafka_ef_outbox_item_with_priorities_priority_retry_after_s",
                table: "kafka_ef_outbox_item_with_priorities",
                columns: new[] { "priority", "retry_after", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ef_outbox_items");

            migrationBuilder.DropTable(
                name: "kafka_ef_outbox_item_with_priorities");

            migrationBuilder.DropTable(
                name: "kafka_ef_outbox_items");
        }
    }
}
