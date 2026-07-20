using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RacVTSBridge
{
    /// <summary>
    /// 通过 UDP 将加速度数据发送到远程主机（推流机）
    /// 数据格式：简单的二进制包，包含魔术字、版本号、两个 float
    /// </summary>
    public class AccelerationUdpSender : IDisposable
    {
        private readonly string _remoteIp;
        private readonly int _remotePort;
        private UdpClient? _udpClient;
        private IPEndPoint? _remoteEndPoint;
        private bool _disposed;

        // 协议魔术字 "RACV" (0x52414356)
        private const uint Magic = 0x52414356;
        private const byte Version = 1;

        public AccelerationUdpSender(string remoteIp, int remotePort)
        {
            _remoteIp = remoteIp;
            _remotePort = remotePort;
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AccelerationUdpSender));

            _udpClient = new UdpClient();
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_remoteIp), _remotePort);

            System.Diagnostics.Debug.WriteLine($"[UdpSender] 已启动，目标: {_remoteIp}:{_remotePort}");
        }

        /// <summary>
        /// 发送加速度数据
        /// 数据包结构 (13 字节):
        /// - 4 字节: 魔术字 "RACV"
        /// - 1 字节: 版本号 (1)
        /// - 4 字节: AccelerationX (float, little-endian)
        /// - 4 字节: AccelerationZ (float, little-endian)
        /// </summary>
        public void Send(float accelerationX, float accelerationZ)
        {
            if (_disposed || _udpClient == null || _remoteEndPoint == null) return;

            try
            {
                byte[] data = new byte[13];

                // 魔术字 (4 bytes) - Big-endian: "RACV" = 0x52 0x41 0x43 0x56
                byte[] magicBytes = BitConverter.GetBytes(Magic);
                if (BitConverter.IsLittleEndian) Array.Reverse(magicBytes);
                Buffer.BlockCopy(magicBytes, 0, data, 0, 4);

                // 版本号 (1 byte)
                data[4] = Version;

                // AccelerationX (4 bytes)
                byte[] xBytes = BitConverter.GetBytes(accelerationX);
                if (!BitConverter.IsLittleEndian) Array.Reverse(xBytes);
                Buffer.BlockCopy(xBytes, 0, data, 5, 4);

                // AccelerationZ (4 bytes)
                byte[] zBytes = BitConverter.GetBytes(accelerationZ);
                if (!BitConverter.IsLittleEndian) Array.Reverse(zBytes);
                Buffer.BlockCopy(zBytes, 0, data, 9, 4);

                _udpClient.Send(data, data.Length, _remoteEndPoint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UdpSender] 发送失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _udpClient?.Close(); } catch { }
            _udpClient = null;
            _remoteEndPoint = null;

            System.Diagnostics.Debug.WriteLine("[UdpSender] 已停止");
        }
    }
}
