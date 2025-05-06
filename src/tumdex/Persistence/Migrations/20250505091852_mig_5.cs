using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class mig_5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarouselVideoFileId",
                table: "Carousel",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "Carousel",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoId",
                table: "Carousel",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoType",
                table: "Carousel",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Carousel",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VideoFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Storage = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: true),
                    Duration = table.Column<long>(type: "bigint", nullable: false),
                    Resolution = table.Column<string>(type: "text", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ThumbUrl = table.Column<string>(type: "text", nullable: true),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    ExternalType = table.Column<string>(type: "text", nullable: true),
                    Discriminator = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Carousel_CarouselVideoFileId",
                table: "Carousel",
                column: "CarouselVideoFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Carousel_VideoFiles_CarouselVideoFileId",
                table: "Carousel",
                column: "CarouselVideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Carousel_VideoFiles_CarouselVideoFileId",
                table: "Carousel");

            migrationBuilder.DropTable(
                name: "VideoFiles");

            migrationBuilder.DropIndex(
                name: "IX_Carousel_CarouselVideoFileId",
                table: "Carousel");

            migrationBuilder.DropColumn(
                name: "CarouselVideoFileId",
                table: "Carousel");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Carousel");

            migrationBuilder.DropColumn(
                name: "VideoId",
                table: "Carousel");

            migrationBuilder.DropColumn(
                name: "VideoType",
                table: "Carousel");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "Carousel");
        }
    }
}
