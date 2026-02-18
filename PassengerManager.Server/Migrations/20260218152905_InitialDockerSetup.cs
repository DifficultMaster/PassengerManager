using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PassengerManager.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialDockerSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agencies",
                columns: table => new
                {
                    agency_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true),
                    timezone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "'Europe/Kyiv'::character varying"),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lang = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, defaultValueSql: "'uk'::character varying")
                },
                constraints: table =>
                {
                    table.PrimaryKey("agencies_pkey", x => x.agency_id);
                });

            migrationBuilder.CreateTable(
                name: "shape_headers",
                columns: table => new
                {
                    shape_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    total_distance_meters = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("shape_headers_pkey", x => x.shape_id);
                });

            migrationBuilder.CreateTable(
                name: "stops",
                columns: table => new
                {
                    stop_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    wheelchair_boarding = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    location_type = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    platform_code = table.Column<string>(type: "text", nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("stops_pkey", x => x.stop_id);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    default_window = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_roles_pkey", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    agency_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    short_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    long_name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
                    color = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, defaultValueSql: "'#000000'::character varying"),
                    text_color = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, defaultValueSql: "'#FFFFFF'::character varying")
                },
                constraints: table =>
                {
                    table.PrimaryKey("routes_pkey", x => x.route_id);
                    table.ForeignKey(
                        name: "routes_agency_id_fkey",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "agency_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    vehicle_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    agency_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    license_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    manufacture_year = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    last_maintenance = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vehicles_pkey", x => x.vehicle_id);
                    table.ForeignKey(
                        name: "vehicles_agency_id_fkey",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "agency_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shape_points",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shape_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    dist_traveled = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("shape_points_pkey", x => x.id);
                    table.ForeignKey(
                        name: "shape_points_shape_id_fkey",
                        column: x => x.shape_id,
                        principalTable: "shape_headers",
                        principalColumn: "shape_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agency_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: true),
                    is_locked_out = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    lockout_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                    table.ForeignKey(
                        name: "users_agency_id_fkey",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "agency_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "users_role_id_fkey",
                        column: x => x.role_id,
                        principalTable: "user_roles",
                        principalColumn: "role_id");
                });

            migrationBuilder.CreateTable(
                name: "service_alerts",
                columns: table => new
                {
                    alert_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    agency_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    stop_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    header_text = table.Column<string>(type: "text", nullable: false),
                    description_text = table.Column<string>(type: "text", nullable: true),
                    cause = table.Column<int>(type: "integer", nullable: true),
                    effect = table.Column<int>(type: "integer", nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_alerts_pkey", x => x.alert_id);
                    table.ForeignKey(
                        name: "service_alerts_agency_id_fkey",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "agency_id");
                    table.ForeignKey(
                        name: "service_alerts_route_id_fkey",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "route_id");
                    table.ForeignKey(
                        name: "service_alerts_stop_id_fkey",
                        column: x => x.stop_id,
                        principalTable: "stops",
                        principalColumn: "stop_id");
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    trip_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    service_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    headsign = table.Column<string>(type: "text", nullable: true),
                    direction_id = table.Column<int>(type: "integer", nullable: true),
                    shape_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("trips_pkey", x => x.trip_id);
                    table.ForeignKey(
                        name: "trips_route_id_fkey",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "route_id");
                    table.ForeignKey(
                        name: "trips_shape_id_fkey",
                        column: x => x.shape_id,
                        principalTable: "shape_headers",
                        principalColumn: "shape_id");
                });

            migrationBuilder.CreateTable(
                name: "telemetry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    trip_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    bearing = table.Column<double>(type: "double precision", nullable: true),
                    speed = table.Column<double>(type: "double precision", nullable: true),
                    odometer = table.Column<double>(type: "double precision", nullable: true),
                    current_status = table.Column<int>(type: "integer", nullable: true),
                    stop_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    current_stop_sequence = table.Column<int>(type: "integer", nullable: true),
                    congestion_level = table.Column<int>(type: "integer", nullable: true),
                    occupancy_status = table.Column<int>(type: "integer", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("telemetry_pkey", x => x.id);
                    table.ForeignKey(
                        name: "telemetry_route_id_fkey",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "route_id");
                    table.ForeignKey(
                        name: "telemetry_vehicle_id_fkey",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "vehicle_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    transaction_uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ticket_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true, defaultValueSql: "'UAH'::character varying"),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("transactions_pkey", x => x.transaction_uuid);
                    table.ForeignKey(
                        name: "transactions_route_id_fkey",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "route_id");
                    table.ForeignKey(
                        name: "transactions_vehicle_id_fkey",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "vehicle_id");
                });

            migrationBuilder.CreateTable(
                name: "login_audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    username_attempted = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    attempt_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("login_audit_pkey", x => x.id);
                    table.ForeignKey(
                        name: "login_audit_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "password_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("password_history_pkey", x => x.id);
                    table.ForeignKey(
                        name: "password_history_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shifts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    vehicle_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_approved = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    current_trip_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("shifts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shifts_route",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "route_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_shifts_trip",
                        column: x => x.current_trip_id,
                        principalTable: "trips",
                        principalColumn: "trip_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "shifts_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "shifts_vehicle_id_fkey",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "vehicle_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_updates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trip_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    vehicle_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delay_seconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("trip_updates_pkey", x => x.id);
                    table.ForeignKey(
                        name: "trip_updates_trip_id_fkey",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "trip_id");
                    table.ForeignKey(
                        name: "trip_updates_vehicle_id_fkey",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "vehicle_id");
                });

            migrationBuilder.InsertData(
                table: "user_roles",
                columns: new[] { "role_id", "access_level", "default_window", "description", "role_name" },
                values: new object[,]
                {
                    { 1, 100, "AdminDashboard", "Full database control. Developed for persons responsible for transportation management, such as within the city council.", "Administrator" },
                    { 2, 50, "DispatcherDashboard", "Incident management control. Developed for persons responsible for transportation monitoring, such as depot dispatchers.", "Dispatcher" },
                    { 3, 10, "DriverView", "Current route control. Developed for use in tablets inside driver compartments of transportational vehicles.", "Driver" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_login_audit_user_id",
                table: "login_audit",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_password_history_user_id",
                table: "password_history",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_routes_agency_id",
                table: "routes",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "idx_alerts_active_route",
                table: "service_alerts",
                column: "route_id",
                filter: "(is_active = true)");

            migrationBuilder.CreateIndex(
                name: "IX_service_alerts_agency_id",
                table: "service_alerts",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_alerts_stop_id",
                table: "service_alerts",
                column: "stop_id");

            migrationBuilder.CreateIndex(
                name: "idx_shape_points_sequence",
                table: "shape_points",
                columns: new[] { "shape_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "idx_shifts_active",
                table: "shifts",
                column: "vehicle_id",
                filter: "(end_time IS NULL)");

            migrationBuilder.CreateIndex(
                name: "idx_shifts_route",
                table: "shifts",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "idx_shifts_user",
                table: "shifts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_current_trip_id",
                table: "shifts",
                column: "current_trip_id");

            migrationBuilder.CreateIndex(
                name: "idx_stops_lat_lon",
                table: "stops",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "idx_telemetry_route_time",
                table: "telemetry",
                columns: new[] { "route_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_telemetry_timestamp",
                table: "telemetry",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "idx_telemetry_vehicle_time",
                table: "telemetry",
                columns: new[] { "vehicle_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_transactions_route",
                table: "transactions",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_time",
                table: "transactions",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_vehicle_id",
                table: "transactions",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_trip_updates_trip_id",
                table: "trip_updates",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_trip_updates_vehicle_id",
                table: "trip_updates",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "idx_trips_route",
                table: "trips",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "idx_trips_shapes",
                table: "trips",
                column: "shape_id");

            migrationBuilder.CreateIndex(
                name: "user_roles_role_name_key",
                table: "user_roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_agency_id",
                table: "users",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vehicles_active",
                table: "vehicles",
                column: "agency_id",
                filter: "(is_active = true)");

            migrationBuilder.CreateIndex(
                name: "vehicles_license_plate_key",
                table: "vehicles",
                column: "license_plate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_audit");

            migrationBuilder.DropTable(
                name: "password_history");

            migrationBuilder.DropTable(
                name: "service_alerts");

            migrationBuilder.DropTable(
                name: "shape_points");

            migrationBuilder.DropTable(
                name: "shifts");

            migrationBuilder.DropTable(
                name: "telemetry");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "trip_updates");

            migrationBuilder.DropTable(
                name: "stops");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "trips");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "shape_headers");

            migrationBuilder.DropTable(
                name: "agencies");
        }
    }
}
