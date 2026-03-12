# Kubernetes + Observability Kurulum Kılavuzu

## Önkoşullar

| Araç | Versiyon | İndirme |
|------|----------|---------|
| Docker Desktop | ≥ 4.x | https://www.docker.com/products/docker-desktop |
| kubectl | ≥ 1.28 | Docker Desktop ile gelir |
| Helm | ≥ 3.14 | `winget install Helm.Helm` |
| Lens | ≥ 2024 | https://k8slens.dev |

**Docker Desktop → Settings → Kubernetes → Enable Kubernetes** aktif olmalı.

---

## Mimari

```
                      ┌─────────────────────────────────────────────────────┐
                      │              Kubernetes (Docker Desktop)            │
                      │                                                      │
Internet ──► Gateway:80 ──► shopping-cart:8080 ──► RabbitMQ:5552           │
             (YARP)        └──────────────────────────────────────────────► │
                      │    discount:8080 ────────── SQL Server:1433         │
                      │    notification (worker) ──► Redis:6379             │
                      │                                                      │
                      │  ┌─────────── monitoring namespace ───────────────┐ │
                      │  │  OTel Collector ──► Tempo (traces)             │ │
                      │  │       │         ──► Prometheus (metrics)       │ │
                      │  │  Promtail      ──► Loki (logs)                 │ │
                      │  │  Grafana (dashboard: localhost:3000)           │ │
                      │  └────────────────────────────────────────────────┘ │
                      └─────────────────────────────────────────────────────┘
```

### Telemetri Akışı
- **Traces**: .NET OTel SDK → OTLP gRPC → OTel Collector → Tempo → Grafana Explore
- **Metrics**: .NET OTel SDK + /metrics endpoint → Prometheus scrape → Grafana dashboards
- **Logs**: Serilog JSON Console → stdout → Promtail DaemonSet → Loki → Grafana Explore

---

## Kurulum Adımları

### 1. Docker Image Build

```bash
# Projenin kök dizininde:
chmod +x scripts/build-images.sh
./scripts/build-images.sh

# Ya da Windows PowerShell:
docker build -t shopping-cart:latest -f src/ShoppingCartService/Dockerfile src/ShoppingCartService
docker build -t discount:latest -f src/DiscountService/Dockerfile src/DiscountService
docker build -t notification:latest -f src/NotificationService/Dockerfile src/NotificationService
docker build -t gateway:latest -f src/Bff.Gateway/Dockerfile src/Bff.Gateway
```

### 2. Observability Stack Kur

```bash
chmod +x scripts/setup-observability.sh
./scripts/setup-observability.sh
```

Bu script sırasıyla şunları kurar:
- kube-prometheus-stack (Prometheus + Grafana + AlertManager)
- Loki + Promtail (log toplama)
- Grafana Tempo (distributed tracing)
- OTel Collector

### 3. Mikroservisleri Deploy Et

```bash
kubectl apply -f k8s/services/
```

### 4. Durum Kontrolü

```bash
# Tüm pod'lar Running olana kadar bekle:
kubectl get pods -n microservices -w
kubectl get pods -n monitoring -w

# Servis URL'leri:
kubectl get svc -n microservices
kubectl get svc -n monitoring
```

---

## Grafana Erişimi

```bash
# Grafana port-forward (LoadBalancer alternatifi):
kubectl port-forward svc/kube-prometheus-stack-grafana 3000:80 -n monitoring
```

Tarayıcı: http://localhost:3000
- Kullanıcı: `admin`
- Şifre: `admin123`

### Dashboard Import

Grafana → + → Import ile şu ID'leri ekle:

| Dashboard | ID | İçerik |
|-----------|-----|--------|
| ASP.NET Core | **10427** | HTTP RPS, latency, error rate |
| RabbitMQ | **10991** | Queue depth, publish/consume rate |
| Redis | **11835** | Commands/s, memory, keyspace |
| .NET Runtime | **13009** | GC, thread pool, heap |
| Kubernetes Cluster | **7249** | Pod CPU/Memory, node utilization |

### Loki Log Sorguları (Grafana → Explore → Loki)

```logql
# Servis bazlı log arama:
{service="shopping-cart"} |= "Error"

# RabbitMQ publish event'leri:
{service="shopping-cart"} |= "Published" | json

# Tüm servislerde son 100 hata:
{namespace="microservices"} |= "Exception" | json | line_format "{{.Message}}"
```

### Tempo Trace Sorguları (Grafana → Explore → Tempo)

- **Search**: Service = `shopping-cart`, Duration > 100ms
- **TraceID**: Log satırındaki `traceId` alanından doğrudan atlama
- **Service Graph**: Service → Service bağlantılarını görselleştir

---

## Lens Yapılandırması

### 1. Prometheus Bağlantısı
Lens → Settings → Kubernetes → Prometheus:
- Type: **Prometheus Operator** (kube-prometheus-stack yüklüyse otomatik bulur)
- Ya da Manual URL: `http://kube-prometheus-stack-prometheus.monitoring:9090`

### 2. Pod Metrics (Lens'de)
- Sol panel → Workloads → Pods
- Herhangi bir pod'a tıkla → Metrics sekmesi görünür
- CPU, Memory, Network grafiklerini gerçek zamanlı izle

### 3. Log İzleme (Lens'de)
- Pod → Logs sekmesi
- JSON formatında Serilog logları görünür
- `Follow` ile canlı takip, `Search` ile filtreleme

### 4. Terminal (Lens'de)
- Pod → Terminal sekmesi
- Direkt pod içine bağlan, `dotnet-counters`, `dotnet-trace` çalıştır

---

## Port Haritası

| Servis | Port | Erişim |
|--------|------|--------|
| Gateway API | `localhost:80` | LoadBalancer |
| Grafana | `localhost:3000` | port-forward |
| Prometheus | `localhost:9090` | port-forward |
| RabbitMQ UI | `localhost:15672` | port-forward |
| Jaeger/Tempo | Grafana içinde | — |

```bash
# RabbitMQ Management UI:
kubectl port-forward svc/rabbitmq 15672:15672 -n microservices

# Prometheus UI:
kubectl port-forward svc/kube-prometheus-stack-prometheus 9090:9090 -n monitoring
```

---

## Troubleshooting

```bash
# Pod log'larına bak:
kubectl logs -f deployment/shopping-cart -n microservices

# OTel Collector çalışıyor mu?
kubectl logs deployment/otel-collector -n monitoring

# Prometheus scrape targets:
# http://localhost:9090/targets (port-forward gerekli)

# Image pull hatası (imagePullPolicy: Never — sadece local build olmalı):
docker images | grep shopping-cart
```
