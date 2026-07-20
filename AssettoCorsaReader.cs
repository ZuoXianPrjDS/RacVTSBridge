using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RacVTSBridge
{
    /// <summary>
    /// Assetto Corsa / Assetto Corsa Competizione 共享内存读取器
    /// 通过共享内存而非 UDP 获取游戏数据
    /// </summary>
    public class AssettoCorsaReader : IDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private readonly int _readIntervalMs;

        /// <summary>
        /// 接收到加速度数据时触发
        /// </summary>
        public event Action<float, float>? OnAccelerationReceived;

        /// <summary>
        /// 状态变化时触发
        /// </summary>
        public event Action<string>? OnStatusChanged;

        public bool IsRunning { get; private set; }

        public AssettoCorsaReader(int readIntervalMs = 16) // 默认约60fps
        {
            _readIntervalMs = readIntervalMs;
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _cts = new CancellationTokenSource();
                IsRunning = true;
                OnStatusChanged?.Invoke("AC 共享内存读取已启动");

                _readTask = Task.Run(ReadLoop, _cts.Token);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                OnStatusChanged?.Invoke($"AC 启动失败: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _cts?.Cancel();
            _readTask?.Wait(TimeSpan.FromSeconds(1));
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
            OnStatusChanged?.Invoke("AC 共享内存读取已停止");
        }

        private async Task ReadLoop()
        {
            var token = _cts!.Token;

            try
            {
                // 尝试打开 AC 的共享内存
                using (var mmf = MemoryMappedFile.OpenExisting("Local\\acpmf_physics"))
                using (var accessor = mmf.CreateViewAccessor())
                {
                    OnStatusChanged?.Invoke("已连接到 Assetto Corsa");

                    byte[] buffer = new byte[Marshal.SizeOf<PhysicsData>()];

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            accessor.ReadArray(0, buffer, 0, buffer.Length);
                            var physics = BytesToStructure<PhysicsData>(buffer);

                            // 提取加速度（AC 原始数值偏低，需要放大）
                            // AccG: X=横向(左右), Y=垂直(上下), Z=前后(前后)
                            float accelerationX = physics.AccG.X * -8f;   // 横向加速度 (Sway)，负号修正方向
                            float accelerationZ = physics.AccG.Z * 10f;   // 前后加速度 (Surge)

                            System.Diagnostics.Debug.WriteLine($"[AC] 读取数据: X={accelerationX:F4}, Z={accelerationZ:F4}");

                            OnAccelerationReceived?.Invoke(accelerationX, accelerationZ);

                            await Task.Delay(_readIntervalMs, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnStatusChanged?.Invoke($"读取错误: {ex.Message}");
                            await Task.Delay(1000, token);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"无法连接到 Assetto Corsa: {ex.Message}");
            }
        }

        private static T BytesToStructure<T>(byte[] bytes) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Assetto Corsa 物理数据结构
        /// 基于 AC 共享内存文档
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PhysicsData
        {
            public int PacketId;
            public float Gas;
            public float Brake;
            public float Fuel;
            public int Gear;
            public int Rpms;
            public float SteerAngle;
            public float SpeedKmh;
            public Vector3 Velocity;
            public Vector3 AccG;           // 加速度 (X=横向, Y=垂直, Z=前后)
            public Vector3 WheelSlip;
            public Vector3 WheelLoad;
            public Vector3 WheelsPressure;
            public Vector3 WheelAngularSpeed;
            public Vector3 TyreWear;
            public Vector3 TyreDirtyLevel;
            public Vector3 TyreCoreTemperature;
            public Vector3 CamberRad;
            public Vector3 SuspensionTravel;
            public float Drs;
            public float TC;
            public float Heading;
            public float Pitch;
            public float Roll;
            public float CgHeight;
            public Vector3 CarDamage;
            public int NumberOfTyresOut;
            public int PitLimiterOn;
            public float Abs;
            public float KersCharge;
            public float KersInput;
            public int AutoShifterOn;
            public float RideHeight;
            public float TurboBoost;
            public float Ballast;
            public float AirDensity;
            public float AirTemp;
            public float RoadTemp;
            public Vector3 LocalAngularVelocity;
            public float FinalFF;
            public float PerformanceMeter;
            public int EngineBrake;
            public int ErsRecovery;
            public int ErsPower;
            public int ErsHeatCharging;
            public int ErsIsCharging;
            public float KersCurrentKJ;
            public int DrsAvailable;
            public int DrsEnabled;
            public Vector3 BrakeTemp;
            public float Clutch;
            public Vector3 TyreTempI;
            public Vector3 TyreTempM;
            public Vector3 TyreTempO;
            public int IsAIControlled;
            public Vector3 TyreContactPointFL;
            public Vector3 TyreContactPointFR;
            public Vector3 TyreContactPointRL;
            public Vector3 TyreContactPointRR;
            public Vector3 TyreContactNormalFL;
            public Vector3 TyreContactNormalFR;
            public Vector3 TyreContactNormalRL;
            public Vector3 TyreContactNormalRR;
            public Vector3 TyreContactHeadingFL;
            public Vector3 TyreContactHeadingFR;
            public Vector3 TyreContactHeadingRL;
            public Vector3 TyreContactHeadingRR;
            public float BrakeBias;
            public Vector3 LocalVelocity;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Vector3
        {
            public float X;
            public float Y;
            public float Z;
        }
    }
}
