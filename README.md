# LLBLGen.Pagination
## Restaurant DB Schema
Database Engine: Microsoft SQL Server.
Tables:
- Customer
  - Id: primary key.
  - Name: customer name.
  - Table Number: table number the customer is sitting at.
  - IsActive: simple boolean properly for soft deletion.
- Order:
  - Id: primary key.
  - Name: order name.
  - CustomerId: ID of the customer who made the order.
  - IsActive: simple boolean properly for soft deletion.
- Receipt
  - Id: primary key.
  - CustomerId: ID of the customer who made the order.
  - OrderId: ID of the order the customer made.
  - IsActive: simple boolean properly for soft deletion.
  
Notes: all columns are not nullable for simplicity, and seeded with 1 million rows using the migration project.

## How to initialize the DB
- Create a DB query called Restaurant (if you change the name, update the connection strings in all projects).
- Run `LLBLGen.Pagination.Migration` with `--up` argument.
- You should have a schema as documented above.

## Issue
When using pagination with projection without a WHERE clause or with a non-restrictive one, it leads to a complex query that accesses all rows instead of applying it to the rows included in the requested page.

## How to reproduce the issue
Run `LLBLGen.Pagination` project. It has multiple different scenarios, documented below. Make sure you have ORM Profiler running. A log and snapshot of an example run are included for ease of access.

For all scenarios, the basic query is to get rows from the table `Customer` using a WHERE clause that returns a lot of rows for illustration and apply pagination.

## Scenario 1: Pagination without projection.  
Simply fetches 50 rows (page size is 50).  
Result: fetches exactly 50 rows across all queries as shown in ORM Profiler.  
ORM Profiler Connection: #1.  
Query:
```csharp
var rows = await meta.Customer
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id);
                .Skip((pageNumber - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync();
```

Generated logs:
```log

```

## Scenario 2: Pagination with projection and filtering.  
Fetches 50 rows using pagination without projection first and selects IDs then it is used as a filter in the pagination with projection query.  
Result:
```csharp
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
    .ProjectToCustomerTestView()
    .ToListAsync();
```

## Scenario 3: Pagination with projection.  
Simply fetches 50 rows using pagination and projection.  
Result: Simplest form of the issue where all rows are accessed due to JOINs before applying pagination.
```csharp
var rows = await meta.Customer
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id)
                .Skip((pageNumber - 1) * _pageSize)
                .Take(_pageSize)
                .ProjectToCustomerTestView()
                .ToListAsync();
```