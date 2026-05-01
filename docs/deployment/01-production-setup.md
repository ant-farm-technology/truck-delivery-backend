# Production Setup Guide

> Audience: DevOps / Backend lead
> Stack: Docker Compose (dev/staging), Kubernetes (production)

---

## 1. Prerequisites

- Docker 24+ / Docker Compose v2
- .NET 10 SDK (build only — not needed on server)
- MySQL 8.0+, MongoDB 7.0+, Redis 7+, Kafka 3.7+ (KRaft mode)
- MinIO (or any S3-compatible store)
- PaddleOCR pre-baked into OCR image (see §6)

---

## 2. Environment Variables per Service

All secrets must be injected as environment variables — never committed to source.

### API Gateway (`:8080`)
| Variable | Example |
|---|---|
| `Jwt__Authority` | `https://identity.internal` |
| `Jwt__Audience` | `truck-delivery-api` |
| `Otel__Endpoint` | `http://tempo:4317` |

### Identity (`:8081`)
| Variable | Example |
|---|---|
| `ConnectionStrings__IdentityDb` | `Server=mysql;Database=truck_identity;...` |
| `Jwt__SecretKey` | (min 32 chars, random) |
| `Jwt__Issuer` | `truck-delivery` |
| `Jwt__Audience` | `truck-delivery-api` |

### Driver (`:8083`)
| Variable | Example |
|---|---|
| `ConnectionStrings__DriverDb` | `Server=mysql;Database=truck_driver;...` |
| `ConnectionStrings__Redis` | `redis:6379` |
| `Kafka__BootstrapServers` | `kafka:9092` |
| `MinIO__Endpoint` | `minio:9000` |
| `MinIO__AccessKey` | (from MinIO console) |
| `MinIO__SecretKey` | (from MinIO console) |

### Notification (`:8088`)
| Variable | Example |
|---|---|
| `ConnectionStrings__NotificationDb` | `Server=mysql;Database=truck_notification;...` |
| `Firebase__CredentialsJson` | (service-account JSON, base64 or file path) |
| `Twilio__AccountSid` | `ACxxxxxxxx` |
| `Twilio__AuthToken` | (from Twilio console) |
| `Twilio__FromNumber` | `+84xxxxxxxxx` |
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__Username` | `notifications@yourdomain.com` |
| `Smtp__Password` | (app-specific password) |
| `Notification__AdminEmail` | `admin@yourdomain.com` |

### Payment (`:8089`)
| Variable | Example |
|---|---|
| `ConnectionStrings__PaymentDb` | `Server=mysql;Database=truck_payment;...` |
| `VnPay__TmnCode` | (from VNPay merchant portal) |
| `VnPay__HashSecret` | (from VNPay merchant portal) |
| `VnPay__PaymentUrl` | `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` |
| `VnPay__ReturnUrl` | `https://yourdomain.com/api/v1/payments/webhook/vnpay` |

---

## 3. Database Initialization

Run EFCore migrations for each .NET service before first start:

```bash
# Identity
dotnet ef database update \
  --project src/Services/Identity/TruckDelivery.Identity.Infrastructure \
  --startup-project src/Services/Identity/TruckDelivery.Identity.Api

# Order
dotnet ef database update \
  --project src/Services/Order/TruckDelivery.Order.Infrastructure \
  --startup-project src/Services/Order/TruckDelivery.Order.Api

# Driver
dotnet ef database update \
  --project src/Services/Driver/TruckDelivery.Driver.Infrastructure \
  --startup-project src/Services/Driver/TruckDelivery.Driver.Api

# Shipment
dotnet ef database update \
  --project src/Services/Shipment/TruckDelivery.Shipment.Infrastructure \
  --startup-project src/Services/Shipment/TruckDelivery.Shipment.Api

# Notification
dotnet ef database update \
  --project src/Services/Notification/TruckDelivery.Notification.Infrastructure \
  --startup-project src/Services/Notification/TruckDelivery.Notification.Api

# Payment
dotnet ef database update \
  --project src/Services/Payment/TruckDelivery.Payment.Infrastructure \
  --startup-project src/Services/Payment/TruckDelivery.Payment.Api
```

MongoDB collections and indexes are created automatically on first use by the respective services.

PostGIS schema is managed by the Rust Route service via `sqlx migrate run` on startup.

---

## 4. Kafka Topic Creation

```bash
KAFKA_CONTAINER=kafka   # or your container name

docker exec $KAFKA_CONTAINER kafka-topics.sh --bootstrap-server localhost:9092 --create \
  --topic userregistered --partitions 3 --replication-factor 1

for topic in \
  order.order.created \
  shipment.driver.assigned \
  shipment.shipment.completed \
  shipment.shipment.status-updated \
  shipment.breakdown.reassignment-completed \
  driver.driver.status-updated \
  driver.documents.submitted \
  driver.driver.manual-review-required \
  driver.vehicle.breakdown \
  driver.fraud.suspicious-pair-detected \
  payment.payment.completed \
  payment.payment.failed \
  ocr.driver.verification-completed \
  tracking.location.updated; do
  docker exec $KAFKA_CONTAINER kafka-topics.sh --bootstrap-server localhost:9092 --create \
    --topic $topic --partitions 3 --replication-factor 1
  # DLQ topic
  docker exec $KAFKA_CONTAINER kafka-topics.sh --bootstrap-server localhost:9092 --create \
    --topic ${topic}.dlq --partitions 1 --replication-factor 1
done
```

---

## 5. Docker Compose (local / staging)

```bash
docker compose -f docker/docker-compose.yml up -d
```

Services start order (managed by `depends_on` + health checks):
1. Infrastructure: mysql, mongodb, redis, kafka, minio
2. Identity (waits for mysql)
3. All other services (wait for kafka + mysql/mongo)
4. Gateway (waits for identity)

---

## 6. OCR Service Cold Start

The OCR Dockerfile pre-bakes PaddleOCR Vietnamese models (~1.5GB total image):

```bash
docker build -t truck-delivery-ocr:latest src/Services/OCR/truck-delivery-ocr/
```

Model files are at `/home/appuser/.paddleocr/` inside the container. No internet access needed at runtime.

---

## 7. Health Checks

After startup, verify system health:

```bash
# Gateway liveness
curl http://localhost:8080/health

# All downstream services
curl http://localhost:8080/health/all | jq .

# Individual service
curl http://localhost:8081/health   # identity
curl http://localhost:8082/health   # order
```

Expected response: `{ "status": "Healthy", ... }`

---

## 8. Observability Stack

| Service | URL | Purpose |
|---|---|---|
| Grafana | `http://localhost:3000` | Dashboards (logs, metrics, traces) |
| Prometheus | `http://localhost:9090` | Metrics scraping |
| Grafana Loki | `http://localhost:3100` | Log aggregation |
| Grafana Tempo | `http://localhost:4317` (OTLP) | Distributed tracing |

Prometheus auto-discovers `/metrics` endpoints via Docker service labels.

---

## 9. Seeding Admin Account

The first Admin account must be created via the Identity service seed:

```bash
# The DatabaseInitializerService in Identity seeds an admin if none exists
# Configure via environment:
Identity__SeedAdmin__Email=admin@company.com
Identity__SeedAdmin__Password=SecurePassword123!
```

Or call directly after startup:
```bash
curl -X POST http://localhost:8081/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@company.com","password":"SecurePassword123!","firstName":"Super","lastName":"Admin","role":3}'
```

Role=3 is Admin. This endpoint should be disabled or restricted after initial setup.
