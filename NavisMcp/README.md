# NavisMcp v2 — Navisworks MCP 插件（73 个工具，Cherry Studio 直连）

插件在 Navisworks 进程内提供 **MCP Streamable HTTP** 服务：`http://127.0.0.1:3900/mcp`
不需要 Node.js / Python / 管理员权限。

## 部署
1. `build.bat`：使用 Windows 自带 csc.exe 编译，输出 `bin\NavisMcp.dll`，编译日志写入 `build.log`
   - 默认路径：`D:\Users\LI_Y1\Program Files\Autodesk\Navisworks Manage 2024`
   - 其他路径：`build.bat "C:\Program Files\Autodesk\Navisworks Manage 2025"`
2. 关闭 Navisworks → `install.bat`（其他版本：`install.bat 2025`）
3. 打开 Navisworks 后服务自动启动；**Tool Add-ins → Navis MCP** 可手动开关
4. Cherry Studio → 设置 → MCP 服务器 → 添加 → 类型 **Streamable HTTP** → URL `http://127.0.0.1:3900/mcp`
   （也可导入 `cherry-mcp.json`）

## 列出所有工具及说明
| 方式 | 说明 |
|---|---|
| AI 对话 | 调用 `list_tools`（可按 category / keyword 过滤，format=markdown） |
| Navisworks | **Tool Add-ins → Navis MCP Tools**：表格窗口，可复制为 Markdown |
| 浏览器 | `http://127.0.0.1:3900/tools`（HTML）、`/tools.json`、`/tools.md` |
| 文件 | `TOOLS.md` |

## 工具分类（详见 TOOLS.md）
Meta · Document · Model Tree · Search · Selection · Properties · Data Check · Selection Sets ·
Visibility & Appearance · Transform · View · Viewpoints · Sectioning · Clash · TimeLiner · Plugins · Advanced API

## 构件引用方式（所有带 target 的工具通用）
- `handles`：其他工具返回的 handle（如 `h12`）
- `guids`：InstanceGuid
- `selection_set`：已保存的选择集或搜索集名称
- `use_selection: true`：使用当前选择

属性写法：`类别|属性`，例如 `Element|CAT Code`；只写属性名时默认类别为 `Element`。

## 提示词示例
- 列出所有工具
- 统计 Element|Category 的分布，并按 Category 自动着色
- 检查 CAT Code, TS_ASS, TS_LOC 的完整性，失败构件标红，导出到桌面 check.csv
- 为所有缺少 CAT Code 的构件建立搜索集 "Missing CAT"
- 用 "ARC" 和 "MEP" 两个选择集建立硬碰撞测试并运行，导出碰撞报告 CSV
- 把 Clash1–Clash5 设为 Reviewed，指派给 YL
- 在 L3 楼面标高 12.5 处剖切，切换为正交顶视图并截图
- 给选中构件写入自定义属性 Tab "QA"：Status=OK, Checker=YL
- 遍历所有视点导出 PNG 到 D:\temp\views

## 版本兼容说明
- 编译按 C# 5 语法，适用于 .NET Framework 4.8 的 Navisworks（2024 已确认运行在 .NET Framework 4.8）。
- 截图（`View.GenerateImage`）、剖切（`Get/SetClippingPlanes`）、TimeLiner、部分 Clash 编辑、文件打开/保存等与版本相关的 API 通过反射调用：编译不依赖这些接口；如果当前版本不支持，工具会返回明确的错误信息。
- `api_inspect` / `api_invoke` 可通过反射访问其他未封装为专用工具的 API。

## 排错
| 现象 | 处理 |
|---|---|
| 编译失败 | 把 `build.log` 发回来修正 |
| 插件不出现 | 文件夹名必须与 DLL 同名；右键 DLL → 属性 → 解除锁定 |
| `/health` 打不开 | 查看 `%TEMP%\NavisMcp.log`；端口被占用时设置环境变量 `NAVIS_MCP_PORT` |
| 不需要自动启动 | 设置环境变量 `NAVIS_MCP_AUTOSTART=0` |
| handle 失效 | 打开或追加文件后 handle 会重置，重新 search 即可 |
| 自测 | `powershell -ExecutionPolicy Bypass -File test-mcp.ps1` |
