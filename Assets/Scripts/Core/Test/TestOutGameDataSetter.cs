using System.Text;
using UnityEngine;

// 인게임에서 받은 시작 설정을 로그로 확인하는 테스트용 구현체입니다.
public class TestOutGameDataSetter : IOutGameDataSetter
{
    public void SetOutGameData(OutGameStartContext context)
    {
        Debug.Log(BuildStartLog(context));
    }

    private string BuildStartLog(OutGameStartContext context)
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("[InGame] 전달받은 게임 시작 설정");
        log.AppendLine($"제단: {context.SelectedAltar} ({(int)context.SelectedAltar})");
        log.AppendLine($"특성 ({context.Traits.Count}개):");
        for (int i = 0; i < context.Traits.Count; i++)
        {
            TraitLevelEntry entry = context.Traits[i];
            log.AppendLine($"  {entry.Id} ({(int)entry.Id}): Lv.{entry.Level}");
        }

        log.AppendLine($"토템 ({context.Totems.Count}개):");
        for (int i = 0; i < context.Totems.Count; i++)
        {
            TotemLevelEntry entry = context.Totems[i];
            log.AppendLine($"  {entry.Id} ({(int)entry.Id}): Lv.{entry.Level}");
        }

        log.Append($"누적 보너스: {context.RewardBonusPercent}%");
        return log.ToString();
    }
}
