using BpsrDetector.core;
using BpsrDetector.Utils;

namespace BpsrDetector.Ctrl;

public class RetnCtrl : Singleton<RetnCtrl>
{
    
    /// <summary>
    /// 根据特征拿取分线列表
    /// </summary>
    /// <param name="lineListResponse"></param>
    [MethodId(705167632)]
    public void LineListRetn(LineListResponse lineListResponse)
    {
        Console.WriteLine(lineListResponse);
    }

    
    
    
    
}