# MirrorVault 更新日志

## 2026-05-21 — 图标更新与任务栏改进（待实施）

### 图标更新（使用 icon_now.png）
- 用户新图标文件为 `icon_now.png`（1.2MB，1254×1254），之前错误使用了旧的 `icon.png`
- 将 `icon_now.png` 复制到 `Assets/icon.png`（覆盖旧图）
- 重新生成多分辨率 ICO（16/32/48/256），清理构建缓存确保嵌入

### 任务栏不显示（根因分析）
- `WindowDecorations="None"` 无边框窗口在 Windows 上默认为 `WS_POPUP` 样式，缺少 `WS_EX_APPWINDOW` 标志导致任务栏不显示
- 在 `Opened` 事件中通过 P/Invoke `SetWindowLong` 添加 `WS_EX_APPWINDOW` 扩展样式
- 同时确保 `ShowInTaskbar="True"`

### 任务栏右键退出
- 在 Avalonia 中通过 `NativeMenu.SetMenu()` 为窗口添加原生菜单
- 菜单项：「显示主窗口」+ 分隔线 +「退出」
- 退出时设置 `_isExiting` 标志，使 `Closing` 事件不再拦截

### 已创建 publish.sh
- 一键发布 Linux + Windows，参数标准化

---

## 2026-05-21 — Bug 修复：程序集名称、图标与 Windows 版本

### 程序集名称修复
- `.csproj` 添加 `<AssemblyName>MirrorVault</AssemblyName>`、`<AssemblyTitle>MirrorVault</AssemblyTitle>`、`<Product>MirrorVault</Product>`
- 发布的可执行文件名从 `BackupApp.UI.exe` 改为 `MirrorVault.exe`（Windows）/ `MirrorVault`（Linux）
- 修复 `App.axaml.cs` 中主题资源 URI：`avares://BackupApp.UI/Themes/` → `avares://MirrorVault/Themes/`（与程序集名称同步）

### 图标修复
- 重新生成 `icon.ico`，包含 4 个标准 Windows 分辨率：16×16、32×32、48×48、256×256（原版仅 256×256 一项）
- 工具：Pillow (PIL) 多分辨率 ICO 生成

### Windows 版本无法打开（根因修复）
- **根因**：单文件自解压未包含原生 SkiaSharp DLL，导致 `DllNotFoundException: libSkiaSharp.dll`，应用静默退出
- **修复**：发布时添加 `-p:IncludeNativeLibrariesForSelfExtract=true`，将原生库嵌入自解压包
- exe 体积从 76MB → 96MB（原生库正确嵌入）

### Wine 测试环境
- 下载 Wine 11.9（Kron4ek 预编译版）安装到 `APP_data/wine-11.9/`
- 通过 VcXsrv + SSH X11 Forwarding 验证 Windows exe 在 Linux 上正常运行

---

## 2026-05-21 — 窗口美化与功能增强

### 外围主题边框
- 窗口外围增加 2px 边框，颜色跟随主题 `AppPrimaryBrush` 变化
- 窗口圆角 8px，背景设为透明让边框可见
- 每个主题切换时边框自动变色

### 窗口手动调节大小
- 状态栏右侧添加缩放手柄（⤡），拖拽可改变窗口大小
- 支持边缘拖拽缩放（底部/右侧/右下角）

### 关闭到系统托盘
- 点击 ✕ 关闭按钮 → 窗口隐藏（最小化到托盘）
- 系统托盘图标点击 → 恢复显示窗口
- 托盘图标与窗口图标统一

### 应用图标
- 生成 64x64 蓝色圆形备份图标（嵌入式 base64 PNG）
- 应用于窗口标题栏图标和系统托盘图标

### 版本保留策略优化
- 时间格式化：60min→1h, 1440min→1天, 4320min→3天
- 新增 5 个快速预设按钮：1天全部 / 3天每天 / 7天每3天 / 30天每7天 / 90天删除
- 预设一键应用完整分层策略

---

## 2026-05-21 — UI 功能增强

### 去掉外层窗口装饰
- MainWindow 添加 `SystemDecorations="None"`，去掉 OS 窗口管理器绘制的标题栏
- 自定义标题栏支持拖拽移动窗口（`BeginMoveDrag`）
- 解决 X11 转发下双层标题栏（OS + 自定义）的问题

### 浏览文件夹按钮
- 源目录输入框右侧新增「浏览...」按钮，点击可打开系统文件夹选择器
- 目标位置输入框右侧同样新增「浏览...」按钮
- 选择后路径填入文本框，仍可手动修改
- 使用 Avalonia `StorageProvider.OpenFolderPickerAsync()` API

### 合并任务编辑器标签页
- 「源目录」与「目标位置」合并为「源与目标」
- 「备份模式」与「调度」合并为「备份模式」
- 标签页从 8 个精简为 6 个：基本信息、源与目标、备份模式、文件过滤、版本保留、高级选项

### 任务卡片状态可点击切换
- 任务卡片右侧「已启用」/「已禁用」徽章改为可点击按钮
- 点击直接切换任务启用/禁用状态，即时保存并刷新列表

### 工具栏按钮优化
- 按钮文字增加图标：✎ 编辑、✕ 删除、▶ 立即执行、↺ 恢复向导、📋 历史、↻ 刷新
- 未选中任务时点击上下文按钮，状态栏显示「请先选择一个任务」提示

### 修复历史按钮无响应
- 根因：未选中任务时静默返回，无任何反馈
- 所有上下文按钮（编辑、删除、执行、恢复、历史）统一增加选中检查

---

## 2026-05-21 — 单面板架构重构

### 子窗口改为单面板视图导航
- 5 个子窗口（SettingsWindow、TaskEditorWindow、RestoreWizard、ConflictDialog、PreviewDialog）全部改为 UserControl
- MainWindow 新增 ContentControl 视图容器 + Overlay 模态层
- 标题栏新增「← 返回」按钮，支持视图间导航
- 删除所有旧 Window 文件

### 主题全局生效
- 所有视图共享同一 Window 的 ResourceDictionary，`DynamicResource` 绑定自动全局生效
- 切换主题无需额外处理，所有面板即时响应

### 新增功能
- **删除确认**：删除任务前弹出确认 overlay，防止误删
- **空状态**：无任务时显示引导提示「还没有备份任务」
- **备份历史**：查看任务的所有备份记录（时间、文件数、状态、耗时）
- **系统设置**：开机自启动、关闭到托盘、桌面通知开关

### Bug 修复
- 修复 UINotification 异步 bug：`OnConflictRequired` / `OnPreviewReady` 之前用 `InvokeAsync` 异步派发但同步返回（永远返回 null/false），改为 `TaskCompletionSource` + overlay 模式正确等待用户操作
- 修复备份时间戳碰撞：目录名从 `yyyy-MM-dd_HH-mm-ss` 改为 `yyyy-MM-dd_HH-mm-ss-fff`
- 修复 SQLite null 参数：`NextRun?.ToString("o")` 传入 null 改为 `?? DBNull.Value`
