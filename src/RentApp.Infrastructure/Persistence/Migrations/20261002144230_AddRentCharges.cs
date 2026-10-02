using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_rent_agreements_id_tenant_id_property_id_organization_id",
                table: "rent_agreements",
                columns: new[] { "id", "tenant_id", "property_id", "organization_id" });

            migrationBuilder.CreateTable(
                name: "rent_charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rent_agreement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    adjusted_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    balance_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, computedColumnSql: "amount - paid_amount - adjusted_amount", stored: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rent_charges", x => x.id);
                    table.UniqueConstraint("ak_rent_charges_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_rent_charges_adjusted_non_negative", "adjusted_amount >= 0");
                    table.CheckConstraint("ck_rent_charges_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_rent_charges_not_over_settled", "paid_amount + adjusted_amount <= amount");
                    table.CheckConstraint("ck_rent_charges_paid_non_negative", "paid_amount >= 0");
                    table.CheckConstraint("ck_rent_charges_period", "period_end >= period_start");
                    table.ForeignKey(
                        name: "fk_rent_charges_rent_agreements_rent_agreement_id_tenant_id_pr",
                        columns: x => new { x.rent_agreement_id, x.tenant_id, x.property_id, x.organization_id },
                        principalTable: "rent_agreements",
                        principalColumns: new[] { "id", "tenant_id", "property_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rent_charge_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rent_charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rent_charge_adjustments", x => x.id);
                    table.CheckConstraint("ck_rent_charge_adjustments_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_rent_charge_adjustments_rent_charges_rent_charge_id_organiz",
                        columns: x => new { x.rent_charge_id, x.organization_id },
                        principalTable: "rent_charges",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rent_charge_adjustments_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rent_charge_adjustments_created_by_user_id",
                table: "rent_charge_adjustments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rent_charge_adjustments_organization_id",
                table: "rent_charge_adjustments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_rent_charge_adjustments_rent_charge_id",
                table: "rent_charge_adjustments",
                column: "rent_charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_rent_charge_adjustments_rent_charge_id_organization_id",
                table: "rent_charge_adjustments",
                columns: new[] { "rent_charge_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_charges_organization_id_period_start",
                table: "rent_charges",
                columns: new[] { "organization_id", "period_start" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_charges_outstanding_by_due_date",
                table: "rent_charges",
                columns: new[] { "organization_id", "due_date" },
                filter: "cancelled_at IS NULL AND balance_amount > 0");

            migrationBuilder.CreateIndex(
                name: "ix_rent_charges_property_id_due_date",
                table: "rent_charges",
                columns: new[] { "property_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_charges_rent_agreement_id_period_start",
                table: "rent_charges",
                columns: new[] { "rent_agreement_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rent_charges_rent_agreement_id_tenant_id_property_id_organi",
                table: "rent_charges",
                columns: new[] { "rent_agreement_id", "tenant_id", "property_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rent_charges_tenant_id_due_date",
                table: "rent_charges",
                columns: new[] { "tenant_id", "due_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rent_charge_adjustments");

            migrationBuilder.DropTable(
                name: "rent_charges");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_rent_agreements_id_tenant_id_property_id_organization_id",
                table: "rent_agreements");
        }
    }
}
