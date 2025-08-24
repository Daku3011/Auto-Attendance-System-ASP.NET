using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoAAS.Migrations
{
    /// <inheritdoc />
    public partial class AddRefenrencePhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ReferenceImage",
                table: "Students",
                type: "varbinary(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceImage",
                table: "Students");
        }
    }
}
