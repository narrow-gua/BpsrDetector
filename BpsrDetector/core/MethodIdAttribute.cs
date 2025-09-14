namespace BpsrDetector.core;

[AttributeUsage(AttributeTargets.Method)]
public class MethodIdAttribute : Attribute
{
    public uint MethodId { get; }

    public MethodIdAttribute(uint methodId)
    {
        MethodId = methodId;
    }
}