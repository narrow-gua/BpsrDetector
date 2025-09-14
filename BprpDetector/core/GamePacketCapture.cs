namespace BprpDetector.core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using System.IO;
using System.Collections.Concurrent;
public class GamePacketCapture
{
    private readonly ILogger _logger;
    private readonly object _tcpLock = new object();
    private readonly Dictionary<string, IpFragmentCache> _fragmentIpCache = new Dictionary<string, IpFragmentCache>();
    private readonly Dictionary<uint, byte[]> _tcpCacheS2C = new Dictionary<uint, byte[]>(); // 服务器到客户端
    private readonly Dictionary<uint, byte[]> _tcpCacheC2S = new Dictionary<uint, byte[]>(); // 客户端到服务器
    
    private string _currentServer = string.Empty;
    private byte[] _dataS2C = new byte[0];
    private byte[] _dataC2S = new byte[0];
    private uint _tcpNextSeqS2C = uint.MaxValue;
    private uint _tcpNextSeqC2S = uint.MaxValue;
    private DateTime _tcpLastTime = DateTime.Now;
    
    private const int FRAGMENT_TIMEOUT = 30000; // 30秒
    private const int TCP_TIMEOUT = 30000; // 30秒
    private const int BUFFER_SIZE = 10 * 1024 * 1024; // 10MB
    
    private ICaptureDevice _device;
    private Timer _cleanupTimer;
    private CancellationTokenSource _cancellationTokenSource;
    
    public class IpFragmentCache
    {
        public List<byte[]> Fragments { get; set; } = new List<byte[]>();
        public DateTime Timestamp { get; set; }
    }
    
    
    
    // 获取所有网卡
    public List<LibPcapLiveDevice> GetAllNetworkDevices()
    {
        var devices = LibPcapLiveDeviceList.Instance.ToList();
        
        _logger.Info("Available network devices:");
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            _logger.Info($"{i}: {device.Name} - {device.Description}");
            
            if (device.Addresses?.Count > 0)
            {
                foreach (var addr in device.Addresses)
                {
                    if (addr.Addr?.ipAddress != null)
                    {
                        _logger.Info($"    IP: {addr.Addr.ipAddress}");
                    }
                }
            }
        }
        
        return devices;
    }

    // 开始监听指定网卡
    public async Task StartCapture(int deviceIndex)
    {
        var devices = GetAllNetworkDevices();
        
        if (deviceIndex < 0 || deviceIndex >= devices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceIndex), "Invalid device index");
        }

        _device = devices[deviceIndex];
        _logger.Info($"Starting capture on device: {_device.Description}");

        try
        {
            // 打开设备
            _device.Open(DeviceMode.Promiscuous, 1000);
            
            // 设置过滤器：只捕获TCP和IP包
            _device.Filter = "ip and tcp";
            
            // 设置数据包到达事件处理
            _device.OnPacketArrival += Device_OnPacketArrival;
            
            _logger.Info("Attempting to find the game server, please wait!");
            
            // 开始捕获
            _device.StartCapture();
            
            // 保持运行直到取消
            await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Capture stopped by user");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error during capture: {ex.Message}");
        }
        finally
        {
            StopCapture();
        }
    }

    // 停止抓包
    public void StopCapture()
    {
        _device?.StopCapture();
        _device?.Close();
        _cancellationTokenSource?.Cancel();
        _cleanupTimer?.Dispose();
    }

    // 数据包到达事件处理
    private void Device_OnPacketArrival(object sender, CaptureEventArgs e)
    {
        try
        {
            ProcessEthPacket(e.Packet.Data);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing packet: {ex.Message}");
        }
    }

    // 处理以太网数据包
    private void ProcessEthPacket(byte[] frameBuffer)
    {
        var packet = Packet.ParsePacket(LinkLayers.Ethernet, frameBuffer);
        var ethernetPacket = packet.Extract<EthernetPacket>();
        
        if (ethernetPacket?.PayloadPacket is not IpPacket ipPacket) return;

        var tcpBuffer = GetTCPPacket(frameBuffer, ethernetPacket, ipPacket);
        if (tcpBuffer == null) return;

        var tcpPacket = TcpPacket.ParsePacket(LinkLayers.Raw, tcpBuffer).Extract<TcpPacket>();
        if (tcpPacket == null) return;

        var payload = tcpPacket.PayloadData ?? new byte[0];
        var srcAddr = ipPacket.SourceAddress.ToString();
        var dstAddr = ipPacket.DestinationAddress.ToString();
        var srcPort = tcpPacket.SourcePort;
        var dstPort = tcpPacket.DestinationPort;
        var srcServer = $"{srcAddr}:{srcPort} -> {dstAddr}:{dstPort}";

        // 判断包的方向
        var isClientPacket = IsPrivateIP(srcAddr);
        var isServerPacket = !isClientPacket;

        lock (_tcpLock)
        {
            // 服务器识别逻辑
            if (isServerPacket && payload.Length > 0)
            {
                bool shouldSwitchServer = false;
                bool isGameServerPacket = false;

                try
                {
                    // 尝试通过小包识别服务器
                    if (payload.Length > 10 && payload[4] == 0)
                    {
                        var data = payload.Skip(10).ToArray();
                        if (data.Length > 0)
                        {
                            isGameServerPacket = DetectServerBySmallPacket(data);
                        }
                    }

                    // 尝试通过登录返回包识别服务器
                    if (!isGameServerPacket && payload.Length == 0x62)
                    {
                        isGameServerPacket = DetectServerByLoginResponse(payload);
                    }

                    // 如果检测到服务器特征包，且与当前服务器不同，则切换
                    if (isGameServerPacket && _currentServer != srcServer)
                    {
                        shouldSwitchServer = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Error in server detection: {ex.Message}");
                }

                // 执行服务器切换
                if (shouldSwitchServer)
                {
                    _currentServer = srcServer;
                    ClearTcpCache();
                    _tcpNextSeqS2C = tcpPacket.SequenceNumber + (uint)payload.Length;
                    _tcpNextSeqC2S = uint.MaxValue;
                    _logger.Info($"Got Scene Server Address: {srcServer}");
                }
            }

            // 如果还没有识别到服务器，不处理数据
            if (string.IsNullOrEmpty(_currentServer)) return;

            // 检查包是否属于当前服务器连接
            if (!IsCurrentServerConnection(srcAddr, srcPort, dstAddr, dstPort)) return;

            // 选择对应方向的TCP重组状态
            var tcpCacheCurrent = isClientPacket ? _tcpCacheC2S : _tcpCacheS2C;
            var tcpNextSeqCurrent = isClientPacket ? _tcpNextSeqC2S : _tcpNextSeqS2C;
            var dataCurrent = isClientPacket ? _dataC2S : _dataS2C;

            // 处理序列号初始化
            if (tcpNextSeqCurrent == uint.MaxValue)
            {
                if (isClientPacket)
                {
                    tcpNextSeqCurrent = tcpPacket.SequenceNumber;
                    _logger.Debug($"Initialized client TCP sequence: {tcpNextSeqCurrent}");
                }
                else
                {
                    _logger.Error("Unexpected TCP capture error! tcp_next_seq is -1");
                    if (payload.Length > 4)
                    {
                        var packetSize = BitConverter.ToUInt32(payload.Take(4).Reverse().ToArray(), 0);
                        if (packetSize < 0x0fffff)
                        {
                            tcpNextSeqCurrent = tcpPacket.SequenceNumber;
                        }
                    }
                }
            }

            // TCP 重组逻辑
            var seqDiff = (int)(tcpNextSeqCurrent - tcpPacket.SequenceNumber);
            if (seqDiff <= 0 || tcpNextSeqCurrent == uint.MaxValue)
            {
                tcpCacheCurrent[tcpPacket.SequenceNumber] = payload;
            }

            while (tcpCacheCurrent.ContainsKey(tcpNextSeqCurrent))
            {
                var seq = tcpNextSeqCurrent;
                var cachedTcpData = tcpCacheCurrent[seq];
                dataCurrent = CombineByteArrays(dataCurrent, cachedTcpData);
                tcpNextSeqCurrent = seq + (uint)cachedTcpData.Length;
                tcpCacheCurrent.Remove(seq);
                _tcpLastTime = DateTime.Now;
            }

            // 更新对应方向的状态
            if (isClientPacket)
            {
                _tcpNextSeqC2S = tcpNextSeqCurrent;
                _dataC2S = dataCurrent;
            }
            else
            {
                _tcpNextSeqS2C = tcpNextSeqCurrent;
                _dataS2C = dataCurrent;
            }

            // 处理完整的数据包
            ProcessCompletePackets(ref dataCurrent, isClientPacket);

            // 更新处理后的数据缓存
            if (isClientPacket)
            {
                _dataC2S = dataCurrent;
            }
            else
            {
                _dataS2C = dataCurrent;
            }
        }
    }
}