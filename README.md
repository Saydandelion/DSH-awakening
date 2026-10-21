# DeepSeek Harness Awakening

[简体中文](#中文) | [English](#english)

A lightweight Windows companion for DeepSeek Harness (DSH). It plays an opening video over the DSH window, keeps the video aligned with the visible window frame, and hands focus back to DSH when both are ready.

> DeepSeek Harness must be installed separately. This project does not bundle or replace DSH. It is an independent companion project and is not an official DeepSeek product.

## 中文

### 项目简介

DeepSeek Harness Awakening 是一个 Windows 桌面启动动画播放器。它会定位或启动 DeepSeek Harness（简称 DSH），在 DSH 主窗口上播放 `DSH_aw.mp4`，并在 DSH 窗口就绪后完成交接。

播放器使用无边框窗口，跟随 DSH 主窗口的可见外框移动和调整大小。画面保持比例并铺满窗口；当前 16:9 视频放入 16:10 的 DSH 窗口时，会从左右两侧等量裁切，不会拉伸画面。

### 功能

- 检测已运行的 DSH，或启动找到的 DSH 程序。
- 按运行进程、`config.json`、Windows 安装记录、常见安装目录和 `PATH` 查找 `DeepSeek Harness.exe`。
- 自动查找失败时显示文件选择窗口；选择后会保存路径，之后启动时继续使用该路径。
- DSH 窗口就绪后，将动画播放器对齐到窗口边界；动画结束后关闭播放器并把焦点交还给 DSH。
- 视频先结束时会停在最后画面继续等待 DSH。等待超时后可继续等待或关闭播放器。
- 不需要安装器或管理员权限；关闭播放器不会结束 DSH。

### 快速开始

1. 安装并确认 DeepSeek Harness 可以正常启动。DSH 本体不包含在本项目中。
2. 将 `DSH_Awakening.exe`、`DSH_aw.mp4`、`dsh.ico` 和 `config.json` 放在同一目录。
3. 从源码仓库首次运行时，先将 `config.example.json` 复制为 `config.json`。
4. 双击 `DSH_Awakening.exe`。如果程序没有找到 DSH，请在文件选择窗口中选择 `DeepSeek Harness.exe`。
5. 程序会播放启动动画，并在 DSH 窗口就绪后交接。

这是一款便携播放器，不会自动创建桌面快捷方式，也不会自动固定到 Windows“开始”菜单。可直接运行 EXE，或自行使用 Windows 创建快捷方式。

### 视频和画面

默认视频为 `DSH_aw.mp4`（1920×1080，约 12 秒）。播放器按比例放大至窗口并居中裁切，以覆盖 DSH 的可见外框。更换视频时，请将新文件放在程序目录，并在 `config.json` 中更新 `Video`；视频编码还需受当前 Windows 媒体组件支持。

### 快捷键

| 按键 | 操作 |
| --- | --- |
| `Esc` | 停止播放并等待 DSH 就绪；就绪后交接 |
| `Space` | 暂停或继续 |
| `M` | 静音切换 |
| `D` | 显示或隐藏中心参考线 |

### 配置

`config.json` 示例：

```json
{
  "DshExe": "",
  "Video": "DSH_aw.mp4",
  "HoldAtSeconds": null,
  "MaxWaitSeconds": 60,
  "Volume": 0.8
}
```

- `DshExe`：DSH 主程序完整路径。留空时自动查找；找不到时可手动选择。
- `Video`：视频文件名或完整路径。相对路径以程序目录为基准。
- `HoldAtSeconds`：可选的停留时间点；`null` 表示在视频结束前约 0.2 秒停留。
- `MaxWaitSeconds`：每轮等待 DSH 的秒数，默认 60 秒。
- `Volume`：播放音量，范围为 `0.0` 到 `1.0`。

### 构建

在 Windows 10/11 x64 上运行 `src\build.cmd`。构建使用系统 .NET Framework 4.x 的 C# 与 WPF 编译工具；运行时不依赖第三方 DLL。

运行日志写入程序目录中的 `app.log`。`config.json`、`app.log` 和快捷方式文件已列入 `.gitignore`；发布源码时请使用 `config.example.json`，不要上传本机运行配置或日志。

### 已知限制

- DSH 必须已安装，且当前 Windows 用户必须有权限读取和启动其安装目录。播放器不能绕过 Windows 文件权限或安全策略。
- DSH 的安装路径、Windows 权限、媒体解码能力和显示缩放比例因电脑而异；自动查找失败时需要手动选择 DSH 主程序。
- 当前项目没有自动安装或卸载流程，也不管理 DSH 的更新。

### 第三方资源与许可证

`dsh.ico` 来自 DeepSeek Harness 官方仓库中的图标资源。上游项目使用 MIT 许可证；版权与许可文本见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。本项目自身尚未声明许可证。

---

## English

### Overview

DeepSeek Harness Awakening is a Windows desktop intro player for DeepSeek Harness (DSH). It locates or starts DSH, plays `DSH_aw.mp4` over the DSH main window, and hands focus back once the DSH window is ready.

The player uses a borderless window that follows the visible DSH frame as it moves or resizes. Video keeps its aspect ratio and fills the frame. With the current 16:9 video on a 16:10 DSH window, the player crops both sides evenly instead of stretching the image.

### Features

- Finds an already-running DSH instance or starts the located DSH executable.
- Searches the running process, `config.json`, Windows install records, common install folders, and `PATH` for `DeepSeek Harness.exe`.
- If automatic discovery fails, opens a file picker. The selected path is saved for later launches.
- Aligns the player to the visible DSH window and returns focus to DSH when the intro ends and DSH is ready.
- Holds the final frame while waiting if the video ends first. After a timeout, the user can keep waiting or close the player.
- Requires no installer or administrator privileges. Closing the player does not terminate DSH.

### Quick start

1. Install DeepSeek Harness and verify that it launches normally. DSH itself is not included in this project.
2. Keep `DSH_Awakening.exe`, `DSH_aw.mp4`, `dsh.ico`, and `config.json` in the same directory.
3. For a fresh source checkout, copy `config.example.json` to `config.json` before the first run.
4. Run `DSH_Awakening.exe`. If DSH is not found, select `DeepSeek Harness.exe` in the file picker.
5. The intro plays and the player hands off to DSH when its window is ready.

This is a portable player. It does not create a desktop shortcut or automatically pin itself to the Windows Start menu. Run the EXE directly or create a shortcut with Windows.

### Video and framing

The default video is `DSH_aw.mp4` (1920×1080, approximately 12 seconds). The player scales it proportionally to fill the visible DSH frame and crops from the center. To use another video, place it next to the EXE and update `Video` in `config.json`. The video codec must be supported by the Windows media components on the target PC.

### Keyboard shortcuts

| Key | Action |
| --- | --- |
| `Esc` | Stop playback and wait for DSH; hand off when ready |
| `Space` | Pause or resume |
| `M` | Toggle mute |
| `D` | Show or hide the center guide |

### Configuration

Example `config.json`:

```json
{
  "DshExe": "",
  "Video": "DSH_aw.mp4",
  "HoldAtSeconds": null,
  "MaxWaitSeconds": 60,
  "Volume": 0.8
}
```

- `DshExe`: Full path to the DSH executable. Leave empty for automatic discovery and the file-picker fallback.
- `Video`: Video filename or absolute path. Relative paths are resolved from the application directory.
- `HoldAtSeconds`: Optional hold point; `null` holds about 0.2 seconds before the end of the video.
- `MaxWaitSeconds`: Timeout for each DSH wait period; defaults to 60 seconds.
- `Volume`: Playback volume from `0.0` to `1.0`.

### Build

Run `src\build.cmd` on Windows 10/11 x64. The build uses the C# and WPF tools included with .NET Framework 4.x and has no third-party DLL runtime dependency.

Runtime logs are written to `app.log` beside the application. `config.json`, `app.log`, and shortcut files are ignored by Git. Use `config.example.json` in the public repository; do not publish machine-specific configuration or logs.

### Limitations

- DeepSeek Harness must be installed, and the current Windows user needs permission to read and launch its installation directory. The player does not bypass Windows permissions or security policies.
- DSH install paths, Windows permissions, media decoding support, and display scaling vary between PCs. Manual selection is required when automatic discovery cannot find DSH.
- This project has no install or uninstall workflow and does not manage DSH updates.

### Third-party asset and license

`dsh.ico` comes from the icon resources in the official DeepSeek Harness repository. The upstream project is licensed under MIT; see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) for the copyright and license text. This companion project has not declared its own license.
