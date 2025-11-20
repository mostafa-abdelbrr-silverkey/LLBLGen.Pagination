using FluentMigrator;

namespace LLBLGen.Pagination.Migration.Migrations;

[Migration(2)]
public class _02_OrderTable : FluentMigrator.Migration
{
    private readonly List<string> _orderNames = new()
    {
        "Meal",
        "Fruit",
        "Vegetable",
        "Grilled Food"
    };

    public override void Up()
    {
        Create.Table("Order")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity().NotNullable()
            .WithColumn("Name").AsString(255).NotNullable()
            .WithColumn("Price").AsInt32().NotNullable()
            .WithColumn("CustomerId").AsInt32().Nullable().ForeignKey("Customer", "Id")
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true).Indexed();

        var rnd = new Random();
        for (var index = 1; index <= 1_000_000; index++)
        {
            Insert.IntoTable("Order")
                .Row(new
                {
                    // select random name from the list
                    Name = _orderNames[rnd.Next(_orderNames.Count)],
                    Price = index % 1000,
                    CustomerId = index,
                    IsActive = true
                });
        }
    }

    public override void Down()
    {
        Delete.Table("Order");
    }
}
