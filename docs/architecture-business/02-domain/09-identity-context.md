## 1. Mục tiêu

Identity Context chịu trách nhiệm:

- Authentication (xác thực)
- Authorization (phân quyền)
- Quản lý user & role
- Phát hành và validate token (JWT)

---

## 2. Boundary & Responsibility

---

### Thuộc Identity

- Login / Register  
- Token issuance (JWT)  
- Role & Permission  
- Password management

---

### KHÔNG thuộc Identity

- Không chứa Driver profile  
- Không chứa Order data  
- Không chứa business logic domain

Ví dụ:

Driver profile → Fleet Context  
Customer info → Order Context (nếu cần)

---

## 3. Ubiquitous Language

User  
Credential  
Role  
Permission  
AccessToken  
RefreshToken  
Session

---

## 4. Aggregate Design

---

### 4.1 Aggregate Root: `User`

User  
 ├── UserId  
 ├── Email / Username  
 ├── PasswordHash  
 ├── Status (Active, Locked)  
 ├── Roles  
 ├── CreatedAt  
 └── UpdatedAt

---

### 4.2 Value Objects

---

#### Credential

public record Credential(string PasswordHash);

---

#### Role

public record Role(string Name);

---

---

## 5. Authentication Flow

---

### Login

Client → Identity  
   ↓  
Validate credential  
   ↓  
Generate JWT  
   ↓  
Return AccessToken + RefreshToken

---

### Register

Client → Identity  
   ↓  
Create user  
   ↓  
Publish UserRegistered event

---

---

## 6. JWT Design (CRITICAL)

---

### Payload (claims)

{  
  "sub": "userId",  
  "role": "Driver",  
  "exp": 123456,  
  "iss": "identity-service"  
}

---

### Rules

- Expire ngắn (15–60 phút)  
- Không nhét data nhạy cảm  
- Không nhét business data

---

---

## 7. Refresh Token Flow

---

AccessToken hết hạn  
   ↓  
Client gửi RefreshToken  
   ↓  
Validate  
   ↓  
Issue AccessToken mới

---

### Storage

RefreshToken lưu DB (hoặc Redis)

---

### Rule

Có thể revoke bất kỳ lúc nào

---

---

## 8. Authorization Model

---

### RBAC (Role-Based Access Control)

Admin  
Driver  
Customer

---

### Example

Driver → update location  
Admin → assign vehicle

---

### Rule

Authorization không nằm trong controller  
→ dùng policy/middleware

---

---

## 9. Domain Events

---

### Publish

UserRegistered  
UserLocked  
RoleAssigned

---

### Consume

(none hoặc rất ít)

---

Ví dụ:

- Fleet consume `UserRegistered` → tạo Driver profile

---

---

## 10. Integration Points

---

### API Gateway (YARP)

- Validate JWT
- Inject user context

---

### Downstream Services

Nhận:  
- userId  
- role

---

---

## 11. Persistence Model (MySQL)

---

### Tables

Users  
Roles  
UserRoles  
RefreshTokens

---

---

## 12. Security Practices (BẮT BUỘC)

---

### Password

- Hash (BCrypt/Argon2)  
- Không lưu plain text

---

### Token

- Sign bằng secret/private key  
- Rotate key định kỳ

---

### API

- Rate limit login  
- Protect brute force

---

---

## 13. Concurrency & Session

---

### Multiple sessions

1 user có thể login nhiều device

---

### Logout

→ revoke refresh token

---

---

## 14. Failure Handling

---

### Case 1: Token expired

→ dùng refresh token

---

### Case 2: Token invalid

→ reject (401)

---

### Case 3: User locked

→ block login

---

---

## 15. Anti-patterns

---

Nhét business data vào JWT  
Dùng JWT quá dài hạn  
Không có refresh token  
Identity gọi trực tiếp service khác  
Shared DB với domain khác

---

---

## 16. Design Guarantees

---

Identity Context đảm bảo:

- Secure authentication
- Clear authorization
- Stateless access token
- Không coupling business domain