using BprpDetector.Models.Enums;
using BprpDetector.Utils;
using PacketDotNet;

namespace BprpDetector.core;

public class PacketDispatcher: Singleton<PacketDispatcher>
{

    /// <summary>
    /// 接收到的消息的dispatch
    /// </summary>
    /// <param name="packet"></param>
    public void DoRecvPacketDispatch(Byte[] packet)
    {
        //接收消息
        ByteBuffer byteBuffer = ByteBuffer.From(packet);
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
        }
    }

    private void _dispatchRetnMsg(ByteBuffer buffer, int isZstdCompressed)
    {
        
    }

    private void _dispatchNotifyMsg(ByteBuffer buffer, int isZstdCompressed)
    {
        var serviceUuid = buffer.ReadUInt64();
        var stubId = buffer.ReadUInt32();
        var methodId = buffer.ReadUInt32();
        
        Console.WriteLine("收到消息：" + methodId);
    }


    /// <summary>
    /// 发送到的消息的dispatch
    /// </summary>
    /// <param name="packet"></param>
    public void DoSendPacketDispatch(Byte[] packet)
    {
        //
    }
    
    
    
}