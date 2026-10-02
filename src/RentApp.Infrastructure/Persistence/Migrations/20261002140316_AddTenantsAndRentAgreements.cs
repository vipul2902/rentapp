using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantsAndRentAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_rooms_id_property_id_organization_id",
                table: "rooms",
                columns: new[] { "id", "property_id", "organization_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_beds_id_room_id_organization_id",
                table: "beds",
                columns: new[] { "id", "room_id", "organization_id" });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone_digits = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    permanent_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.UniqueConstraint("ak_tenants_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_tenants_status", "status IN ('Active', 'Archived')");
                    table.ForeignKey(
                        name: "fk_tenants_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rent_agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monthly_rent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    security_deposit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    rent_due_day = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    end_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rent_agreements", x => x.id);
                    table.CheckConstraint("ck_rent_agreements_dates", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_rent_agreements_deposit_non_negative", "security_deposit >= 0");
                    table.CheckConstraint("ck_rent_agreements_due_day", "rent_due_day BETWEEN 1 AND 31");
                    table.CheckConstraint("ck_rent_agreements_end_reason", "end_reason IS NULL OR end_reason IN ('MovedOut', 'Transferred', 'Cancelled')");
                    table.CheckConstraint("ck_rent_agreements_ended_has_end_date", "(status = 'Ended') = (end_date IS NOT NULL)");
                    table.CheckConstraint("ck_rent_agreements_ended_has_reason", "(status = 'Ended') = (end_reason IS NOT NULL)");
                    table.CheckConstraint("ck_rent_agreements_rent_positive", "monthly_rent > 0");
                    table.CheckConstraint("ck_rent_agreements_status", "status IN ('Active', 'Ended')");
                    table.ForeignKey(
                        name: "fk_rent_agreements_beds_bed_id_room_id_organization_id",
                        columns: x => new { x.bed_id, x.room_id, x.organization_id },
                        principalTable: "beds",
                        principalColumns: new[] { "id", "room_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rent_agreements_rooms_room_id_property_id_organization_id",
                        columns: x => new { x.room_id, x.property_id, x.organization_id },
                        principalTable: "rooms",
                        principalColumns: new[] { "id", "property_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rent_agreements_tenants_tenant_id_organization_id",
                        columns: x => new { x.tenant_id, x.organization_id },
                        principalTable: "tenants",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rent_agreements_bed_id_room_id_organization_id",
                table: "rent_agreements",
                columns: new[] { "bed_id", "room_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_agreements_organization_id_status",
                table: "rent_agreements",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_agreements_property_id_status",
                table: "rent_agreements",
                columns: new[] { "property_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_agreements_room_id_property_id_organization_id",
                table: "rent_agreements",
                columns: new[] { "room_id", "property_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_agreements_room_id_status",
                table: "rent_agreements",
                columns: new[] { "room_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_agreements_tenant_id_organization_id",
                table: "rent_agreements",
                columns: new[] { "tenant_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ux_rent_agreements_active_bed_id",
                table: "rent_agreements",
                column: "bed_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ux_rent_agreements_active_tenant_id",
                table: "rent_agreements",
                column: "tenant_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_organization_id_phone_digits",
                table: "tenants",
                columns: new[] { "organization_id", "phone_digits" });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_organization_id_status_full_name",
                table: "tenants",
                columns: new[] { "organization_id", "status", "full_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rent_agreements");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_rooms_id_property_id_organization_id",
                table: "rooms");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_beds_id_room_id_organization_id",
                table: "beds");
        }
    }
}
