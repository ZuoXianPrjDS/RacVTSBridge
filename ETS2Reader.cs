using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RacVTSBridge
{
    /// <summary>
    /// 欧洲卡车模拟2 / 美国卡车模拟 共享内存读取器
    /// 通过 SCS SDK 插件提供的共享内存获取游戏数据
    /// </summary>
    public class ETS2Reader : IDisposable
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

        public ETS2Reader(int readIntervalMs = 16)
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
                OnStatusChanged?.Invoke("ETS2 共享内存读取已启动");

                _readTask = Task.Run(ReadLoop, _cts.Token);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                OnStatusChanged?.Invoke($"ETS2 启动失败: {ex.Message}");
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
            OnStatusChanged?.Invoke("ETS2 共享内存读取已停止");
        }

        private async Task ReadLoop()
        {
            var token = _cts!.Token;

            try
            {
                // ETS2/ATS SDK 共享内存名称
                using (var mmf = MemoryMappedFile.OpenExisting("Local\\SimTelemetryETS2"))
                using (var accessor = mmf.CreateViewAccessor())
                {
                    OnStatusChanged?.Invoke("已连接到 ETS2/ATS");

                    // 直接按偏移量读取，避免结构体对齐问题
                    // 偏移量根据 ets2-telemetry-common.hpp 计算（默认MSVC对齐）
                    // time(0) + paused(4) + tel_revId(8,12bytes) + engine_enabled(20,1byte) + trailer_attached(21,1byte) + padding(2) + speed(24)
                    const int OffsetAccelerationX = 28; // 前后加速度 (加速/刹车)
                    const int OffsetAccelerationY = 32; // 垂直加速度
                    const int OffsetAccelerationZ = 36; // 横向加速度 (左右)
                    const int OffsetTime = 0;

                    byte[] timeBuf = new byte[4];
                    byte[] accelBuf = new byte[4];

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            // 检查游戏是否在运行
                            accessor.ReadArray(OffsetTime, timeBuf, 0, 4);
                            uint time = BitConverter.ToUInt32(timeBuf, 0);
                            if (time == 0)
                            {
                                await Task.Delay(1000, token);
                                continue;
                            }

                            // 直接读取加速度数据
                            accessor.ReadArray(OffsetAccelerationX, accelBuf, 0, 4);
                            float accelX_raw = BitConverter.ToSingle(accelBuf, 0);

                            accessor.ReadArray(OffsetAccelerationZ, accelBuf, 0, 4);
                            float accelZ_raw = BitConverter.ToSingle(accelBuf, 0);

                            // ETS2: accelerationX=前后(加速刹车), accelerationZ=横向(左右)
                            // 交换映射：AccX(横向) <- ETS2 X, AccZ(前后) <- ETS2 Z (取反)
                            float accelerationX = accelX_raw * 10f;  // 横向加速度 (Sway) - 来自ETS2的X
                            float accelerationZ = -accelZ_raw * 10f;  // 前后加速度 (Surge) - 来自ETS2的Z (取反)

                            System.Diagnostics.Debug.WriteLine($"[ETS2] 原始: aX={accelX_raw:F4}, aZ={accelZ_raw:F4} | 映射: AccX={accelerationX:F4}, AccZ={accelerationZ:F4}");

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
                OnStatusChanged?.Invoke($"无法连接到 ETS2: {ex.Message}");
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

        #region ETS2 Telemetry Data Structure

        // 根据 ets2-telemetry-common.hpp 定义的结构
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct ETS2TelemetryMap
        {
            // Header
            public uint time;
            public uint paused;

            // Version info
            public uint tel_revId_ets2_telemetry_plugin_revision;
            public uint tel_revId_ets2_version_major;
            public uint tel_revId_ets2_version_minor;

            // tel_rev1 - vehicle dynamics
            public byte tel_rev1_engine_enabled;
            public byte tel_rev1_trailer_attached;
            public float tel_rev1_speed;
            public float tel_rev1_accelerationX;  // 前后加速度 (加速/刹车)
            public float tel_rev1_accelerationY;  // 垂直加速度
            public float tel_rev1_accelerationZ;  // 横向加速度 (左右)
            public float tel_rev1_coordinateX;
            public float tel_rev1_coordinateY;
            public float tel_rev1_coordinateZ;
            public float tel_rev1_rotationX;
            public float tel_rev1_rotationY;
            public float tel_rev1_rotationZ;
            public int tel_rev1_gear;
            public int tel_rev1_gears;
            public int tel_rev1_gearRanges;
            public int tel_rev1_gearRangeActive;
            public float tel_rev1_engineRpm;
            public float tel_rev1_engineRpmMax;
            public float tel_rev1_fuel;
            public float tel_rev1_fuelCapacity;
            public float tel_rev1_fuelRate;
            public float tel_rev1_fuelAvgConsumption;
            public float tel_rev1_userSteer;
            public float tel_rev1_userThrottle;
            public float tel_rev1_userBrake;
            public float tel_rev1_userClutch;
            public float tel_rev1_gameSteer;
            public float tel_rev1_gameThrottle;
            public float tel_rev1_gameBrake;
            public float tel_rev1_gameClutch;
            public float tel_rev1_truckWeight;
            public float tel_rev1_trailerWeight;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] tel_rev1_modelType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] tel_rev1_trailerType;

            // tel_rev2
            public long tel_rev2_time_abs;
            public int tel_rev2_gears_reverse;
            public float tel_rev2_trailerMass;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev2_trailerId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev2_trailerName;
            public int tel_rev2_jobIncome;
            public int tel_rev2_time_abs_delivery;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev2_citySrc;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev2_cityDst;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev2_compSrc;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev2_compDst;

            // tel_rev3 (简化，只包含需要的字段)
            public int tel_rev3_retarderBrake;
            public int tel_rev3_shifterSlot;
            public int tel_rev3_shifterToggle;
            public int tel_rev3_fill;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
            public byte[] tel_rev3_flags1; // 布尔值打包
            public float tel_rev3_airPressure;
            public float tel_rev3_brakeTemperature;
            public int tel_rev3_fuelWarning;
            public float tel_rev3_adblue;
            public float tel_rev3_adblueConsumption;
            public float tel_rev3_oilPressure;
            public float tel_rev3_oilTemperature;
            public float tel_rev3_waterTemperature;
            public float tel_rev3_batteryVoltage;
            public float tel_rev3_lightsDashboard;
            public float tel_rev3_wearEngine;
            public float tel_rev3_wearTransmission;
            public float tel_rev3_wearCabin;
            public float tel_rev3_wearChassis;
            public float tel_rev3_wearWheels;
            public float tel_rev3_wearTrailer;
            public float tel_rev3_truckOdometer;
            public float tel_rev3_cruiseControlSpeed;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev3_truckMake;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev3_truckMakeId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] tel_rev3_truckModel;

            // tel_rev4
            public float tel_rev4_speedLimit;
            public float tel_rev4_routeDistance;
            public float tel_rev4_routeTime;
            public float tel_rev4_fuelRange;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
            public float[] tel_rev4_gearRatiosForward;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public float[] tel_rev4_gearRatiosReverse;
            public float tel_rev4_gearDifferential;
            public int tel_rev4_gearDashboard;

            // tel_rev5
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] tel_rev5_flags;
        }

        #endregion
    }
}
