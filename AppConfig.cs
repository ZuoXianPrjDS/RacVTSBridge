using System;
using System.IO;
using Newtonsoft.Json;

namespace RacVTSBridge
{
    /// <summary>
    /// 应用程序配置 - 保存用户设置，下次启动自动加载
    /// 配置文件保存在程序同目录下的 config.json
    /// </summary>
    public class AppConfig
    {
        // 运行模式: Local / Sender / Receiver
        public string Mode { get; set; } = "Local";

        // 当前游戏类型
        public string GameType { get; set; } = "Forza";

        // 界面语言: CN / EN
        public string Language { get; set; } = "CN";

        // Forza UDP 端口
        public int ForzaUdpPort { get; set; } = 5300;

        // VTube Studio 端口
        public int VtsPort { get; set; } = 8001;

        // 横向加速度参数名
        public string ParamX { get; set; } = "AccelerationX";

        // 前后加速度参数名
        public string ParamZ { get; set; } = "AccelerationZ";

        // 发送端：目标IP地址（推流机IP）
        public string TargetIp { get; set; } = "192.168.1.100";

        // 发送端：目标端口
        public int TargetPort { get; set; } = 9000;

        // 接收端：监听端口
        public int ListenPort { get; set; } = 9000;

        [JsonIgnore]
        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonConvert.DeserializeObject<AppConfig>(json);
                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Config] 已加载配置: {ConfigPath}");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Config] 加载配置失败: {ex.Message}");
            }

            return new AppConfig();
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                System.Diagnostics.Debug.WriteLine($"[Config] 已保存配置: {ConfigPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Config] 保存配置失败: {ex.Message}");
            }
        }
    }
}
