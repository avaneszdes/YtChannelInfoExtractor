using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailAddressExtractor.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneNumberColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "ChannelInfos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "ChannelInfos");
        }
    }
}
