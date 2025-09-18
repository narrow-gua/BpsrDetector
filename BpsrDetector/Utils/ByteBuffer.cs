using System.Numerics;

namespace BpsrDetector.Utils;

public class ByteBuffer
{
    private readonly byte[] buffer;
    public int index;

    private ByteBuffer(byte[] buffer, int index)
    {
        this.buffer = buffer;
        this.index = index;
    }

    public static ByteBuffer From(byte[] array)
    {
        return new ByteBuffer(array, 0);
    }

    private byte[] ReadBytesInternal(int length)
    {
        if (index + length > buffer.Length)
            throw new IndexOutOfRangeException("Read exceeds buffer length");

        byte[] result = new byte[length];
        Array.Copy(buffer, index, result, 0, length);
        index += length;
        return result;
    }

    private byte[] PeekBytesInternal(int length)
    {
        if (index + length > buffer.Length)
            throw new IndexOutOfRangeException("Peek exceeds buffer length");

        byte[] result = new byte[length];
        Array.Copy(buffer, index, result, 0, length);
        return result;
    }

    private static ulong ToUInt64BE(byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static uint ToUInt32BE(byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static int ToInt32BE(byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static ushort ToUInt16BE(byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt16(bytes, 0);
    }

    // === 64位 ===
    public ulong ReadUInt64()
    {
        return ToUInt64BE(ReadBytesInternal(8));
    }

    public ulong PeekUInt64()
    {
        return ToUInt64BE(PeekBytesInternal(8));
    }

    // === 32位 ===
    public uint ReadUInt32()
    {
        return ToUInt32BE(ReadBytesInternal(4));
    }

    public uint PeekUInt32()
    {
        return ToUInt32BE(PeekBytesInternal(4));
    }

    public int ReadInt32()
    {
        return ToInt32BE(ReadBytesInternal(4));
    }

    public int PeekInt32()
    {
        return ToInt32BE(PeekBytesInternal(4));
    }

    public uint ReadUInt32LE()
    {
        return BitConverter.ToUInt32(ReadBytesInternal(4), 0); // LE 系统直接OK
    }

    // === 16位 ===
    public ushort ReadUInt16()
    {
        return ToUInt16BE(ReadBytesInternal(2));
    }

    public ushort PeekUInt16()
    {
        return ToUInt16BE(PeekBytesInternal(2));
    }

    // === 8位 ===
    public byte ReadUInt8()
    {
        if (index + 1 > buffer.Length)
            throw new IndexOutOfRangeException("Read exceeds buffer length");

        return buffer[index++];
    }

    // === bytes ===
    public byte[] ReadBytes(int length)
    {
        return ReadBytesInternal(length);
    }

    public int Remaining()
    {
        return buffer.Length - index;
    }

    public byte[] ReadRemaining()
    {
        int length = buffer.Length - index;
        return ReadBytesInternal(length);
    }
}