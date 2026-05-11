using VTS.Core;

namespace RacVTSBridge
{
    /// <summary>
    /// 适配 WPF 的 VTS 日志器，将日志输出到调试窗口
    /// </summary>
    public class WpfVTSLogger : IVTSLogger
    {
        public void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[VTS] {message}");
        }

        public void LogWarning(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[VTS Warning] {message}");
        }

        public void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[VTS Error] {message}");
        }

        public void LogError(System.Exception message)
        {
            System.Diagnostics.Debug.WriteLine($"[VTS Error] {message}");
        }
    }
}
