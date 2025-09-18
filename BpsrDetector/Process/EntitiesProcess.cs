using System.Text.Json;
using BpsrDetector.Manager;
using BpsrDetector.Models.Enums;
using BpsrDetector.Utils;
using Google.Protobuf;
using Attribute = Notify.Attribute;

namespace BpsrDetector.Process;

public class EntitiesProcess : Singleton<EntitiesProcess>
{

    private readonly Dictionary<int, string> _entityNames;
    
    public EntitiesProcess()
    {
        try
        {
            string filePath = Path.Combine("tables", "Monster_names.json");
            string jsonData = File.ReadAllText(filePath);
            _entityNames = JsonSerializer.Deserialize<Dictionary<int, string>>(jsonData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载实体名称文件失败: {ex.Message}");
            _entityNames = new Dictionary<int, string>(); 
        }
    }
    
    
    /// <summary>
    /// 处理怪物信息
    /// </summary>
    /// <param name="entityUuid"></param>
    /// <param name="appearInfoAttrs"></param>
    public void ProcessMonsterAttr(ulong entityUuid, AttributeList appearInfoAttrs)
    {
        foreach (AttributeInfo attr in appearInfoAttrs.Attrs)
        {
            if (attr.RawData == null) continue;
            var input = new CodedInputStream(attr.RawData.ToByteArray());
            switch (attr.Id)
            {
                case (int)AttrType.AttrName:
                    var enemyName = input.ReadString();
                    Console.WriteLine("Found monster name " + enemyName);
                    break;
                case (int)AttrType.AttrId:
                    var attrId = input.ReadInt32();
                    Console.WriteLine("Found monster name " + _entityNames[attrId]);
                    break;
            }
        }
    }


    /// <summary>
    /// 处理位置的信息
    /// </summary>
    /// <param name="entityUuid"></param>
    /// <param name="appearInfoAttrs"></param>
    public void ProcessPositionAttr(ulong entityUuid, AttrItem attr)
    {
        var input = new CodedInputStream(attr.RawData.ToByteArray());
        (float x, float y, float z) position = DecodePositionFromStream(input);
        
        // UserDataManager.Instance.PositionX = position.x;
        // UserDataManager.Instance.PositionY = position.y;
        // UserDataManager.Instance.PositionZ = position.z;
        //
        
        // Console.WriteLine($"[当前位置] UUID: {entityUuid}, 位置: {position}");
    }
    

    /// <summary>
    /// 处理位置的信息
    /// </summary>
    /// <param name="entityUuid"></param>
    /// <param name="appearInfoAttrs"></param>
    public void ProcessPositionAttr(ulong entityUuid, Attribute attr)
    {
        var input = new CodedInputStream(attr.Data.ToByteArray());
        (float x, float y, float z) position = DecodePositionFromStream(input);
        if (UserDataManager.Instance.UserId == (long)entityUuid)
        {
            UserDataManager.Instance.PositionX = position.x;
            UserDataManager.Instance.PositionY = position.y;
            UserDataManager.Instance.PositionZ = position.z;
            Console.WriteLine($"[当前位置] UUID: {entityUuid}, 位置: {position}");
        }
    }
    
    
    
    private (float x, float y, float z) DecodePositionFromStream(CodedInputStream input)
    {
        float x = 0, y = 0, z = 0;
        while (!input.IsAtEnd)
        {
            uint tag = input.ReadTag();
            int fieldNumber = WireFormat.GetTagFieldNumber(tag);
            WireFormat.WireType wireType = WireFormat.GetTagWireType(tag);
            if (wireType != WireFormat.WireType.Fixed32)
            {
                input.SkipLastField();
                continue;
            }
            uint rawValue = input.ReadFixed32();
            float floatValue = BitConverter.ToSingle(BitConverter.GetBytes(rawValue), 0);
            switch (fieldNumber)
            {
                case 1:
                    x = floatValue;
                    break;
                case 2:
                    y = floatValue;
                    break;
                case 3:
                    z = floatValue;
                    break;
            }
        }
        return (x, y, z);
    }

    
    
    

    /// <summary>
    /// 处理玩家信息
    /// </summary>
    /// <param name="entityUuid"></param>
    /// <param name="appearInfoAttrs"></param>
    public void ProcessPlayerAttr(ulong entityUuid, AttributeList appearInfoAttrs)
    {
        foreach (AttributeInfo attr in appearInfoAttrs.Attrs)
        {
            try
            {
                // 创建CodedInputStream来读取protobuf数据
                var input = new CodedInputStream(attr.RawData.ToByteArray());

                switch (attr.Id)
                {
                    case (int)AttrType.AttrName:
                        var playerName = input.ReadString();
                        Console.WriteLine("Found player name " + playerName);
                        //this.userDataManager.SetName(playerUid, playerName);
                        break;

                    case (int)AttrType.AttrProfessionId:
                        var professionId = input.ReadInt32();
                        //var professionName = GetProfessionNameFromId(professionId);
                        //this.userDataManager.SetProfession(playerUid, professionName);
                        break;

                    case (int)AttrType.AttrFightPoint:
                        var playerFightPoint = input.ReadInt32();
                        //this.userDataManager.SetFightPoint(playerUid, playerFightPoint);
                        break;

                    case (int)AttrType.AttrLevel:
                        var playerLevel = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "level", playerLevel);
                        break;

                    case (int)AttrType.AttrRankLevel:
                        var playerRankLevel = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "rank_level", playerRankLevel);
                        break;

                    case (int)AttrType.AttrCri:
                        var playerCri = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "cri", playerCri);
                        break;

                    case (int)AttrType.AttrLucky:
                        var playerLucky = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "lucky", playerLucky);
                        break;

                    case (int)AttrType.AttrHp:
                        var playerHp = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "hp", playerHp);
                        break;

                    case (int)AttrType.AttrMaxHp:
                        var playerMaxHp = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "max_hp", playerMaxHp);
                        break;

                    case (int)AttrType.AttrElementFlag:
                        var playerElementFlag = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "element_flag", playerElementFlag);
                        break;

                    case (int)AttrType.AttrEnergyFlag:
                        var playerEnergyFlag = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "energy_flag", playerEnergyFlag);
                        break;

                    case (int)AttrType.AttrReductionLevel:
                        var playerReductionLevel = input.ReadInt32();
                        //this.userDataManager.SetAttrKV(playerUid, "reduction_level", playerReductionLevel);
                        break;

                    default:
                        break;
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"Error processing attribute {attr.Id} for player {entityUuid}: {ex.Message}");
            }
        }
    }


    
}