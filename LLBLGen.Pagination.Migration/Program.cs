using System.CommandLine;
using System.Text.Json;
using FluentMigrator.Runner;
using LLBLGen.Pagination.Migration.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LLBLGen.Pagination.Migration;

internal class Program
{
    private static Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Environment.Exit(1);

        var upOption = new Option<bool>("--up")
        {
            Description = "Migrate Up",
            DefaultValueFactory = _ => false
        };
        var downOption = new Option<long>("--down")
        {
            Description = "Rollback database to a version",
            DefaultValueFactory = _ => -1
        };

        var rootCommand = new RootCommand("LLBLGen Pagination Fluent Migrator Runner")
        {
            upOption,
            downOption,
        };

        rootCommand.SetAction(parseResult =>
        {
            var up = parseResult.GetValue(upOption);
            var down = parseResult.GetValue(downOption);

            var serviceProvider = CreateServices();

            using var scope = serviceProvider.CreateScope();
            if (up)
            {
                UpdateDatabase(scope.ServiceProvider);
            }

            if (down > -1)
            {
                RollbackDatabase(scope.ServiceProvider, down);
            }

        });

        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.InvokeAsync();
    }

    /// <summary>
    ///     Configure the dependency injection services
    /// </summary>
    private static IServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
            .Build();
        // Read appSettings.json manually to avoid depending on the AddJsonFile extension method
        string? conn = null;
        var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appSettings.json");
        conn = configuration.GetConnectionString("SqlServer") ?? string.Empty;

        return new ServiceCollection()
            // Add common FluentMigrator services
            .AddFluentMigratorCore()
            .ConfigureRunner(rb =>
            {
                // Add SQL Server support to FluentMigrator
                rb.AddSqlServer2016();

                // Use the explicit static extension method overload that accepts a string
                // to avoid any ambiguous overload resolution with delegates.
                FluentMigrator.Runner.MigrationRunnerBuilderExtensions.WithGlobalConnectionString(rb, conn);

                // Define the assembly containing the migrations
                rb.ScanIn(typeof(_01_CustomerTable).Assembly).For.Migrations();
            })
            // Enable logging to console in the FluentMigrator way
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            // Build the service provider
            .BuildServiceProvider(false);
    }

    private static void UpdateDatabase(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Going up...");
        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }

    private static void RollbackDatabase(IServiceProvider serviceProvider, long rollbackVersion)
    {
        Console.WriteLine("Going down...");
        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateDown(rollbackVersion);
    }
}
