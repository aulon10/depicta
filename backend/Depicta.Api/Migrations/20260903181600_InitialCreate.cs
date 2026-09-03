using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Depicta.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    WpUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedAppPassword = table.Column<byte[]>(type: "bytea", nullable: false),
                    EncryptedPluginSecret = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastScannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    WpAttachmentId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Filename = table.Column<string>(type: "text", nullable: true),
                    MimeType = table.Column<string>(type: "text", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CurrentAlt = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActiveProposalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaItems_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Discovered = table.Column<int>(type: "integer", nullable: false),
                    AlreadyHadAlt = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanRuns_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AltProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedAlt = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    EditedAlt = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AltProposals_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AltRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AltProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousAlt = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    NewAlt = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevertedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AltRevisions_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AltProposals_MediaItemId",
                table: "AltProposals",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AltRevisions_MediaItemId_AppliedAt",
                table: "AltRevisions",
                columns: new[] { "MediaItemId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_SiteId_State",
                table: "MediaItems",
                columns: new[] { "SiteId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_SiteId_WpAttachmentId",
                table: "MediaItems",
                columns: new[] { "SiteId", "WpAttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_SiteId_StartedAt",
                table: "ScanRuns",
                columns: new[] { "SiteId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sites_Url",
                table: "Sites",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AltProposals");

            migrationBuilder.DropTable(
                name: "AltRevisions");

            migrationBuilder.DropTable(
                name: "ScanRuns");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "Sites");
        }
    }
}
