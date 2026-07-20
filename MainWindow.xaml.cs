using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
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
        private ETS2Reader? _ets2Reader;
        private CoreVTSPlugin? _vtsPlugin;
        private AccelerationUdpSender? _udpSender;
        private AccelerationUdpReceiver? _udpReceiver;

        private bool _isRunning;
        private string _currentGame = "Forza";
        private string _currentMode = "Local"; // Local / Sender / Receiver
        private bool _isInitialized = false;
        private string _currentLang = "CN";
        private AppConfig _appConfig = new AppConfig();

        private VTSParameterInjectionValue[] _paramValues = Array.Empty<VTSParameterInjectionValue>();
        private string _paramNameX = "AccelerationX";
        private string _paramNameZ = "AccelerationZ";

        // 多语言文本
        private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["CN"] = new()
            {
                ["Title"] = "RacVTSBridge - 赛车游戏加速度桥接",
                ["Mode"] = "运行模式:",
                ["ModeLocal"] = "本地模式",
                ["ModeSender"] = "发送端（游戏主机）",
                ["ModeReceiver"] = "接收端（推流机）",
                ["GameType"] = "游戏类型:",
                ["Forza"] = "极限竞速 (Forza)",
                ["AC"] = "神力科莎系列",
                ["ETS2"] = "欧洲卡车模拟2",
                ["UdpPort"] = "UDP 端口:",
                ["TargetIp"] = "目标IP地址:",
                ["TargetPort"] = "目标端口:",
                ["ListenPort"] = "监听端口:",
                ["VtsPort"] = "VTube Studio 端口:",
                ["ParamX"] = "参数名 (横向加速度):",
                ["ParamZ"] = "参数名 (前后加速度):",
                ["ForzaInfo"] = "极限竞速模式: 通过 UDP 接收 Forza Horizon 5 数据\n请在 Forza 设置中开启「数据输出」，端口设为上方 UDP 端口",
                ["ACInfo"] = "神力科莎模式: 通过共享内存读取 Assetto Corsa / ACC 数据\n请确保游戏正在运行，程序会自动连接",
                ["ETS2Info"] = "欧卡2模式: 通过共享内存读取 ETS2 / ATS 数据\n需要安装 SCS SDK 插件，程序会自动连接",
                ["SenderInfo"] = "发送端模式: 读取游戏加速度数据，通过 UDP 发送到推流机\n请填写推流机的 IP 地址和端口，确保两台电脑在同一局域网",
                ["ReceiverInfo"] = "接收端模式: 监听 UDP 端口接收游戏主机的加速度数据，注入 VTube Studio\n请确保防火墙已开放监听端口，发送端地址填写本机IP",
                ["Start"] = "启动",
                ["Stop"] = "停止",
                ["Status"] = "状态",
                ["Ready"] = "就绪 - 选择模式后点击「启动」",
                ["Running"] = "运行中",
                ["Stopped"] = "已停止",
                ["Sending"] = "发送中",
                ["Receiving"] = "接收中",
                ["Hint"] = "提示：VTube Studio 需开启「启用 API」，Forza 需设置数据输出端口，AC/ETS2 需运行游戏",
                ["HintSender"] = "提示：发送端运行在游戏主机上，需要游戏正在运行。请确保与推流机在同一局域网",
                ["HintReceiver"] = "提示：接收端运行在推流机上，需要 VTube Studio 开启 API。请确保防火墙已开放端口",
                ["Credit"] = "B站左舷基于TRAE Solo创作，加q群954463059联系",
                ["ErrorInvalidTargetIp"] = "错误: 目标IP地址无效",
                ["ErrorInvalidTargetPort"] = "错误: 目标端口无效",
                ["ErrorInvalidListenPort"] = "错误: 监听端口无效",
                ["SenderRunning"] = "发送中 | 游戏: {0} | 目标: {1}:{2}",
                ["ReceiverRunning"] = "接收中 | 端口: {0} | VTS: {1}",
                ["ConnectingVts"] = "正在连接 VTube Studio...",
                ["CreatingParams"] = "正在创建自定义参数...",
                ["StartingSender"] = "正在启动发送端...",
                ["StartingReceiver"] = "正在启动接收端...",
                ["SenderStarted"] = "发送端已启动",
                ["ReceiverStarted"] = "接收端已启动，等待数据...",
            },
            ["EN"] = new()
            {
                ["Title"] = "RacVTSBridge",
                ["Mode"] = "Mode:",
                ["ModeLocal"] = "Local Mode",
                ["ModeSender"] = "Sender (Gaming PC)",
                ["ModeReceiver"] = "Receiver (Streaming PC)",
                ["GameType"] = "Game Type:",
                ["Forza"] = "Forza",
                ["AC"] = "Assetto Corsa",
                ["ETS2"] = "Euro Truck Simulator 2",
                ["UdpPort"] = "UDP Port:",
                ["TargetIp"] = "Target IP:",
                ["TargetPort"] = "Target Port:",
                ["ListenPort"] = "Listen Port:",
                ["VtsPort"] = "VTube Studio Port:",
                ["ParamX"] = "Param Name (Lateral):",
                ["ParamZ"] = "Param Name (Longitudinal):",
                ["ForzaInfo"] = "Forza Mode: Receive data via UDP from Forza Horizon 5\nPlease enable 'Data Out' in Forza settings with the UDP port above",
                ["ACInfo"] = "Assetto Corsa Mode: Read data via shared memory from AC/ACC\nPlease ensure the game is running",
                ["ETS2Info"] = "ETS2 Mode: Read data via shared memory from ETS2/ATS\nSCS SDK plugin required",
                ["SenderInfo"] = "Sender Mode: Read game acceleration data and send to streaming PC via UDP\nEnter the streaming PC's IP address and port. Both PCs must be on the same network.",
                ["ReceiverInfo"] = "Receiver Mode: Listen for acceleration data from gaming PC and inject into VTube Studio\nMake sure firewall allows the listen port. Gaming PC sends to this PC's IP.",
                ["Start"] = "Start",
                ["Stop"] = "Stop",
                ["Status"] = "Status",
                ["Ready"] = "Ready - Select mode and click Start",
                ["Running"] = "Running",
                ["Stopped"] = "Stopped",
                ["Sending"] = "Sending",
                ["Receiving"] = "Receiving",
                ["Hint"] = "Tip: VTube Studio needs 'Enable API' turned on, Forza needs data output port set, AC/ETS2 needs game running",
                ["HintSender"] = "Tip: Sender runs on gaming PC with the game open. Ensure both PCs are on the same LAN.",
                ["HintReceiver"] = "Tip: Receiver runs on streaming PC with VTube Studio. Ensure firewall allows the port.",
                ["Credit"] = "Contact www.x.com/@zuxin119417 for more information",
                ["ErrorInvalidTargetIp"] = "Error: Invalid target IP address",
                ["ErrorInvalidTargetPort"] = "Error: Invalid target port",
                ["ErrorInvalidListenPort"] = "Error: Invalid listen port",
                ["SenderRunning"] = "Sending | Game: {0} | Target: {1}:{2}",
                ["ReceiverRunning"] = "Receiving | Port: {0} | VTS: {1}",
                ["ConnectingVts"] = "Connecting to VTube Studio...",
                ["CreatingParams"] = "Creating custom parameters...",
                ["StartingSender"] = "Starting sender...",
                ["StartingReceiver"] = "Starting receiver...",
                ["SenderStarted"] = "Sender started",
                ["ReceiverStarted"] = "Receiver started, waiting for data...",
            }
        };

        public MainWindow()
        {
            InitializeComponent();

            _isInitialized = true;

            this.Closing += MainWindow_Closing;
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载保存的配置
            _appConfig = AppConfig.Load();
            ApplyConfigToUI();

            UpdateModeUI();
            UpdateGameInfo();
            UpdateLanguage();
        }

        /// <summary>
        /// 将保存的配置应用到UI控件
        /// </summary>
        private void ApplyConfigToUI()
        {
            // 语言
            _currentLang = string.IsNullOrEmpty(_appConfig.Language) ? "CN" : _appConfig.Language;
            if (LangComboBox != null)
            {
                foreach (ComboBoxItem item in LangComboBox.Items)
                {
                    if (item.Tag is string tag && tag == _currentLang)
                    {
                        LangComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // 运行模式
            _currentMode = string.IsNullOrEmpty(_appConfig.Mode) ? "Local" : _appConfig.Mode;
            if (ModeComboBox != null)
            {
                foreach (ComboBoxItem item in ModeComboBox.Items)
                {
                    if (item.Tag is string tag && tag == _currentMode)
                    {
                        ModeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // 游戏类型
            _currentGame = string.IsNullOrEmpty(_appConfig.GameType) ? "Forza" : _appConfig.GameType;
            if (GameTypeComboBox != null)
            {
                foreach (ComboBoxItem item in GameTypeComboBox.Items)
                {
                    if (item.Tag is string tag && tag == _currentGame)
                    {
                        GameTypeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // Forza UDP 端口
            if (UdpPortTextBox != null)
                UdpPortTextBox.Text = _appConfig.ForzaUdpPort.ToString();

            // VTS 端口
            if (VtsPortTextBox != null)
                VtsPortTextBox.Text = _appConfig.VtsPort.ToString();

            // 参数名
            if (ParamXTextBox != null)
                ParamXTextBox.Text = _appConfig.ParamX;
            if (ParamZTextBox != null)
                ParamZTextBox.Text = _appConfig.ParamZ;

            // 发送端：目标IP
            if (TargetIpTextBox != null)
                TargetIpTextBox.Text = _appConfig.TargetIp;

            // 发送端：目标端口
            if (TargetPortTextBox != null)
                TargetPortTextBox.Text = _appConfig.TargetPort.ToString();

            // 接收端：监听端口
            if (ListenPortTextBox != null)
                ListenPortTextBox.Text = _appConfig.ListenPort.ToString();
        }

        /// <summary>
        /// 从UI收集当前配置并保存
        /// </summary>
        private void SaveConfigFromUI()
        {
            _appConfig.Mode = _currentMode;
            _appConfig.GameType = _currentGame;
            _appConfig.Language = _currentLang;

            // 端口和IP
            if (int.TryParse(UdpPortTextBox?.Text, out int forzaPort))
                _appConfig.ForzaUdpPort = forzaPort;
            if (int.TryParse(VtsPortTextBox?.Text, out int vtsPort))
                _appConfig.VtsPort = vtsPort;
            if (int.TryParse(TargetPortTextBox?.Text, out int targetPort))
                _appConfig.TargetPort = targetPort;
            if (int.TryParse(ListenPortTextBox?.Text, out int listenPort))
                _appConfig.ListenPort = listenPort;

            // IP 地址
            if (!string.IsNullOrWhiteSpace(TargetIpTextBox?.Text))
                _appConfig.TargetIp = TargetIpTextBox.Text.Trim();

            // 参数名
            if (!string.IsNullOrWhiteSpace(ParamXTextBox?.Text))
                _appConfig.ParamX = ParamXTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ParamZTextBox?.Text))
                _appConfig.ParamZ = ParamZTextBox.Text.Trim();

            _appConfig.Save();
        }

        private void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LangComboBox == null) return;

            if (LangComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
            {
                _currentLang = lang;
                UpdateLanguage();
            }
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModeComboBox == null || !_isInitialized) return;

            if (ModeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            {
                _currentMode = mode;
                UpdateModeUI();
                UpdateGameInfo();
                UpdateLanguage();
            }
        }

        private void GameTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameTypeComboBox == null || !_isInitialized) return;

            if (GameTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string game)
            {
                _currentGame = game;

                // 显示/隐藏 UDP 端口输入（仅本地/发送端模式 + Forza）
                if (ForzaPortPanel != null)
                {
                    bool showForzaPort = (_currentMode == "Local" || _currentMode == "Sender") && game == "Forza";
                    ForzaPortPanel.Visibility = showForzaPort ? Visibility.Visible : Visibility.Collapsed;
                }

                UpdateGameInfo();
            }
        }

        /// <summary>
        /// 根据当前模式显示/隐藏各UI元素
        /// </summary>
        private void UpdateModeUI()
        {
            if (!_isInitialized) return;

            bool isLocalOrSender = _currentMode == "Local" || _currentMode == "Sender";
            bool isLocalOrReceiver = _currentMode == "Local" || _currentMode == "Receiver";

            // 游戏类型选择：本地/发送端显示
            if (GameTypePanel != null)
                GameTypePanel.Visibility = isLocalOrSender ? Visibility.Visible : Visibility.Collapsed;

            // Forza UDP 端口：本地/发送端 + Forza 显示
            if (ForzaPortPanel != null)
                ForzaPortPanel.Visibility = isLocalOrSender && _currentGame == "Forza" ? Visibility.Visible : Visibility.Collapsed;

            // 目标IP：发送端显示
            if (TargetIpPanel != null)
                TargetIpPanel.Visibility = _currentMode == "Sender" ? Visibility.Visible : Visibility.Collapsed;

            // 目标端口：发送端显示
            if (TargetPortPanel != null)
                TargetPortPanel.Visibility = _currentMode == "Sender" ? Visibility.Visible : Visibility.Collapsed;

            // 监听端口：接收端显示
            if (ListenPortPanel != null)
                ListenPortPanel.Visibility = _currentMode == "Receiver" ? Visibility.Visible : Visibility.Collapsed;

            // VTS 端口：本地/接收端显示
            if (VtsPortPanel != null)
                VtsPortPanel.Visibility = isLocalOrReceiver ? Visibility.Visible : Visibility.Collapsed;

            // 参数名：本地/接收端显示
            if (ParamXPanel != null)
                ParamXPanel.Visibility = isLocalOrReceiver ? Visibility.Visible : Visibility.Collapsed;
            if (ParamZPanel != null)
                ParamZPanel.Visibility = isLocalOrReceiver ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateLanguage()
        {
            if (!_isInitialized) return;

            var t = _translations[_currentLang];

            // 更新标题
            this.Title = t["Title"];
            if (TitleText != null) TitleText.Text = t["Title"];

            // 更新模式标签
            if (ModeLabel != null) ModeLabel.Text = t["Mode"];

            // 更新模式下拉框选项文本
            if (ModeComboBox != null)
            {
                foreach (ComboBoxItem item in ModeComboBox.Items)
                {
                    if (item.Tag is string tag)
                    {
                        switch (tag)
                        {
                            case "Local": item.Content = t["ModeLocal"]; break;
                            case "Sender": item.Content = t["ModeSender"]; break;
                            case "Receiver": item.Content = t["ModeReceiver"]; break;
                        }
                    }
                }
            }

            // 更新游戏类型标签
            if (GameTypeLabel != null) GameTypeLabel.Text = t["GameType"];

            // 更新游戏类型下拉框选项文本
            if (GameTypeComboBox != null)
            {
                foreach (ComboBoxItem item in GameTypeComboBox.Items)
                {
                    if (item.Tag is string tag)
                    {
                        switch (tag)
                        {
                            case "Forza": item.Content = t["Forza"]; break;
                            case "AC": item.Content = t["AC"]; break;
                            case "ETS2": item.Content = t["ETS2"]; break;
                        }
                    }
                }
            }

            // 更新各输入框标签
            if (UdpPortLabel != null) UdpPortLabel.Text = t["UdpPort"];
            if (TargetIpLabel != null) TargetIpLabel.Text = t["TargetIp"];
            if (TargetPortLabel != null) TargetPortLabel.Text = t["TargetPort"];
            if (ListenPortLabel != null) ListenPortLabel.Text = t["ListenPort"];
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

            // 更新底部提示（根据模式）
            if (HintText != null)
            {
                string hintKey = _currentMode switch
                {
                    "Sender" => "HintSender",
                    "Receiver" => "HintReceiver",
                    _ => "Hint"
                };
                HintText.Text = t[hintKey];
            }

            if (CreditText != null) CreditText.Text = t["Credit"];

            // 更新游戏/模式说明
            UpdateGameInfo();

            // 更新状态（如果不是运行中）
            if (!_isRunning && StatusText != null)
                StatusText.Text = t["Ready"];
        }

        private void UpdateGameInfo()
        {
            if (GameInfoText == null || GameInfoBorder == null || !_isInitialized) return;

            var t = _translations[_currentLang];

            // 接收端模式：显示接收端说明
            if (_currentMode == "Receiver")
            {
                GameInfoText.Text = t["ReceiverInfo"];
                GameInfoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E5F5"));
                GameInfoBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0"));
                GameInfoText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6A1B9A"));
                return;
            }

            // 发送端模式：显示发送端说明
            if (_currentMode == "Sender")
            {
                GameInfoText.Text = t["SenderInfo"];
                GameInfoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF8E1"));
                GameInfoBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
                GameInfoText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8F00"));
                return;
            }

            // 本地模式：显示各游戏说明
            switch (_currentGame)
            {
                case "Forza":
                    GameInfoText.Text = t["ForzaInfo"];
                    GameInfoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
                    GameInfoBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                    GameInfoText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));
                    break;
                case "AC":
                    GameInfoText.Text = t["ACInfo"];
                    GameInfoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                    GameInfoBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    GameInfoText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                    break;
                case "ETS2":
                    GameInfoText.Text = t["ETS2Info"];
                    GameInfoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                    GameInfoBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                    GameInfoText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                    break;
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
            var t = _translations[_currentLang];

            try
            {
                switch (_currentMode)
                {
                    case "Local":
                        await StartLocalMode();
                        break;
                    case "Sender":
                        await StartSenderMode();
                        break;
                    case "Receiver":
                        await StartReceiverMode();
                        break;
                }
            }
            catch (Exception ex)
            {
                SetStatus($"启动失败: {ex.Message}", StatusColor.Error);
                Cleanup();
            }
        }

        /// <summary>
        /// 本地模式：读取游戏数据 + 注入 VTS（原有逻辑）
        /// </summary>
        private async Task StartLocalMode()
        {
            var t = _translations[_currentLang];

            // 读取 VTS 端口
            if (!int.TryParse(VtsPortTextBox.Text, out int vtsPort) || vtsPort < 1 || vtsPort > 65535)
            {
                SetStatus("错误: VTS 端口无效", StatusColor.Error);
                return;
            }

            // Forza 模式需要 UDP 端口
            int udpPort = 5300;
            if (_currentGame == "Forza")
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

            SetStatus(t["ConnectingVts"], StatusColor.Connecting);

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
            SetStatus(t["CreatingParams"], StatusColor.Connecting);
            await CreateCustomParametersAsync();

            // 准备参数注入对象
            _paramValues = new VTSParameterInjectionValue[]
            {
                new VTSParameterInjectionValue { id = _paramNameX, value = 0f, weight = 1f },
                new VTSParameterInjectionValue { id = _paramNameZ, value = 0f, weight = 1f },
            };

            // 启动游戏数据读取
            StartGameDataReader(udpPort);

            _isRunning = true;
            Dispatcher.Invoke(() =>
            {
                StartButton.Content = t["Stop"];
                StartButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                GameTypeComboBox.IsEnabled = false;
                ModeComboBox.IsEnabled = false;
            });

            string modeStr = _currentGame switch
            {
                "Forza" => $"UDP: {udpPort}",
                "AC" => _currentLang == "CN" ? "共享内存" : "Shared Memory",
                "ETS2" => _currentLang == "CN" ? "共享内存" : "Shared Memory",
                _ => ""
            };
            SetStatus($"{t["Running"]} | {modeStr} | VTS: {vtsPort}", StatusColor.Success);
        }

        /// <summary>
        /// 发送端模式：读取游戏数据 + 通过 UDP 发送到推流机
        /// </summary>
        private async Task StartSenderMode()
        {
            var t = _translations[_currentLang];

            // 验证目标 IP
            string targetIp = TargetIpTextBox.Text.Trim();
            if (!IPAddress.TryParse(targetIp, out _))
            {
                SetStatus(t["ErrorInvalidTargetIp"], StatusColor.Error);
                return;
            }

            // 验证目标端口
            if (!int.TryParse(TargetPortTextBox.Text, out int targetPort) || targetPort < 1 || targetPort > 65535)
            {
                SetStatus(t["ErrorInvalidTargetPort"], StatusColor.Error);
                return;
            }

            // Forza 模式需要 UDP 端口
            int udpPort = 5300;
            if (_currentGame == "Forza")
            {
                if (!int.TryParse(UdpPortTextBox.Text, out udpPort) || udpPort < 1 || udpPort > 65535)
                {
                    SetStatus("错误: UDP 端口无效", StatusColor.Error);
                    return;
                }
            }

            SetStatus(t["StartingSender"], StatusColor.Connecting);

            // 创建 UDP 发送器
            _udpSender = new AccelerationUdpSender(targetIp, targetPort);
            _udpSender.Start();

            // 启动游戏数据读取（数据通过 OnAccelerationReceived 转发给 UDP 发送器）
            StartGameDataReader(udpPort);

            _isRunning = true;
            Dispatcher.Invoke(() =>
            {
                StartButton.Content = t["Stop"];
                StartButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                GameTypeComboBox.IsEnabled = false;
                ModeComboBox.IsEnabled = false;
            });

            string status = string.Format(t["SenderRunning"], _currentGame, targetIp, targetPort);
            SetStatus(status, StatusColor.Success);

            await Task.CompletedTask;
        }

        /// <summary>
        /// 接收端模式：监听 UDP 端口 + 注入 VTS
        /// </summary>
        private async Task StartReceiverMode()
        {
            var t = _translations[_currentLang];

            // 验证监听端口
            if (!int.TryParse(ListenPortTextBox.Text, out int listenPort) || listenPort < 1 || listenPort > 65535)
            {
                SetStatus(t["ErrorInvalidListenPort"], StatusColor.Error);
                return;
            }

            // 读取 VTS 端口
            if (!int.TryParse(VtsPortTextBox.Text, out int vtsPort) || vtsPort < 1 || vtsPort > 65535)
            {
                SetStatus("错误: VTS 端口无效", StatusColor.Error);
                return;
            }

            _paramNameX = ParamXTextBox.Text.Trim();
            _paramNameZ = ParamZTextBox.Text.Trim();

            if (string.IsNullOrEmpty(_paramNameX) || string.IsNullOrEmpty(_paramNameZ))
            {
                SetStatus("错误: 参数名不能为空", StatusColor.Error);
                return;
            }

            SetStatus(t["ConnectingVts"], StatusColor.Connecting);

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
            SetStatus(t["CreatingParams"], StatusColor.Connecting);
            await CreateCustomParametersAsync();

            // 准备参数注入对象
            _paramValues = new VTSParameterInjectionValue[]
            {
                new VTSParameterInjectionValue { id = _paramNameX, value = 0f, weight = 1f },
                new VTSParameterInjectionValue { id = _paramNameZ, value = 0f, weight = 1f },
            };

            // 启动 UDP 接收器
            SetStatus(t["StartingReceiver"], StatusColor.Connecting);
            _udpReceiver = new AccelerationUdpReceiver(listenPort);
            _udpReceiver.OnAccelerationReceived += OnAccelerationReceived;
            _udpReceiver.OnStatusChanged += msg => SetStatus(msg, StatusColor.Info);
            _udpReceiver.Start();

            _isRunning = true;
            Dispatcher.Invoke(() =>
            {
                StartButton.Content = t["Stop"];
                StartButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                ModeComboBox.IsEnabled = false;
            });

            string status = string.Format(t["ReceiverRunning"], listenPort, vtsPort);
            SetStatus(status, StatusColor.Success);
        }

        /// <summary>
        /// 启动游戏数据读取器（本地模式和发送端模式共用）
        /// </summary>
        private void StartGameDataReader(int udpPort)
        {
            switch (_currentGame)
            {
                case "Forza":
                    _forzaReceiver = new ForzaUdpReceiver(udpPort);
                    _forzaReceiver.OnAccelerationReceived += OnAccelerationReceived;
                    _forzaReceiver.OnStatusChanged += msg => SetStatus(msg, StatusColor.Info);
                    _forzaReceiver.Start();
                    break;
                case "AC":
                    _acReader = new AssettoCorsaReader();
                    _acReader.OnAccelerationReceived += OnAccelerationReceived;
                    _acReader.OnStatusChanged += msg => SetStatus(msg, StatusColor.Info);
                    _acReader.Start();
                    break;
                case "ETS2":
                    _ets2Reader = new ETS2Reader();
                    _ets2Reader.OnAccelerationReceived += OnAccelerationReceived;
                    _ets2Reader.OnStatusChanged += msg => SetStatus(msg, StatusColor.Info);
                    _ets2Reader.Start();
                    break;
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

            // 清理游戏数据读取器
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

            if (_ets2Reader != null)
            {
                _ets2Reader.OnAccelerationReceived -= OnAccelerationReceived;
                _ets2Reader.Dispose();
                _ets2Reader = null;
            }

            // 清理 UDP 发送器
            if (_udpSender != null)
            {
                _udpSender.Dispose();
                _udpSender = null;
            }

            // 清理 UDP 接收器
            if (_udpReceiver != null)
            {
                _udpReceiver.OnAccelerationReceived -= OnAccelerationReceived;
                _udpReceiver.Dispose();
                _udpReceiver = null;
            }

            // 清理 VTS 连接
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
                GameTypeComboBox.IsEnabled = true;
                ModeComboBox.IsEnabled = true;
                DataText.Text = "";
            });
        }

        private void OnAccelerationReceived(float accelerationX, float accelerationZ)
        {
            // 如果有 UDP 发送器（发送端模式），则通过网络发送
            if (_udpSender != null)
            {
                _udpSender.Send(accelerationX, accelerationZ);
            }

            // 如果有 VTS 插件（本地模式或接收端模式），则注入 VTS
            if (_vtsPlugin != null && _vtsPlugin.IsAuthenticated && _paramValues.Length >= 2)
            {
                _paramValues[0].value = accelerationX;
                _paramValues[1].value = accelerationZ;

                _vtsPlugin.InjectParameterValues(
                    _paramValues,
                    VTSInjectParameterMode.SET,
                    result => { /* 成功，静默处理 */ },
                    error => System.Diagnostics.Debug.WriteLine($"[DEBUG] 发送失败: {error.data?.message}")
                );
            }

            // 更新UI显示
            Dispatcher.BeginInvoke(() =>
            {
                DataText.Text = $"AccelX: {accelerationX:F4}  AccelZ: {accelerationZ:F4}";
            });
        }

        private enum StatusColor { Info, Success, Error, Connecting }

        private void SetStatus(string message, StatusColor color)
        {
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
            // 保存配置
            SaveConfigFromUI();
            Cleanup();
        }
    }
}
