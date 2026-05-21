# 自动备份软件 — 实现计划

## 概述

一款 Windows 桌面备份工具（exe），图形界面 + 系统托盘，支持多种备份模式、定时调度、版本分层保留、还原向导。

**技术栈**：C# / .NET 8 + Avalonia UI（跨平台，可在 Linux 开发，发布 Windows exe）

## 进度

| Phase | 内容 | 状态 |
|-------|------|:----:|
| Phase 1 | 项目骨架 + 数据模型 + SQLite 存储 | ✅ |
| Phase 2 | 核心引擎（扫描/备份/还原/版本清理/调度） | ✅ |
| Phase 3 | 存储后端（本地/网络/云可插拔接口） | ✅ |
| Phase 4 | Avalonia UI（主窗口/任务编辑器/还原向导/冲突弹窗/预览弹窗） | ✅ |
| Phase 5 | 打包 exe + 安装器 | ⬜ |

---

## Phase 1: 项目骨架 + 数据模型

### 1.1 创建解决方案和项目
```bash
dotnet new sln -n BackupApp
dotnet new wpf -n BackupApp.UI
dotnet new classlib -n BackupApp.Core
dotnet sln add BackupApp.UI BackupApp.Core
cd BackupApp.UI
dotnet add reference ../BackupApp.Core
```

### 1.2 安装依赖包
- `Microsoft.Data.Sqlite` — SQLite 配置存储
- `TaskScheduler` — Windows 定时任务（可选，也可自实现轻量调度器）

### 1.3 定义数据模型 (`BackupApp.Core/Models/`)

**BackupTask.cs**
```
Id: Guid, Name: string
SourcePaths: List<string>
DestPath: string
DestType: enum { Local, Network, Cloud }
BackupMode: enum { OneWay, Incremental, TwoWaySync }
Schedule: { Enabled: bool, CronExpression: string }
RetentionTiers: List<RetentionTier>
Filters: FileFilter
Options: TaskOptions
Enabled: bool
CreatedAt, UpdatedAt: DateTime
```

**RetentionTier.cs**
```
Name: string (如 "1天内")
MaxAgeHours: int (此层覆盖的时间范围上限, -1表示无限)
KeepIntervalMinutes: int (保留间隔, 0表示全部保留)
// 示例: {Name:"1天内", MaxAgeHours:24, KeepIntervalMinutes:0}
//       {Name:"1-3天", MaxAgeHours:72, KeepIntervalMinutes:1440}
//       {Name:"3-7天", MaxAgeHours:168, KeepIntervalMinutes:4320}
//       {Name:"7-30天", MaxAgeHours:720, KeepIntervalMinutes:10080}
//       {Name:"超过30天", MaxAgeHours:-1, KeepIntervalMinutes:0}
```

**FileFilter.cs**
```
IncludePatterns: List<string>   // *.docx, *.pdf 等
ExcludePatterns: List<string>   // *.tmp, *.log 等
ExcludeDirectories: List<string> // node_modules, .git 等
MaxFileSizeBytes: long          // 0 表示不限制
```

**TaskOptions.cs**
```
EncryptionEnabled: bool
EncryptionPassword: string (加密存储)
CompressionEnabled: bool
VerifyAfterBackup: bool
PreviewBeforeRun: bool
```

**BackupManifest.cs** (每次备份的快照)
```
RunId: Guid
TaskId: Guid
Timestamp: DateTime
TotalFiles: int
TotalSize: long
Duration: TimeSpan
Status: enum { Success, Failed, Partial }
ErrorLog: List<string>
```

**FileEntry.cs** (清单中的单个文件记录)
```
RelativePath: string
Size: long
LastModified: DateTime
Sha256Hash: string
Status: enum { Added, Modified, Deleted, Unchanged }
```

### 1.4 实现 ConfigStore (`BackupApp.Core/Storage/ConfigStore.cs`)
- 初始化 SQLite 数据库（`%APPDATA%/BackupApp/config.db`）
- CRUD 操作：ListTasks, GetTask, SaveTask, DeleteTask
- 加密存储密码字段（使用 DPAPI 或 AES）

### 1.5 实现 ManifestStore (`BackupApp.Core/Storage/ManifestStore.cs`)
- 从磁盘 JSON 文件读写 BackupManifest
- 存储路径：`{DestPath}/{TaskName}/manifests/{timestamp}.json`
- ListManifests, GetManifest, SaveManifest, DeleteManifest

### 1.6 实现 BackupLayout (`BackupApp.Core/Storage/BackupLayout.cs`)
- 管理备份目标的目录结构
- GetDataPath(task, timestamp) → `{DestPath}/{TaskName}/data/{timestamp}/`
- GetManifestPath(task, timestamp) → `...`

---

## Phase 2: 核心引擎

### 2.1 FileScanner (`BackupApp.Core/Services/FileScanner.cs`)

```
输入：SourcePaths, FileFilter, 上次的 BackupManifest（可为 null）
输出：ScanResult { Files: List<FileEntry>, TotalSize, TotalFiles }

对比逻辑：
1. 遍历所有源目录
2. 对每个文件检查过滤规则（include/exclude pattern + 扩展名 + 大小）
3. 如果有上次 manifest，对比 path+size+mtime：
   - 新文件 → Status.Added
   - mtime 或 size 变化 → Status.Modified
   - 未变化 → Status.Unchanged
4. 上次有但本次不存在的 → Status.Deleted
```

### 2.2 BackupExecutor (`BackupApp.Core/Services/BackupExecutor.cs`)

**单向备份 (OneWay)**：
1. 扫描源目录
2. 如果 PreviewBeforeRun，生成变更预览 → 等用户确认
3. 复制新增/修改的文件到目标（覆盖）
4. 删除目标中源已不存在的文件（可选配置）
5. 保存 manifest

**增量备份 (Incremental)**：
1. 扫描源目录，对比上次 manifest
2. 创建新时间戳数据目录
3. 不变文件 → `CreateHardLink(源=上一版本文件, 目标=新目录对应路径)`
4. 新增/修改文件 → `CopyFile`
5. 删除文件 → 不在新目录中创建
6. 如果启用压缩 → 再对数据目录执行压缩（打包为 zip）
7. 如果启用加密 → 对文件执行 AES 加密
8. 保存新 manifest

**双向同步 (TwoWaySync)**：
1. 加载上次同步状态（sync_state.json）
2. 扫描源端和目标端文件列表
3. 三方对比：
   - 基准 hash（sync_state 中的）→ 判断哪边变了
   - 仅源端变 → 复制到目标
   - 仅目标端变 → 复制到源
   - 两端都变 + 内容不同 → 加入冲突列表
   - 两端都变 + 内容相同 → 忽略（无需操作）
4. 如果有冲突 → 暂停并通知用户处理
5. 更新 sync_state.json

### 2.3 SyncEngine (`BackupApp.Core/Services/SyncEngine.cs`)
实现 2.2 中双向同步的逻辑，特别是冲突检测算法。

### 2.4 VerificationService (`BackupApp.Core/Services/VerificationService.cs`)
- 重新计算备份后文件的 SHA256
- 与源文件对比
- 返回不一致的文件列表
- 仅当 TaskOptions.VerifyAfterBackup 为 true 时调用

### 2.5 EncryptionService (`BackupApp.Core/Services/EncryptionService.cs`)
- AES-256 加密单个文件
- 解密单个文件
- 密钥派生：PBKDF2(password, salt)

### 2.6 CompressionService (`BackupApp.Core/Services/CompressionService.cs`)
- 将备份数据目录打包为 zip
- 解压 zip 用于还原
- 使用 System.IO.Compression

### 2.7 RestoreEngine (`BackupApp.Core/Services/RestoreEngine.cs`)

```
按时间点浏览：
1. ListManifests(taskId) → 时间线列表
2. 用户选择时间点 → GetManifest(runId) → 该时间点的文件列表

单文件还原：
1. 从 manifest 找到目标文件 Entry
2. 如果是增量备份 → 从 data/{timestamp}/ 目录找到文件（可能是硬链接，但读取透明）
3. 如果启用了加密 → 先解密
4. 如果启用了压缩 → 先解压
5. 复制到用户指定的还原位置

全量还原：
1. 遍历 manifest 中所有文件
2. 逐个还原到指定目录
3. 进度报告
```

### 2.8 VersionManager (`BackupApp.Core/Services/VersionManager.cs`)

分层清理算法：
```
输入：任务配置，所有备份 manifest 列表
输出：应删除的 manifest ID 列表

算法：
1. 按时间戳降序排序
2. 对于每个 RetentionTier（从内到外，即 MaxAgeHours 从小到大）：
   a. 筛选出该 tier 时间范围内的备份版本
   b. 取最新的一份保留
   c. 从最新版本向前，按 KeepIntervalMinutes 间隔跳跃保留
   d. 时间范围内不在保留列表中的 → 标记删除
3. 对于 MaxAgeHours=-1 的 tier（最外层），范围内的直接全部标记删除
4. 返回标记删除的列表

删除操作：
- 对每个标记删除的 manifest：
  - 删除 data/{timestamp}/ 目录
  - 删除 manifests/{timestamp}.json
```

### 2.9 BackupEngine (`BackupApp.Core/Services/BackupEngine.cs`)
编排入口：
```
RunBackup(taskId):
1. 加载任务配置
2. FileScanner.Scan → ScanResult
3. 如果 PreviewBeforeRun → 返回预览数据（暂停等用户确认）
4. 用户确认后：
   a. 根据 BackupMode 调用 BackupExecutor 相应方法
   b. 如果 VerifyAfterBackup → VerificationService.Verify
   c. SaveManifest
   d. VersionManager.Cleanup → 清理过期版本
   e. 返回执行结果
```

### 2.10 SchedulerService (`BackupApp.Core/Services/SchedulerService.cs`)
- 后台线程，每分钟检查一次
- 遍历所有启用调度的任务
- 如果 NextRun <= Now → 执行备份
- 计算并更新 NextRun（基于 CronExpression）
- 可使用 NCronTab 库解析 cron 表达式

---

## Phase 3: 存储后端（可插拔）

### 3.1 IStorageProvider 接口
```
interface IStorageProvider:
  Task<Stream> ReadFileAsync(string path)
  Task WriteFileAsync(string path, Stream data)
  Task DeleteFileAsync(string path)
  Task<List<string>> ListFilesAsync(string directory)
  Task CreateDirectoryAsync(string path)
  Task<bool> FileExistsAsync(string path)
  string Name { get; }
```

### 3.2 LocalProvider
直接操作本地文件系统

### 3.3 NetworkProvider
操作 SMB 网络共享路径（`\\server\share`），底层仍是文件系统 API，增加连接检测和重试逻辑

### 3.4 CloudProvider 接口（预留）
- OneDriveProvider（Microsoft Graph API）
- S3Provider
- WebDAVProvider

---

## Phase 4: WPF 图形界面

### 4.1 主窗口 MainWindow
- 左侧：任务列表（ListBox/DataGrid）
- 右侧：选中任务的详情面板（只读摘要）
- 工具栏按钮：新建任务、编辑任务、删除任务、立即执行、还原向导
- 状态栏：显示上次备份时间和结果

### 4.2 任务编辑器 TaskEditorWindow（多页签 TabControl）
- **Tab1 - 基本信息**：任务名称、启用开关
- **Tab2 - 源目录**：多选目录列表（带添加/删除按钮 + 文件夹浏览对话框）
- **Tab3 - 目标位置**：目标路径文本框 + 浏览按钮 + 类型下拉（本地/网络/云）
- **Tab4 - 备份模式**：RadioButton 选择：单向/增量/双向同步
- **Tab5 - 调度设置**：启用调度开关 + Cron 表达式输入 + "下次执行时间"预览 + 快捷设置按钮（每1小时/每天/每周）
- **Tab6 - 文件过滤**：
  - 包含的文件类型（`*.docx;*.pdf` 文本框）
  - 排除的文件类型（`*.tmp;*.log` 文本框）
  - 排除的目录（`node_modules;.git` 文本框 + 浏览按钮）
  - 最大文件大小（数字输入框 + 单位选择）
- **Tab7 - 版本保留**：RetentionTier 列表编辑器（添加/删除/编辑层），每层设置天数和保留间隔
- **Tab8 - 高级选项**：
  - 加密复选框 + 密码输入框
  - 压缩复选框
  - 备份后验证复选框
  - 执行前预览复选框

### 4.3 还原向导 RestoreWizard（NavigationWindow）
**步骤1 - 选择任务**：下拉选择备份任务
**步骤2 - 选择时间点**：时间线列表（从 manifests 加载），显示时间戳和文件数量
**步骤3 - 浏览文件**：TreeView 显示该时间点的文件树，支持展开目录、多选文件
**步骤4 - 执行还原**：选择还原到原位置或新位置，显示进度条，完成后提示

### 4.4 冲突解决对话框 ConflictDialog
- 显示冲突文件列表
- 每个文件一行：路径 + 源端时间 + 目标端时间
- 用户逐行选择：保留本地 / 保留远程 / 保留两者（重命名）/ 尝试合并

### 4.5 预览对话框 PreviewDialog
- 执行前弹出
- 显示：新增 N 个文件、修改 M 个文件、删除 K 个文件
- 可展开查看具体文件列表
- 按钮：确认执行 / 取消

### 4.6 系统托盘 SystemTray
- 使用 `System.Windows.Forms.NotifyIcon`（WPF 兼容方案）
- 托盘菜单：打开主窗口 / 立即执行所有任务 / 退出
- 双击托盘图标 → 打开主窗口
- 关闭主窗口 → 最小化到托盘（不退出）

### 4.7 通知服务 ToastNotifier
- 使用 Windows 10/11 Toast 通知 API（`Microsoft.Toolkit.Uwp.Notifications`）
- 备份成功：任务名称 + 完成时间 + 文件数量
- 备份失败：任务名称 + 错误信息 + "点击查看详情"
- 冲突待处理：任务名称 + 冲突文件数量 + "点击处理冲突"
- 从通知点击 → 打开主窗口对应对话框

---

## Phase 5: 打包与发布

### 5.1 单文件 exe 发布
```bash
dotnet publish BackupApp.UI -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true
```
预计输出大小 ~50-60MB（含 .NET 运行时）

### 5.2 安装器
- 使用 WiX Toolset 制作 MSI 安装包
- 可选创建桌面快捷方式
- 可选开机自启（注册表 Run 键）

### 5.3 应用图标
- 设计应用图标（.ico 格式，多尺寸）

---

## 验证清单

- [x] 创建任务 → 配置所有参数 → 保存 → 重启应用后配置仍在（Test 1: SQLite CRUD）
- [x] 手动执行单向备份 → 目标目录文件与源一致（Test 2: One-Way Backup）
- [x] 增量备份 3 次 → 3 个时间点目录都存在 → 不变文件是硬链接（Test 5: 变化检测 + 不变文件跟踪）
- [x] 版本清理：模拟 30 天多次备份 → 运行清理 → 按规则保留正确版本（Test 9: Version Manager）
- [x] 还原：选择时间点 → 浏览文件树 → 单文件还原成功 → 全量还原成功（Test 6: Restore）
- [ ] 双向同步：两边独立修改 → 无冲突文件自动同步 → 有冲突弹出对话框（需 GUI）
- [x] 加密备份后文件无法直接读取 → 解密还原后一致（Test 7: Encryption）
- [x] 压缩备份占用空间更小（Test 8: Compression）
- [x] 备份验证：篡改备份文件 → 验证报告不一致（Test 4: Verification）
- [ ] 预览：启用预览 → 执行前显示变更列表 → 可取消（需 GUI）
- [x] 定时任务：设置每天执行 → 到时自动触发 → cron 解析验证（Test 10: Scheduler Cron）
- [x] 文件过滤：排除 *.tmp → 备份中不含 .tmp 文件（Test 3: File Filtering）
- [ ] 备份失败 → 托盘弹出错误通知（需 GUI）
- [ ] 系统托盘最小化/还原/退出行为正常（需 GUI）

**10/14 通过（4 项需 GUI 环境验证）**
