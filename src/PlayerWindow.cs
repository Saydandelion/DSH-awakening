// 阶段 B — 播放器与状态机
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DshAwakening
{
    internal sealed class PlayerWindow : Window
    {
        private readonly Config _cfg;
        private readonly string _videoPath;
        private readonly string _dshExePath;

        private const double MediaOpenTimeoutSeconds = 15.0;

        private MediaElement _media;
        private Grid _root;
        private TextBlock _hint;
        private TextBlock _status;
        private Border _guide;

        private DispatcherTimer _timer;
        private readonly Stopwatch _clock = new Stopwatch();

        private IntPtr _dsh;
        private bool _dshReady;
        private bool _holding;
        private bool _ended;
        private bool _closing;
        private bool _muted;
        private bool _mediaOpened;
        private bool _skipAfterDshReady;
        private bool _timeoutPromptOpen;
        private double _nextDshTimeoutAt;
        private double _mediaOpenDeadline;

        private Native.RECT _lastFrame;
        private Native.RECT _probeFrame;
        private int _stableTicks;
        private double _duration;
        private double _holdAt = -1;
        private string _layoutKey = "";

        public PlayerWindow(Config cfg, string videoPath, string baseDir, IntPtr dsh)
        {
            _cfg = cfg;
            _videoPath = videoPath;
            _dshExePath = cfg.ResolveDshExe(baseDir);
            _dsh = dsh;

            string iconPath = Path.Combine(baseDir, "dsh.ico");
            if (File.Exists(iconPath)) Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = false;
            Background = Brushes.Black;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowState = WindowState.Normal;
            Width = 640;
            Height = 400;
            Left = 0;
            Top = 0;
            Title = "DSH Awakening";

            _media = new MediaElement();
            _media.LoadedBehavior = MediaState.Manual;
            _media.UnloadedBehavior = MediaState.Manual;
            // 等比 + 精确尺寸 + 父容器裁切，保证左右裁切量严格相等
            _media.Stretch = Stretch.Uniform;
            _media.HorizontalAlignment = HorizontalAlignment.Center;
            _media.VerticalAlignment = VerticalAlignment.Center;
            _media.Volume = _cfg.Volume;
            _media.MediaEnded += OnMediaEnded;
            _media.MediaFailed += OnMediaFailed;
            _media.MediaOpened += OnMediaOpened;

            _hint = new TextBlock();
            _hint.Text = "Esc 退出 / 空格暂停 / M 静音 / D 中心线";
            _hint.Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
            _hint.FontSize = 13;
            _hint.Padding = new Thickness(10, 7, 10, 7);
            _hint.Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
            _hint.HorizontalAlignment = HorizontalAlignment.Left;
            _hint.VerticalAlignment = VerticalAlignment.Top;
            _hint.Margin = new Thickness(16, 16, 0, 0);
            _hint.IsHitTestVisible = false;

            _status = new TextBlock();
            _status.Text = "正在启动 DeepSeek Harness…";
            _status.Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
            _status.FontSize = 14;
            _status.Padding = new Thickness(14, 8, 14, 8);
            _status.Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
            _status.HorizontalAlignment = HorizontalAlignment.Center;
            _status.VerticalAlignment = VerticalAlignment.Bottom;
            _status.Margin = new Thickness(0, 0, 0, 28);
            _status.IsHitTestVisible = false;

            _guide = new Border();
            _guide.BorderBrush = new SolidColorBrush(Color.FromArgb(220, 255, 64, 64));
            _guide.BorderThickness = new Thickness(1, 0, 0, 0);
            _guide.HorizontalAlignment = HorizontalAlignment.Center;
            _guide.VerticalAlignment = VerticalAlignment.Stretch;
            _guide.IsHitTestVisible = false;
            _guide.Visibility = Visibility.Collapsed;

            _root = new Grid();
            _root.ClipToBounds = true;
            _root.Children.Add(_media);
            _root.Children.Add(_guide);
            _root.Children.Add(_status);
            _root.Children.Add(_hint);
            Content = _root;
            _root.SizeChanged += delegate { LayoutVideo(); };

            // 先建句柄、先摆位，再显示，避免预热时先闪一下默认位置
            IntPtr self = new WindowInteropHelper(this).EnsureHandle();
            Native.RECT initial;
            string src;
            if (_dsh != IntPtr.Zero && DshTarget.TryGetFrame(_dsh, out initial, out src))
            {
                PositionFor(initial);
            }
            else
            {
                PositionFor(CenteredTempRect(self));
            }

            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
            Closed += OnClosed;
        }

        // ───────────────────────── 启动 ─────────────────────────

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Log.Write("窗口已显示");
            _clock.Start();
            _nextDshTimeoutAt = _cfg.MaxWaitSeconds;

            if (_dsh != IntPtr.Zero)
            {
                _status.Text = "正在唤醒 DeepSeek Harness…";
                Log.Write(string.Format("检测到已运行的 DSH：hwnd=0x{0:X}，走唤回", _dsh.ToInt64()));
                DshTarget.Recall(_dsh);
            }
            else
            {
                _status.Text = "正在启动 DeepSeek Harness…";
                string exe = _dshExePath;
                if (exe.Length == 0)
                {
                    Fail("配置里的 DshExe 为空，且当前没有检测到正在运行的 DSH。\n请在 config.json 中填入 DSH 桌面入口的完整路径。");
                    return;
                }
                if (!File.Exists(exe))
                {
                    Fail("配置的 DSH 入口不存在：\n" + exe);
                    return;
                }

                string err;
                Log.Write("启动 DSH：" + exe);
                if (!DshTarget.Launch(exe, out err))
                {
                    Fail(err);
                    return;
                }
            }

            if (!File.Exists(_videoPath))
            {
                Fail("视频文件不存在：\n" + _videoPath);
                return;
            }

            Log.Write("加载视频：" + _videoPath + "（音量 " + _cfg.Volume.ToString("0.00") + "）");
            try
            {
                _mediaOpenDeadline = _clock.Elapsed.TotalSeconds + MediaOpenTimeoutSeconds;
                _media.Source = new Uri(_videoPath, UriKind.Absolute);
                _media.Play();
            }
            catch (Exception ex)
            {
                Fail("视频无法打开：\n" + ex.Message + "\n\n" + _videoPath);
                return;
            }

            TakeFocus();
            Fade(_hint, 3.0);

            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Interval = TimeSpan.FromMilliseconds(60);
            _timer.Tick += OnTick;
            _timer.Start();
        }

        // ───────────────────────── 主循环 ─────────────────────────

        private void OnTick(object sender, EventArgs e)
        {
            if (_closing || _timeoutPromptOpen) return;

            if (!_mediaOpened && _mediaOpenDeadline > 0 &&
                _clock.Elapsed.TotalSeconds >= _mediaOpenDeadline)
            {
                Fail("视频加载超过 15 秒仍未打开：\n" + _videoPath);
                return;
            }

            if (!_dshReady)
            {
                TrackDsh();
                if (_dshReady)
                {
                    OnDshReady();
                    if (_closing) return;
                }
                else if (_clock.Elapsed.TotalSeconds >= _nextDshTimeoutAt)
                {
                    OnTimeout();
                    return;
                }
            }
            else
            {
                FollowDsh();
            }

            UpdateHold();
        }

        /// <summary>等待目标窗口：出现 → 可见且可响应 → 边界短时稳定，才算就绪。</summary>
        private void TrackDsh()
        {
            if (_dsh == IntPtr.Zero)
            {
                IntPtr found = DshTarget.Find(_dshExePath);
                if (found != IntPtr.Zero)
                {
                    _dsh = found;
                    Log.Write(string.Format("发现 DSH 主窗口 hwnd=0x{0:X}（已等待 {1:0.0}s）",
                        _dsh.ToInt64(), _clock.Elapsed.TotalSeconds));
                    DshTarget.Recall(_dsh);
                }
                return;
            }

            if (!Native.IsWindow(_dsh))
            {
                Log.Write("DSH 窗口句柄失效，重新定位");
                _dsh = IntPtr.Zero;
                _stableTicks = 0;
                return;
            }

            if (!Native.IsWindowVisible(_dsh) || Native.IsIconic(_dsh))
            {
                DshTarget.Recall(_dsh);
                _stableTicks = 0;
                return;
            }

            Native.RECT f;
            string src;
            if (!DshTarget.TryGetFrame(_dsh, out f, out src) || !f.IsValid)
            {
                _stableTicks = 0;
                return;
            }
            if (!DshTarget.IsResponsive(_dsh))
            {
                _stableTicks = 0;
                return;
            }

            if (SameRect(f, _probeFrame))
            {
                _stableTicks++;
            }
            else
            {
                _probeFrame = f;
                _stableTicks = 0;
            }

            if (_stableTicks >= 4)   // 约 240ms 边界不动
            {
                _dshReady = true;
                _lastFrame = f;
                AlignTo(f, src, true);
                Log.Write(string.Format("目标窗口稳定（{0}），开始对齐；总等待 {1:0.00}s", f.Text, _clock.Elapsed.TotalSeconds));
            }
        }

        private void OnDshReady()
        {
            _status.Text = "已就绪";
            Fade(_status, 0.8);
            TakeFocus();

            if (_skipAfterDshReady)
            {
                Handoff("用户按 Esc 后 DSH 已就绪");
                return;
            }

            // 视频比窗口先播完的情况：此刻才具备交接条件
            if (_ended)
            {
                Handoff("视频已播完，DSH 已就绪");
            }
        }

        /// <summary>跟随：窗口移动/缩放/最小化都要跟。</summary>
        private void FollowDsh()
        {
            if (!Native.IsWindow(_dsh))
            {
                Handoff("DSH 窗口已关闭");
                return;
            }

            if (Native.IsIconic(_dsh))
            {
                if (WindowState != WindowState.Minimized) WindowState = WindowState.Minimized;
                return;
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                AlignTo(_probeFrame, "恢复", true);
                return;
            }

            Native.RECT f;
            string src;
            if (DshTarget.TryGetFrame(_dsh, out f, out src) && f.IsValid)
            {
                AlignTo(f, src, false);
            }
        }

        /// <summary>到达等待节点仍未就绪就停住；就绪后补播剩余片段。</summary>
        private void UpdateHold()
        {
            if (_ended) return;

            if (!_dshReady && !_holding && _holdAt >= 0 && _media.Position.TotalSeconds >= _holdAt)
            {
                _media.Pause();
                _holding = true;
                Log.Write(string.Format("到达等待节点 {0:0.000}s，暂停并保持画面（已等待 {1:0.0}s）",
                    _holdAt, _clock.Elapsed.TotalSeconds));
            }

            if (_holding && _dshReady)
            {
                _media.Play();
                _holding = false;
                Log.Write("DSH 就绪，补播剩余片段");
            }
        }

        // ───────────────────────── 定位与画幅 ─────────────────────────

        private Native.RECT CenteredTempRect(IntPtr self)
        {
            Native.MONITORINFO mi = new Native.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
            IntPtr mon = Native.MonitorFromWindow(self, Native.MONITOR_DEFAULTTONEAREST);
            Native.GetMonitorInfo(mon, ref mi);

            int workW = mi.rcWork.Width;
            int workH = mi.rcWork.Height;
            int h = (int)(workH * 0.55);
            if (h > 640) h = 640;
            if (h < 360) h = 360;
            int w = (int)(h * 1.6);
            if (w > workW - 80)
            {
                w = workW - 80;
                h = (int)(w / 1.6);
            }

            Native.RECT r = new Native.RECT();
            r.Left = mi.rcWork.Left + (workW - w) / 2;
            r.Top = mi.rcWork.Top + (workH - h) / 2;
            r.Right = r.Left + w;
            r.Bottom = r.Top + h;
            Log.Write("临时居中播放器：" + r.Text);
            return r;
        }

        /// <summary>显示前先按 DIP 摆好，避免 Show 时闪一下默认位置。</summary>
        private void PositionFor(Native.RECT px)
        {
            IntPtr self = new WindowInteropHelper(this).EnsureHandle();
            uint dpi = Native.GetDpiForWindow(self);
            if (dpi == 0) dpi = 96;
            double k = 96.0 / dpi;
            Left = px.Left * k;
            Top = px.Top * k;
            Width = px.Width * k;
            Height = px.Height * k;
        }

        private void AlignTo(Native.RECT f, string src, bool force)
        {
            if (!f.IsValid) return;
            if (!force && SameRect(f, _lastFrame)) return;

            IntPtr self = new WindowInteropHelper(this).Handle;
            Native.SetWindowPos(self, Native.HWND_TOPMOST, f.Left, f.Top, f.Width, f.Height,
                Native.SWP_NOACTIVATE);

            _lastFrame = f;
            if (force)
            {
                Log.Write("对齐播放器 -> " + f.Text + "（" + src + "）");
            }
            else
            {
                Log.Write("跟随调整 -> " + f.Text);
            }
            LayoutVideo();
        }

        private static bool SameRect(Native.RECT a, Native.RECT b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        /// <summary>等比放大到铺满容器，超出部分由父容器对称裁切。</summary>
        private void LayoutVideo()
        {
            double vw = _media.NaturalVideoWidth;
            double vh = _media.NaturalVideoHeight;
            if (vw <= 0 || vh <= 0) return;

            double cw = _root.ActualWidth;
            double ch = _root.ActualHeight;
            if (cw <= 0 || ch <= 0) return;

            double scale = Math.Max(cw / vw, ch / vh);
            double w = vw * scale;
            double h = vh * scale;
            _media.Width = w;
            _media.Height = h;

            double cropX = (w - cw) / 2.0 / scale;
            double cropY = (h - ch) / 2.0 / scale;

            string key = string.Format("{0:0.#}x{1:0.#}", cw, ch);
            if (key == _layoutKey) return;
            _layoutKey = key;

            Log.Write(string.Format(
                "布局：容器 {0:0}x{1:0}，视频 {2:0}x{3:0}，缩放 {4:0.0000}，绘制 {5:0.0}x{6:0.0}；" +
                "左右各裁 {7:0.0}px、上下各裁 {8:0.0}px（源像素，左右严格相等）",
                cw, ch, vw, vh, scale, w, h, cropX, cropY));
        }

        private void TakeFocus()
        {
            IntPtr self = new WindowInteropHelper(this).Handle;
            IntPtr fg = Native.GetForegroundWindow();
            uint pid;
            uint fgThread = Native.GetWindowThreadProcessId(fg, out pid);
            uint myThread = Native.GetCurrentThreadId();
            bool attached = false;
            try
            {
                if (fgThread != 0 && fgThread != myThread)
                    attached = Native.AttachThreadInput(myThread, fgThread, true);
                Native.BringWindowToTop(self);
                Native.SetForegroundWindow(self);
                Native.SetFocus(self);
                Activate();
                Focus();
            }
            catch { }
            finally
            {
                if (attached) Native.AttachThreadInput(myThread, fgThread, false);
            }
        }

        // ───────────────────────── 媒体事件 ─────────────────────────

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            _mediaOpened = true;
            if (_skipAfterDshReady)
            {
                try { _media.Stop(); }
                catch (Exception ex) { Log.Write("Esc 后媒体打开时停止失败：" + ex.Message); }
                return;
            }

            _duration = _media.NaturalDuration.HasTimeSpan ? _media.NaturalDuration.TimeSpan.TotalSeconds : 0;
            Log.Write(string.Format("视频已打开：{0}x{1}，实际时长 {2:0.000}s",
                _media.NaturalVideoWidth, _media.NaturalVideoHeight, _duration));

            string note;
            _holdAt = _cfg.ResolveHoldAt(_duration, out note);
            Log.Write(string.Format("等待节点：{0:0.000}s（{1}）", _holdAt, note));

            LayoutVideo();
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            _ended = true;
            if (_dshReady)
            {
                Handoff("视频播放结束");
            }
            else
            {
                Log.Write("视频已播完，保持最后一帧继续等待 DSH");
            }
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            string msg = e.ErrorException == null ? "未知错误" : e.ErrorException.Message;
            Fail("视频无法播放：\n" + msg + "\n\n" + _videoPath);
        }

        // ───────────────────────── 结束 ─────────────────────────

        private void OnTimeout()
        {
            Log.Write(string.Format("等待 DSH 超时（{0}s）", _cfg.MaxWaitSeconds));
            _timeoutPromptOpen = true;
            bool continueWaiting = AskContinueWaiting();
            _timeoutPromptOpen = false;

            if (_closing) return;
            if (continueWaiting)
            {
                _nextDshTimeoutAt = _clock.Elapsed.TotalSeconds + _cfg.MaxWaitSeconds;
                Log.Write(string.Format("用户选择继续等待；下次提示将在 {0}s 后", _cfg.MaxWaitSeconds));
                return;
            }

            Log.Write("用户选择关闭播放器；DSH 进程不会被终止");
            _closing = true;
            Close();
        }

        private bool AskContinueWaiting()
        {
            Window dialog = new Window();
            dialog.Title = "DeepSeek Harness 尚未就绪";
            dialog.Width = 470;
            dialog.Height = 190;
            dialog.ResizeMode = ResizeMode.NoResize;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Owner = this;
            dialog.ShowInTaskbar = false;
            dialog.Topmost = true;
            dialog.Background = Brushes.White;

            Grid grid = new Grid();
            grid.Margin = new Thickness(18);
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock message = new TextBlock();
            message.Text = string.Format(
                "等待 {0} 秒后仍未检测到已就绪的 DSH 主窗口。\n可继续等待一个周期，或关闭播放器；DSH 不会因此被结束。",
                _cfg.MaxWaitSeconds);
            message.TextWrapping = TextWrapping.Wrap;
            message.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(message);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 14, 0, 0);
            Grid.SetRow(buttons, 1);

            Button wait = new Button();
            wait.Content = "继续等待";
            wait.MinWidth = 100;
            wait.Margin = new Thickness(0, 0, 8, 0);
            wait.IsDefault = true;
            wait.Click += delegate { dialog.DialogResult = true; };

            Button exit = new Button();
            exit.Content = "关闭播放器";
            exit.MinWidth = 100;
            exit.IsCancel = true;

            buttons.Children.Add(wait);
            buttons.Children.Add(exit);
            grid.Children.Add(buttons);
            dialog.Content = grid;

            return dialog.ShowDialog() == true;
        }

        private void Fail(string message)
        {
            Log.Write("错误：" + message.Replace("\n", " "));
            _closing = true;
            MessageBox.Show(message, "DSH Awakening", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }

        /// <summary>交接：再核对一次边界 → 关播放器 → 把焦点交回真实 DSH。</summary>
        private void Handoff(string reason)
        {
            if (_closing) return;
            _closing = true;
            Log.Write("交接：" + reason);

            if (_dsh != IntPtr.Zero && Native.IsWindow(_dsh))
            {
                Native.RECT f;
                string src;
                if (DshTarget.TryGetFrame(_dsh, out f, out src) && f.IsValid && !SameRect(f, _lastFrame))
                {
                    IntPtr self = new WindowInteropHelper(this).Handle;
                    Native.SetWindowPos(self, Native.HWND_TOPMOST, f.Left, f.Top, f.Width, f.Height,
                        Native.SWP_NOACTIVATE);
                    Log.Write("交接前边界有变化，已重新核对 -> " + f.Text);
                }
                else
                {
                    Log.Write("交接前边界核对一致");
                }
            }

            Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (_timer != null) _timer.Stop();
            _clock.Stop();

            try
            {
                _media.Stop();
                _media.Close();
            }
            catch { }

            if (_dsh != IntPtr.Zero && Native.IsWindow(_dsh))
            {
                DshTarget.Activate(_dsh);
                Log.Write("已激活 DSH，交接完成");
            }
            Log.Write("播放器已关闭，总耗时 " + _clock.Elapsed.TotalSeconds.ToString("0.00") + "s");
        }

        // ───────────────────────── 交互 ─────────────────────────

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_dshReady)
                {
                    Handoff("用户按 Esc 退出");
                }
                else
                {
                    _skipAfterDshReady = true;
                    try
                    {
                        if (_media.Source != null) _media.Stop();
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Esc 时停止媒体失败：" + ex.Message);
                    }
                    _status.BeginAnimation(OpacityProperty, null);
                    _status.Text = "播放已停止，等待 DSH 就绪…";
                    _status.Opacity = 1.0;
                    _status.Visibility = Visibility.Visible;
                    Log.Write("用户按 Esc；停止播放并继续等待 DSH，准备就绪后交接");
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Space)
            {
                if (_skipAfterDshReady)
                {
                    Log.Write("DSH 就绪前已请求退出，忽略空格播放");
                }
                else if (_media.CanPause)
                {
                    _media.Pause();
                    Log.Write("暂停");
                }
                else
                {
                    _media.Play();
                    Log.Write("继续");
                }
                e.Handled = true;
            }
            else if (e.Key == Key.M)
            {
                _muted = !_muted;
                _media.IsMuted = _muted;
                Log.Write(_muted ? "静音" : "取消静音");
                e.Handled = true;
            }
            else if (e.Key == Key.D)
            {
                _guide.Visibility = _guide.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
                Log.Write(_guide.Visibility == Visibility.Visible ? "显示中心线" : "隐藏中心线");
                e.Handled = true;
            }
        }

        private static void Fade(UIElement el, double delaySeconds)
        {
            DoubleAnimation fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(600));
            fade.BeginTime = TimeSpan.FromSeconds(delaySeconds);
            fade.Completed += delegate { el.Visibility = Visibility.Collapsed; };
            el.BeginAnimation(OpacityProperty, fade);
        }
    }
}
