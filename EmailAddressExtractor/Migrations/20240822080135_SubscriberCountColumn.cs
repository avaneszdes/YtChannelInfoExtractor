using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailAddressExtractor.Migrations
{
    /// <inheritdoc />
    public partial class SubscriberCountColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SubscriberCount",
                table: "ChannelInfos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriberCount",
                table: "ChannelInfos");
        }
    }
}
