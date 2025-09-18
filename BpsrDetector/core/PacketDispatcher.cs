using BpsrDetector.Ctrl;
using BpsrDetector.Models.Enums;
using BpsrDetector.Utils;
using PacketDotNet;
using ZstdSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BpsrDetector.core;
using BpsrDetector.Ctrl;
using BpsrDetector.Models.Enums;
using BpsrDetector.Utils;
using Google.Protobuf;

namespace BpsrDetector.core
{
/// <summary>
    /// 控制器类型枚举
    /// </summary>
    public enum ControllerType
    {
        Notify,
        Return,
        Send
    }

    /// <summary>
    /// 方法缓存信息
    /// </summary>
    public class MethodCacheInfo
    {
        public MethodInfo Method { get; set; }
        public Type ParameterType { get; set; }
        public PropertyInfo ParserProperty { get; set; }
        public object ControllerInstance { get; set; }
        public ControllerType ControllerType { get; set; }
    }

    public class PacketDispatcher : Singleton<PacketDispatcher>
    {
        // 为不同控制器类型缓存方法信息
        private static readonly ConcurrentDictionary<uint, MethodCacheInfo> _notifyMethodCache = new();
        private static readonly ConcurrentDictionary<uint, MethodCacheInfo> _returnMethodCache = new();
        private static readonly ConcurrentDictionary<uint, MethodCacheInfo> _sendMethodCache = new();
        
        private static readonly object _cacheLock = new object();
        private static bool _isCacheInitialized = false;

        /// <summary>
        /// 初始化所有控制器的方法缓存
        /// </summary>
        private void InitializeMethodCache()
        {
            if (_isCacheInitialized) return;

            lock (_cacheLock)
            {
                if (_isCacheInitialized) return;

                try
                {
                    // 清空现有缓存
                    _notifyMethodCache.Clear();
                    _returnMethodCache.Clear();
                    _sendMethodCache.Clear();

                    // 缓存NotifyCtrl的方法
                    CacheControllerMethods<NotifyCtrl>(NotifyCtrl.Instance, _notifyMethodCache, ControllerType.Notify);
                    
                    // 缓存RetnCtrl的方法
                    CacheControllerMethods<RetnCtrl>(RetnCtrl.Instance, _returnMethodCache, ControllerType.Return);
                    
                    // 缓存SendCtrl的方法
                    CacheControllerMethods<SendCtrl>(SendCtrl.Instance, _sendMethodCache, ControllerType.Send);

                    _isCacheInitialized = true;
                    
                    Console.WriteLine($"Method cache initialized:");
                    Console.WriteLine($"  - NotifyCtrl: {_notifyMethodCache.Count} methods");
                    Console.WriteLine($"  - RetnCtrl: {_returnMethodCache.Count} methods");
                    Console.WriteLine($"  - SendCtrl: {_sendMethodCache.Count} methods");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing method cache: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 缓存指定控制器类型的方法
        /// </summary>
        private void CacheControllerMethods<T>(T controllerInstance, ConcurrentDictionary<uint, MethodCacheInfo> cache, ControllerType controllerType)
        {
            try
            {
                var controllerType_Type = typeof(T);
                var methods = controllerType_Type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (var method in methods)
                {
                    var methodIdAttr = method.GetCustomAttribute<MethodIdAttribute>();
                    if (methodIdAttr != null)
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1)
                        {
                            var paramType = parameters[0].ParameterType;
                            
                            // 查找Parser属性
                            var parserProperty = paramType.GetProperty("Parser", 
                                BindingFlags.Public | BindingFlags.Static);

                            if (parserProperty != null)
                            {
                                var cacheInfo = new MethodCacheInfo
                                {
                                    Method = method,
                                    ParameterType = paramType,
                                    ParserProperty = parserProperty,
                                    ControllerInstance = controllerInstance,
                                    ControllerType = controllerType
                                };

                                cache[methodIdAttr.MethodId] = cacheInfo;
                                
                                Console.WriteLine($"Cached method {method.Name} with MethodId {methodIdAttr.MethodId} for {controllerType}Ctrl");
                            }
                            else
                            {
                                Console.WriteLine($"Warning: No Parser property found for parameter type {paramType.Name} in method {method.Name}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Method {method.Name} has {parameters.Length} parameters, expected 1");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching methods for {typeof(T).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 通用的消息处理方法
        /// </summary>
        private void ProcessMessage(uint methodId, byte[] msgPayload, ConcurrentDictionary<uint, MethodCacheInfo> methodCache, string messageType)
        {
            try
            {
                if (methodCache.TryGetValue(methodId, out var cacheInfo))
                {
                    try
                    {
                        // 获取Parser实例
                        var parser = cacheInfo.ParserProperty.GetValue(null);
                        
                        // 调用ParseFrom方法
                        var parseFromMethod = parser.GetType().GetMethod("ParseFrom", new[] { typeof(byte[]) });
                        if (parseFromMethod != null)
                        {
                            var parsedMessage = parseFromMethod.Invoke(parser, new object[] { msgPayload });
                            
                            // 调用对应的处理方法
                            cacheInfo.Method.Invoke(cacheInfo.ControllerInstance, new object[] { parsedMessage });
                            
                            //Console.WriteLine($"Successfully processed {messageType} message for methodId: {methodId} in {cacheInfo.ControllerType}Ctrl");
                        }
                        else
                        {
                            Console.WriteLine($"ParseFrom method not found for methodId: {methodId} in {messageType}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error invoking method for methodId {methodId} in {messageType}: {ex.Message}");
                    }
                }
                else
                {
                    //Console.WriteLine($"No {messageType} handler found for methodId: {methodId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {messageType} message: {ex.Message}");
            }
        }

        /// <summary>
        /// 接收到的消息的dispatch
        /// </summary>
        /// <param name="packet"></param>
        public void DoRecvPacketDispatch(Byte[] packet)
        {
            //接收消息
            ByteBuffer byteBuffer = ByteBuffer.From(packet);

            while (byteBuffer.Remaining() > 0)
            {
                uint packetSize = byteBuffer.PeekUInt32();
                if (packetSize < 6)
                {
                    return;
                }
                ByteBuffer buffer = ByteBuffer.From(byteBuffer.ReadBytes((int)packetSize));
                packetSize = buffer.ReadUInt32();
                var packetType = buffer.ReadUInt16();
                var isZstdCompressed = packetType & 0x8000;
                var msgTypeId = packetType & 0x7fff;
                switch (msgTypeId)
                {
                    case (int)MessageType.Notify:
                        this._dispatchNotifyMsg(buffer, isZstdCompressed);
                        break;
                    case (int)MessageType.Return:
                        this._dispatchRetnMsg(buffer, isZstdCompressed);
                        break;
                    case (int)MessageType.FrameDown:
                        var serverSequenceId = buffer.ReadUInt32();
                        if (buffer.Remaining() == 0) break;
                        var nestedPacket = buffer.ReadRemaining();
                        if (isZstdCompressed != 0)
                        {
                            nestedPacket = PayloadDecompressor.DecompressPayloadWithZstdSharp(nestedPacket);
                        }
                        DoRecvPacketDispatch(nestedPacket);
                        break;
                    default:
                        //需要测别的类型的时候把下面注释打开
                        //Console.WriteLine($"Ignore recv packet with message type {msgTypeId}.");
                        break;
                }
            }
        }

        /// <summary>
        /// 处理Return类型消息 - 使用RetnCtrl
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="isZstdCompressed"></param>
        private void _dispatchRetnMsg(ByteBuffer buffer, int isZstdCompressed)
        {
            try
            {
                //正确解析方式，但是没有MethodId，猜测可能是通过sequenceId去对应，这里直接就抓关键Feature了
                // var methodId = buffer.ReadUInt32();
                // var sequenceId = buffer.ReadUInt32();
                // var stufId = buffer.ReadUInt32();
                
                //根据feature推算
                var serviceUuid = buffer.ReadUInt64();
                var stubId = buffer.ReadUInt32();
                var stuf = buffer.ReadUInt32();
                var feature = buffer.ReadUInt32();
                buffer.index = 18;
                
                //到这的偏移应该是 18字节 -> 0A 应该是protobuf的协议一部分
                var msgPayload = buffer.ReadRemaining();

                // 如果有压缩，先解压
                if (isZstdCompressed != 0)
                {
                    msgPayload = PayloadDecompressor.DecompressPayloadWithZstdSharp(msgPayload);
                }
                
                // 确保缓存已初始化
                InitializeMethodCache();

                // 使用RetnCtrl处理返回消息
                ProcessMessage(feature, msgPayload, _returnMethodCache, "Return");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing return message: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理Notify类型消息 - 使用NotifyCtrl
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="isZstdCompressed"></param>
        private void _dispatchNotifyMsg(ByteBuffer buffer, int isZstdCompressed)
        {
            try
            {
                var serviceUuid = buffer.ReadUInt64();
                var stubId = buffer.ReadUInt32();
                var methodId = buffer.ReadUInt32();
                var msgPayload = buffer.ReadRemaining();

                // 如果有压缩，先解压
                if (isZstdCompressed != 0)
                {
                    msgPayload = PayloadDecompressor.DecompressPayloadWithZstdSharp(msgPayload);
                }

                // Console.WriteLine($"Received Notify message - ServiceUuid: {serviceUuid:X}, " +
                //                 $"StubId: {stubId}, MethodId: {methodId}");

                // 确保缓存已初始化
                InitializeMethodCache();

                // 使用NotifyCtrl处理通知消息
                ProcessMessage(methodId, msgPayload, _notifyMethodCache, "Notify");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error processing notify message: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送消息的dispatch
        /// </summary>
        /// <param name="packet"></param>
        public void DoSendPacketDispatch(Byte[] packet)
        {
            try
            {
                // 解析发送的数据包
                ByteBuffer byteBuffer = ByteBuffer.From(packet);

                while (byteBuffer.Remaining() > 0)
                {
                    uint packetSize = byteBuffer.PeekUInt32();
                    if (packetSize < 6)
                    {
                        return;
                    }

                    ByteBuffer buffer = ByteBuffer.From(byteBuffer.ReadBytes((int)packetSize));
                    packetSize = buffer.ReadUInt32();
                    var packetType = buffer.ReadUInt16();
                    var isZstdCompressed = packetType & 0x8000;
                    var msgTypeId = packetType & 0x7fff;

                    //Console.WriteLine($"Sending packet - Type: {msgTypeId}, Size: {packetSize}, Compressed: {isZstdCompressed != 0}");

                    switch (msgTypeId)
                    {
                        case (int)MessageType.Call:
                            this._processSendCallMsg(buffer, isZstdCompressed);
                            break;
                        case (int)MessageType.FrameUp:
                            this._processSendFrameUpMsg(buffer, isZstdCompressed);
                            break;
                        case (int)MessageType.Notify:
                            this._processSendNotifyMsg(buffer, isZstdCompressed);
                            break;
                        default:
                            // Console.WriteLine($"Unknown send message type: {msgTypeId}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing send packet: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理发送的Call消息 - 使用SendCtrl
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="isZstdCompressed"></param>
        private void _processSendCallMsg(ByteBuffer buffer, int isZstdCompressed)
        {
            try
            {
                var serviceUuid = buffer.ReadUInt64();
                var stubId = buffer.ReadUInt32();
                var methodId = buffer.ReadUInt32();
                var sequenceId = buffer.ReadUInt32();
                var msgPayload = buffer.ReadRemaining();

                if (isZstdCompressed != 0)
                {
                    msgPayload = PayloadDecompressor.DecompressPayloadWithZstdSharp(msgPayload);
                }

                // Console.WriteLine($"Send Call - ServiceUuid: {serviceUuid:X}, StubId: {stubId}, " +
                //                 $"MethodId: {methodId}, SequenceId: {sequenceId}");

                // 确保缓存已初始化
                InitializeMethodCache();

                // 使用SendCtrl处理发送的Call消息
                ProcessMessage(methodId, msgPayload, _sendMethodCache, "Send");

                // 记录发送的请求
                _trackSentRequest(serviceUuid, stubId, methodId, sequenceId, msgPayload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing send call message: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理发送的FrameUp消息
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="isZstdCompressed"></param>
        private void _processSendFrameUpMsg(ByteBuffer buffer, int isZstdCompressed)
        {
            try
            {
                var serverSequenceId = buffer.ReadUInt32();
                if (buffer.Remaining() == 0) return;

                var nestedPacket = buffer.ReadRemaining();
                if (isZstdCompressed != 0)
                {
                    nestedPacket = PayloadDecompressor.DecompressPayloadWithZstdSharp(nestedPacket);
                }

                // Console.WriteLine($"Send FrameUp - ServerSequenceId: {serverSequenceId}");
                
                // 递归处理嵌套数据包
                DoSendPacketDispatch(nestedPacket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing send frame up message: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理发送的Notify消息 - 使用SendCtrl
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="isZstdCompressed"></param>
        private void _processSendNotifyMsg(ByteBuffer buffer, int isZstdCompressed)
        {
            try
            {
                var serviceUuid = buffer.ReadUInt64();
                var stubId = buffer.ReadUInt32();
                var methodId = buffer.ReadUInt32();
                var msgPayload = buffer.ReadRemaining();

                if (isZstdCompressed != 0)
                {
                    msgPayload = PayloadDecompressor.DecompressPayloadWithZstdSharp(msgPayload);
                }

                //Console.WriteLine($"Send Notify - ServiceUuid: {serviceUuid:X}, StubId: {stubId}, MethodId: {methodId}");

                // 确保缓存已初始化
                InitializeMethodCache();

                // 使用SendCtrl处理发送的Notify消息
                ProcessMessage(methodId, msgPayload, _sendMethodCache, "Send");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing send notify message: {ex.Message}");
            }
        }

        /// <summary>
        /// 跟踪发送的请求
        /// </summary>
        /// <param name="serviceUuid"></param>
        /// <param name="stubId"></param>
        /// <param name="methodId"></param>
        /// <param name="sequenceId"></param>
        /// <param name="msgPayload"></param>
        private void _trackSentRequest(ulong serviceUuid, uint stubId, uint methodId, uint sequenceId, byte[] msgPayload)
        {
            // TODO: 实现请求跟踪逻辑
            // 可以保存请求信息，用于后续匹配返回消息
            //Console.WriteLine($"Tracking request - SequenceId: {sequenceId}, MethodId: {methodId}");
        }

        /// <summary>
        /// 手动刷新方法缓存（用于开发调试）
        /// </summary>
        public void RefreshMethodCache()
        {
            lock (_cacheLock)
            {
                _isCacheInitialized = false;
                InitializeMethodCache();
                Console.WriteLine("Method cache refreshed manually.");
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public void PrintCacheStats()
        {
            Console.WriteLine("=== Method Cache Statistics ===");
            Console.WriteLine($"NotifyCtrl methods: {_notifyMethodCache.Count}");
            Console.WriteLine($"RetnCtrl methods: {_returnMethodCache.Count}");
            Console.WriteLine($"SendCtrl methods: {_sendMethodCache.Count}");
            Console.WriteLine($"Cache initialized: {_isCacheInitialized}");
            
            if (_notifyMethodCache.Any())
            {
                Console.WriteLine("NotifyCtrl method IDs: " + string.Join(", ", _notifyMethodCache.Keys));
            }
            if (_returnMethodCache.Any())
            {
                Console.WriteLine("RetnCtrl method IDs: " + string.Join(", ", _returnMethodCache.Keys));
            }
            if (_sendMethodCache.Any())
            {
                Console.WriteLine("SendCtrl method IDs: " + string.Join(", ", _sendMethodCache.Keys));
            }
        }

        /// <summary>
        /// 测试方法 - 处理同步附近实体
        /// </summary>
        /// <param name="msgPayload"></param>
        private void _processSyncNearEntities(byte[] msgPayload)
        {
            AppearDisappearMessage nearAppear = AppearDisappearMessage.Parser.ParseFrom(msgPayload);
            Console.WriteLine(nearAppear.Appear);
        }
    }
}