// 阶段 B — 入口
using System;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;

namespace DshAwakening
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Log.Init(baseDir);

            string cfgPath = args.Length > 0 ? args[0] : "config.json";
            if (!Path.IsPathRooted(cfgPath)) cfgPath = Path.Combine(baseDir, cfgPath);

            Log.Write("配置：" + cfgPath);

            string err;
            Config cfg = Config.Load(cfgPath, out err);
            if (cfg == null)
            {
                Log.Write("配置错误：" + err.Replace("\n", " "));
                MessageBox.Show(err, "DSH Awakening — 配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string video = cfg.ResolveVideo(baseDir);
            Log.Write("视频：" + video);

            string configuredExe = cfg.ResolveDshExe(baseDir);
            string resolutionSource;
            string exe = DshExeResolver.Resolve(configuredExe, out resolutionSource);
            if (exe.Length == 0)
            {
                Log.Write("自动探测未找到 DSH，等待用户选择安装文件");
                OpenFileDialog picker = new OpenFileDialog();
                picker.Title = "找不到 DeepSeek Harness，请选择 DeepSeek Harness.exe";
                picker.Filter = "DeepSeek Harness 主程序 (DeepSeek Harness.exe)|DeepSeek Harness.exe";
                picker.CheckFileExists = true;
                picker.Multiselect = false;
                if (picker.ShowDialog() != true)
                {
                    Log.Write("用户取消选择 DSH；启动取消");
                    MessageBox.Show("未选择 DeepSeek Harness，启动已取消。下次运行仍可重新选择。",
                        "DSH Awakening", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (!DshExeResolver.IsValidCandidate(picker.FileName))
                {
                    Log.Write("用户所选文件不是 DeepSeek Harness.exe：" + picker.FileName);
                    MessageBox.Show("请选择 DeepSeek Harness.exe 主程序。", "DSH Awakening",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                exe = Path.GetFullPath(picker.FileName);
                resolutionSource = "用户选择";
            }

            Log.Write("DSH 路径（" + resolutionSource + "）：" + exe);
            if (!string.Equals(cfg.DshExe, exe, StringComparison.OrdinalIgnoreCase))
            {
                cfg.DshExe = exe;
                string saveError;
                if (!cfg.Save(cfgPath, out saveError)) Log.Write(saveError + "；本次仍使用所选路径");
                else Log.Write("已保存 DSH 路径到 config.json");
            }
            IntPtr dsh = DshTarget.Find(exe);
            if (dsh != IntPtr.Zero)
            {
                Log.Write(string.Format("已有 DSH 实例：hwnd=0x{0:X}", dsh.ToInt64()));
            }
            else
            {
                Log.Write("未检测到运行中的 DSH，准备启动：" + (exe.Length == 0 ? "（DshExe 为空）" : exe));
                if (exe.Length == 0 || !File.Exists(exe))
                {
                    string msg = exe.Length == 0
                        ? "当前没有运行中的 DSH，且 config.json 里的 DshExe 为空。\n请填入 DSH 桌面入口的完整路径。"
                        : "config.json 里的 DshExe 指向的文件不存在：\n" + exe;
                    Log.Write("错误：" + msg.Replace("\n", " "));
                    MessageBox.Show(msg, "DSH Awakening", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Write("未处理异常：" + e.Exception.ToString());
                e.Handled = true;
                app.Shutdown(1);
            };

            try
            {
                app.Run(new PlayerWindow(cfg, video, baseDir, dsh));
            }
            catch (Exception ex)
            {
                Log.Write("启动异常：" + ex.ToString());
            }

            Log.Write("退出");
        }
    }
}
