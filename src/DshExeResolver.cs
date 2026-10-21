// 跨电脑定位 DeepSeek Harness 桌面程序。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DshAwakening
{
    internal static class DshExeResolver
    {
        public const string ExeName = "DeepSeek Harness.exe";

        /// <summary>依次返回首个存在且文件名正确的候选路径。</summary>
        public static string FindFirstExistingCandidate(string[] candidates)
        {
            if (candidates == null) return string.Empty;
            foreach (string candidate in candidates)
            {
                if (!IsValidCandidate(candidate)) continue;
                try { return Path.GetFullPath(candidate); }
                catch { return candidate; }
            }
            return string.Empty;
        }

        public static bool IsValidCandidate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                return string.Equals(Path.GetFileName(path.Trim()), ExeName,
                    StringComparison.OrdinalIgnoreCase) && File.Exists(path.Trim());
            }
            catch { return false; }
        }

        /// <summary>运行中的实例优先，避免另开一个不同安装副本。</summary>
        public static string FindRunningExecutable()
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)); }
            catch { return string.Empty; }

            foreach (Process process in processes)
            {
                try
                {
                    string path = process.MainModule == null ? string.Empty : process.MainModule.FileName;
                    if (IsValidCandidate(path)) return Path.GetFullPath(path);
                }
                catch { }
                finally { process.Dispose(); }
            }
            return string.Empty;
        }

        public static string Resolve(string configuredPath, out string source)
        {
            List<string> candidates = new List<string>();
            string running = FindRunningExecutable();
            Add(candidates, running);
            Add(candidates, configuredPath);
            AddRegistryCandidates(candidates);
            AddStandardCandidates(candidates);
            AddPathCandidates(candidates);

            string found = FindFirstExistingCandidate(candidates.ToArray());
            if (found.Length == 0)
            {
                source = "未找到；需要手动选择";
                return string.Empty;
            }
            if (SamePath(found, running)) source = "运行中的 DSH 实例";
            else if (SamePath(found, configuredPath)) source = "config.json";
            else source = "Windows 安装记录或常见安装目录";
            return found;
        }

        private static void AddRegistryCandidates(List<string> candidates)
        {
            RegistryView[] views = new RegistryView[] { RegistryView.Default, RegistryView.Registry64, RegistryView.Registry32 };
            foreach (RegistryView view in views)
            {
                AddAppPath(candidates, RegistryHive.CurrentUser, view);
                AddAppPath(candidates, RegistryHive.LocalMachine, view);
                AddUninstallPaths(candidates, RegistryHive.CurrentUser, view);
                AddUninstallPaths(candidates, RegistryHive.LocalMachine, view);
            }
        }

        private static void AddAppPath(List<string> candidates, RegistryHive hive, RegistryView view)
        {
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + ExeName))
                {
                    if (key != null) Add(candidates, CleanExePath(Convert.ToString(key.GetValue(null))));
                }
            }
            catch { }
        }

        private static void AddUninstallPaths(List<string> candidates, RegistryHive hive, RegistryView view)
        {
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninstall == null) return;
                    foreach (string name in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey entry = uninstall.OpenSubKey(name))
                            {
                                if (entry == null) continue;
                                string display = Convert.ToString(entry.GetValue("DisplayName"));
                                if (display.IndexOf("DeepSeek Harness", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                AddInstallLocation(candidates, Convert.ToString(entry.GetValue("InstallLocation")));
                                Add(candidates, CleanExePath(Convert.ToString(entry.GetValue("DisplayIcon"))));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void AddInstallLocation(List<string> candidates, string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return;
            string value = Environment.ExpandEnvironmentVariables(location.Trim().Trim('"'));
            Add(candidates, value);
            try
            {
                Add(candidates, Path.Combine(value, ExeName));
                Add(candidates, Path.Combine(value, "app", ExeName));
            }
            catch { }
        }

        private static string CleanExePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string path = Environment.ExpandEnvironmentVariables(value.Trim());
            if (path.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = path.IndexOf('"', 1);
                if (end > 1) path = path.Substring(1, end - 1);
            }
            else
            {
                int comma = path.IndexOf(',');
                if (comma >= 0) path = path.Substring(0, comma).Trim();
            }
            return path.Trim().Trim('"');
        }

        private static void AddStandardCandidates(List<string> candidates)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            AddInstallLocation(candidates, Path.Combine(local, "Programs", "DeepSeek Harness"));
            AddInstallLocation(candidates, Path.Combine(programFiles, "DeepSeek Harness"));
            AddInstallLocation(candidates, Path.Combine(programFilesX86, "DeepSeek Harness"));
        }

        private static void AddPathCandidates(List<string> candidates)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path)) return;
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                try { Add(candidates, Path.Combine(directory.Trim().Trim('"'), ExeName)); }
                catch { }
            }
        }

        private static void Add(List<string> candidates, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) candidates.Add(value.Trim());
        }

        private static bool SamePath(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
            try { return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
    }
}
