using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RacVTSBridge
{
    public class ForzaUdpReceiver : IDisposable
    {
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private readonly int _port;

        /// <summary>
        /// 接收到加速度数据时触发
        /// </summary>
        public event Action<float, float>? OnAccelerationReceived;

        /// <summary>
        /// 接收状态变化时触发
        /// </summary>
        public event Action<string>? OnStatusChanged;

        public bool IsRunning { get; private set; }

        public ForzaUdpReceiver(int port)
        {
            _port = port;
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _udpClient = new UdpClient(_port);
                _udpClient.Client.ReceiveBufferSize = 65535;
                _cts = new CancellationTokenSource();
                IsRunning = true;
                OnStatusChanged?.Invoke($"UDP 监听已启动 (端口: {_port})");

                Task.Run(ReceiveLoop, _cts.Token);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                OnStatusChanged?.Invoke($"UDP 启动失败: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _cts?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
            OnStatusChanged?.Invoke("UDP 监听已停止");
        }

        private async Task ReceiveLoop()
        {
            var token = _cts!.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var receiveTask = _udpClient!.ReceiveAsync();
                    using (token.Register(() => { /* 取消时中断 */ }))
                    {
                        try
                        {
                            var result = await receiveTask;
                            ParsePacket(result.Buffer);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnStatusChanged?.Invoke($"UDP 接收错误: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 接收循环结束
            }
        }

        /// <summary>
        /// 解析 Forza UDP 数据包，提取 AccelerationX 和 AccelerationZ
        /// Sled 格式（232字节）和 Dash 格式（311/323/324字节）中，
        /// AccelerationX 在偏移 20，AccelerationZ 在偏移 28，均为 float（4字节，小端序）
        /// </summary>
        private void ParsePacket(byte[] data)
        {
            // 最小包长度检查（至少需要到偏移 32，即 AccelerationZ 之后）
            if (data.Length < 32) return;

            float accelerationX = BitConverter.ToSingle(data, 20);
            float accelerationZ = BitConverter.ToSingle(data, 28);

            OnAccelerationReceived?.Invoke(accelerationX, accelerationZ);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
