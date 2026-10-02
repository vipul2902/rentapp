using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rent_charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminders", x => x.id);
                    table.CheckConstraint("ck_reminders_channel", "channel IN ('Copy', 'Share', 'WhatsApp', 'Sms')");
                    table.CheckConstraint("ck_reminders_sent_has_time", "(status = 'Sent') = (sent_at IS NOT NULL)");
                    table.CheckConstraint("ck_reminders_status", "status IN ('Prepared', 'Sent')");
                    table.CheckConstraint("ck_reminders_type", "type IN ('Upcoming', 'DueToday', 'Overdue', 'LongOverdue')");
                    table.ForeignKey(
                        name: "fk_reminders_rent_charges_rent_charge_id_organization_id",
                        columns: x => new { x.rent_charge_id, x.organization_id },
                        principalTable: "rent_charges",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reminders_tenants_tenant_id_organization_id",
                        columns: x => new { x.tenant_id, x.organization_id },
                        principalTable: "tenants",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reminders_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_created_by_user_id",
                table: "reminders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_organization_id_created_at",
                table: "reminders",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_rent_charge_id_created_at",
                table: "reminders",
                columns: new[] { "rent_charge_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_rent_charge_id_organization_id",
                table: "reminders",
                columns: new[] { "rent_charge_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_tenant_id_created_at",
                table: "reminders",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_tenant_id_organization_id",
                table: "reminders",
                columns: new[] { "tenant_id", "organization_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminders");
        }
    }
}
