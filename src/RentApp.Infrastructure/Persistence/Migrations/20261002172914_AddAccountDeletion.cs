using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_status",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_organizations_status",
                table: "organizations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                table: "users",
                sql: "status IN ('Active', 'Disabled', 'Deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_organizations_status",
                table: "organizations",
                sql: "status IN ('Active', 'Suspended', 'Closed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_status",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_organizations_status",
                table: "organizations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                table: "users",
                sql: "status IN ('Active', 'Disabled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_organizations_status",
                table: "organizations",
                sql: "status IN ('Active', 'Suspended')");
        }
    }
}
