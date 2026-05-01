# MinIO Setup Guide

> Purpose: Object storage for driver document photos and breakdown photos
> MinIO is S3-compatible — can be replaced with AWS S3 or GCS in production

---

## 1. Start MinIO

```bash
# docker-compose.yml already includes MinIO
docker compose up minio -d

# MinIO Console: http://localhost:9001
# Default credentials (change in production):
# MINIO_ROOT_USER=minioadmin
# MINIO_ROOT_PASSWORD=minioadmin
```

---

## 2. Required Buckets

| Bucket | Purpose | Contents |
|---|---|---|
| `trucker-driver-docs` | Driver onboarding documents | CCCD front/back, selfie, license front/back, vehicle reg, vehicle front photo |
| `breakdown-photos` | Breakdown incident evidence | Photos taken at time of breakdown report |

### Create buckets via MinIO Client (`mc`):

```bash
# Connect to local MinIO
mc alias set local http://localhost:9000 minioadmin minioadmin

# Create buckets
mc mb local/trucker-driver-docs
mc mb local/breakdown-photos
```

---

## 3. Bucket Policies

### `trucker-driver-docs` — Private (pre-signed URL access only)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Deny",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::trucker-driver-docs/*",
      "Condition": {
        "StringNotEquals": { "s3:authType": "REST-QUERY-STRING" }
      }
    }
  ]
}
```

```bash
mc policy set-json driver-docs-policy.json local/trucker-driver-docs
```

### `breakdown-photos` — Same policy (private, pre-signed only)

```bash
mc policy set-json driver-docs-policy.json local/breakdown-photos
```

---

## 4. Pre-Signed URL Flow

Driver app uploads directly to MinIO via pre-signed PUT URL. The Driver service generates URLs but never proxies file content.

```
1. Driver app calls: GET /api/v1/uploads/presigned-url?type=driver-document
   Response: { "urls": ["https://minio/trucker-driver-docs/uuid-frontid?X-Amz-Signature=..."] }

2. Driver app PUTs file directly to MinIO URL (no backend involved)

3. Driver app sends photo URLs (just the object path) to backend:
   POST /api/v1/drivers/register  { ..., "frontIdCardUrl": "trucker-driver-docs/uuid-frontid" }

4. OCR service downloads from MinIO using internal access (no pre-signed needed from server side)
```

For breakdown photos:
```
GET /api/v1/uploads/presigned-url?type=breakdown-photo&count=3
```
Returns 3 pre-signed PUT URLs (max 10).

---

## 5. Pre-Signed URL Expiry

| Type | Default TTL |
|---|---|
| Driver documents | 15 minutes |
| Breakdown photos | 10 minutes |

Configured in `MinIOStorageService` via `MinIO:PresignedUrlExpiryMinutes` app setting.

---

## 6. CORS Configuration (for browser-direct upload)

If the Admin Portal or customer web app uploads directly from browser:

```bash
mc cors set local/trucker-driver-docs \
  --allowed-origin "https://admin.yourdomain.com" \
  --allowed-method "PUT" \
  --allowed-header "*" \
  --expose-header "ETag"
```

For mobile apps (iOS/Android), CORS is not required — HTTP client handles it natively.

---

## 7. Production Considerations

| Concern | Recommendation |
|---|---|
| Credentials | Use IAM service account with least-privilege (only the 2 buckets above) |
| Retention | Driver docs: keep indefinitely (legal requirement); Breakdown photos: 2 years |
| Encryption | Enable server-side encryption (`mc encrypt set sse-s3 local/trucker-driver-docs`) |
| Replication | Enable bucket replication to a second region for DR |
| Quota | Driver docs ~7MB/driver × 10k drivers = ~70GB; plan accordingly |
| AWS S3 migration | Replace `MinIO:Endpoint` with your S3 endpoint URL — SDK is compatible |
