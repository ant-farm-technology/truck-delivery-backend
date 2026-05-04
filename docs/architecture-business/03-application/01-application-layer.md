## 1. Mục tiêu

Application Layer chịu trách nhiệm:

- Orchestrate use cases
- Điều phối Domain + Infrastructure
- Triển khai CQRS
- Không chứa business logic phức tạp

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Thin Layer

→ Gọi domain  
→ Gọi repository  
→ Publish event  
  
x→ Không chứa business rule

---

### 2.2 Dependency Rule

Application → Domain  
Application → Infrastructure (qua interface)

---

### 2.3 CQRS

Command = write  
Query = read

---

## 3. Structure

---

Application  
 ├── Commands  
 ├── Queries  
 ├── Handlers  
 ├── DTOs  
 ├── Interfaces  
 └── Behaviors (pipeline)

---

## 4. Command Side

---

### 4.1 Command Definition

public record CreateOrderCommand(  
    double PickupLat,  
    double PickupLng,  
    double DeliveryLat,  
    double DeliveryLng  
);

---

### 4.2 Command Handler

public class CreateOrderHandler   
    : IRequestHandler<CreateOrderCommand, Guid>  
{  
    private readonly IOrderRepository _repo;  
    private readonly IEventBus _eventBus;  
  
    public async Task<Guid> Handle(  
        CreateOrderCommand cmd,   
        CancellationToken ct)  
    {  
        var order = Order.Create(  
            cmd.PickupLat,  
            cmd.PickupLng,  
            cmd.DeliveryLat,  
            cmd.DeliveryLng  
        );  
  
        await _repo.Save(order);  
  
        await _eventBus.Publish(new OrderCreatedEvent(order.Id));  
  
        return order.Id;  
    }  
}

---

## 5. Query Side

---

### 5.1 Query Definition

public record GetOrderQuery(Guid OrderId);

---

### 5.2 Query Handler

public class GetOrderHandler   
    : IRequestHandler<GetOrderQuery, OrderDto>  
{  
    private readonly IDbConnection _db;  
  
    public async Task<OrderDto> Handle(  
        GetOrderQuery query,   
        CancellationToken ct)  
    {  
        return await _db.QueryFirstAsync<OrderDto>(  
            "SELECT * FROM Orders WHERE Id = @Id",  
            new { query.OrderId }  
        );  
    }  
}

---

Rule:

Query không dùng domain model  
→ dùng DTO trực tiếp (Dapper)

---

## 6. Pipeline Behaviors (CỰC QUAN TRỌNG)

---

### 6.1 Validation

- Validate command trước khi xử lý

---

### 6.2 Logging

- Log request + response

---

### 6.3 Transaction

- Wrap command trong transaction

---

### 6.4 Idempotency

- Check duplicate command

---

---

### Example pipeline

Request  
 ↓  
Validation  
 ↓  
Idempotency  
 ↓  
Transaction  
 ↓  
Handler  
 ↓  
Event Publish

---

---

## 7. Interfaces (Ports)

---

### Repository

public interface IOrderRepository  
{  
    Task Save(Order order);  
    Task<Order> Get(Guid id);  
}

---

---

### Event Bus

public interface IEventBus  
{  
    Task Publish<T>(T @event);  
}

---

---

### External Services

public interface IRoutingService  
{  
    Task<DistanceMatrix> GetMatrix(...);  
}

---

---

## 8. Event Handling

---

### Integration Event Handler

public class OrderCreatedConsumer  
{  
    public async Task Handle(OrderCreatedEvent e)  
    {  
        // trigger dispatch  
    }  
}

---

---

## 9. Transaction Strategy

---

### Rule

1 Command = 1 transaction

---

### Outbox Pattern (BẮT BUỘC)

---

#### Flow

Save entity  
↓  
Save event vào Outbox  
↓  
Commit transaction  
↓  
Background worker publish Kafka

---

Tránh:

[KHÔNG] Save DB xong rồi publish event trực tiếp

---

---

## 10. DTO vs Domain Model

---

|Layer|Use|
|---|---|
|Domain|Entity|
|Application|DTO|
|API|Request/Response|

---

---

## 11. Idempotency (Command Level)

---

### Case

Client retry → duplicate command

---

### Solution

IdempotencyKey

---

### Storage

Redis / DB

---

---

## 12. Performance Considerations

---

### Command

- Không blocking lâu  
- Không gọi external nặng

---

### Query

- Dùng read replica  
- Dùng Dapper

---

---

## 13. Async vs Sync

---

### Sync

User request → Command

---

### Async

Event → background processing

---

---

## 14. Anti-patterns

---

Nhét business logic vào handler  
Handler gọi handler khác  
Query dùng EF tracking  
Không dùng outbox  
Shared DTO giữa service

---

---

## 15. Design Guarantees

---

Application Layer đảm bảo:

- Clean orchestration
- Tách biệt read/write
- Có thể scale independently
- Dễ test