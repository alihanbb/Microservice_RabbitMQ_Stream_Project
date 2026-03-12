#!/usr/bin/env bash
# =============================================================================
# build-images.sh
# Builds Docker images and pushes to Docker Hub (alihan1453)
# Usage: ./scripts/build-images.sh
# Prerequisites: docker login -u alihan1453
# =============================================================================
set -euo pipefail

DOCKER_HUB_USER="alihan1453"
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "============================================================"
echo "  Building & Pushing Docker Images → $DOCKER_HUB_USER"
echo "============================================================"

build_and_push() {
  local name=$1
  local context=$2
  local tag="$DOCKER_HUB_USER/$name:latest"

  echo ""
  echo ">>> $tag"
  docker build -t "$tag" -f "$context/Dockerfile" "$context"
  docker push "$tag"
  echo "✓ Pushed $tag"
}

build_and_push "shopping-cart" "$PROJECT_ROOT/src/ShoppingCartService"
build_and_push "discount"      "$PROJECT_ROOT/src/DiscountService"
build_and_push "notification"  "$PROJECT_ROOT/src/NotificationService"
build_and_push "gateway"       "$PROJECT_ROOT/src/Bff.Gateway"

echo ""
echo "============================================================"
echo "  All images pushed to Docker Hub!"
echo "  docker.io/$DOCKER_HUB_USER/shopping-cart:latest"
echo "  docker.io/$DOCKER_HUB_USER/discount:latest"
echo "  docker.io/$DOCKER_HUB_USER/notification:latest"
echo "  docker.io/$DOCKER_HUB_USER/gateway:latest"
echo "============================================================"
