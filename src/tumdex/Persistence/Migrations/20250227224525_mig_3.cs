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
            migrationBuilder.AddColumn<string>(
                name: "ReasonRevoked",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RevokedByIp",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedDate",
                table: "RefreshTokens",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReasonRevoked",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedByIp",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedDate",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "AspNetUsers");
        }
    }
}
