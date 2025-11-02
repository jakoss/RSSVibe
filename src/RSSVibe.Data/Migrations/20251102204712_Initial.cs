using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RSSVibe.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:hstore", ",,");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    NormalizedUrl = table.Column<string>(type: "text", nullable: false),
                    AnalysisStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    PreflightChecks = table.Column<int>(type: "integer", nullable: false),
                    Warnings = table.Column<string[]>(type: "text[]", nullable: false),
                    AiModel = table.Column<string>(type: "text", nullable: true),
                    ApprovedFeedId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnalysisStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AnalysisCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreflightDetails = table.Column<string>(type: "jsonb", nullable: false),
                    Selectors = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedAnalyses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Feeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    NormalizedSourceUrl = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    UpdateIntervalUnit = table.Column<string>(type: "text", nullable: false),
                    UpdateIntervalValue = table.Column<short>(type: "smallint", nullable: false),
                    TtlMinutes = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)60),
                    Etag = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastParsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextParseAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastParseStatus = table.Column<string>(type: "text", nullable: true),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Selectors = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feeds_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Feeds_FeedAnalyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "FeedAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FeedParseRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    HttpStatusCode = table.Column<short>(type: "smallint", nullable: true),
                    FetchedItemsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NewItemsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedItemsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SkippedItemsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ResponseHeaders = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedParseRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedParseRuns_Feeds_FeedId",
                        column: x => x.FeedId,
                        principalTable: "Feeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    NormalizedSourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    FirstParseRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastParseRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawMetadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedItems_FeedParseRuns_FirstParseRunId",
                        column: x => x.FirstParseRunId,
                        principalTable: "FeedParseRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FeedItems_FeedParseRuns_LastParseRunId",
                        column: x => x.LastParseRunId,
                        principalTable: "FeedParseRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FeedItems_Feeds_FeedId",
                        column: x => x.FeedId,
                        principalTable: "Feeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedParseRunItems",
                columns: table => new
                {
                    FeedParseRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeKind = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedParseRunItems", x => new { x.FeedParseRunId, x.FeedItemId });
                    table.ForeignKey(
                        name: "FK_FeedParseRunItems_FeedItems_FeedItemId",
                        column: x => x.FeedItemId,
                        principalTable: "FeedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedParseRunItems_FeedParseRuns_FeedParseRunId",
                        column: x => x.FeedParseRunId,
                        principalTable: "FeedParseRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedAnalyses_AnalysisStatus_CreatedAt",
                table: "FeedAnalyses",
                columns: new[] { "AnalysisStatus", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FeedAnalyses_UserId_NormalizedUrl",
                table: "FeedAnalyses",
                columns: new[] { "UserId", "NormalizedUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_FeedId_Fingerprint",
                table: "FeedItems",
                columns: new[] { "FeedId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_FeedId_LastSeenAt",
                table: "FeedItems",
                columns: new[] { "FeedId", "LastSeenAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_FeedId_NormalizedSourceUrl",
                table: "FeedItems",
                columns: new[] { "FeedId", "NormalizedSourceUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_FeedId_PublishedAt",
                table: "FeedItems",
                columns: new[] { "FeedId", "PublishedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_FirstParseRunId",
                table: "FeedItems",
                column: "FirstParseRunId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_LastParseRunId",
                table: "FeedItems",
                column: "LastParseRunId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedParseRunItems_FeedItemId_SeenAt",
                table: "FeedParseRunItems",
                columns: new[] { "FeedItemId", "SeenAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FeedParseRunItems_FeedParseRunId_ChangeKind",
                table: "FeedParseRunItems",
                columns: new[] { "FeedParseRunId", "ChangeKind" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedParseRuns_FeedId_StartedAt",
                table: "FeedParseRuns",
                columns: new[] { "FeedId", "StartedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FeedParseRuns_Status",
                table: "FeedParseRuns",
                column: "Status",
                filter: "\"Status\" = 'failed'");

            migrationBuilder.CreateIndex(
                name: "IX_Feeds_AnalysisId",
                table: "Feeds",
                column: "AnalysisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feeds_NextParseAfter_LastParseStatus",
                table: "Feeds",
                columns: new[] { "NextParseAfter", "LastParseStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Feeds_UserId_NormalizedSourceUrl",
                table: "Feeds",
                columns: new[] { "UserId", "NormalizedSourceUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_RevokedAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "FeedParseRunItems");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "FeedItems");

            migrationBuilder.DropTable(
                name: "FeedParseRuns");

            migrationBuilder.DropTable(
                name: "Feeds");

            migrationBuilder.DropTable(
                name: "FeedAnalyses");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
