using PacketDotNet.Utils;

namespace BpsrDetector.core;
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
    private const int FRAGMENT_TIMEOUT = 30000;
    
    private string currentServer = "";
    private byte[] _data = new byte[0];
    private uint tcpNextSeq = 0;
    private Dictionary<uint, byte[]> tcpCache = new Dictionary<uint, byte[]>();
    private long tcpLastTime = 0;
    private readonly SemaphoreSlim tcpLock = new SemaphoreSlim(1, 1);
    
    private Dictionary<string, FragmentEntry> fragmentIpCache = new Dictionary<string, FragmentEntry>();
    private ConcurrentQueue<byte[]> ethQueue = new ConcurrentQueue<byte[]>();
    
    // 双向TCP缓存
    private Dictionary<uint, byte[]> tcpCacheS2C = new Dictionary<uint, byte[]>(); // 服务器到客户端
    private Dictionary<uint, byte[]> tcpCacheC2S = new Dictionary<uint, byte[]>(); // 客户端到服务器
    private uint tcpNextSeqS2C = 0;
    private uint tcpNextSeqC2S = 0;
    private byte[] _dataS2C = new byte[0];
    private byte[] _dataC2S = new byte[0];
    
    private LibPcapLiveDevice device;
    private CancellationTokenSource cancellationTokenSource;

    public class FragmentEntry
    {
        public List<byte[]> Fragments { get; set; } = new List<byte[]>();
        public long Timestamp { get; set; }
    }

    public List<LibPcapLiveDevice> GetAllNetworkDevices()
    {
        var devices = LibPcapLiveDeviceList.Instance;
        return devices.ToList();
    }

    public async Task StartCapture(int deviceIndex)
    {
        var devices = GetAllNetworkDevices();
        if (deviceIndex < 0 || deviceIndex >= devices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceIndex));
        }

        device = devices[deviceIndex];
        cancellationTokenSource = new CancellationTokenSource();

        // 打开设备
        device.Open(DeviceModes.Promiscuous, 1000);
        
        // 设置过滤器
        device.Filter = "ip and tcp";

        // 设置包捕获回调
        device.OnPacketArrival += OnPacketArrival;

        Console.WriteLine("Welcome!");
        Console.WriteLine("Attempting to find the game server, please wait!");

        // 启动捕获
        device.StartCapture();

        // 启动包处理任务
        _ = Task.Run(ProcessEthPackets, cancellationTokenSource.Token);
        
        // 启动定时清理任务
        _ = Task.Run(CleanupTask, cancellationTokenSource.Token);

        await Task.Delay(-1, cancellationTokenSource.Token);
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        ethQueue.Enqueue(e.Data.ToArray());
    }

    private async Task ProcessEthPackets()
    {
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (ethQueue.TryDequeue(out byte[] frameBuffer))
            {
                await ProcessEthPacket(frameBuffer);
            }
            else
            {
                await Task.Delay(1, cancellationTokenSource.Token);
            }
        }
    }

    private void ClearTcpCache()
    {
        _data = new byte[0];
        tcpNextSeq = 0;
        tcpLastTime = 0;
        tcpCache.Clear();
        
        _dataS2C = new byte[0];
        _dataC2S = new byte[0];
        tcpNextSeqS2C = 0;
        tcpNextSeqC2S = 0;
        tcpCacheS2C.Clear();
        tcpCacheC2S.Clear();
    }

    private byte[] GetTCPPacket(byte[] frameBuffer, int ethOffset)
    {
        // 解析IP包头
        var ipPacket = new IPv4Packet(new ByteArraySegment(frameBuffer, ethOffset, frameBuffer.Length - ethOffset));
        var ipId = ipPacket.Id;
        var isFragment = (ipPacket.FragmentFlags & 0x1) != 0;
        var key = $"{ipId}-{ipPacket.SourceAddress}-{ipPacket.DestinationAddress}-{ipPacket.Protocol}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (isFragment || ipPacket.FragmentOffset > 0)
        {
            if (!fragmentIpCache.ContainsKey(key))
            {
                fragmentIpCache[key] = new FragmentEntry { Timestamp = now };
            }

            var cacheEntry = fragmentIpCache[key];
            var ipBuffer = new byte[ipPacket.TotalLength];
            Array.Copy(frameBuffer, ethOffset, ipBuffer, 0, ipPacket.TotalLength);
            cacheEntry.Fragments.Add(ipBuffer);
            cacheEntry.Timestamp = now;

            // 还有更多分片，等待其余部分
            if (isFragment)
            {
                return null;
            }

            // 收到最后一个分片，重组
            var fragments = cacheEntry.Fragments;
            if (fragments == null)
            {
                Console.WriteLine($"Can't find fragments for {key}");
                return null;
            }

            // 根据偏移量重组分片
            int totalLength = 0;
            var fragmentData = new List<(int offset, byte[] payload)>();

            foreach (var buffer in fragments)
            {
                var ip = new IPv4Packet(new ByteArraySegment(buffer));
                var fragmentOffset = ip.FragmentOffset * 8;
                var payloadLength = ip.TotalLength - ip.HeaderLength;
                var payload = new byte[payloadLength];
                Array.Copy(buffer, ip.HeaderLength, payload, 0, payloadLength);

                fragmentData.Add((fragmentOffset, payload));

                var endOffset = fragmentOffset + payloadLength;
                if (endOffset > totalLength)
                {
                    totalLength = endOffset;
                }
            }

            var fullPayload = new byte[totalLength];
            foreach (var (offset, payload) in fragmentData)
            {
                Array.Copy(payload, 0, fullPayload, offset, payload.Length);
            }

            fragmentIpCache.Remove(key);
            return fullPayload;
        }

        // 返回TCP载荷
        var tcpDataLength = ipPacket.TotalLength - ipPacket.HeaderLength;
        var tcpData = new byte[tcpDataLength];
        Array.Copy(frameBuffer, ethOffset + ipPacket.HeaderLength, tcpData, 0, tcpDataLength);
        return tcpData;
    }

    private bool IsPrivateIP(string ip)
    {
        var parts = ip.Split('.').Select(int.Parse).ToArray();
        var (a, b, c, d) = (parts[0], parts[1], parts[2], parts[3]);

        // 10.0.0.0/8
        if (a == 10) return true;
        // 172.16.0.0/12
        if (a == 172 && b >= 16 && b <= 31) return true;
        // 192.168.0.0/16
        if (a == 192 && b == 168) return true;
        // 127.0.0.0/8 (本地回环)
        if (a == 127) return true;
        // 169.254.0.0/16 (链路本地地址)
        if (a == 169 && b == 254) return true;

        return false;
    }

    private void ClearDataOnServerChange()
    {
        // 这个方法在原代码中被调用但没有定义，这里留空
    }

    private async Task ProcessEthPacket(byte[] frameBuffer)
    {
        try
        {
            var packet = Packet.ParsePacket(LinkLayers.Ethernet, frameBuffer);
            var ethPacket = packet.Extract<EthernetPacket>();
            
            if (ethPacket?.Type != EthernetType.IPv4) return;

            var ipPacket = packet.Extract<IPv4Packet>();
            // if (ipPacket?.Protocol != ProtocolType.IPv4) return;

            var srcaddr = ipPacket.SourceAddress.ToString();
            var dstaddr = ipPacket.DestinationAddress.ToString();

            var tcpBuffer = GetTCPPacket(frameBuffer, 14); // 以太网头部14字节
            if (tcpBuffer == null) return;

            var tcpPacket = packet.Extract<TcpPacket>();
            var buf = tcpPacket.PayloadData ?? new byte[0];

            var srcport = tcpPacket.SourcePort;
            var dstport = tcpPacket.DestinationPort;
            var srcServer = $"{srcaddr}:{srcport} -> {dstaddr}:{dstport}";

            // 判断包的方向
            var isClientPacket = IsPrivateIP(srcaddr);
            var isServerPacket = !isClientPacket;

            await tcpLock.WaitAsync();

            try
            {
                // 服务器识别逻辑
                if (isServerPacket && buf.Length > 0)
                {
                    bool shouldSwitchServer = false;
                    bool isServerPacketFlag = false;

                    try
                    {
                        // 尝试通过小包识别服务器
                        if (buf.Length > 10 && buf[4] == 0)
                        {
                            var data = new byte[buf.Length - 10];
                            Array.Copy(buf, 10, data, 0, data.Length);
                            if (data.Length > 0)
                            {
                                int offset = 0;
                                while (offset + 4 < data.Length)
                                {
                                    var len = BitConverter.ToUInt32(new byte[] { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] }, 0);
                                    if (offset + len > data.Length) break;
                                    
                                    var data1 = new byte[len - 4];
                                    Array.Copy(data, offset + 4, data1, 0, data1.Length);
                                    
                                    var signature = new byte[] { 0x00, 0x63, 0x33, 0x53, 0x42, 0x00 }; //c3SB??
                                    if (data1.Length >= 5 + signature.Length)
                                    {
                                        bool match = true;
                                        for (int i = 0; i < signature.Length; i++)
                                        {
                                            if (data1[5 + i] != signature[i])
                                            {
                                                match = false;
                                                break;
                                            }
                                        }
                                        if (match)
                                        {
                                            isServerPacketFlag = true;
                                            break;
                                        }
                                    }
                                    offset += (int)len;
                                }
                            }
                        }

                        // 尝试通过登录返回包识别服务器
                        if (buf.Length == 0x62)
                        {
                            var signature = new byte[]
                            {
                                0x00, 0x00, 0x00, 0x62,
                                0x00, 0x03,
                                0x00, 0x00, 0x00, 0x01,
                                0x00, 0x11, 0x45, 0x14,
                                0x00, 0x00, 0x00, 0x00,
                                0x0a, 0x4e, 0x08, 0x01, 0x22, 0x24
                            };
                            
                            bool match1 = true;
                            for (int i = 0; i < 10; i++)
                            {
                                if (buf[i] != signature[i])
                                {
                                    match1 = false;
                                    break;
                                }
                            }
                            
                            bool match2 = true;
                            for (int i = 0; i < 6; i++)
                            {
                                if (buf[14 + i] != signature[14 + i])
                                {
                                    match2 = false;
                                    break;
                                }
                            }
                            
                            if (match1 && match2)
                            {
                                isServerPacketFlag = true;
                            }
                        }

                        // 如果检测到服务器特征包，且与当前服务器不同，则切换
                        if (isServerPacketFlag && currentServer != srcServer)
                        {
                            shouldSwitchServer = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error in server detection: {e.Message}");
                    }

                    // 执行服务器切换
                    if (shouldSwitchServer)
                    {
                        currentServer = srcServer;
                        ClearTcpCache();
                        tcpNextSeqS2C = (uint)(tcpPacket.SequenceNumber + buf.Length);
                        tcpNextSeqC2S = 0;
                        ClearDataOnServerChange();
                        Console.WriteLine($"Got Scene Server Address: {srcServer}");
                    }
                }

                // 如果还没有识别到服务器，继续尝试识别但不处理数据
                if (string.IsNullOrEmpty(currentServer))
                {
                    return;
                }

                // 检查包是否属于当前服务器连接
                var serverConnParts = currentServer.Split(new[] { " -> " }, StringSplitOptions.None);
                if (serverConnParts.Length != 2)
                {
                    return;
                }

                var serverAddr = serverConnParts[0];
                var clientAddr = serverConnParts[1];
                var currentServerAddr = $"{srcaddr}:{srcport}";
                var currentClientAddr = $"{dstaddr}:{dstport}";
                var reverseServerAddr = $"{dstaddr}:{dstport}";
                var reverseClientAddr = $"{srcaddr}:{srcport}";

                // 检查是否属于当前连接
                var isServerToClient = (currentServerAddr == serverAddr && currentClientAddr == clientAddr);
                var isClientToServer = (reverseServerAddr == serverAddr && reverseClientAddr == clientAddr);

                if (!isServerToClient && !isClientToServer)
                {
                    return;
                }

                // 选择对应方向的TCP重组状态
                var tcpCacheCurrent = isClientPacket ? tcpCacheC2S : tcpCacheS2C;
                var tcpNextSeqCurrent = isClientPacket ? tcpNextSeqC2S : tcpNextSeqS2C;
                var _dataCurrent = isClientPacket ? _dataC2S : _dataS2C;

                // 处理序列号初始化
                if (tcpNextSeqCurrent == 0)
                {
                    if (isClientPacket)
                    {
                        tcpNextSeqCurrent = (uint)tcpPacket.SequenceNumber;
                        Console.WriteLine($"Initialized client TCP sequence: {tcpNextSeqCurrent}");
                    }
                    else
                    {
                        Console.WriteLine("Unexpected TCP capture error! tcp_next_seq is 0");
                        if (buf.Length > 4)
                        {
                            var testValue = BitConverter.ToUInt32(new byte[] { buf[3], buf[2], buf[1], buf[0] }, 0);
                            if (testValue < 0x0fffff)
                            {
                                tcpNextSeqCurrent = (uint)tcpPacket.SequenceNumber;
                            }
                        }
                    }
                }

                // TCP 重组逻辑
                var seqno = (uint)tcpPacket.SequenceNumber;
                if (((int)(tcpNextSeqCurrent - seqno)) <= 0 || tcpNextSeqCurrent == 0)
                {
                    tcpCacheCurrent[seqno] = buf;
                }

                while (tcpCacheCurrent.ContainsKey(tcpNextSeqCurrent))
                {
                    var seq = tcpNextSeqCurrent;
                    var cachedTcpData = tcpCacheCurrent[seq];
                    
                    if (_dataCurrent.Length == 0)
                    {
                        _dataCurrent = cachedTcpData;
                    }
                    else
                    {
                        var newData = new byte[_dataCurrent.Length + cachedTcpData.Length];
                        Array.Copy(_dataCurrent, 0, newData, 0, _dataCurrent.Length);
                        Array.Copy(cachedTcpData, 0, newData, _dataCurrent.Length, cachedTcpData.Length);
                        _dataCurrent = newData;
                    }
                    
                    tcpNextSeqCurrent = seq + (uint)cachedTcpData.Length;
                    tcpCacheCurrent.Remove(seq);
                    tcpLastTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }

                // 更新对应方向的状态
                if (isClientPacket)
                {
                    tcpNextSeqC2S = tcpNextSeqCurrent;
                    _dataC2S = _dataCurrent;
                }
                else
                {
                    tcpNextSeqS2C = tcpNextSeqCurrent;
                    _dataS2C = _dataCurrent;
                }

                // 处理完整的数据包
                while (_dataCurrent.Length > 4)
                {
                    var packetSize = BitConverter.ToUInt32(new byte[] { _dataCurrent[3], _dataCurrent[2], _dataCurrent[1], _dataCurrent[0] }, 0);

                    if (_dataCurrent.Length < packetSize) break;

                    if (_dataCurrent.Length >= packetSize)
                    {
                        var packetData = new byte[packetSize];
                        Array.Copy(_dataCurrent, 0, packetData, 0, (int)packetSize);
                        
                        var remainingData = new byte[_dataCurrent.Length - packetSize];
                        Array.Copy(_dataCurrent, (int)packetSize, remainingData, 0, remainingData.Length);
                        _dataCurrent = remainingData;

                        try
                        {
                            // 这里应该调用PacketProcessor，但由于没有具体实现，所以注释掉
                            // var processor = new PacketProcessor(logger, userDataManager, lineInfo);
                            PacketDispatcher packetDispatcher = PacketDispatcher.Instance;
                            if (isClientPacket)
                            {
                                packetDispatcher.DoSendPacketDispatch(packetData);
                            }
                            else
                            {
                                packetDispatcher.DoRecvPacketDispatch(packetData);
                            }
                            
                            // Console.WriteLine($"Processing packet: {(isClientPacket ? "Client->Server" : "Server->Client")}, Size: {packetSize}");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error processing packet: {e.Message}");
                        }
                    }
                    else if (packetSize > 0x0fffff)
                    {
                        Console.WriteLine($"Invalid Length!! {_dataCurrent.Length},{packetSize},{BitConverter.ToString(_dataCurrent)},{tcpNextSeqCurrent}");
                        Environment.Exit(1);
                        break;
                    }
                }

                // 更新处理后的数据缓存
                if (isClientPacket)
                {
                    _dataC2S = _dataCurrent;
                }
                else
                {
                    _dataS2C = _dataCurrent;
                }
            }
            finally
            {
                tcpLock.Release();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in ProcessEthPacket: {e.Message}");
        }
    }

    private async Task CleanupTask()
    {
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var clearedFragments = 0;
                
                var keysToRemove = new List<string>();
                foreach (var kvp in fragmentIpCache)
                {
                    if (now - kvp.Value.Timestamp > FRAGMENT_TIMEOUT)
                    {
                        keysToRemove.Add(kvp.Key);
                        clearedFragments++;
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    fragmentIpCache.Remove(key);
                }
                
                if (clearedFragments > 0)
                {
                    Console.WriteLine($"Cleared {clearedFragments} expired IP fragment caches");
                }

                if (tcpLastTime > 0 && now - tcpLastTime > FRAGMENT_TIMEOUT)
                {
                    currentServer = "";
                    ClearTcpCache();
                }
                
                await Task.Delay(10000, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
        device?.StopCapture();
        device?.Close();
        tcpLock?.Dispose();
    }
}