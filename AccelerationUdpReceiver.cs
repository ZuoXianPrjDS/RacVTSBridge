using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RacVTSBridge
{
    /// <summary>
    /// 通过 UDP 接收远程主机（游戏主机）发送的加速度数据
    /// 数据格式与 AccelerationUdpSender 对应
    /// </summary>
    public class AccelerationUdpReceiver : IDisposable
    {
        private readonly int _listenPort;
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private bool _disposed;

        // 协议魔术字 "RACV" (0x52414356)
        private const uint Magic = 0x52414356;
        private const byte ExpectedVersion = 1;
        private const int PacketSize = 13; // 4 + 1 + 4 + 4

        /// <summary>
        /// 收到加速度数据时触发
        /// </summary>
        public event Action<float, float>? OnAccelerationReceived;

        /// <summary>
        /// 状态变化时触发（如连接成功、错误等）
        /// </summary>
        public event Action<string>? OnStatusChanged;

        public AccelerationUdpReceiver(int listenPort)
        {
            _listenPort = listenPort;
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AccelerationUdpReceiver));

            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient(_listenPort);
            _udpClient.Client.ReceiveBufferSize = 65535;

            _receiveTask = Task.Run(ReceiveLoop, _cts.Token);

            OnStatusChanged?.Invoke($"网络接收已启动，监听端口: {_listenPort}");
            System.Diagnostics.Debug.WriteLine($"[UdpReceiver] 已启动，监听端口: {_listenPort}");
        }

        private async Task ReceiveLoop()
        {
            var token = _cts!.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient!.ReceiveAsync().WithCancellation(token);
                    byte[] data = result.Buffer;

                    if (data.Length < PacketSize)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UdpReceiver] 数据包太小: {data.Length} 字节");
                        continue;
                    }

                    // 验证魔术字 (Big-endian "RACV")
                    byte[] magicBytes = new byte[4];
                    Buffer.BlockCopy(data, 0, magicBytes, 0, 4);
                    if (BitConverter.IsLittleEndian) Array.Reverse(magicBytes);
                    uint magic = BitConverter.ToUInt32(magicBytes, 0);
                    if (magic != Magic)
                    {
                        // 不是我们的数据包，跳过
                        continue;
                    }

                    // 验证版本
                    byte version = data[4];
                    if (version != ExpectedVersion)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UdpReceiver] 协议版本不匹配: {version}");
                        continue;
                    }

                    // 解析加速度数据 (little-endian)
                    float accelerationX = BitConverter.ToSingle(data, 5);
                    float accelerationZ = BitConverter.ToSingle(data, 9);

                    if (!BitConverter.IsLittleEndian)
                    {
                        // 如果系统是大端，需要反转
                        // 实际上 x86/x64 都是小端，这里只是保险
                    }

                    System.Diagnostics.Debug.WriteLine($"[UdpReceiver] 收到: X={accelerationX:F4}, Z={accelerationZ:F4}");

                    OnAccelerationReceived?.Invoke(accelerationX, accelerationZ);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UdpReceiver] 接收错误: {ex.Message}");
                    // 短暂等待后继续
                    try { await Task.Delay(100, token); } catch { break; }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts?.Cancel(); } catch { }

            try
            {
                _udpClient?.Close();
            }
            catch { }

            try
            {
                _receiveTask?.Wait(1000);
            }
            catch { }

            _cts?.Dispose();
            _cts = null;
            _udpClient = null;
            _receiveTask = null;

            System.Diagnostics.Debug.WriteLine("[UdpReceiver] 已停止");
        }
    }

    /// <summary>
    /// UdpClient.ReceiveAsync 的取消扩展方法
    /// </summary>
    internal static class UdpClientExtensions
    {
        public static async Task<UdpReceiveResult> WithCancellation(
            this Task<UdpReceiveResult> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => tcs.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
            }
            return await task;
        }
    }
}
