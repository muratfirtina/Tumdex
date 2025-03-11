using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class mig_4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RefreshTokenEndDateTime",
                table: "AspNetUsers",
                newName: "RefreshTokenExpiryTime");

            migrationBuilder.AlterColumn<string>(
                name: "RevokedByIp",
                table: "RefreshTokens",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ReasonRevoked",
                table: "RefreshTokens",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FamilyId",
                table: "RefreshTokens",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "RefreshTokens",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<string>(
                name: "AppUserId",
                table: "RefreshTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginDate",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginIp",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginUserAgent",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_AppUserId",
                table: "RefreshTokens",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_AppUserId",
                table: "RefreshTokens",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_AppUserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_AppUserId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginIp",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginUserAgent",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "RefreshTokenExpiryTime",
                table: "AspNetUsers",
                newName: "RefreshTokenEndDateTime");

            migrationBuilder.AlterColumn<string>(
                name: "RevokedByIp",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReasonRevoked",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FamilyId",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "RefreshTokens",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);
        }
    }
}
