using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsAndReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.UniqueConstraint("ak_payments_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_payments_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_payments_method", "method IN ('Cash', 'Upi', 'BankTransfer', 'Card', 'Other')");
                    table.CheckConstraint("ck_payments_status", "status IN ('Recorded', 'Voided')");
                    table.CheckConstraint("ck_payments_voided_has_details", "(status = 'Voided') = (voided_at IS NOT NULL AND void_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_payments_tenants_tenant_id_organization_id",
                        columns: x => new { x.tenant_id, x.organization_id },
                        principalTable: "tenants",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_users_voided_by_user_id",
                        column: x => x.voided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_counters",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_counters", x => new { x.organization_id, x.year });
                    table.CheckConstraint("ck_receipt_counters_last_number_positive", "last_number > 0");
                    table.ForeignKey(
                        name: "fk_receipt_counters_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rent_charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_allocations", x => x.id);
                    table.CheckConstraint("ck_payment_allocations_amount_positive", "allocated_amount > 0");
                    table.ForeignKey(
                        name: "fk_payment_allocations_payments_payment_id_organization_id",
                        columns: x => new { x.payment_id, x.organization_id },
                        principalTable: "payments",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_allocations_rent_charges_rent_charge_id_organizatio",
                        columns: x => new { x.rent_charge_id, x.organization_id },
                        principalTable: "rent_charges",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    organization_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    property_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    property_address = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    property_contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tenant_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    room_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bed_label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    period_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
                    table.CheckConstraint("ck_receipts_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_receipts_method", "method IN ('Cash', 'Upi', 'BankTransfer', 'Card', 'Other')");
                    table.ForeignKey(
                        name: "fk_receipts_payments_payment_id_organization_id",
                        columns: x => new { x.payment_id, x.organization_id },
                        principalTable: "payments",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_organization_id",
                table: "payment_allocations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_payment_id_organization_id",
                table: "payment_allocations",
                columns: new[] { "payment_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_payment_id_rent_charge_id",
                table: "payment_allocations",
                columns: new[] { "payment_id", "rent_charge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_rent_charge_id",
                table: "payment_allocations",
                column: "rent_charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_rent_charge_id_organization_id",
                table: "payment_allocations",
                columns: new[] { "rent_charge_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_organization_id_payment_date",
                table: "payments",
                columns: new[] { "organization_id", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_recorded_by_user_id",
                table: "payments",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_organization_id",
                table: "payments",
                columns: new[] { "tenant_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_payment_date",
                table: "payments",
                columns: new[] { "tenant_id", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_voided_by_user_id",
                table: "payments",
                column: "voided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_payments_organization_id_idempotency_key",
                table: "payments",
                columns: new[] { "organization_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_payment_id",
                table: "receipts",
                column: "payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipts_payment_id_organization_id",
                table: "receipts",
                columns: new[] { "payment_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ux_receipts_organization_id_receipt_number",
                table: "receipts",
                columns: new[] { "organization_id", "receipt_number" },
                unique: true);

            migrationBuilder.Sql(FinancialRecordGuards.Create);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FinancialRecordGuards.Drop);

            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "receipt_counters");

            migrationBuilder.DropTable(
                name: "receipts");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
