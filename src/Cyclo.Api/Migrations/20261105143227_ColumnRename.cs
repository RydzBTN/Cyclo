using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cyclo.Api.Migrations
{
    /// <inheritdoc />
    public partial class ColumnRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Summary_Elevation",
                table: "Activities",
                newName: "Summary_ElevationGainMeters");

            migrationBuilder.RenameColumn(
                name: "Summary_Distance",
                table: "Activities",
                newName: "Summary_DistanceMeters");

            migrationBuilder.RenameColumn(
                name: "Summary_Best5Km_AvgSpeed",
                table: "Activities",
                newName: "Summary_Best5Km_AvgSpeedMps");

            migrationBuilder.RenameColumn(
                name: "Summary_Best1Km_AvgSpeed",
                table: "Activities",
                newName: "Summary_Best1Km_AvgSpeedMps");

            migrationBuilder.RenameColumn(
                name: "Summary_Best10Km_AvgSpeed",
                table: "Activities",
                newName: "Summary_Best10Km_AvgSpeedMps");

            migrationBuilder.RenameColumn(
                name: "Summary_AvgSpeed",
                table: "Activities",
                newName: "Summary_AvgSpeedMps");

            migrationBuilder.RenameColumn(
                name: "Summary_ActivityDistance",
                table: "Activities",
                newName: "Summary_ActivityDistanceMeters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Summary_ElevationGainMeters",
                table: "Activities",
                newName: "Summary_Elevation");

            migrationBuilder.RenameColumn(
                name: "Summary_DistanceMeters",
                table: "Activities",
                newName: "Summary_Distance");

            migrationBuilder.RenameColumn(
                name: "Summary_Best5Km_AvgSpeedMps",
                table: "Activities",
                newName: "Summary_Best5Km_AvgSpeed");

            migrationBuilder.RenameColumn(
                name: "Summary_Best1Km_AvgSpeedMps",
                table: "Activities",
                newName: "Summary_Best1Km_AvgSpeed");

            migrationBuilder.RenameColumn(
                name: "Summary_Best10Km_AvgSpeedMps",
                table: "Activities",
                newName: "Summary_Best10Km_AvgSpeed");

            migrationBuilder.RenameColumn(
                name: "Summary_AvgSpeedMps",
                table: "Activities",
                newName: "Summary_AvgSpeed");

            migrationBuilder.RenameColumn(
                name: "Summary_ActivityDistanceMeters",
                table: "Activities",
                newName: "Summary_ActivityDistance");
        }
    }
}
