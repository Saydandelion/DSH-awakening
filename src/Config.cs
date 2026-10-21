// 阶段 B — JSON 配置（只用 System.Web.Extensions 自带的 JavaScriptSerializer，不引入第三方库）
using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DshAwakening
{
    public sealed class Config
    {
        public string DshExe { get; set; }
        public string Video { get; set; }
        public double? HoldAtSeconds { get; set; }
        public int MaxWaitSeconds { get; set; }
        public double Volume { get; set; }

        public Config()
        {
            DshExe = "";
            Video = "";
            HoldAtSeconds = null;
            MaxWaitSeconds = 60;
            Volume = 0.8;
        }

        /// <summary>读取配置；失败返回 null 并给出可读原因。</summary>
        public static Config Load(string path, out string error)
        {
            error = null;
            if (!File.Exists(path))
            {
                error = "找不到配置文件：\n" + path;
                return null;
            }

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                error = "配置文件读取失败：" + ex.Message;
                return null;
            }

            Config cfg;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                cfg = ser.Deserialize<Config>(json);
            }
            catch (Exception ex)
            {
                error = "配置文件格式有误（需要合法 JSON）：\n" + ex.Message;
                return null;
            }

            if (cfg == null)
            {
                error = "配置文件内容为空。";
                return null;
            }

            cfg.Normalize();
            return cfg;
        }

        private void Normalize()
        {
            if (DshExe == null) DshExe = "";
            if (Video == null) Video = "";
            if (MaxWaitSeconds <= 0) MaxWaitSeconds = 60;
            if (MaxWaitSeconds > 600) MaxWaitSeconds = 600;
            if (Volume < 0.0) Volume = 0.0;
            if (Volume > 1.0) Volume = 1.0;
        }

        /// <summary>视频相对路径按启动器目录解析。</summary>
        public string ResolveVideo(string baseDir)
        {
            string v = (Video == null ? "" : Video.Trim());
            if (v.Length == 0) return "";
            if (!Path.IsPathRooted(v)) v = Path.Combine(baseDir, v);
            try
            {
                return Path.GetFullPath(v);
            }
            catch
            {
                return v;
            }
        }

        public string ResolveDshExe(string baseDir)
        {
            string p = (DshExe == null ? "" : DshExe.Trim());
            if (p.Length == 0) return "";
            if (!Path.IsPathRooted(p)) p = Path.Combine(baseDir, p);
            try
            {
                return Path.GetFullPath(p);
            }
            catch
            {
                return p;
            }
        }

        /// <summary>保存首次自动发现或用户选择的 DSH 路径。</summary>
        public bool Save(string path, out string error)
        {
            error = null;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                File.WriteAllText(path, ser.Serialize(this), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = "配置保存失败：" + ex.Message;
                return false;
            }
        }

        /// <summary>等待节点：留空取 max(0, D-0.2)；手填必须满足 0 ≤ t &lt; D。</summary>
        public double ResolveHoldAt(double durationSeconds, out string note)
        {
            double fallback = Math.Max(0.0, durationSeconds - 0.2);
            if (!HoldAtSeconds.HasValue)
            {
                note = "未填等待节点，取 max(0, D-0.2)";
                return fallback;
            }
            double t = HoldAtSeconds.Value;
            if (t < 0.0 || t >= durationSeconds)
            {
                note = string.Format("等待节点 {0:0.###}s 超出 0 ≤ t < {1:0.###}s，回退为 {2:0.###}s", t, durationSeconds, fallback);
                return fallback;
            }
            note = "使用配置的等待节点";
            return t;
        }
    }
}
