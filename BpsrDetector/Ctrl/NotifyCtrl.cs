using BpsrDetector.core;
using BpsrDetector.Models.Enums;
using BpsrDetector.Process;
using BpsrDetector.Utils;

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
            entityUuid = entityUuid >> 16;
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
    
    
    
    
    
    
}