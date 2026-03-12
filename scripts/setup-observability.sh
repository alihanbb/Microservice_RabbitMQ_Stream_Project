#!/usr/bin/env bash
# =============================================================================
# setup-observability.sh
# Installs the full observability stack via Helm on Docker Desktop Kubernetes
# Prerequisites: kubectl, helm, Docker Desktop with Kubernetes enabled
# =============================================================================
set -euo pipefail

MONITORING_NS="monitoring"
MICROSERVICES_NS="microservices"

echo "============================================================"
echo "  Microservices Kubernetes + Observability Setup"
echo "============================================================"

# ─── 0. Namespaces ───────────────────────────────────────────────────────────
echo "[1/7] Creating namespaces..."
kubectl apply -f k8s/namespace.yaml

# ─── 1. Helm Repos ───────────────────────────────────────────────────────────
echo "[2/7] Adding Helm repos..."
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo add grafana               https://grafana.github.io/helm-charts
helm repo add open-telemetry        https://open-telemetry.github.io/opentelemetry-helm-charts
helm repo update

# ─── 2. kube-prometheus-stack (Prometheus + Grafana + AlertManager) ──────────
echo "[3/7] Installing kube-prometheus-stack..."
helm upgrade --install kube-prometheus-stack prometheus-community/kube-prometheus-stack \
  --namespace "$MONITORING_NS" \
  --create-namespace \
  --set grafana.adminPassword=admin123 \
  --set grafana.service.type=LoadBalancer \
  --set prometheus.service.type=ClusterIP \
  --set prometheus.prometheusSpec.serviceMonitorSelectorNilUsesHelmValues=false \
  --set prometheus.prometheusSpec.podMonitorSelectorNilUsesHelmValues=false \
  --wait --timeout=5m

# ─── 3. Loki + Promtail (log aggregation) ────────────────────────────────────
echo "[4/7] Installing Loki + Promtail..."
helm upgrade --install loki grafana/loki-stack \
  --namespace "$MONITORING_NS" \
  --set loki.enabled=true \
  --set promtail.enabled=true \
  --set grafana.enabled=false \
  --wait --timeout=3m

# ─── 4. Tempo (distributed tracing) ─────────────────────────────────────────
echo "[5/7] Installing Grafana Tempo..."
helm upgrade --install tempo grafana/tempo \
  --namespace "$MONITORING_NS" \
  --set tempo.storage.trace.backend=local \
  --wait --timeout=3m

# ─── 5. OpenTelemetry Collector ──────────────────────────────────────────────
echo "[6/7] Deploying OpenTelemetry Collector..."
kubectl apply -f k8s/monitoring/otel-collector.yaml

# ─── 6. Infrastructure + Services ────────────────────────────────────────────
echo "[7/7] Deploying infrastructure and services..."
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmaps.yaml
kubectl apply -f k8s/infrastructure/rabbitmq/
kubectl apply -f k8s/infrastructure/sqlserver/
kubectl apply -f k8s/infrastructure/redis/

echo ""
echo "============================================================"
echo "  Infrastructure deployed! Build and push images first:"
echo "  ./scripts/build-images.sh"
echo "  then: kubectl apply -f k8s/services/"
echo "============================================================"
