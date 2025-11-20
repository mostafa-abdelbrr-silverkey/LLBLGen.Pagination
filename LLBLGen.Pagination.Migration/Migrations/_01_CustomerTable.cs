using FluentMigrator;

namespace LLBLGen.Pagination.Migration.Migrations;

[Migration(1)]
public class _01_CustomerTable : FluentMigrator.Migration
{
    private readonly List<string> _names = new()
    {
        "John",
        "Jane",
        "Michael",
        "Emily",
        "David",
        "Sarah",
        "Daniel",
        "Olivia",
        "James",
        "Sophia",
        "William",
        "Isabella",
        "Alexander",
        "Mia",
        "Ethan",
        "Charlotte",
        "Matthew",
        "Amelia",
        "Joseph",
        "Harper",
        "Christopher",
        "Christine",
        "Anthony",
        "Evelyn",
        "Joshua",
        "Abigail"
    };

    public override void Up()
    {
        Create.Table("Customer")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity().NotNullable()
            .WithColumn("Name").AsString(255).NotNullable()
            .WithColumn("TableNumber").AsInt32().NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true).Indexed();

        var rnd = new Random();
        for (var index = 1; index <= 1_000_000; index++)
        {
            Insert.IntoTable("Customer")
                .Row(new
                {
                    Name = $"{_names[rnd.Next(_names.Count)]} {_names[rnd.Next(_names.Count)]}",
                    TableNumber = index % 10,
                    IsActive = true
                });
        }
    }

    public override void Down()
    {
        Delete.Table("Customer");
    }
}