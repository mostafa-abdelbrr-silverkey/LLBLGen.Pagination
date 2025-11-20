using FluentMigrator;

namespace LLBLGen.Pagination.Migration.Migrations;

[Migration(3)]
public class _03_ReceiptTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("Receipt")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity().NotNullable()
            .WithColumn("OrderId").AsInt32().NotNullable()
            .WithColumn("CustomerId").AsInt32().Nullable().ForeignKey("Customer", "Id")
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true).Indexed();

        for (var index = 1; index <= 1_000_000; index++)
        {
            Insert.IntoTable("Receipt")
                .Row(new
                {
                    OrderId = index,
                    CustomerId = index,
                    IsActive = true
                });
        }
    }

    public override void Down()
    {
        Delete.Table("Receipt");
    }
}
