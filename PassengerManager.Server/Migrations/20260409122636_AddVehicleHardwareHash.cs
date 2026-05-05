using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PassengerManager.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleHardwareHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_shifts_route",
                table: "shifts");

            migrationBuilder.DropForeignKey(
                name: "fk_shifts_trip",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "idx_shifts_route",
                table: "shifts");

            migrationBuilder.DropIndex(
                name: "IX_shifts_current_trip_id",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "current_trip_id",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "shifts");

            migrationBuilder.AddColumn<string>(
                name: "hardware_hash",
                table: "vehicles",
                type: "text",
                nullable: true,
                defaultValue: "UNSET");

            migrationBuilder.AddColumn<int>(
                name: "driver_id",
                table: "telemetry",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_driver_id",
                table: "telemetry",
                column: "driver_id");

            migrationBuilder.AddForeignKey(
                name: "telemetry_driver_id_fkey",
                table: "telemetry",
                column: "driver_id",
                principalTable: "users",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "telemetry_driver_id_fkey",
                table: "telemetry");

            migrationBuilder.DropIndex(
                name: "IX_telemetry_driver_id",
                table: "telemetry");

            migrationBuilder.DropColumn(
                name: "hardware_hash",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "driver_id",
                table: "telemetry");

            migrationBuilder.AddColumn<string>(
                name: "current_trip_id",
                table: "shifts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_id",
                table: "shifts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_shifts_route",
                table: "shifts",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_current_trip_id",
                table: "shifts",
                column: "current_trip_id");

            migrationBuilder.AddForeignKey(
                name: "fk_shifts_route",
                table: "shifts",
                column: "route_id",
                principalTable: "routes",
                principalColumn: "route_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_shifts_trip",
                table: "shifts",
                column: "current_trip_id",
                principalTable: "trips",
                principalColumn: "trip_id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
