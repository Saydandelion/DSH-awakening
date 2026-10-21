// 阶段 B — DSH 目标定位、唤回与启动
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DshAwakening
{
    internal static class DshTarget
    {
        public const string ProcessImage = "DeepSeek Harness.exe";
        private const string MainWindowClass = "Chrome_WidgetWin_1";

        /// <summary>按「进程映像 + 类名 + 有标题」定位主窗口，可见/未最小化者优先。</summary>
        public static IntPtr Find(string expectedExePath)
        {
            IntPtr best = IntPtr.Zero;
            int bestScore = -1;

            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                string path = ImagePath(h);
                if (!MatchesImagePath(expectedExePath, path)) return true;

                StringBuilder cls = new StringBuilder(256);
                Native.GetClassName(h, cls, 256);
                if (!string.Equals(cls.ToString(), MainWindowClass, StringComparison.Ordinal)) return true;

                StringBuilder title = new StringBuilder(512);
                Native.GetWindowText(h, title, 512);
                if (title.Length == 0) return true;

                Native.RECT r;
                bool hasRect = Native.GetWindowRect(h, out r) && r.Width >= 400 && r.Height >= 300;

                int score = 0;
                if (Native.IsWindowVisible(h)) score += 100;
                if (!Native.IsIconic(h)) score += 10;
                if (hasRect) score += 5;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = h;
                }
                return true;
            }, IntPtr.Zero);

            return best;
        }

        /// <summary>配置了入口时精确比对完整路径；未配置时只接受已知的 DSH 映像名。</summary>
        internal static bool MatchesImagePath(string expectedExePath, string actualImagePath)
        {
            if (string.IsNullOrWhiteSpace(actualImagePath)) return false;
            if (!string.Equals(Path.GetFileName(actualImagePath), ProcessImage,
                StringComparison.OrdinalIgnoreCase)) return false;

            if (string.IsNullOrWhiteSpace(expectedExePath)) return true;

            try
            {
                string expected = Path.GetFullPath(expectedExePath.Trim());
                string actual = Path.GetFullPath(actualImagePath.Trim());
                return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ImagePath(IntPtr hwnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return string.Empty;
            try
            {
                StringBuilder buf = new StringBuilder(1024);
                uint size = 1024;
                if (!Native.QueryFullProcessImageName(h, 0, buf, ref size)) return string.Empty;
                return buf.ToString();
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }

        /// <summary>优先 DWM 可见外框，失败退回 GetWindowRect 并说明差异。</summary>
        public static bool TryGetFrame(IntPtr hwnd, out Native.RECT frame, out string source)
        {
            frame = new Native.RECT();
            source = "none";
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return false;

            Native.RECT dwm;
            int hr = Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS,
                out dwm, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.RECT)));
            if (hr == 0 && dwm.IsValid)
            {
                frame = dwm;
                source = "DWMWA_EXTENDED_FRAME_BOUNDS";
                return true;
            }

            Native.RECT gwr;
            if (Native.GetWindowRect(hwnd, out gwr) && gwr.IsValid)
            {
                frame = gwr;
                source = "GetWindowRect（DWM 失败 hr=" + hr + "）";
                return true;
            }
            return false;
        }

        /// <summary>已有实例唤回：最小化先还原、隐藏先显示，再置前。</summary>
        public static void Recall(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return;
            if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
            else if (!Native.IsWindowVisible(hwnd)) Native.ShowWindow(hwnd, Native.SW_SHOW);
            Native.SetForegroundWindow(hwnd);
        }

        /// <summary>窗口是否还能响应消息（不发输入，只探活）。</summary>
        public static bool IsResponsive(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return false;
            IntPtr result;
            IntPtr ok = Native.SendMessageTimeout(hwnd, Native.WM_NULL, IntPtr.Zero, IntPtr.Zero,
                Native.SMTO_ABORTIFHUNG, 300, out result);
            return ok != IntPtr.Zero;
        }

        /// <summary>按桌面入口启动（等同双击），不在参数里塞任何东西。</summary>
        public static bool Launch(string exePath, out string error)
        {
            error = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exePath;
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exePath);
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                error = "启动 DSH 失败：" + ex.Message;
                return false;
            }
        }

        /// <summary>把焦点交给 DSH（交接用，不持续抢占）。</summary>
        public static void Activate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return;
            Native.SetForegroundWindow(hwnd);
        }
    }
}
