using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class mig_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitorTrackingEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Page = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    IsAuthenticated = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VisitTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Referrer = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ReferrerDomain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReferrerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UTMSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UTMMedium = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UTMCampaign = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BrowserName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsNewVisitor = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorTrackingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitorTrackingEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_DeviceType",
                table: "VisitorTrackingEvents",
                column: "DeviceType");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_IsAuthenticated",
                table: "VisitorTrackingEvents",
                column: "IsAuthenticated");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_IsNewVisitor",
                table: "VisitorTrackingEvents",
                column: "IsNewVisitor");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_ReferrerDomain",
                table: "VisitorTrackingEvents",
                column: "ReferrerDomain");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_ReferrerType",
                table: "VisitorTrackingEvents",
                column: "ReferrerType");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_SessionId",
                table: "VisitorTrackingEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_UserId",
                table: "VisitorTrackingEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorTrackingEvents_VisitTime",
                table: "VisitorTrackingEvents",
                column: "VisitTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitorTrackingEvents");
        }
    }
}
