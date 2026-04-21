using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsoDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Documents_IsDeleted_UpdatedAt",
                table: "Documents",
                columns: new[] { "IsDeleted", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerId",
                table: "Documents",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status_UpdatedAt",
                table: "Documents",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_DocumentId",
                table: "ApprovalWorkflows",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_DocumentId_Status",
                table: "ApprovalWorkflows",
                columns: new[] { "DocumentId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_IsDeleted_UpdatedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_Status_UpdatedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalWorkflows_DocumentId",
                table: "ApprovalWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalWorkflows_DocumentId_Status",
                table: "ApprovalWorkflows");
        }
    }
}
