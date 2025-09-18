using BpsrDetector.Utils;

namespace BpsrDetector.Manager;

/// <summary>
/// 用户数据管理
/// </summary>
public class UserDataManager : Singleton<UserDataManager>
{
    /// <summary>
    /// 用户id
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; }
    
    /// <summary>
    /// 坐标X
    /// </summary>
    public float PositionX { get; set; }
    
    /// <summary>
    /// 坐标Y
    /// </summary>
    public float PositionY { get; set; }
    
    /// <summary>
    /// 坐标Z
    /// </summary>
    public float PositionZ { get; set; }
    
    /// <summary>
    /// 角色所在的线路
    /// </summary>
    public uint CurrentLine { get; set; }
    
    /// <summary>
    /// 当前场景 7:平原
    /// </summary>
    public int CurrentScene { get; set; }
}