# /new-query — Scaffold CQRS Query Handler

Scaffold một CQRS Query handler (read side) dùng Dapper hoặc MongoDriver.

**Query name:** $ARGUMENTS

## Yêu cầu

Tạo các files sau (query name = `$ARGUMENTS`):

### 1. Query record
```
Application/Queries/$ARGUMENTS/$ARGUMENTSQuery.cs
```
- Record với các filter/pagination parameters
- Ghi rõ data source: MySQL (Dapper) hay MongoDB

### 2. Query Result DTO
```
Application/Queries/$ARGUMENTS/$ARGUMENTSResult.cs
```
- Flat DTO, không expose domain entity
- Nếu là list: `$ARGUMENTSListResult` với pagination metadata

### 3. Query Handler
```
Application/Queries/$ARGUMENTS/$ARGUMENTSQueryHandler.cs
```

**Pattern cho MySQL (Dapper):**
```csharp
public sealed class $ARGUMENTSQueryHandler : IRequestHandler<$ARGUMENTSQuery, $ARGUMENTSResult>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    // KHÔNG inject DbContext, KHÔNG inject EFCore repository
    
    public async Task<$ARGUMENTSResult> Handle($ARGUMENTSQuery request, CancellationToken ct)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            SELECT ...
            FROM ...
            WHERE ...
            """;
        var result = await connection.QueryFirstOrDefaultAsync<$ARGUMENTSResult>(sql, new { ... });
        return result ?? throw new NotFoundException(...);
    }
}
```

**Pattern cho MongoDB:**
```csharp
public sealed class $ARGUMENTSQueryHandler : IRequestHandler<$ARGUMENTSQuery, $ARGUMENTSResult>
{
    private readonly IMongoCollection<{Document}> _collection;
    
    public async Task<$ARGUMENTSResult> Handle($ARGUMENTSQuery request, CancellationToken ct)
    {
        var filter = Builders<{Document}>.Filter...;
        var document = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return document is null ? throw new NotFoundException(...) : MapToResult(document);
    }
}
```

### 4. Unit test stub
```
Tests/Queries/$ARGUMENTSQueryHandlerTests.cs
```

## Rules
- KHÔNG dùng EFCore trong Query handler — chỉ Dapper hoặc MongoDriver
- KHÔNG load domain entity — chỉ project thẳng vào DTO
- SQL query phải dùng raw SQL string, không dùng LINQ
- Pagination: dùng OFFSET/LIMIT (MySQL) hoặc Skip/Limit (MongoDB)
- Không có side effects (no writes, no events)
