using BpsrDetector.log;
using ZstdSharp;

namespace BpsrDetector.Utils;

public class PayloadDecompressor
{
    


    public static byte[] DecompressPayloadWithZstdSharp(byte[] buffer)
    {
        try
        {
            using var decompressor = new Decompressor();
            return decompressor.Unwrap(buffer).ToArray();
        }
        catch (Exception ex)
        {
            return null;
        }
    }
    
    public byte[] DecompressPayloadStreamWithZstdSharp(byte[] buffer)
    {
        try
        {
            using var inputStream = new MemoryStream(buffer);
            using var outputStream = new MemoryStream();
            using var decompressionStream = new DecompressionStream(inputStream);
            
            decompressionStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}