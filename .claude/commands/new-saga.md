# /new-saga — Scaffold Choreography-based Saga

Scaffold một Choreography-based Saga dùng Kafka events cho distributed transaction.

**Saga name:** $ARGUMENTS

## Yêu cầu

Tạo các files sau (saga name = `$ARGUMENTS`):

### 1. Saga State Document (MongoDB)
```
Infrastructure/Persistence/Mongo/Sagas/$ARGUMENTSSagaState.cs
```
```csharp
public sealed class $ARGUMENTSSagaState
{
    [BsonId] public Guid SagaId { get; set; }
    public $ARGUMENTSSagaStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public List<string> CompletedSteps { get; set; } = [];
    // ... saga-specific data
}
```

### 2. Saga Status Enum
```
Domain/Sagas/$ARGUMENTSSagaStatus.cs
```
- Định nghĩa tất cả states: `Started`, `Step1Completed`, `Step2Completed`, ..., `Completed`, `Compensating`, `Failed`

### 3. Saga Event Classes (một file cho mỗi step)
```
src/Shared/Contracts/Sagas/$ARGUMENTS/
  $ARGUMENTSStartedEvent.cs
  $ARGUMENTSStep1CompletedEvent.cs
  $ARGUMENTSStep1FailedEvent.cs       # trigger compensation
  $ARGUMENTSStep2CompletedEvent.cs
  $ARGUMENTSStep2FailedEvent.cs
  ...
  $ARGUMENTSCompletedEvent.cs
  $ARGUMENTSCompensatedEvent.cs       # compensation finished
```
Mỗi event phải có `MessageId`, `SagaId`, `OccurredAt`, `SchemaVersion`.

### 4. Saga Step Handlers (Kafka consumers trong từng service)

Với mỗi step, tạo handler pattern:
```csharp
// Service A lắng nghe event từ Service B để thực hiện step tiếp theo
public sealed class On$ARGUMENTSStep1CompletedHandler : IRequestHandler<On$ARGUMENTSStep1CompletedCommand>
{
    public async Task Handle(On$ARGUMENTSStep1CompletedCommand request, CancellationToken ct)
    {
        // 1. Load saga state từ MongoDB
        // 2. Validate state transition hợp lệ
        // 3. Thực hiện step 2
        // 4. Update saga state
        // 5. Publish step 2 event (success hoặc failure)
    }
}
```

### 5. Compensating Transaction Handlers
```
Application/EventHandlers/Sagas/Compensating/
  On$ARGUMENTSStep2FailedHandler.cs   # undo step 1
  On$ARGUMENTSStep1FailedHandler.cs   # undo step 0 / cleanup
```

### 6. Saga State Repository
```
Infrastructure/Persistence/Mongo/Sagas/$ARGUMENTSSagaStateRepository.cs
```
- `GetByIdAsync(Guid sagaId)`
- `SaveAsync($ARGUMENTSSagaState state)`
- Optimistic concurrency với `_version` field

## Saga Flow Documentation
Luôn tạo comment block ở đầu SagaState file documenting flow:
```
// SAGA FLOW:
// Step 1: ServiceA publishes $ARGUMENTSStartedEvent
// Step 2: ServiceB handles → publishes $ARGUMENTSStep1CompletedEvent
// Step 3: ServiceC handles → publishes $ARGUMENTSStep2CompletedEvent
// ...
// COMPENSATION (reverse order):
// If Step 2 fails → ServiceB publishes $ARGUMENTSStep1FailedEvent → ServiceA compensates
```

## Rules
- Choreography-based: không có central orchestrator
- Mỗi step handler phải validate state transition trước khi xử lý
- Saga state persistence ở MongoDB (optimistic concurrency)
- Compensation phải theo thứ tự ngược
- Idempotency check ở mỗi step handler
- Timeout handling: background job scan saga states quá hạn → trigger compensation
