using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// 선택 결과를 모아 게임 시작을 처리합니다. UI 표시와 화면 이동은 UIController가 담당합니다.
public class OutGameStartController : MonoBehaviour
{
    private IAltarSelection _altars;
    private ITraitProgression _traits;
    private ITotemSelection _totems;
    private OutGameSaveCoordinator _saveCoordinator;

    public OutGameStartContext LastStartContext { get; private set; }

    private bool _isRunning;
    public void Initialize(IAltarSelection altars, ITraitProgression traits, ITotemSelection totems,
        OutGameSaveCoordinator saveCoordinator = null)
    {
        _altars = altars;
        _traits = traits;
        _totems = totems;
        _saveCoordinator = saveCoordinator;
        LastStartContext = null;
        _isRunning = false;
    }

    // SO나 UI 참조 대신 ID와 레벨만 복사합니다. 이후 선택 변경은 이 결과에 영향을 주지 않습니다.
    public OutGameStartContext CreateStartContext()
    {
        if (_altars == null || _traits == null || _totems == null || _altars.SelectedAltar == AltarId.None)
        {
            Debug.LogError("[OutGameController] 초기화와 제단 선택이 먼저 필요합니다.", this);
            return null;
        }

        OutGameStartContext context = new OutGameStartContext();
        context.SelectedAltar = _altars.SelectedAltar;
        context.Traits = _traits.CaptureLevels();
        context.Totems = _totems.CaptureLevels();
        context.RewardBonusPercent = _totems.GetRewardBonusPercent();
        return context;
    }

    public void StartRun()
    {
        if (_isRunning) return;
        _isRunning = true;
        try
        {
            OutGameStartContext context = CreateStartContext();
            if (context == null)
            {
                _isRunning = false;
                return;
            }
            if (_saveCoordinator == null || !_saveCoordinator.IsReady)
            {
                Debug.LogError("[OutGameController] 시작 설정을 저장할 OutGameSaveCoordinator를 먼저 초기화해주세요.", this);
                _isRunning = false;
                return;
            }
            if (!_saveCoordinator.TrySave(out string error))
            {
                Debug.LogError("[OutGameController] 시작 설정 저장에 실패했습니다. " + error, this);
                _isRunning = false;
                return;
            }

            LastStartContext = context;
            Debug.Log(BuildStartLog(context), this);
            SceneManager.LoadScene("Test");
        }
        catch (System.Exception exception)
        {
            _isRunning = false;
            Debug.LogError("[OutGameController] 게임 시작에 실패했습니다. " + exception.Message, this);
        }
    }
    private string BuildStartLog(OutGameStartContext context)
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("[OutGame] 게임 시작 컨텍스트");
        log.AppendLine($"제단: {GetAltarName(context.SelectedAltar)} / {context.SelectedAltar} ({(int)context.SelectedAltar})");
        log.AppendLine("특성 (레벨 0 포함):");
        for (int i = 0; i < context.Traits.Count; i++)
        {
            TraitLevelEntry entry = context.Traits[i];
            log.AppendLine($"  {GetTraitName(entry.Id)} / {entry.Id} ({(int)entry.Id}): Lv.{entry.Level}");
        }

        log.AppendLine("토템 (레벨 0 포함):");
        for (int i = 0; i < context.Totems.Count; i++)
        {
            TotemLevelEntry entry = context.Totems[i];
            log.AppendLine($"  {GetTotemName(entry.Id)} / {entry.Id} ({(int)entry.Id}): Lv.{entry.Level}");
        }
        log.Append($"누적 보너스: {context.RewardBonusPercent}%");
        return log.ToString();
    }

    private string GetAltarName(AltarId id)
    {
        for (int i = 0; i < _altars.Data.Count; i++)
        {
            if (_altars.Data[i].Id == id) return _altars.Data[i].DisplayName;
        }
        return id.ToString();
    }

    private string GetTraitName(TraitId id)
    {
        for (int i = 0; i < _traits.Data.Count; i++)
        {
            if (_traits.Data[i].Id == id) return _traits.Data[i].DisplayName;
        }
        return id.ToString();
    }

    private string GetTotemName(TotemId id)
    {
        for (int i = 0; i < _totems.Data.Count; i++)
        {
            if (_totems.Data[i].Id == id) return _totems.Data[i].DisplayName;
        }
        return id.ToString();
    }
}
