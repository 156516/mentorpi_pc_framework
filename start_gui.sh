#!/usr/bin/env bash
# 一键启动 / 重启 MentorPi GUI 控制台（关窗口后容器会退出，跑这个再拉起来）
# 用法：bash ~/mentorpi_pc_framework/start_gui.sh
set -euo pipefail
cd "$(dirname "$0")"

# 允许容器访问宿主 X11（否则窗口弹不出来）
xhost +local:docker >/dev/null 2>&1 || true

# 启动 GUI 容器（默认 profile 的 monitor/cpp_demo/rosbridge 不受影响）
docker compose --profile gui up -d mentorpi_pc_gui

echo ""
echo ">>> GUI 已启动。窗口没弹出时看日志："
echo "    docker logs -f mentorpi_gui"
echo ">>> 窗口里点左上角「连接」按钮，即显示电池 / 速度 / IMU。"
