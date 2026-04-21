# GitHubAutoApprove (WinForms + WebView2)

自动轮询 GitHub Actions 中处于 `waiting` 状态的 workflow run，并模拟人工点击 `Approve and run` / `Approve and deploy`。

## 环境要求

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Win11 默认已安装）

## 编译运行

```cmd
cd src\GitHubAutoApprove
dotnet build -c Release
dotnet run -c Release
```

或用 Visual Studio 2022 打开 `GitHubAutoApprove.sln` 直接 F5 运行。

## 使用流程

1. 启动后，上半部的内嵌浏览器会打开 `https://github.com/login`，请像平时一样登录（支持密码、2FA、passkey）。登录后 cookie 会保存在 `%AppData%\GitHubAutoApprove\WebView2`，下次启动免重复登录。
2. 在顶部工具栏填入 `Owner`（GitHub 用户名/组织名）与 `Repo`（仓库名），点击「保存配置」。
3. 点击「▶ 开始轮询」。工具会每隔 N 秒执行：
   - 导航到 `https://github.com/<owner>/<repo>/actions?query=is:waiting`
   - 用 JS 抓取所有等待审批的 run 的 ID
   - 对每个 run 打开详情页，尝试点击 `Approve and run` / `Approve and deploy` / `Review deployments` 按钮
4. 所有动作都会写到下半部的日志面板，并追加到 `%AppData%\GitHubAutoApprove\log-YYYYMMDD.txt`。

## 注意事项

- **必须是仓库管理员 / 有 write 权限的账号**才能点击 Approve，否则按钮根本不会显示。
- 轮询间隔最小 5 秒；建议 30 秒以上，避免对 github.com 造成不必要压力。
- 本工具走的是真实浏览器 + 用户自己的登录态，不需要 PAT，也不会把你的凭证上传到任何第三方。
- 如需完全无人值守，推荐把 Windows 登录设置为自动登录，并用任务计划程序在登录后自动启动本程序。
