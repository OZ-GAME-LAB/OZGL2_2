using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.SaveTests
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SaveManager))]
    [AddComponentMenu("Tests/OutGame Save Test Panel")]
    public sealed class OutGameSaveTestPanel : MonoBehaviour, ISaveDataProvider<OutGameSaveTestData>
    {
        public static readonly SaveKey<OutGameSaveTestData> SaveKey =
            new SaveKey<OutGameSaveTestData>("outgame-test-profile");

        [SerializeField] private OutGameSaveTestData _state = new OutGameSaveTestData();
        [SerializeField, TextArea] private string _lastResult = "컴포넌트 메뉴의 Save Test에서 실행하세요.";

        public OutGameSaveTestData CaptureSaveData()
        {
            if (_state == null) throw new ArgumentException("테스트 상태가 없습니다.");
            return _state.Copy();
        }

        public void RestoreSaveData(OutGameSaveTestData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            // 검증·복사가 끝난 뒤 교체하므로 잘못된 데이터는 현재 상태를 바꾸지 않습니다.
            _state = data.Copy();
        }

        public bool TrySave(out string error)
        {
            try
            {
                return GetComponent<SaveManager>().TrySave(SaveKey, CaptureSaveData(), out error);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryLoad(out string error)
        {
            if (!GetComponent<SaveManager>().TryLoad(SaveKey, out OutGameSaveTestData data, out error))
                return false;
            if (!data.TryValidate(out error)) return false;
            RestoreSaveData(data);
            return true;
        }

        [ContextMenu("Save Test/1. Apply Sample Data")]
        public void ApplySampleData()
        {
            _state = new OutGameSaveTestData
            {
                Permanent = new TestPermanentState
                {
                    Bloodstone = 500,
                    UnlockedAltarIds = new List<string> { "test_altar_sun", "test_altar_moon" },
                    Traits = new List<TestLevelEntry>
                    {
                        new TestLevelEntry { Id = "test_trait_attack", Level = 2 }
                    }
                },
                LastSelection = new TestOutGameSelection
                {
                    AltarId = "test_altar_sun",
                    Totems = new List<TestLevelEntry>
                    {
                        new TestLevelEntry { Id = "test_totem_sun", Level = 3 }
                    }
                }
            };
            Report("샘플을 메모리에 입력했습니다. 아직 파일에는 저장하지 않았습니다.");
        }

        [ContextMenu("Save Test/2. Save To JSON")]
        public void SaveFromInspector()
        {
            bool success = TrySave(out string error);
            Report(success ? "저장 완료: " + GetComponent<SaveManager>().GetFilePath(SaveKey) : "저장 실패: " + error);
        }

        [ContextMenu("Save Test/3. Clear Memory Only")]
        public void ClearMemory()
        {
            _state = new OutGameSaveTestData();
            Report("메모리 상태를 초기화했습니다. 저장 파일은 유지됩니다.");
        }

        [ContextMenu("Save Test/4. Load And Restore")]
        public void LoadFromInspector()
        {
            bool success = TryLoad(out string error);
            Report(success ? "복원 완료. State의 재화·특성·제단·토템을 확인하세요." : "복원 실패: " + error);
        }

        [ContextMenu("Save Test/5. Print Current State")]
        public void PrintCurrentState() => Report(JsonUtility.ToJson(_state, true));

        private void Report(string message)
        {
            _lastResult = message;
            Debug.Log("[OutGame/SaveTest] " + message, this);
        }
    }
}
