using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cyclo.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessingStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessingEndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Summary_Distance = table.Column<float>(type: "real", nullable: true),
                    Summary_ActivityDistance = table.Column<float>(type: "real", nullable: true),
                    Summary_TotalSeconds = table.Column<int>(type: "int", nullable: true),
                    Summary_ActivitySeconds = table.Column<int>(type: "int", nullable: true),
                    Summary_AvgSpeed = table.Column<float>(type: "real", nullable: true),
                    Summary_AvgHeartRate = table.Column<int>(type: "int", nullable: true),
                    Summary_MaxHeartRate = table.Column<int>(type: "int", nullable: true),
                    Summary_Elevation = table.Column<float>(type: "real", nullable: true),
                    Summary_Best1Km_StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary_Best1Km_EndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary_Best1Km_AvgSpeed = table.Column<float>(type: "real", nullable: true),
                    Summary_Best5Km_StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary_Best5Km_EndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary_Best5Km_AvgSpeed = table.Column<float>(type: "real", nullable: true),
                    Summary_Best10Km_StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary_Best10Km_EndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary_Best10Km_AvgSpeed = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_FileHash",
                table: "Activities",
                columns: new[] { "UserId", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_UploadedAt",
                table: "Activities",
                columns: new[] { "UserId", "UploadedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}
