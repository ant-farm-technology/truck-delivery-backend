# /new-command — Scaffold CQRS Command Handler

Scaffold một CQRS Command handler (write side) dùng EFCore cho service truck delivery.

**Command name:** $ARGUMENTS

## Yêu cầu

Tạo các files sau (command name = `$ARGUMENTS`):

### 1. Command record
```
Application/Commands/$ARGUMENTS/$ARGUMENTSCommand.cs
```
- Record với các properties cần thiết
- FluentValidation validator: `$ARGUMENTSCommandValidator`
- Data annotations nếu cần

### 2. Command Result
```
Application/Commands/$ARGUMENTS/$ARGUMENTSResult.cs
```
- Record hoặc class chứa kết quả trả về

### 3. Command Handler
```
Application/Commands/$ARGUMENTS/$ARGUMENTSCommandHandler.cs
```
Pattern bắt buộc:
```csharp
public sealed class $ARGUMENTSCommandHandler : IRequestHandler<$ARGUMENTSCommand, $ARGUMENTSResult>
{
    // Inject: IUnitOfWork, I{Entity}Repository, IEventBus, ILogger
    
    public async Task<$ARGUMENTSResult> Handle($ARGUMENTSCommand request, CancellationToken ct)
    {
        // 1. Load aggregate từ repository
        // 2. Gọi domain method (business logic ở đây)
        // 3. Save qua UnitOfWork (commit transaction)
        // 4. Publish domain events → Kafka events qua IEventBus
        // 5. Return result
    }
}
```

### 4. Unit test stub
```
Tests/Commands/$ARGUMENTSCommandHandlerTests.cs
```
- Test happy path
- Test domain exception cases
- Dùng NSubstitute hoặc Moq cho mocks

## Rules
- Handler KHÔNG được dùng EFCore DbContext trực tiếp — chỉ qua Repository
- Business logic KHÔNG được nằm trong Handler — phải nằm trong Domain
- Sau khi save PHẢI publish Kafka event tương ứng qua IEventBus
- Tên Kafka event: `{Entity}{Action}Event` (e.g. `OrderCreatedEvent`)
- Async/await everywhere, không dùng `.Result` hay `.Wait()`
