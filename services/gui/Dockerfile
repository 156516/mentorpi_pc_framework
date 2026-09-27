# C# GUI 镜像（Avalonia + rosbridge WebSocket）
# 跨平台：Linux / Windows / Mac
# 已实现：连接 rosbridge + 订阅 /odom /imu /battery 并刷新界面

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# 用 NuGet 国内镜像（避开 nuget.org 网络问题）
RUN dotnet nuget add source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --name huaweicloud 2>&1 || true \
 && dotnet nuget add source https://nuget.azure.cn/v3/index.json --name azure-cn 2>&1 || true

# 先 COPY csproj 单独 restore（利用 docker 缓存）
COPY Gui.csproj .
RUN dotnet restore Gui.csproj --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json

# 再 COPY 源码
COPY . .
RUN dotnet publish Gui.csproj -c Release -o /out --no-restore

# runtime 镜像（体积小）
FROM mcr.microsoft.com/dotnet/runtime:8.0

# Avalonia + SkiaSharp 在 Linux 需要原生依赖（runtime 镜像默认不带 libfontconfig 等）
# 先换阿里云源（避开校园网 DNS 劫持）再装；fonts-wqy-microhei 提供中文字形
RUN sed -i 's|deb.debian.org|mirrors.aliyun.com|g; s|security.debian.org|mirrors.aliyun.com|g' /etc/apt/sources.list.d/debian.sources 2>/dev/null || true \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
      libfontconfig1 libfreetype6 fontconfig \
      fonts-wqy-microhei \
      libx11-6 libice6 libsm6 libxcursor1 libxrandr2 libxinerama1 \
      libxdamage1 libxcomposite1 libxi6 libxrender1 libxtst6 \
      libxkbcommon0 libxkbcommon-x11-0 \
 && fc-cache -f \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out .

ENV ROS_BRIDGE_URL=ws://localhost:9090

CMD ["dotnet", "mentorpi_pc_gui.dll"]