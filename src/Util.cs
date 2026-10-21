// 阶段 B — 日志（追加写，超过 1MB 自动截断，只留最近内容）
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DshAwakening
{
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;
        private const long MaxBytes = 1024 * 1024;

        public static void Init(string dir)
        {
            _path = System.IO.Path.Combine(dir, "app.log");
            try
            {
                FileInfo fi = new FileInfo(_path);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    string old = _path + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(_path, old);
                }
            }
            catch { }
            Write("──────── 启动 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ────────");
        }

        public static void Write(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg;
            Debug.WriteLine(line);
            if (_path == null) return;
            try
            {
                lock (Gate) File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
