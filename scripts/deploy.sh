#!/usr/bin/env bash
# =============================================================================
# deploy.sh  —  Full Kubernetes deployment for Microservice_RabbitMQ_Stream_Project
# Docker Hub: alihan1453
# Prerequisites:
#   1. Docker Desktop → Settings → Kubernetes → Enable Kubernetes (✓)
#   2. helm installed  (winget install Helm.Helm)
#   3. docker login -u alihan1453
# =============================================================================
set -euo pipefail

echo "============================================================"
echo "  Deploy: RabbitMQ Stream Microservices"
echo "  Registry: docker.io/alihan1453"
echo "============================================================"

# ─── Kubernetes bağlantı kontrolü ──────────────────────────────────────────
if ! kubectl cluster-info &>/dev/null; then
  echo ""
  echo "ERROR: Kubernetes bağlantısı yok!"
  echo "  Docker Desktop → Settings → Kubernetes → Enable Kubernetes"
  echo "  Aktif olduktan sonra tekrar çalıştır."
  exit 1
fi

echo "✓ Kubernetes hazır: $(kubectl config current-context)"

# ─── Helm repo ekle ─────────────────────────────────────────────────────────
echo ""
echo "[1/6] Helm repos ekleniyor..."
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts 2>/dev/null || true
helm repo add grafana               https://grafana.github.io/helm-charts              2>/dev/null || true
helm repo update --fail-on-repo-update-fail 2>/dev/null || helm repo update

# ─── Namespace ──────────────────────────────────────────────────────────────
echo "[2/6] Namespace oluşturuluyor..."
kubectl apply -f k8s/namespace.yaml

# ─── Observability Stack (Helm) ─────────────────────────────────────────────
echo "[3/6] Prometheus + Grafana kuruluyor..."
helm upgrade --install kube-prometheus-stack prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --set grafana.adminPassword=admin123 \
  --set grafana.service.type=LoadBalancer \
  --set prometheus.prometheusSpec.serviceMonitorSelectorNilUsesHelmValues=false \
  --wait --timeout=5m

echo "     Loki + Promtail kuruluyor..."
helm upgrade --install loki grafana/loki-stack \
  --namespace monitoring \
  --set loki.enabled=true \
  --set promtail.enabled=true \
  --set grafana.enabled=false \
  --wait --timeout=3m

echo "     Tempo kuruluyor..."
helm upgrade --install tempo grafana/tempo \
  --namespace monitoring \
  --wait --timeout=3m

kubectl apply -f k8s/monitoring/otel-collector.yaml

# ─── Infrastructure ──────────────────────────────────────────────────────────
echo "[4/6] Altyapı deploy ediliyor (RabbitMQ, SQL Server, Redis)..."
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmaps.yaml
kubectl apply -f k8s/infrastructure/rabbitmq/
kubectl apply -f k8s/infrastructure/sqlserver/
kubectl apply -f k8s/infrastructure/redis/

echo "     RabbitMQ hazır olana kadar bekleniyor..."
kubectl wait --for=condition=ready pod -l app=rabbitmq \
  -n microservices --timeout=120s || echo "(timeout — devam ediliyor)"

# ─── Mikroservisler ──────────────────────────────────────────────────────────
echo "[5/6] Mikroservisler deploy ediliyor..."
kubectl apply -f k8s/services/

# ─── Durum kontrolü ──────────────────────────────────────────────────────────
echo "[6/6] Deploy durumu:"
echo ""
kubectl get pods -n microservices
echo ""
kubectl get pods -n monitoring
echo ""
kubectl get svc  -n microservices

echo ""
echo "============================================================"
echo "  DEPLOY TAMAMLANDI!"
echo ""
echo "  Gateway API erişimi:"
GATEWAY_PORT=$(kubectl get svc gateway -n microservices -o jsonpath='{.spec.ports[0].nodePort}' 2>/dev/null || echo "80")
echo "  http://localhost:$GATEWAY_PORT"
echo ""
echo "  Grafana (log/trace/metric dashboard):"
echo "  kubectl port-forward svc/kube-prometheus-stack-grafana 3000:80 -n monitoring"
echo "  Tarayıcı: http://localhost:3000  (admin / admin123)"
echo ""
echo "  RabbitMQ Management:"
echo "  kubectl port-forward svc/rabbitmq 15672:15672 -n microservices"
echo "  Tarayıcı: http://localhost:15672"
echo "============================================================"
