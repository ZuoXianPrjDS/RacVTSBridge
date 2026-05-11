using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VTS.Core;

namespace RacVTSBridge
{
    public partial class MainWindow : Window
    {
        private ForzaUdpReceiver? _forzaReceiver;
        private AssettoCorsaReader? _acReader;
        private CoreVTSPlugin? _vtsPlugin;
        private bool _isRunning;
        private bool _isForzaMode = true;
        private bool _isInitialized = false;
        private string _currentLang = "CN"; // 当前语言

        private VTSParameterInjectionValue[] _paramValues = Array.Empty<VTSParameterInjectionValue>();
        private string _paramNameX = "AccelerationX";
        private string _paramNameZ = "AccelerationZ";

        // 多语言文本
        private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["CN"] = new()
            {
                ["Title"] = "RacVTSBridge - 赛车游戏加速度桥接",
                ["GameType"] = "游戏类型:",
                ["ForzaMode"] = "极限竞速 (Forza)",
                ["ACMode"] = "神力科莎系列",
                ["UdpPort"] = "UDP 端口:",
                ["VtsPort"] = "VTube Studio 端口:",
                ["ParamX"] = "参数名 (横向加速度):",
                ["ParamZ"] = "参数名 (前后加速度):",
                ["ForzaInfo"] = "极限竞速模式: 通过 UDP 接收 Forza Horizon 5 数据\n请在 Forza 设置中开启「数据输出」，端口设为上方 UDP 端口",
                ["ACInfo"] = "神力科莎模式: 通过共享内存读取 Assetto Corsa / ACC 数据\n请确保游戏正在运行，程序会自动连接",
                ["Start"] = "启动",
                ["Stop"] = "停止",
                ["Status"] = "状态",
                ["Ready"] = "就绪 - 选择游戏类型后点击「启动」",
                ["Running"] = "运行中",
                ["Stopped"] = "已停止",
                ["Hint"] = "提示：VTube Studio 需开启「启用 API」，Forza 需设置数据输出端口，AC 需运行游戏",
                ["Credit"] = "B站左舷基于TRAE Solo创作，加q群954463059联系"
            },
            ["EN"] = new()
            {
                ["Title"] = "RacVTSBridge",
                ["GameType"] = "Game Type:",
                ["ForzaMode"] = "Forza",
                ["ACMode"] = "Assetto Corsa",
                ["UdpPort"] = "UDP Port:",
                ["VtsPort"] = "VTube Studio Port:",
                ["ParamX"] = "Param Name (Lateral):",
                ["ParamZ"] = "Param Name (Longitudinal):",
                ["ForzaInfo"] = "Forza Mode: Receive data via UDP from Forza Horizon 5\nPlease enable 'Data Out' in Forza settings with the UDP port above",
                ["ACInfo"] = "Assetto Corsa Mode: Read data via shared memory from AC/ACC\nPlease ensure the game is running",
                ["Start"] = "Start",
                ["Stop"] = "Stop",
                ["Status"] = "Status",
                ["Ready"] = "Ready - Select game type and click Start",
                ["Running"] = "Running",
                ["Stopped"] = "Stopped",
                ["Hint"] = "Tip: VTube Studio needs 'Enable API' turned on, Forza needs data output port set, AC needs game running",
                ["Credit"] = "Contact www.x.com/@zuxin119417 for more information"
            }
        };

        public MainWindow()
        {
            InitializeComponent();
            
            _isInitialized = true;
            
            ForzaRadio.Checked += GameType_Checked;
            ForzaRadio.Unchecked += GameType_Unchecked;
            ACRadio.Checked += GameType_Checked;
            ACRadio.Unchecked += GameType_Unchecked;
            
            this.Closing += MainWindow_Closing;
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateGameInfo();
            UpdateLanguage();
        }

        private void LangButton_Click(object sender, RoutedEventArgs e)
        {
            // 保留旧方法以防万一
        }

        private void LangComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LangComboBox == null) return;
            
            if (LangComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string lang)
            {
                _currentLang = lang;
                UpdateLanguage();
            }
        }

        private void UpdateLanguage()
        {
            if (!_isInitialized) return;
            
            var t = _translations[_currentLang];
            
            // 更新标题
            this.Title = t["Title"];
            if (TitleText != null) TitleText.Text = t["Title"];
            
            // 更新标签
            if (GameTypeLabel != null) GameTypeLabel.Text = t["GameType"];
            if (ForzaRadio != null) ForzaRadio.Content = t["ForzaMode"];
            if (ACRadio != null) ACRadio.Content = t["ACMode"];
            if (UdpPortLabel != null) UdpPortLabel.Text = t["UdpPort"];
            if (VtsPortLabel != null) VtsPortLabel.Text = t["VtsPort"];
            if (ParamXLabel != null) ParamXLabel.Text = t["ParamX"];
            if (ParamZLabel != null) ParamZLabel.Text = t["ParamZ"];
            if (StatusLabel != null) StatusLabel.Text = t["Status"];
            
            // 更新按钮
            if (StartButton != null)
            {
                if (!_isRunning)
                    StartButton.Content = t["Start"];
                else
                    StartButton.Content = t["Stop"];
            }
            
            // 更新提示
            if (HintText != null) HintText.Text = t["Hint"];
            if (CreditText != null) CreditText.Text = t["Credit"];
            
            // 更新游戏说明
            UpdateGameInfo();
            
            // 更新状态（如果不是运行中）
            if (!_isRunning && StatusText != null)
                StatusText.Text = t["Ready"];
        }

        private void GameType_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // 窗口初始化期间不处理事件
                if (!_isInitialized) return;
                
                // 控件可能还未完全创建
                if (ForzaPortPanel == null || GameInfoText == null || GameInfoBorder == null) return;
                
                if (ForzaRadio.IsChecked == true)
                {
                    _isForzaMode = true;
                    ForzaPortPanel.Visibility = Visibility.Visible;
                }
                else if (ACRadio.IsChecked == true)
                {
                    _isForzaMode = false;
                    ForzaPortPanel.Visibility = Visibility.Collapsed;
                }
                
                UpdateGameInfo();
            }
            catch
            {
                // 忽略初始化期间的任何错误
            }
        }

        private void GameType_Unchecked(object sender, RoutedEventArgs e)
        {
            // 不需要处理
        }

        private void UpdateGameInfo()
        {
            var t = _translations[_currentLang];
            
            if (_isForzaMode)
            {
                GameInfoText.Text = t["ForzaInfo"];
                GameInfoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
                GameInfoBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                GameInfoText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));
            }
            else
            {
                GameInfoText.Text = t["ACInfo"];
                GameInfoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                GameInfoBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                GameInfoText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
                await StartAsync();
            else
                Stop();
        }

        private async Task StartAsync()
        {
            // 读取 VTS 端口
            if (!int.TryParse(VtsPortTextBox.Text, out int vtsPort) || vtsPort < 1 || vtsPort > 65535)
            {
                SetStatus("错误: VTS 端口无效", StatusColor.Error);
                return;
            }

            // Forza 模式需要 UDP 端口
            int udpPort = 5300;
            if (_isForzaMode)
            {
                if (!int.TryParse(UdpPortTextBox.Text, out udpPort) || udpPort < 1 || udpPort > 65535)
                {
                    SetStatus("错误: UDP 端口无效", StatusColor.Error);
                    return;
                }
            }

            _paramNameX = ParamXTextBox.Text.Trim();
            _paramNameZ = ParamZTextBox.Text.Trim();

            if (string.IsNullOrEmpty(_paramNameX) || string.IsNullOrEmpty(_paramNameZ))
            {
                SetStatus("错误: 参数名不能为空", StatusColor.Error);
                return;
            }

            SetStatus("正在连接 VTube Studio...", StatusColor.Connecting);

            try
            {
                var logger = new WpfVTSLogger();
                _vtsPlugin = new CoreVTSPlugin(logger, 100, "RacVTSBridge", "RacVTSBridge", "");

                var tcs = new TaskCompletionSource<bool>();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                using (cts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    _vtsPlugin.Initialize(
                        new WebSocketImpl(logger),
                        new NewtonsoftJsonUtilityImpl(),
                        new TokenStorageImpl(""),
                        onConnect: () => tcs.TrySetResult(true),
                        onDisconnect: () => Dispatcher.Invoke(() => SetStatus("VTube Studio 连接已断开", StatusColor.Error)),
                        onError: (error) => tcs.TrySetException(new Exception(error.data?.message ?? "未知错误"))
                    );

                    if (vtsPort != 8001 && _vtsPlugin.Socket != null)
                    {
                        _vtsPlugin.Socket.SetPort(vtsPort);
                    }

                    try
                    {
                        await tcs.Task;
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException("连接超时，请检查:\n1. VTube Studio 是否已启动\n2. 是否已开启「启用 API」\n3. 端口是否正确（默认8001）");
                    }
                }

                if (!_vtsPlugin.IsAuthenticated)
                    throw new Exception("认证失败");

                // 创建自定义参数
                SetStatus("正在创建自定义参数...", StatusColor.Connecting);
                await CreateCustomParametersAsync();

                // 准备参数注入对象
                _paramValues = new VTSParameterInjectionValue[]
                {
                    new VTSParameterInjectionValue { id = _paramNameX, value = 0f, weight = 1f },
                    new VTSParameterInjectionValue { id = _paramNameZ, value = 0f, weight = 1f },
                };

                // 根据模式启动对应的游戏数据接收器
                if (_isForzaMode)
                {
                    _forzaReceiver = new ForzaUdpReceiver(udpPort);
                    _forzaReceiver.OnAccelerationReceived += OnAccelerationReceived;
                    _forzaReceiver.OnStatusChanged += msg => SetStatus(msg, StatusColor.Info);
                    _forzaReceiver.Start();
                }
                else
                {
                    _acReader = new AssettoCorsaReader();
                    _acReader.OnAccelerationReceived += OnAccelerationReceived;
                    _acReader.OnStatusChanged += msg => SetStatus(msg, StatusColor.Info);
                    _acReader.Start();
                }

                _isRunning = true;
                Dispatcher.Invoke(() =>
                {
                    StartButton.Content = _translations[_currentLang]["Stop"];
                    StartButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                    ForzaRadio.IsEnabled = false;
                    ACRadio.IsEnabled = false;
                });

                string modeStr = _isForzaMode ? $"UDP: {udpPort}" : (_currentLang == "CN" ? "共享内存" : "Shared Memory");
                string runningText = _translations[_currentLang]["Running"];
                SetStatus($"{runningText} | {modeStr} | VTS: {vtsPort}", StatusColor.Success);
            }
            catch (TimeoutException ex)
            {
                SetStatus(ex.Message, StatusColor.Error);
                Cleanup();
            }
            catch (Exception ex)
            {
                SetStatus($"启动失败: {ex.Message}", StatusColor.Error);
                Cleanup();
            }
        }

        private async Task CreateCustomParametersAsync()
        {
            var paramX = new VTSCustomParameter
            {
                parameterName = _paramNameX,
                explanation = "横向加速度 (Sway)",
                min = -9f,
                max = 9f,
                defaultValue = 0f
            };

            var paramZ = new VTSCustomParameter
            {
                parameterName = _paramNameZ,
                explanation = "前后加速度 (Surge)",
                min = -9f,
                max = 9f,
                defaultValue = 0f
            };

            try
            {
                await _vtsPlugin!.AddCustomParameter(paramX);
                await _vtsPlugin!.AddCustomParameter(paramZ);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建参数时出错: {ex.Message}");
            }
        }

        private void Stop()
        {
            Cleanup();
            SetStatus(_translations[_currentLang]["Stopped"], StatusColor.Info);
        }

        private void Cleanup()
        {
            _isRunning = false;

            if (_forzaReceiver != null)
            {
                _forzaReceiver.OnAccelerationReceived -= OnAccelerationReceived;
                _forzaReceiver.Dispose();
                _forzaReceiver = null;
            }

            if (_acReader != null)
            {
                _acReader.OnAccelerationReceived -= OnAccelerationReceived;
                _acReader.Dispose();
                _acReader = null;
            }

            if (_vtsPlugin != null)
            {
                try { _vtsPlugin.Disconnect(); } catch { }
                _vtsPlugin = null;
            }

            _paramValues = Array.Empty<VTSParameterInjectionValue>();

            Dispatcher.Invoke(() =>
            {
                StartButton.Content = _translations[_currentLang]["Start"];
                StartButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90D9"));
                ForzaRadio.IsEnabled = true;
                ACRadio.IsEnabled = true;
                DataText.Text = "";
            });
        }

        private void OnAccelerationReceived(float accelerationX, float accelerationZ)
        {
            if (_vtsPlugin == null || !_vtsPlugin.IsAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] VTS未连接，跳过数据");
                return;
            }

            _paramValues[0].value = accelerationX;
            _paramValues[1].value = accelerationZ;

            System.Diagnostics.Debug.WriteLine($"[DEBUG] 发送数据: X={accelerationX:F4}, Z={accelerationZ:F4}");

            _vtsPlugin.InjectParameterValues(
                _paramValues,
                VTSInjectParameterMode.SET,
                result => System.Diagnostics.Debug.WriteLine($"[DEBUG] 发送成功"),
                error => System.Diagnostics.Debug.WriteLine($"[DEBUG] 发送失败: {error.data?.message}")
            );

            Dispatcher.BeginInvoke(() =>
            {
                DataText.Text = $"AccelX: {accelerationX:F4}  AccelZ: {accelerationZ:F4}";
            });
        }

        private enum StatusColor { Info, Success, Error, Connecting }

        private void SetStatus(string message, StatusColor color)
        {
            // 必须在 UI 线程中创建 SolidColorBrush 并更新 UI
            Dispatcher.BeginInvoke(() =>
            {
                var brush = color switch
                {
                    StatusColor.Success => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")),
                    StatusColor.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828")),
                    StatusColor.Connecting => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100")),
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"))
                };

                StatusText.Text = message;
                StatusText.Foreground = brush;
            });
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Cleanup();
        }
    }
}
