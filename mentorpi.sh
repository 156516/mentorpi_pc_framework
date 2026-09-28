#!/usr/bin/env bash
# MentorPi PC 上位机框架一键管理脚本
#
# 用法:
#   bash mentorpi.sh start           起 4 个核心容器 (monitor / cpp_demo / obstacle / rosbridge)
#   bash mentorpi.sh gui             起 GUI 控制台 (要图形显示)
#   bash mentorpi.sh status          一眼看所有容器状态
#   bash mentorpi.sh logs [模块]     跟日志（默认 monitor，Ctrl+C 退出）
#   bash mentorpi.sh stop            停所有容器
#   bash mentorpi.sh rebuild [模块]  强制重 build + 重启（不指定模块 = 全部）
#   bash mentorpi.sh rebuild -w [模块] build 完自动跟日志（Ctrl+C 退出日志）
#   bash mentorpi.sh restart-bridge  重启 rosbridge（GUI 显示 0 假活时用）
#   bash mentorpi.sh check-bridge    诊断 rosbridge 是否"假活"（Subscribers 是否空）
#   bash mentorpi.sh slam            起 SLAM 建图（slam_toolbox 在线异步）
#   bash mentorpi.sh save-map <名>   把当前 /map 存到 maps/<名>.{pgm,yaml}
#
# 第一次跑 start 会自动 build base 镜像（~几分钟）

set -euo pipefail

cd "$(dirname "$0")"

# 拿 compose 服务对应的容器 ID（兼容带 / 不带 hash 前缀的命名）。
# 用法: cid=$(container_id mentorpi_pc_monitor) || return 1
container_id() {
    local svc="$1"
    docker compose ps -q "$svc" 2>/dev/null | head -1
}

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
        if MON_CID=$(container_id mentorpi_pc_monitor) && docker logs --tail 3 "$MON_CID" 2>&1 | grep -q "topics:"; then
            TOPICS=$(docker logs --tail 3 "$MON_CID" 2>&1 | grep "topics:" | tail -1 | sed 's/.*topics: //')
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
        # 准备 Xauthority：Wayland 下 ~/.Xauthority 是空文件，要从 xauth info 拿真实路径
        # xauth info 用 TAB+空格 分隔（不是单一 ': '），awk 后要 strip 前导空白
        XAUTH_SRC="$(xauth info 2>/dev/null | awk -F': ' '/Authority file/ {print $2; exit}' | sed 's/^[[:space:]]*//')"
        if [[ -n "$XAUTH_SRC" && -f "$XAUTH_SRC" && -s "$XAUTH_SRC" ]]; then
            cp -f "$XAUTH_SRC" /tmp/.docker-xauth
            chmod 644 /tmp/.docker-xauth
            ok "Xauthority: $XAUTH_SRC → /tmp/.docker-xauth"
        elif [[ -f "$HOME/.Xauthority" && -s "$HOME/.Xauthority" ]]; then
            cp -f "$HOME/.Xauthority" /tmp/.docker-xauth
            chmod 644 /tmp/.docker-xauth
            ok "Xauthority: ~/.Xauthority → /tmp/.docker-xauth"
        else
            warn "找不到 Xauthority（Wayland 没起？），尝试用 Xauthority= 无 cookie 启动（可能 X server 会拒）"
        fi
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

        # 数据流状态（兼容带/不带 hash 前缀的容器名）
        if MON_CID=$(container_id mentorpi_pc_monitor); then
            info "monitor 数据流（最近一行）"
            docker logs --tail 1 "$MON_CID" 2>&1 | grep -v "^$" | head -4
        else
            warn "monitor 容器没在跑 → bash mentorpi.sh start"
        fi

        # GUI 状态
        if container_id mentorpi_pc_gui >/dev/null 2>&1; then
            ok "GUI 在跑"
        else
            echo ""
            info "GUI 没起 → bash mentorpi.sh gui"
        fi

        # rosbridge 健康度（4-6 小时后会出现"假活"，见文档）
        if docker ps --format '{{.Names}}' | grep -q 'mentorpi_rosbridge'; then
            info "rosbridge 健康度"
            SUBS=$(docker exec "$MON_CID" 2>/dev/null bash -c \
                "source /opt/ros/humble/setup.bash 2>/dev/null && ros2 node info /rosbridge_websocket 2>/dev/null | grep -A 20 'Subscribers:' | grep -c '/'" \
                || echo "?")
            if [[ "$SUBS" =~ ^[0-9]+$ ]] && [[ $SUBS -ge 3 ]]; then
                ok "rosbridge 健康（$SUBS 个 ROS 订阅）"
            elif [[ "$SUBS" == "0" ]]; then
                warn "rosbridge 假活！0 个 ROS 订阅 → bash mentorpi.sh restart-bridge"
            else
                echo "  (Subscribers 数: $SUBS)"
            fi
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

    restart-bridge)
        # GUI 显示全 0 时第一招——rosbridge 假活，restart 一下就好
        info "重启 rosbridge（修「假活」——容器在跑但 Subscribers 是空）"
        docker compose restart rosbridge
        sleep 3
        # 自动验证：restart 后应该有 ≥3 个 ROS 订阅（用 compose 服务名拿 ID，兼容 hash 前缀）
        MON_CID=$(container_id mentorpi_pc_monitor)
        SUBS=$(docker exec "$MON_CID" 2>/dev/null bash -c \
            "source /opt/ros/humble/setup.bash 2>/dev/null && ros2 node info /rosbridge_websocket 2>/dev/null | grep -A 30 'Subscribers:' | grep -c '/'" \
            || echo "?")
        if [[ "$SUBS" =~ ^[0-9]+$ ]] && [[ $SUBS -ge 3 ]]; then
            ok "rosbridge 已恢复（$SUBS 个 ROS 订阅）—— GUI 重新点「连接」"
        else
            warn "restart 后仍异常（Subscribers: $SUBS），看 logs: bash mentorpi.sh logs rosbridge"
        fi
        ;;

    check-bridge)
        # 诊断：rosbridge 是否真的活着（vs 假活）
        info "rosbridge 健康度"
        if ! docker ps --format '{{.Names}}' | grep -q 'mentorpi_rosbridge'; then
            warn "rosbridge 容器没在跑 → bash mentorpi.sh start"
            exit 1
        fi
        MON_CID=$(container_id mentorpi_pc_monitor)
        INFO=$(docker exec "$MON_CID" bash -c \
            "source /opt/ros/humble/setup.bash 2>/dev/null && ros2 node info /rosbridge_websocket 2>/dev/null" 2>&1)
        echo "$INFO" | grep -E "^(  Subscribers:|  Publishers:|    /)" | head -30
        SUBS=$(echo "$INFO" | grep -A 30 "^  Subscribers:" | grep -c "^    /")
        if [[ $SUBS -ge 3 ]]; then
            ok "健康（$SUBS 个 ROS 订阅）"
        else
            warn "假活！只有 $SUBS 个订阅 → bash mentorpi.sh restart-bridge"
        fi
        ;;

    slam)
        # SLAM 在线建图——订阅 /scan_raw + /odom + /tf，发布 /map + map→odom TF
        info "起 SLAM 建图（slam_toolbox 在线异步）"
        if ! docker images --format '{{.Repository}}:{{.Tag}}' | grep -q '^mentorpi_pc/slam_toolbox:latest$'; then
            info "首次跑 slam：先 build（~2-3 分钟）"
            docker compose --profile slam build mentorpi_pc_slam_toolbox
        fi
        mkdir -p maps
        docker compose --profile slam up -d mentorpi_pc_slam_toolbox
        ok "SLAM 容器已起。开 APP / 手柄开车扫一圈后：bash mentorpi.sh save-map <房间名>"
        info "跟日志：bash mentorpi.sh logs mentorpi_pc_slam_toolbox"
        ;;

    save-map)
        # 用 slam_toolbox 自带的 service 把当前 /map 存成 .pgm + .yaml
        # slam_toolbox/srv/SaveMap（async_slam_toolbox_node 服务名 /slam_toolbox/save_map）
        NAME="${2:-map}"
        CID=$(container_id mentorpi_pc_slam_toolbox)
        if [[ -z "$CID" ]]; then
            err "SLAM 容器没在跑 → bash mentorpi.sh slam"
            exit 1
        fi
        info "保存地图到 maps/${NAME}.{pgm,yaml}"
        # ⚠️ docker exec 默认不读 stdin（heredoc 不工作）；
        # 引号嵌套太深（4 层：宿 bash → 宿 ssh 字符串 → 容器 bash -lc → ROS yaml）会出 \}
        # 修法：把 yaml 字符串作为 bash -c 的参数传入（bash -c "$0" "$1" — $1 = yaml 字符串）
        docker exec "$CID" bash -lc \
            'source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 service call /slam_toolbox/save_map slam_toolbox/srv/SaveMap "$1" && ls -la /workspace/maps/'"${NAME}"'.*' \
            _ "{name: {data: '/workspace/maps/${NAME}'}}"
        # ls 在容器内跑、看不到宿主 maps——等几秒让 bind mount 同步
        # 等文件落盘（map_saver lifecycle + map_io 写盘 + bind mount 同步，通常 < 3s）
        for i in 1 2 3 4 5; do
            if [[ -f "maps/${NAME}.pgm" ]] && [[ -f "maps/${NAME}.yaml" ]]; then
                SIZE=$(stat -c '%s' "maps/${NAME}.pgm" 2>/dev/null || echo 0)
                if [[ $SIZE -gt 100 ]]; then
                    ok "已保存：maps/${NAME}.pgm (${SIZE} bytes) + maps/${NAME}.yaml"
                    info "下一步：导航课会用 nav2 load map → maps/${NAME}.yaml"
                    exit 0
                fi
            fi
            sleep 1
        done
        warn "保存失败，看 logs：bash mentorpi.sh logs mentorpi_pc_slam_toolbox"
        ;;

    *)
        err "未知命令: $cmd"
        usage
        ;;
esac