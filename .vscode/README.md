# VSCode 开发工作流

> `~/mentorpi_pc_framework/.vscode/` —— 顶层项目配置
> `~/mentorpi_pc_framework/services/gui/.vscode/` —— GUI 子项目配置

## 一次性：装插件

打开 `~/mentorpi_pc_framework/` 时 VSCode 会自动推荐右上角弹窗：
- 一键「Install All」即可

涉及：`ms-azuretools.vscode-docker` / `ms-vscode-remote.remote-containers` /
`ms-python.python` / `ms-python.debugpy` / `ms-dotnettools.csharp` /
`AvaloniaTeam.vscode-avalonia` 等

## 通用快捷键

| 操作 | 快捷键 |
|------|--------|
| 运行任务 | `Ctrl+Shift+P` → "Tasks: Run Task" → 选 |
| 调试 | `F5` |
| 终端 | `Ctrl+`` |

---

## 三种开发场景

### A. 改 Python 节点（monitor / obstacle）

最快路径（**推荐**）：

1. VSCode 打开 `~/mentorpi_pc_framework/`
2. 编辑 `modules/mentorpi_pc_monitor/mentorpi_pc_monitor/monitor_node.py`
3. `Ctrl+Shift+P` → `Tasks: Run Task` → **"docker compose build (current module)"**
   （自动识别光标所在文件属于哪个模块，重 build 该模块镜像）
4. 同上 → **"docker compose up -d"**
5. 同上 → **"docker compose logs (current module)"** 看输出

要断点调试 Python 节点（**进阶**）：

1. 先 `docker compose up -d` 起容器
2. `Ctrl+Shift+P` → **"Dev Containers: Attach to Running Container"** → 选 `mentorpi_monitor`
   （VSCode 会重开一个窗口直接进容器，能装插件、断点调试）
3. 在容器内 VSCode 打开 `/workspace/src/mentorpi_pc_monitor/.../monitor_node.py`
4. 加断点 → F5 → 选 **"Python: ros2 run (entry)"**（或类似）

### B. 改 C++ 节点（cpp_demo）

ROS2 C++ 工具链重（CMake + colcon），**不要在宿主编译**：

1. VSCode 打开 `~/mentorpi_pc_framework/`
2. 编辑 `modules/mentorpi_pc_cpp_demo/src/odom_echo_node.cpp`
3. `Ctrl+Shift+P` → `Tasks: Run Task` → **"docker compose build (current module)"**
4. 容器内调试（看下面"容器内调试"）

### C. 改 GUI（C# / Avalonia）

GUI 独立调试最快路径：

1. VSCode 打开 `~/mentorpi_pc_framework/services/gui/`（**子目录**单独打开）
2. 编辑 `.cs` / `.axaml`
3. `Ctrl+Shift+P` → `Tasks: Run Task` → **"dotnet build"**
4. **F5** → 选 **"GUI: dotnet run (宿主)"** → 直接弹窗
5. 看到 GUI 窗口 → 点「连接」→ 数据更新

要发布 docker 镜像：

1. `Ctrl+Shift+P` → `Tasks: Run Task` → **"docker build GUI 镜像"**
2. 或顶层项目 → **"docker build GUI 镜像 + 重启"**（含 logs）

---

## 容器内调试（断点跟进去）

适用于 Python / C++ 节点：

1. `Ctrl+Shift+P` → **"Dev Containers: Attach to Running Container"**
2. 选目标容器（`mentorpi_monitor` / `mentorpi_cpp_demo` 等）
3. VSCode 在容器内重启，加载容器里的 Python / gdb 工具链
4. 在容器内打开 `/workspace/src/<pkg>/...`
5. 加断点 → F5 → 选对应 debug 配置

---

## 注意事项

- **Pylance 报 `import rclpy` 红波浪线** —— 正常，rclpy 在容器里。attach 容器后自动消
- **FluentTheme 报 .NET 编译错误** —— 确认 `Gui.csproj` 用了 **11.3.22**，11.0 的 Fonts.Inter 包是空壳
- **GUI 窗口起不来** —— 跑 `xhost +local:docker`
- **改了代码但容器里没生效** —— 重新 `docker compose build (current module)` 再 `up -d`
- **`scripts/start.sh` 第一次跑** —— 会自动 build base 镜像（~几分钟）

## 任务清单速查

| Task 标签 | 用途 |
|----------|------|
| `build base image` | 一次性 build ROS2 Humble base |
| `docker compose build` | build 所有服务镜像 |
| `docker compose build (current module)` | build 当前文件所属模块（最常用） |
| `docker compose up -d` | 起所有服务（后台） |
| `docker compose up -d (gui profile)` | 起 GUI |
| `docker compose down` | 停所有 |
| `docker compose logs (current module)` | 跟日志 |
| `exec into module (bash)` | 进容器 shell |
| `dotnet build services/gui` | 编译 GUI |
| `dotnet run services/gui (宿主直接跑)` | 宿主跑 GUI（最快反馈） |
| `docker build GUI 镜像 + 重启` | 镜像 build + 容器重启 + 看 logs |