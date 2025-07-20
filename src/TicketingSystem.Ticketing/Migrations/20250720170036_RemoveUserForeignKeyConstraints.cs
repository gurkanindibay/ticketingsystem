using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingSystem.Ticketing.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserForeignKeyConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventTickets_User_UserId",
                table: "EventTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_EventTicketTransactions_User_UserId",
                table: "EventTicketTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_EventTickets_User_UserId",
                table: "EventTickets",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventTicketTransactions_User_UserId",
                table: "EventTicketTransactions",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
