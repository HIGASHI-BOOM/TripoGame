# Codex + Unity + MCP for Unity 环境安装指引

本文档用于在 Windows 上搭建一套可用的 `Codex + Unity + MCP for Unity` 开发环境，让 Codex 能直接读取 Unity Editor 状态、查看 Console、操作场景/Prefab/资源、执行 Unity 测试。

本文以目标 Unity `2022.3.62f2c1` 环境为参考：

- Unity 项目路径：`E:\Unity\Porject\TripoGame`
- Unity Editor 示例路径：`E:\Unity\2022.3.62f2c1\Editor\Unity.exe`
- Unity 版本：`2022.3.62f2c1`
- 渲染管线：按项目实际 `Packages/manifest.json` 为准；Unity 2022 LTS 项目通常使用 URP `14.x`
- MCP 插件：CoplayDev `unity-mcp`，项目内嵌 `Packages/com.coplaydev.unity-mcp`
- MCP 传输方式：HTTP Local
- Codex MCP URL：`http://127.0.0.1:8080/mcp`

> 注意：项目目录里的 `Porject` 是现有路径拼写，不要自动改成 `Project`。

## 参考链接

- Unity Download Archive：<https://unity.com/releases/editor/archive>
- Python Windows 安装说明：<https://docs.python.org/3.13/using/windows.html>
- uv 安装说明：<https://docs.astral.sh/uv/getting-started/installation/>
- Codex 产品页：<https://openai.com/codex/>
- Codex App 介绍：<https://openai.com/index/introducing-the-codex-app/>
- Codex CLI 入门：<https://help.openai.com/en/articles/11096431>
- Windows Package Manager / winget：<https://learn.microsoft.com/windows/package-manager/winget>
- CoplayDev MCP for Unity：<https://github.com/CoplayDev/unity-mcp>
- OpenAI Docs MCP / Codex MCP 配置示例：<https://developers.openai.com/learn/docs-mcp>
- MCP transport 规范：<https://modelcontextprotocol.io/specification/2025-06-18/basic/transports>

## 一、安装前准备

目标机器需要：

1. Windows 10/11
2. Unity Hub
3. Unity Editor `2022.3.62f2c1`
4. Python `3.10+`，建议使用 Python `3.13.x`
5. uv / uvx
6. Codex App
7. 可选：Codex CLI
8. 项目源码，包括 `Packages/com.coplaydev.unity-mcp`

验证命令：

```powershell
python --version
uv --version
uvx --version
```

如果 PowerShell 里识别不到 `uv` 或 `uvx`，重开终端，或者确认下面路径在 `PATH` 中：

```text
C:\Users\<你的用户名>\.local\bin
```

## 二、安装 Unity

1. 安装 Unity Hub。
2. 在 Unity Download Archive 或 Unity Hub 中找到 Unity `2022.3.62f2c1`。
3. 通过 Unity Hub 安装该版本。
4. 确认 Unity Editor 路径类似：

```text
E:\Unity\2022.3.62f2c1\Editor\Unity.exe
```

如果安装路径不同，后续文档里的 Unity 路径按实际路径替换。

## 三、安装 Python 和 uv

### Python

安装 Python `3.10+`。推荐从 Python 官网安装 Python `3.13.x`。

安装时建议勾选：

- Add python.exe to PATH
- Install launcher for all users 或安装 Python Launcher

验证：

```powershell
python --version
```

### uv / uvx

在 PowerShell 中执行：

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

重开 PowerShell 后验证：

```powershell
uv --version
uvx --version
```

## 四、安装 Codex

### 方式 A：正常安装 Codex App

1. 打开 Codex 产品页：

```text
https://openai.com/codex/
```

2. 下载或打开 Codex App。
3. 安装完成后，用 ChatGPT 账号登录。
4. 第一次打开项目时，选择项目目录：

```text
E:\Unity\Porject\TripoGame
```

5. 如果 Codex 提示是否信任项目配置，选择信任。

OpenAI 官方说明中，Codex App 已支持 Windows；Codex App、CLI、IDE 扩展和云端 Codex 都可以使用同一个 ChatGPT 登录体系。

### 方式 B：Windows 商店打不开时，用命令行安装 Codex App

如果 Microsoft Store 打不开、卡住、无法搜索，优先使用 `winget` 安装 Codex App。Codex App 的 Microsoft Store ID 是：

```text
9PLM9XGG6VKS
```

在 PowerShell 中执行：

```powershell
winget install --id 9PLM9XGG6VKS --source msstore --accept-package-agreements --accept-source-agreements
```

如果希望静默安装：

```powershell
winget install --id 9PLM9XGG6VKS --source msstore --accept-package-agreements --accept-source-agreements --silent
```

验证是否安装成功：

```powershell
winget list --id 9PLM9XGG6VKS --source msstore
```

或：

```powershell
Get-StartApps | Where-Object { $_.Name -match 'Codex' }
```

正常结果应能看到：

```text
Codex
```

### 如果 winget 不可用

先检查：

```powershell
winget --version
```

如果提示找不到 `winget`，通常是 Windows App Installer 没注册或版本过旧。可以先尝试重新注册：

```powershell
Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe
```

然后重开 PowerShell，再执行：

```powershell
winget --version
```

如果仍不可用，去 Microsoft 官方文档检查 Windows Package Manager / App Installer 的安装状态：

```text
https://learn.microsoft.com/windows/package-manager/winget
```

### 可选：安装 Codex CLI

Codex App 是本文推荐的主入口。需要命令行工作流时，也可以安装 Codex CLI。

需要先安装 Node.js / npm，然后执行：

```powershell
npm i -g @openai/codex
```

登录：

```powershell
codex --login
```

升级：

```powershell
codex --upgrade
```

验证：

```powershell
codex --version
```

## 五、安装或确认 MCP for Unity 插件

当前项目采用“内嵌包”方式，插件目录应存在：

```text
E:\Unity\Porject\TripoGame\Packages\com.coplaydev.unity-mcp
```

锁文件里应能看到：

```text
com.coplaydev.unity-mcp
version: file:com.coplaydev.unity-mcp
source: embedded
```

如果是新项目，可参考 CoplayDev 官方仓库安装：

```text
https://github.com/CoplayDev/unity-mcp
```

团队项目建议把插件固定为项目内嵌包或固定 Git tag，避免不同同事拉到不同版本。

## 六、打开 Unity 并完成 MCP for Unity 设置

1. 用 Unity `2022.3.62f2c1` 打开项目：

```text
E:\Unity\Porject\TripoGame
```

2. 等待 Unity 完成导入和编译。
3. 打开菜单：

```text
Window -> MCP for Unity
```

4. 如果出现 Setup 页面，确认：

- Python 是绿色
- UV Package Manager 是绿色
- 出现 `All requirements met`

5. 点击 `Done`。
6. 在 MCP for Unity 主面板中选择或确认 HTTP Local 模式。
7. 确认状态类似：

```text
Session Active (TripoGame)
```

当前跑通配置使用的地址是：

```text
http://127.0.0.1:8080/mcp
```

## 七、配置 Codex MCP

Codex 读取 MCP 配置的位置通常是：

```text
C:\Users\<你的用户名>\.codex\config.toml
```

在该文件中加入：

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:8080/mcp"
```

本项目也保留一份项目本地配置：

```text
E:\Unity\Porject\TripoGame\.codex\config.toml
```

内容同样是：

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:8080/mcp"
```

实践中，当前 Codex 桌面环境优先依赖全局配置是否注入工具；如果只写项目本地 `.codex/config.toml`，可能出现 Unity 已连接但 Codex 看不到 `unityMCP` 的情况。

## 八、启动顺序

推荐顺序：

1. 启动 Unity Hub。
2. 打开 `E:\Unity\Porject\TripoGame`。
3. 打开 `Window -> MCP for Unity`。
4. 确认 HTTP Local / Session Active。
5. 启动或重启 Codex。
6. 在 Codex 中打开项目目录：

```text
E:\Unity\Porject\TripoGame
```

7. 在 Codex 里确认 MCP 工具可用。

如果修改了 `C:\Users\<你的用户名>\.codex\config.toml`，需要重启 Codex。MCP 工具通常在会话启动时注入，不会在当前会话里热加载。

## 九、验证方式

### 在 PowerShell 验证端口

```powershell
Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -eq 8080 }
```

能看到 `127.0.0.1:8080` 或相关监听即可。

### 在 Codex 里验证

向 Codex 提问：

```text
检查一下 Unity 状态，读取当前 scene 和 Console
```

如果 MCP 已连接，Codex 应能使用 Unity MCP 工具读取 Editor 状态、场景信息或 Console。

### 在 Unity 里验证

打开：

```text
Window -> MCP for Unity
```

确认：

```text
Session Active (TripoGame)
```

## 十、常见问题

### 1. Unity 显示 ready，但 Codex 里没有 unityMCP

检查全局配置：

```powershell
Get-Content C:\Users\<你的用户名>\.codex\config.toml
```

确认存在：

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:8080/mcp"
```

然后重启 Codex。

### 2. 只配置了项目 `.codex/config.toml`，Codex 仍看不到工具

把同样的配置也写入全局：

```text
C:\Users\<你的用户名>\.codex\config.toml
```

这是当前环境里已经验证过的关键点。

### 3. Unity MCP setup 写着 Claude Code，不代表 Codex 一定可用

MCP for Unity 的 Setup 可能显示：

```text
Successfully registered with Claude Code using HTTP transport
```

这只能说明 Unity 插件端配置成功，不代表 Codex 的 `config.toml` 已写好。Codex 仍需要自己的：

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:8080/mcp"
```

### 4. 端口不是 8080

如果 Unity MCP 面板显示了其他 HTTP Local 端口，就把 Codex 配置里的 URL 改成实际端口：

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:<实际端口>/mcp"
```

改完重启 Codex。

### 5. Python / uv 乱码或中文变成问号

在 PowerShell 管道里运行包含中文的脚本前，先设置 UTF-8：

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:PYTHONUTF8 = '1'
```

写入重要中文内容后，用文件内容或 UTF-8 字节回读确认，不要只看终端显示。

### 6. Unity Console 里出现 Unity Connect 证书错误

例如：

```text
Curl error 35: Cert verify failed
UnityConnectWebRequestException
```

这通常是 Unity 账号/联网服务相关，不一定会影响本地 MCP 操作。优先确认 MCP 面板是否 `Session Active`，再看 Codex 是否能读 Console/Scene。

### 7. Install Skills 失败

MCP for Unity 的 `Install Skills` 是可选便利功能。失败不等于 MCP 不能用。只要 MCP server 连接正常，Codex 仍可读取 Console、操作 scene/prefab、执行工具。

### 8. Microsoft Store 打不开，无法安装 Codex App

用 `winget` 从 Microsoft Store 源安装：

```powershell
winget install --id 9PLM9XGG6VKS --source msstore --accept-package-agreements --accept-source-agreements
```

如果 `winget` 本身不可用，先重新注册 App Installer：

```powershell
Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe
```

重开 PowerShell 后再试安装 Codex App。

### 9. Codex App 安装了，但没有读取 MCP 配置

检查全局配置文件：

```text
C:\Users\<你的用户名>\.codex\config.toml
```

确认存在：

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:8080/mcp"
```

保存后重启 Codex App。MCP server 通常在 Codex 会话启动时加载。

## 十一、给同事的最短安装清单

1. 安装 Unity Hub 和 Unity `2022.3.62f2c1`。
2. 安装 Python `3.10+`，建议 `3.13.x`。
3. 安装 uv：

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

4. 安装 Codex App。正常情况下从 Codex 产品页下载安装；如果 Windows 商店打不开，用命令行：

```powershell
winget install --id 9PLM9XGG6VKS --source msstore --accept-package-agreements --accept-source-agreements
```

5. 拉取项目，确认有：

```text
Packages/com.coplaydev.unity-mcp
```

6. 用 Unity 打开项目。
7. 打开 `Window -> MCP for Unity`，完成 Setup，确认 `Session Active (TripoGame)`。
8. 在 `C:\Users\<你的用户名>\.codex\config.toml` 写入：

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:8080/mcp"
```

9. 重启 Codex。
10. 在 Codex 里打开项目目录，让 Codex 检查 Unity 状态。
