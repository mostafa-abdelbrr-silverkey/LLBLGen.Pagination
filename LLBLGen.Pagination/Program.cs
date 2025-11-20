using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using LLBLGen.Pagination.Crud.Persistence;
using LLBLGen.Pagination.Data.DatabaseSpecific;
using LLBLGen.Pagination.Data.FactoryClasses;
using LLBLGen.Pagination.Data.HelperClasses;
using LLBLGen.Pagination.Data.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SD.LLBLGen.Pro.DQE.SqlServer;
using SD.LLBLGen.Pro.LinqSupportClasses;
using SD.LLBLGen.Pro.ORMSupportClasses;
using SD.LLBLGen.Pro.QuerySpec;
using SD.LLBLGen.Pro.QuerySpec.Adapter;
using SD.Tools.OrmProfiler.Interceptor;

namespace LLBLGen.Pagination;

internal class Program
{
    private static IConfiguration? _configuration;
    private static int _pageSize = 50;
    private static int _testTimeoutSeconds = 30; // timeout in seconds for each scenario

    private static async Task Main()
    {
        InitializeConfiguration();

        Console.WriteLine("=== LLBLGen LINQ Pagination Scenario ===\n");

        try
        {
            await RunWithTimeout(ScenarioPaginationWithoutProjection, "Test 1 - Pagination without Projection");

            await RunWithTimeout(ScenarioPaginationWithProjectionAndFiltering,
                "Test 2 - Pagination with Projection and Filtering");

            await RunWithTimeout(ScenarioPaginationWithProjection, "Test 3 - Pagination with Projection");

            await RunWithTimeout(ScenarioPaginationWithProjectionTakePage, "Test 4 - Pagination using TakePage");

            await RunWithTimeout(ScenarioPaginationWithProjectionQuerySpec, "Test 5 - Pagination using QuerySpec");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Scenario execution failed: {ex.Message}");
        }
    }

    private static void InitializeConfiguration()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appSettings.json", false, true)
            .Build();

        // Try to get page size from configuration, default to 50
        var pageSizeValue = _configuration["Pagination:PageSize"];
        _pageSize = int.TryParse(pageSizeValue, out var parsedPageSize) ? parsedPageSize : 50;

        // Read test timeout (seconds) from configuration, default to 30 seconds
        var timeoutValue = _configuration["TestExecution:TimeoutSeconds"];
        _testTimeoutSeconds =
            int.TryParse(timeoutValue, out var parsedTimeout) && parsedTimeout > 0 ? parsedTimeout : 30;

        Console.WriteLine($"Page Size: {_pageSize}");
        Console.WriteLine($"Per-test timeout (seconds): {_testTimeoutSeconds}\n");

        // Keep console listeners for interactive output
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
        Trace.Listeners.Add(new ConsoleTraceListener());
        Trace.AutoFlush = true;

        RuntimeConfiguration.Tracing.SetTraceLevel("ORMGeneral", TraceLevel.Verbose);
        RuntimeConfiguration.Tracing.SetTraceLevel("ORMQueryExecution", TraceLevel.Verbose);
        RuntimeConfiguration.AddConnectionString("ConnectionString.SQL Server (SqlClient)",
            _configuration.GetConnectionString("SqlServer"));
        RuntimeConfiguration.ConfigureDQE<SQLServerDQEConfiguration>(c =>
        {
            c.AddDbProviderFactory(InterceptorCore.Initialize("LLBLGen Pagination Issue", typeof(SqlClientFactory)));
#if DEBUG
            // Enable verbose tracing in development for detailed query logging
            c.SetTraceLevel(TraceLevel.Verbose);
#endif
        });
    }

    // Helper: create and return a TextWriterTraceListener that writes to a scenario-specific log file.
    private static TraceListener CreateScenarioFileListener(string scenarioName)
    {
        // Ensure logs directory
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        Directory.CreateDirectory(logsDir);

        // Sanitize scenario name for a safe filename
        var safeName = SanitizeFileName(scenarioName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
        var fileName = $"{safeName}_{timestamp}.log";
        var filePath = Path.Combine(logsDir, fileName);

        // Create a TextWriterTraceListener that writes to the file. Trace.AutoFlush will ensure writes are flushed.
        var writer = File.CreateText(filePath);
        var listener = new TextWriterTraceListener(writer)
        {
            Name = safeName
        };
        return listener;
    }

    // Helper: remove invalid chars and replace spaces to create a safe filename
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "scenario";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            if (Array.IndexOf(invalid, ch) >= 0)
                sb.Append('_');
            else if (char.IsWhiteSpace(ch))
                sb.Append('_');
            else
                sb.Append(ch);

        // Collapse repeated underscores
        var result = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        return string.IsNullOrEmpty(result) ? "scenario" : result;
    }

    private static async Task RunWithTimeout(Func<DataAccessAdapter, Task> scenarioFunc, string name)
    {
        var timeout = TimeSpan.FromSeconds(_testTimeoutSeconds);

        Console.WriteLine($"Running scenario: {name} (timeout: {timeout.TotalSeconds}s)");

        var adapter = new DataAccessAdapter();

        // Create per-scenario file listener and add it to Trace listeners
        TraceListener? fileListener = null;
        try
        {
            fileListener = CreateScenarioFileListener(name);
            Trace.Listeners.Add(fileListener);

            var scenarioTask = scenarioFunc(adapter);

            if (_testTimeoutSeconds <= 0)
            {
                try
                {
                    await scenarioTask;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Scenario '{name}' failed: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        adapter.Dispose();
                    }
                    catch
                    {
                        /* swallow disposal errors */
                    }

                    // flush and remove listener
                    Trace.Flush();
                    if (fileListener != null)
                    {
                        Trace.Listeners.Remove(fileListener);
                        fileListener.Dispose();
                    }
                }

                return;
            }

            var delayTask = Task.Delay(timeout);
            var completed = await Task.WhenAny(scenarioTask, delayTask);

            if (completed == scenarioTask)
            {
                // Completed in time - observe any exception
                try
                {
                    await scenarioTask;
                    Console.WriteLine($"Scenario '{name}' completed successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Scenario '{name}' failed: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        adapter.Dispose();
                    }
                    catch
                    {
                        /* swallow disposal errors */
                    }

                    // flush and remove listener
                    Trace.Flush();
                    if (fileListener != null)
                    {
                        Trace.Listeners.Remove(fileListener);
                        fileListener.Dispose();
                    }
                }
            }
            else
            {
                // Timeout
                Console.WriteLine(
                    $"Scenario '{name}' timed out after {timeout.TotalSeconds} seconds. Attempting to abort and dispose resources...");

                try
                {
                    adapter.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while disposing adapter after timeout: {ex.Message}");
                }

                // flush and remove listener
                Trace.Flush();
                if (fileListener != null)
                {
                    Trace.Listeners.Remove(fileListener);
                    fileListener.Dispose();
                }

                // We won't throw here to allow remaining scenarios to run; caller can inspect logs.
            }
        }
        finally
        {
            // Ensure listener removed in case of unexpected exceptions
            if (fileListener != null && Trace.Listeners.Contains(fileListener))
            {
                Trace.Flush();
                Trace.Listeners.Remove(fileListener);
                fileListener.Dispose();
            }
        }

        Console.WriteLine();
    }

    private static async Task ScenarioPaginationWithoutProjection(DataAccessAdapter adapter)
    {
        Console.WriteLine("--- Test 1: Pagination without Projection ---");
        var meta = new LinqMetaData(adapter);

        const int pageNumber = 1;

        try
        {
            var rows = await meta.Customer
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id)
                .Skip((pageNumber - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync();

            Console.WriteLine($"Retrieved {rows.Count} rows from page {pageNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task ScenarioPaginationWithProjectionAndFiltering(DataAccessAdapter adapter)
    {
        Console.WriteLine("--- Test 2: Pagination with Projection and filtering ---");
        var meta = new LinqMetaData(adapter);

        const int pageNumber = 1;

        try
        {
            var query = meta.Customer
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id);

            var rowsIds = await query
                .Skip((pageNumber - 1) * _pageSize)
                .Take(_pageSize)
                .Select(x => x.Id)
                .ToListAsync();

            var rows = await query
                .Where(x => rowsIds.Contains(x.Id))
                .Skip((pageNumber - 1) * _pageSize)
                .Take(_pageSize)
                .ProjectToCustomerView()
                .ToListAsync();

            Console.WriteLine($"Retrieved {rows.Count} rows from page {pageNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task ScenarioPaginationWithProjection(DataAccessAdapter adapter)
    {
        Console.WriteLine("--- Test 3: Pagination with Projection ---");
        var meta = new LinqMetaData(adapter);

        const int pageNumber = 1;

        try
        {
            var rows = await meta.Customer
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id)
                .Skip((pageNumber - 1) * _pageSize)
                .Take(_pageSize)
                .ProjectToCustomerView()
                .ToListAsync();

            Console.WriteLine($"Retrieved {rows.Count} rows from page {pageNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static async Task ScenarioPaginationWithProjectionTakePage(DataAccessAdapter adapter)
    {
        Console.WriteLine("--- Test 4: Pagination using TakePage with Projection ---");
        var meta = new LinqMetaData(adapter);

        const int pageNumber = 1;

        try
        {
            var rows = await meta.Customer
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id)
                .TakePage(pageNumber, _pageSize)
                .ProjectToCustomerView()
                .ToListAsync();

            Console.WriteLine($"Retrieved {rows.Count} rows from page {pageNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }


    private static async Task ScenarioPaginationWithProjectionQuerySpec(DataAccessAdapter adapter)
    {
        Console.WriteLine("--- Test 5: Pagination with Projection using QuerySpec and QueryFactory ---");
        const int pageNumber = 1;

        try
        {
            var qf = new QueryFactory();

            var q = qf.Customer
                .Where(CustomerFields.IsActive.Equal(true))
                .OrderBy(CustomerFields.Id.Descending())
                .Page(pageNumber, _pageSize)
                .ProjectToCustomerView(qf);
            var rows = await adapter.FetchQueryAsync(q);

            Console.WriteLine($"Retrieved {rows.Count} rows from page {pageNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }
}