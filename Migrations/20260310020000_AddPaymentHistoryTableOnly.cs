using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradeScout.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sadece PaymentHistories tablosunu oluştur
            migrationBuilder.CreateTable(
                name: "PaymentHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PackageName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "TRY"),
                    CreditsAdded = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Installment = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CardLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexler
            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_UserId",
                table: "PaymentHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_OrderId",
                table: "PaymentHistories",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_TransactionId",
                table: "PaymentHistories",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_Status",
                table: "PaymentHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_PaymentDate",
                table: "PaymentHistories",
                column: "PaymentDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentHistories");
        }
    }
}
