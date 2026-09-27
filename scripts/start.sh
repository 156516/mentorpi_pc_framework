#!/usr/bin/env bash
# MentorPi 框架一键启动脚本
# 用法：
#   ./scripts/start.sh              # 全部后台（默认不含 GUI）
#   ./scripts/start.sh gui          # 含 GUI
#   ./scripts/start.sh tools        # 含 description
#   ./scripts/start.sh all          # 全部
#   ./scripts/start.sh rebuild      # 强制重 build 所有镜像

set -euo pipefail

cd "$(dirname "$0")/.."

# 默认 profile
PROFILES=""
REBUILD=""
TARGET="${1:-default}"

case "$TARGET" in
    default|"")    PROFILES="" ;;
    gui)           PROFILES="--profile gui" ;;
    tools)         PROFILES="--profile tools" ;;
    all)           PROFILES="--profile gui --profile tools" ;;
    rebuild)
        REBUILD="--build"
        PROFILES="--profile gui --profile tools"
        ;;
    *)
        echo "用法: $0 [gui|tools|all|rebuild]"
        exit 1
        ;;
esac

# 先确保 base 镜像 build 过
if ! docker images | grep -q "mentorpi_pc/ros2-base.*latest"; then
    echo ">>> 首次运行：build base 镜像"
    docker build -t mentorpi_pc/ros2-base:latest -f base/ros2-humble.Dockerfile .
fi

# GUI 需要 X11
if [[ "$PROFILES" == *gui* ]]; then
    echo ">>> 允许 docker 访问宿主 X11"
    xhost +local:docker >/dev/null 2>&1 || true
fi

echo ">>> docker compose up -d $PROFILES $REBUILD"
docker compose up -d $PROFILES $REBUILD

echo ""
echo ">>> 当前运行的容器："
docker compose ps

echo ""
echo ">>> 看日志："
echo "    docker compose logs -f monitor"