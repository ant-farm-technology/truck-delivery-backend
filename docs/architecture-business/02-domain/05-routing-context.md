## 1. Mục tiêu

Routing Context chịu trách nhiệm:

- Tính toán khoảng cách và thời gian di chuyển
- Xây dựng distance matrix cho optimization
- Map matching (GPS → road)
- Cung cấp tuyến đường (route path)

---

## 2. Boundary & Responsibility

---

### Thuộc Routing

- Shortest path (A → B)  
- Distance matrix (NxN)  
- Map matching  
- ETA estimation

---

### KHÔNG thuộc Routing

- Không chọn driver  
- Không batch order  
- Không tối ưu nhiều xe (VRP)

VRP thuộc Optimization Context

---

## 3. Ubiquitous Language

Node  
Edge  
Graph  
DistanceMatrix  
RoutePath  
ETA  
MapMatching

---

## 4. Domain Model

---

### 4.1 Graph Model

Graph  
 ├── Nodes (intersections)  
 ├── Edges (roads)  
 └── Weights (distance, time)

---

### 4.2 Route

Route  
 ├── StartLocation  
 ├── EndLocation  
 ├── Path (list of nodes)  
 ├── TotalDistance  
 ├── EstimatedTime

---

### 4.3 Distance Matrix

DistanceMatrix  
 ├── Locations (N)  
 ├── Matrix[N][N]  
 ├── Distance  
 ├── Duration

---

## 5. Data Source

---

### Primary

- OpenStreetMap

---

### Storage

- PostGIS

---

---

## 6. Core Algorithms

---

### 6.1 Shortest Path

- Dijkstra (baseline)
- A* (recommended)

---

### 6.2 Map Matching

- Snap GPS → nearest road segment

---

### 6.3 Distance Matrix Generation

N locations → N x N matrix

---

Complexity:

O(N^2 log V)

---

## 7. Distance Matrix (CRITICAL)

---

### Tại sao quan trọng?

Optimizer phụ thuộc hoàn toàn vào matrix

---

### Rule

Matrix phải:  
- Consistent  
- Symmetric (nếu không có one-way)  
- Cacheable

---

### Ví dụ

|     | A   | B   | C   |
| --- | --- | --- | --- |
| A   | 0   | 10  | 20  |
| B   | 10  | 0   | 15  |
| C   | 20  | 15  | 0   |


---

## 8. API Design

---

### 8.1 Calculate Route

POST /route

#### Input

{  
  "from": { "lat": 0, "lng": 0 },  
  "to": { "lat": 0, "lng": 0 }  
}

---

### 8.2 Distance Matrix

POST /matrix

#### Input

{  
  "locations": [ ... ]  
}

---

---

## 9. Performance & Scaling

---

### 9.1 Bottleneck

Distance matrix = cực kỳ tốn CPU

---

### 9.2 Strategy

---

#### Cache Matrix

Key: hash(locations)  
TTL: 5–30 phút

---

#### Precompute

- Popular routes  
- Fixed hubs

---

#### Parallelization

Split matrix computation

---

---

## 10. Cache Strategy

---

### Layer

Redis (hot cache)  
Memory (local cache)

---

### Rule

Cache miss → compute → store

---

---

## 11. Map Matching

---

### Input

GPS raw (lat, lng)

---

### Output

Nearest road segment

---

### Rule

Luôn normalize location trước khi routing

---

---

## 12. Integration Points

---

### Dispatch Context

- Request:

Distance matrix

---

### Optimization Context

- Input:

Matrix từ routing

---

---

## 13. Data Model (PostGIS)

---

### Tables

roads  
nodes  
edges

---

### Index

GiST index (spatial)

---

---

## 14. Consistency Rules

---

- Same input → same output  
- Không random  
- Không phụ thuộc state ngoài

---

---

## 15. Failure Handling

---

### Case 1: Route not found

→ fallback: straight-line distance (Haversine)

---

### Case 2: Graph missing

→ reload data

---

---

## 16. Anti-patterns

---

Gọi Google Maps API cho mỗi request  
Không cache matrix  
Tính toán trong .NET service  
Trộn business logic vào routing

---

---

## 17. Design Guarantees

---

Routing Context đảm bảo:

- Deterministic output
- High performance
- Scalable computation
- Clean separation khỏi business logic