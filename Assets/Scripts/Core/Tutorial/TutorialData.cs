using System;
using System.Linq;
using UnityEngine;

/// <summary>안내 문구와 표시 순서만 보관합니다. 화면 연결과 조작은 TutorialController가 담당합니다.</summary>
[CreateAssetMenu(menuName = "Game/Tutorial Data", fileName = "TutorialData")]
public sealed class TutorialData : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("기존 컨트롤러가 열 화면과 허용할 조작입니다.")]
        public TutorialController.Step Type;
        public string Title;
        [TextArea(3, 8)] public string Body;
        public string NextLabel = "다음";
    }

    [Header("목록을 드래그해 표시 순서를 변경하세요")]
    [Tooltip("아웃게임 안내. OutDone은 마지막에 둡니다.")]
    public Entry[] OutGame = Array.Empty<Entry>();
    [Tooltip("인게임 안내. InDone은 마지막에 둡니다. Slot 다음에는 Construction을 권장합니다.")]
    public Entry[] InGame = Array.Empty<Entry>();

    public bool TryGetSequence(bool inGame, out Entry[] sequence)
    {
        sequence = inGame ? InGame : OutGame;
        var done = inGame ? TutorialController.Step.InDone : TutorialController.Step.OutDone;
        // 잘못된 구간/완료 단계로 화면이 잠기지 않도록 시작 전에 검사합니다.
        if (sequence == null || sequence.Length == 0 || sequence.Any(entry => entry == null ||
            !Enum.IsDefined(typeof(TutorialController.Step), entry.Type) ||
            (entry.Type >= TutorialController.Step.Core) != inGame) ||
            sequence[sequence.Length - 1].Type != done ||
            sequence.Take(sequence.Length - 1).Any(entry => entry.Type == done))
        {
            Debug.LogError($"[Tutorial] {(inGame ? "InGame" : "OutGame")} 목록의 단계 구간을 확인하고 {done}을 마지막에 배치하세요.", this);
            return false;
        }
        return true;
    }
}
