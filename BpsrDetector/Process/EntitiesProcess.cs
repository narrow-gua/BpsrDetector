using BpsrDetector.Models.Enums;
using BpsrDetector.Utils;
using Google.Protobuf;

namespace BpsrDetector.Process;

public class EntitiesProcess : Singleton<EntitiesProcess>
{
    /// <summary>
    /// 处理怪物信息
    /// </summary>
    /// <param name="entityUuid"></param>
    /// <param name="appearInfoAttrs"></param>
    public void ProcessMonsterAttr(ulong entityUuid, AttributeList appearInfoAttrs)
    {
        
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