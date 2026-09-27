#!/usr/bin/env bash
# MentorPi PC 上位机框架一键管理脚本
#
# 用法:
#   bash mentorpi.sh start      起 4 个核心容器 (monitor / cpp_demo / obstacle / rosbridge)
#   bash mentorpi.sh gui        起 GUI 控制台 (要图形显示)
#   bash mentorpi.sh status     一眼看所有容器状态
#   bash mentorpi.sh logs [模块] 跟日志（默认 monitor，Ctrl+C 退出）
#   bash mentorpi.sh stop       停所有容器
#   bash mentorpi.sh rebuild [模块] 强制重 build + 重启（不指定模块 = 全部）
#   bash mentorpi.sh rebuild -w [模块] build 完自动跟日志（Ctrl+C 退出日志）
#
# 第一次跑 start 会自动 build base 镜像（~几分钟）

set -euo pipefail

cd "$(dirname "$0")"

C_GREEN='\033[0;32m'
C_YELLOW='\033[1;33m'
C_RED='\033[0;31m'
C_BLUE='\033[0;34m'
C_RESET='\033[0m'

info()  { echo -e "${C_BLUE}>>>${C_RESET} $*"; }
ok()    { echo -e "${C_GREEN}✓${C_RESET} $*"; }
warn()  { echo -e "${C_YELLOW}!${C_RESET} $*"; }
err()   { echo -e "${C_RED}✗${C_RESET} $*" >&2; }

usage() {
    sed -n '3,12p' "$0" | sed 's/^# \?//'
    exit 1
}

ensure_base_image() {
    if ! docker images --format '{{.Repository}}:{{.Tag}}' | grep -q '^mentorpi_pc/ros2-base:latest$'; then
        info "首次运行：build base 镜像 (~几分钟)"
        docker build -t mentorpi_pc/ros2-base:latest -f base/ros2-humble.Dockerfile .
    fi
}

cmd="${1:-}"

if [[ -z "$cmd" ]] || [[ "$cmd" == "help" ]] || [[ "$cmd" == "--help" ]] || [[ "$cmd" == "-h" ]]; then
    usage
fi

case "$cmd" in
    start)
        info "起 4 个核心容器 (monitor / cpp_demo / obstacle / rosbridge)"
        ensure_base_image

        xhost +local:docker >/dev/null 2>&1 || true
        docker compose up -d

        echo ""
        info "等 5 秒让节点连上 DDS..."
        sleep 5

        # 看 monitor 最后一行验证数据流
        if docker logs --tail 3 mentorpi_monitor 2>&1 | grep -q "topics:"; then
            TOPICS=$(docker logs --tail 3 mentorpi_monitor 2>&1 | grep "topics:" | tail -1 | sed 's/.*topics: //')
            if echo "$TOPICS" | grep -q "fresh"; then
                ok "数据流通：$TOPICS"
            else
                warn "数据还没到：$TOPICS"
                warn "  → 检查树莓派是否上电（听'滴'声）、是否连上热点"
            fi
        else
            warn "monitor 还没输出 topics 行——稍等 10 秒再看 logs"
        fi

        echo ""
        info "下一步：bash mentorpi.sh gui    (起 GUI 控制台)"
        info "      bash mentorpi.sh logs   (跟 monitor 日志)"
        ;;

    gui)
        info "起 GUI 控制台"
        xhost +local:docker >/dev/null 2>&1 || warn "xhost 失败，窗口可能弹不出"
        docker compose --profile gui up -d mentorpi_pc_gui
        ok "GUI 容器已起。窗口没弹出来时：docker logs -f mentorpi_gui"
        warn "窗口里点左上角「连接」→ 显示电池 / 速度 / IMU"
        ;;

    status)
        echo ""
        info "容器状态"
        docker compose ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null \
            || docker compose ps
        echo ""

        # 数据流状态
        if docker ps --format '{{.Names}}' | grep -q '^mentorpi_monitor$'; then
            info "monitor 数据流（最近一行）"
            docker logs --tail 1 mentorpi_monitor 2>&1 | grep -v "^$" | head -4
        else
            warn "monitor 容器没在跑 → bash mentorpi.sh start"
        fi

        # GUI 状态
        if docker ps --format '{{.Names}}' | grep -q '^mentorpi_gui$'; then
            ok "GUI 在跑"
        else
            echo ""
            info "GUI 没起 → bash mentorpi.sh gui"
        fi
        ;;

    logs)
        SVC="${2:-mentorpi_pc_monitor}"
        info "跟日志：$SVC  (Ctrl+C 退出)"
        docker compose logs -f --tail=50 "$SVC"
        ;;

    stop)
        info "停所有容器"
        docker compose down
        ok "已停"
        warn "下一步：关 PC → 拔树莓派电（不要先拔树莓派！）"
        ;;

    rebuild)
        # 解析参数: rebuild [-w|--watch] [模块名]
        WATCH=0
        TARGET=""
        for arg in "${@:2}"; do
            case "$arg" in
                -w|--watch) WATCH=1 ;;
                *)          TARGET="$arg" ;;
            esac
        done

        # 自动从当前编辑文件识别模块（VSCode terminal 调用时 ${CWD} 是项目根，
        # 但 ${file} 没法传给脚本——所以让用户在文件名里识别太麻烦，
        # 这里只支持显式传参。如果不传，默认全部模块）
        if [[ -z "$TARGET" ]]; then
            info "重 build 全部模块"
            docker compose build
            docker compose up -d
            ok "全部重 build 完成"
        else
            info "重 build 模块：$TARGET"
            if ! docker compose build "$TARGET"; then
                err "build 失败，停止后续步骤"
                exit 1
            fi
            docker compose up -d "$TARGET"
            ok "$TARGET 已重 build + 重启"
        fi

        if [[ $WATCH -eq 1 ]]; then
            info "自动跟日志：$TARGET  (Ctrl+C 退出)"
            docker compose logs -f --tail=30 "$TARGET"
        else
            echo ""
            info "看 logs: bash mentorpi.sh logs $TARGET"
            info "或加 -w 自动跟: bash mentorpi.sh rebuild -w $TARGET"
        fi
        ;;

    *)
        err "未知命令: $cmd"
        usage
        ;;
esac