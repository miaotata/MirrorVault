# CLAUDE.md — Backup App Project

## 环境隔离规则

1. **Conda 环境** — 所有依赖通过 conda 安装在该项目的专用 conda 环境中，与外界环境隔离
2. **conda 优先** — 安装新软件优先用 conda；conda 没有的软件手动下载：
   - 安装包（压缩文件）下载到 `/home/zhaoyao/App_design/src/`
   - 安装后的软件放到 `/home/zhaoyao/App_design/APP_data/`
3. **文件操作限制** — 所有文件操作必须在 `/home/zhaoyao/App_design/` 下进行，不得越界操作其他系统目录
4. **禁止 sudo** — 不允许独自执行 `sudo` 或任何需要 root 权限的操作，如需管理员权限必须先征求用户同意

## 项目规范

- 按 `backup_app_plan.md` 中的阶段顺序执行实现
- 每完成一个 Phase 或关键步骤后更新进度

## 技术栈

- **框架**：C# / .NET 8 + Avalonia UI（跨平台，Linux 开发，发布 Windows exe）
- **Conda 环境**：`backup_app`（含 dotnet-sdk 8.0.408）
- **数据库**：SQLite（`%APPDATA%/BackupApp/config.db`）
- **清单格式**：JSON

## 当前进度

| Phase | 内容 | 状态 |
|-------|------|:----:|
| Phase 1 | 项目骨架 + 数据模型 + SQLite 存储 | ✅ |
| Phase 2 | 核心引擎（扫描/备份/还原/版本清理/调度） | ✅ |
| Phase 3 | 存储后端（本地/网络/云可插拔接口） | ✅ |
| Phase 4 | Avalonia UI（主窗口/任务编辑器/还原向导/冲突弹窗/预览弹窗） | ✅ |
| Phase 5 | 打包 exe + 安装器 | ⬜ |

## 更新日志

每次用户提出的修复或更新需求：
1. **先写入 `updata.md`** — 在实施前将变更计划记录到 `/home/zhaoyao/App_design/updata.md`
2. **提优化建议** — 可附带相关的优化建议或新颖想法供用户参考
3. **询问确认** — 更新完 md 文件后，询问用户是否按此方案执行
4. **实施** — 用户确认后再开始修改代码
5. **同步 README.md** — 新功能上线或功能变更后，同步更新 `/home/zhaoyao/App_design/README.md` 使用说明

## 项目结构

```
BackupApp/
├── BackupApp.Core/           # 业务逻辑库
│   ├── Models/               # 10 个数据模型
│   ├── Services/             # 11 个服务（含调度、加解密、压缩、硬链接）
│   ├── Storage/              # ConfigStore(SQLite) + ManifestStore(JSON) + BackupLayout
│   └── Providers/            # IStorageProvider + Local + Network
├── BackupApp.UI/             # Avalonia 桌面应用
│   ├── MainWindow            # 主面板：任务列表
│   ├── TaskEditorWindow      # 8 页签任务配置
│   ├── RestoreWizard         # 4 步还原向导
│   ├── ConflictDialog        # 冲突解决弹窗
│   ├── PreviewDialog         # 变更预览弹窗
│   └── UINotification        # 通知服务适配器
└── backup_app_plan.md        # 详细实现计划
```
