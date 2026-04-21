# autoagents4

一个完整的演示工程：用 **WinForms + WebView2** 自动点击 GitHub Actions 中
"Approve and run" 按钮，让 GitHub Copilot 编码代理（coding agent）
开 PR → 测试 → 审批 → 合并 → 关闭 Issue → 触发下一个子任务的流水线
**完全无人值守**。

## 目录结构

```
.
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   └── copilot-subtask.yml         Copilot 子任务 Issue 模板
│   └── workflows/
│       ├── copilot-task-demo.yml       演示 workflow（故意卡在 environment 审批）
│       └── auto-merge-and-chain.yml    自动合并 + 关闭 Issue + 触发下一个任务
├── docs/
│   └── 操作说明.md                      👈 零基础全流程中文文档（先看这个）
├── src/
│   └── GitHubAutoApprove/              WinForms + WebView2 自动审批工具
│       ├── GitHubAutoApprove.sln
│       ├── GitHubAutoApprove.csproj
│       ├── Program.cs
│       ├── MainForm.cs
│       ├── MainForm.Designer.cs
│       ├── AppSettings.cs
│       └── README.md
└── README.md
```

## 快速开始

1. 阅读 [`docs/操作说明.md`](docs/操作说明.md)（**强烈建议先通读**）。
2. 在 GitHub 上把本仓库 Fork 或把相关文件复制到你自己的仓库。
3. 按文档 **第 3 节** 配置仓库权限和 `production-approval` environment。
4. 按文档 **第 5 节** 编译并运行 `src/GitHubAutoApprove`（需要 .NET 8 + WebView2 Runtime）。
5. 按文档 **第 6 节** 创建第一个演示 Issue，观察端到端自动化。

## 核心思路

- **问题**：Copilot 编码代理开的 PR 或带审批 environment 的 workflow 会停在
  "Approve and run" / "Review deployments"，必须人工点按钮。
- **方案**：WinForm 内嵌 WebView2 登录真实 github.com，程序定时轮询仓库
  `actions?query=is:waiting` 列表，解析出每个待审批的 run，导航到详情页后用
  JavaScript 找到按钮并 `.click()`。
- **效果**：只要 WinForm 在一台有登录态的机器上常驻，所有审批卡点都会在
  30 秒内自动放行。

详细机制、权限配置、排错指南都在 [`docs/操作说明.md`](docs/操作说明.md)。
