using System.Numerics;
using BpsrDetector.core;
using BpsrDetector.Manager;
using BpsrDetector.Models.Enums;
using BpsrDetector.Process;
using BpsrDetector.Utils;
using Google.Protobuf;
using Notify;
using Zproto;
using Attribute = Notify.Attribute;

namespace BpsrDetector.Ctrl;

public class NotifyCtrl : Singleton<NotifyCtrl>
{

    /// <summary>
    /// 同步附近的实体
    /// </summary>
    /// <param name="disappearMessage"></param>
    [MethodId(6)]
    public void SyncNearEntities(AppearDisappearMessage disappearMessage)
    {
        if (disappearMessage.Appear.Count == 0) return;

        foreach (var appearInfo in disappearMessage.Appear)
        {
            var entityUuid = appearInfo.Uuid;
            entityUuid = entityUuid >>> 16;
            var attrCollection = appearInfo.Attrs;
            if (attrCollection is null || attrCollection.Attrs is null) return;
            switch (appearInfo.EntType)
            {
                case (int) EntType.EntMonster:
                    EntitiesProcess.Instance.ProcessMonsterAttr(entityUuid, appearInfo.Attrs);
                    //处理怪物的逻辑
                    break;
                
                case (int) EntType.EntChar:
                    EntitiesProcess.Instance.ProcessPlayerAttr(entityUuid, appearInfo.Attrs);
                    break;
                
            }

        }
    }


    /**
     * 同步Container
     */
    [MethodId(0x00000015)]
    public void SyncContainer(ContainerRetn retn)
    {
        UserDataManager.Instance.UserId = retn.CharSerialize.CharId;
        UserDataManager.Instance.UserName = retn.CharSerialize.CharBase.ToString();
        UserDataManager.Instance.CurrentLine = retn.CharSerialize.SceneData.LineId;
        UserDataManager.Instance.PositionX = retn.CharSerialize.SceneData.Pos.X;
        UserDataManager.Instance.PositionY = retn.CharSerialize.SceneData.Pos.Y;
        UserDataManager.Instance.PositionZ = retn.CharSerialize.SceneData.Pos.Z;
        UserDataManager.Instance.CurrentScene = retn.CharSerialize.SceneData.SceneAreaId;
    }



    /// <summary>
    /// 同步附近的实体变化
    /// </summary>
    /// <param name="DeltaInfoContainer"></param>
    [MethodId(0x0000002d)]
    public void SyncNearDeltaInfo(DeltaInfoContainer deltaInfo)
    {
        ulong targetUuid = deltaInfo.DeltaInfo.Uuid;
        var entityUuid = targetUuid >>> 16;
        foreach (AttrItem attr in deltaInfo.DeltaInfo.Attrs.Attrs)
        {
       
            switch (attr.Id)
            {
                case 52:
                    EntitiesProcess.Instance.ProcessPositionAttr(entityUuid, attr);
                    break;
                case 53:
                    //这个位置暂时不解析
                    break;
            }
        }
        //Console.WriteLine(deltaInfo);
    }


    [MethodId(0x0000002e)]
    public void SyncToMe(DeltaToMeContainer deltaInfo)
    {
        var targetUuid = deltaInfo.Info.Uuid;
        var entityUuid = targetUuid >>> 16;
        if (deltaInfo.Info.BaseDelta.Attrs == null)
        {
            return;
        }
        foreach (Attribute attr in deltaInfo.Info.BaseDelta.Attrs.Attrs)
        {
       
            switch (attr.Id)
            {
                case 52:
                    EntitiesProcess.Instance.ProcessPositionAttr(entityUuid, attr);
                    break;
                case 53:
                    //这个位置暂时不解析
                    break;
            }
        }
        
    }

 

}