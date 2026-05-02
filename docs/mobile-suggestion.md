### GPS Rate Limiting — Đề xuất cho Backend Team

**Vấn đề:** Backend giới hạn 120 req/min cho GPS endpoint. Với adaptive interval tối thiểu 1s, client tối đa gửi 60/min trong điều kiện bình thường — an toàn. **Nhưng** khi driver đi vào vùng không có sóng rồi quay lại online, offline cache 100 điểm cần được flush → có thể gửi 100 requests liên tiếp ngay lập tức, vi phạm rate limit.

**Đề xuất giải pháp (cần trao đổi với backend team):**

**Option A — Batch GPS Endpoint (khuyến nghị):**
```
POST /api/v1/tracking/batch
Body: { "points": [ { lat, lng, speed, heading, timestamp }, ... ] }
```
- Client flush toàn bộ cache trong 1 request duy nhất
- Backend xử lý async qua Kafka
- Rate limit count = 1 thay vì 100
- **Trade-off:** Backend cần implement thêm endpoint mới

**Option B — Client-side Throttle với queue:**
- Client implement `LeakyBucket` — gửi tối đa 100 points/min (buffer ~17% dưới limit)
- Offline points queue, drain với interval 600ms
- Nếu backend trả 429 + `Retry-After` header → client pause và retry
- **Trade-off:** Delay flush tới 1 phút nếu cache đầy

**Option C — Tăng rate limit cho GPS batch:**
- Backend team tăng limit GPS lên 300/min (đồng với global limit)
- Hoặc whitelist theo `userId` thay vì theo IP (vì nhiều driver share IP qua NAT)
- **Trade-off:** Tăng load backend khi nhiều driver online đồng thời

**Khuyến nghị:** **Option A + Option B song song** — implement batch endpoint (giải quyết flush problem) đồng thời client luôn throttle để tránh spike. Backend respond 429 + `Retry-After` như safety net.

**Message cho backend team:**
> "GPS rate limit 120/min: bình thường ổn (adaptive interval ~60/min max). Vấn đề là offline cache flush — 100 points gửi cùng lúc khi reconnect. Đề xuất thêm `POST /api/v1/tracking/batch` nhận array GPS points, xử lý async Kafka. Ngoài ra xem xét rate limit theo userId thay vì IP vì nhiều driver dùng chung carrier NAT."