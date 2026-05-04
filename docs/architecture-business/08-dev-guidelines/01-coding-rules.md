## 1. Mục tiêu

Coding Rules đảm bảo:

- Code dễ đọc & maintain
- Consistent giữa services
- Dễ onboard dev mới
- Giảm bug & tech debt

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Readability > Cleverness

Code phải dễ đọc hơn là “ngầu”

---

### 2.2 Explicit > Implicit

Không đoán → phải rõ ràng

---

### 2.3 Consistency > Personal style

Team rule > sở thích cá nhân

---

---

## 3. Structure Rule

---

### 3.1 Clean Architecture

Domain  
Application  
Infrastructure  
API

---

---

### 3.2 Folder Structure (.NET)

src/  
 ├── Order.Domain  
 ├── Order.Application  
 ├── Order.Infrastructure  
 └── Order.API

---

---

### 3.3 Rust / Python

routing-service/  
 ├── domain/  
 ├── application/  
 ├── infra/  
 └── api/

---

---

## 4. Naming Convention

---

### 4.1 Class / Type

PascalCase  
OrderService  
CreateOrderCommand

---

---

### 4.2 Variable

camelCase  
orderId  
driverStatus

---

---

### 4.3 Boolean

isActive  
hasDriver

---

---

### 4.4 Method

Verb + Noun  
  
CreateOrder()  
AssignDriver()  
CalculateRoute()

---

---

## 5. Function Rules

---

### 5.1 Single Responsibility

1 function = 1 job

---

---

### 5.2 Function size

≤ 30 lines

---

---

### 5.3 Parameters

≤ 3 params → nếu nhiều → dùng object

---

---

### 5.4 Return

Không return nhiều kiểu dữ liệu

---

---

## 6. Class Rules

---

### 6.1 Size

≤ 200–300 lines

---

---

### 6.2 Cohesion

Method liên quan cùng responsibility

---

---

### 6.3 Dependency Injection

Không new trực tiếp dependency

---

---

## 7. Domain Rules

---

### 7.1 Domain không phụ thuộc infra

DbContext  
HTTP client

---

---

### 7.2 Business logic nằm trong Domain

  → Order.AssignDriver()  
x → Handler logic

---

---

### 7.3 Immutable Value Object

public record Location(double Lat, double Lng);

---

---

## 8. Application Rules

---

### 8.1 Handler mỏng

- Gọi domain  
- Gọi repo  
- Publish event

---

---

### 8.2 Không chứa business rule

Rule phải ở Domain

---

---

### 8.3 CQRS

Command ≠ Query

---

---

## 9. Data Access Rules

---

### 9.1 Repository Pattern

Chỉ expose method cần thiết

---

---

### 9.2 Query

- Dapper (read)  
- Không tracking

---

---

### 9.3 Transaction

1 command = 1 transaction

---

---

## 10. API Rules

---

### 10.1 DTO only

   → Domain entity  
x → DTO

---

---

### 10.2 Validation

Validate ở API layer

---

---

### 10.3 Response format

{  
  "success": true,  
  "data": {},  
  "error": null  
}

---

---

## 11. Async & Concurrency

---

### 11.1 Async all the way

Không block thread

---

---

### 11.2 Avoid shared state

Stateless service

---

---

### 11.3 Locking

Prefer optimistic locking

---

---

## 12. Testing Rules

---

### 12.1 Unit test

- Test domain logic  
- Không phụ thuộc DB

---

---

### 12.2 Integration test

- Test DB + messaging

---

---

### 12.3 Naming

Should_DoSomething_WhenCondition

---

---

## 13. Logging Rules

---

### 13.1 Structured logging

{  
  "message": "Order created",  
  "orderId": "..."  
}

---

---

### 13.2 Không log sensitive data

password  
token

---

---

## 14. Error Handling

---

### 14.1 Không swallow exception

catch {} 

---

---

### 14.2 Domain exception

OrderNotFoundException

---

---

### 14.3 Mapping

Exception → HTTP code

---

---

## 15. Code Quality

---

### 15.1 Linting

- .NET analyzers
- Rust clippy
- Python flake8

---

---

### 15.2 Formatting

Auto-format (CI enforced)

---

---

### 15.3 Code review

Mandatory PR review

---

---

## 16. Anti-patterns

---

God class  
Copy-paste code  
Business logic trong controller  
Shared util vô tội vạ  
Hardcode config  
Không có test

---

---

## 17. Cross-language Consistency

---

### Rule

.NET / Rust / Python phải:  
- Cùng naming logic  
- Cùng architecture  
- Cùng logging format

---

---

## 18. Design Guarantees

---

Coding Rules đảm bảo:

- Code dễ đọc
- Dễ maintain
- Dễ scale team
- Ít bug hơn