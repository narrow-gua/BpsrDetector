namespace BprpDetector.Utils;

public abstract class Singleton<T> where T : class, new()
{
    private static readonly object lockObject = new object();
    private static T instance;

    
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                lock (lockObject)
                {
                    if (instance == null)
                    {
                        instance = new T();
                    }
                }
            }
            return instance;
        }
    }

    protected Singleton()
    {
        // 防止通过反射创建实例
        if (instance != null)
        {
            throw new InvalidOperationException("不能创建多个单例实例");
        }
    }
}