using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertiesRoomsAndBeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_properties", x => x.id);
                    table.UniqueConstraint("ak_properties_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_properties_status", "status IN ('Active', 'Archived')");
                    table.ForeignKey(
                        name: "fk_properties_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    room_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                    table.UniqueConstraint("ak_rooms_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_rooms_capacity", "capacity BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_rooms_status", "status IN ('Active', 'Unavailable', 'Archived')");
                    table.ForeignKey(
                        name: "fk_rooms_properties_property_id_organization_id",
                        columns: x => new { x.property_id, x.organization_id },
                        principalTable: "properties",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "beds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_monthly_rent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_beds", x => x.id);
                    table.CheckConstraint("ck_beds_default_rent_positive", "default_monthly_rent IS NULL OR default_monthly_rent > 0");
                    table.CheckConstraint("ck_beds_status", "status IN ('Available', 'Reserved', 'Unavailable', 'Archived')");
                    table.ForeignKey(
                        name: "fk_beds_rooms_room_id_organization_id",
                        columns: x => new { x.room_id, x.organization_id },
                        principalTable: "rooms",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_beds_organization_id",
                table: "beds",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_beds_room_id_label",
                table: "beds",
                columns: new[] { "room_id", "label" },
                unique: true,
                filter: "status <> 'Archived'");

            migrationBuilder.CreateIndex(
                name: "ix_beds_room_id_organization_id",
                table: "beds",
                columns: new[] { "room_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_properties_organization_id_status",
                table: "properties",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rooms_organization_id",
                table: "rooms",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_property_id_organization_id",
                table: "rooms",
                columns: new[] { "property_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rooms_property_id_room_number",
                table: "rooms",
                columns: new[] { "property_id", "room_number" },
                unique: true,
                filter: "status <> 'Archived'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beds");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "properties");
        }
    }
}
