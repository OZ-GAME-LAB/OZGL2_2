using System;
using Cysharp.Threading.Tasks;
using Game.UI.Samples;
using TMPro;
using Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>테스트 전용 씬의 실제 유닛으로 등록·생존 수·수명·HUD 재활성화를 검사한다.</summary>
    public static class MvpRuntimeUnitCountValidation
    {
        private static int _checks;

        public static async UniTask RunChecksAsync()
        {
            _checks = 0;
            var hud = UnityEngine.Object.FindFirstObjectByType<RuntimeUnitCountHud>();
            var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeUnitSelectionSample>();
            Check(hud != null && sample != null && sample.IsReady, "sample and HUD ready");
            var fields = new SerializedObject(sample);
            var sources = fields.FindProperty("_sources");
            var ally = (RuntimeUnitInfoSource)sources.GetArrayElementAtIndex(0).objectReferenceValue;
            var enemy = (RuntimeUnitInfoSource)sources.GetArrayElementAtIndex(1).objectReferenceValue;
            var allyCore = ally.GetComponent<Unit_Core>();
            var enemyCore = enemy.GetComponent<Unit_Core>();
            var reset = (Button)fields.FindProperty("_respawnButton").objectReferenceValue;
            var hudFields = new SerializedObject(hud);
            var allyText = (TMP_Text)hudFields.FindProperty("_allyText").objectReferenceValue;
            var enemyText = (TMP_Text)hudFields.FindProperty("_enemyText").objectReferenceValue;
            var selection = UnityEngine.Object.FindFirstObjectByType<RuntimeUnitInfoBinding>();

            Expect(hud, 1, 1, 2, "both initialized units registered");
            Check(allyText.text == "아군 생존 1" && enemyText.text == "적군 생존 1", "initial visible counts");
            Check(!hud.TryRegister(null), "null registration rejected");
            for (int i = 0; i < 3; i++) Check(hud.TryRegister(ally), "idempotent registration");
            Expect(hud, 1, 1, 2, "duplicate is counted once");
            allyCore.TakeDamage(new DamageResult(null, null, 25, DamageSourceType.BasicAttack));
            allyCore.Heal(10);
            allyCore.AddShield(20);
            Expect(hud, 1, 1, 2, "nonfatal life and shield notifications do not change counts");
            allyCore.TakeDamage(new DamageResult(null, null, 999, DamageSourceType.BasicAttack));
            Expect(hud, 0, 1, 2, "HP zero and Died notifications reduce once");
            Check(allyText.text == "아군 생존 0", "death immediately visible");
            allyCore.TakeDamage(new DamageResult(null, null, 999, DamageSourceType.BasicAttack));
            allyCore.Heal(999);
            Expect(hud, 0, 1, 2, "repeated death and failed healing cannot underflow or revive count");
            allyCore.Initialize(null);
            Expect(hud, 1, 1, 2, "real life initialization updates existing entry");

            string oldId = ally.SelectionId;
            ally.enabled = false;
            Expect(hud, 0, 1, 1, "source unavailable removes entry");
            ally.enabled = true;
            Expect(hud, 0, 1, 1, "new lifetime requires explicit registration");
            Check(hud.TryRegister(ally), "reused source registers");
            Check(!hud.TryUnregister(ally, oldId), "old lifetime cannot unregister reused unit");
            Expect(hud, 1, 1, 2, "stale unregister is atomic");
            Check(hud.TryUnregister(ally, ally.SelectionId), "matching lifetime unregisters");
            Check(!hud.TryUnregister(ally, ally.SelectionId), "duplicate unregister rejected");
            Check(hud.TryRegister(ally), "explicit re-registration");

            hud.gameObject.SetActive(false);
            enemyCore.TakeDamage(new DamageResult(null, null, 999, DamageSourceType.BasicAttack));
            Check(enemyText.text == "적군 생존 1", "hidden HUD unsubscribes");
            hud.gameObject.SetActive(true);
            Expect(hud, 1, 0, 2, "show reconciles missed death");
            Check(enemyText.text == "적군 생존 0", "show renders latest count");
            hud.gameObject.SetActive(false);
            ally.gameObject.SetActive(false);
            ally.gameObject.SetActive(true);
            allyCore.Initialize(null);
            hud.gameObject.SetActive(true);
            Expect(hud, 0, 0, 1, "hidden stale lifetime pruned instead of counted automatically");
            Check(hud.TryRegister(ally), "new lifetime explicitly accepted");
            enemyCore.Initialize(null);
            Expect(hud, 1, 1, 2, "current entries recover");

            // 팀 코드에 컴포넌트 단독 활성화 이벤트가 없으므로 명시적 재동기화를 검증한다.
            allyCore.enabled = false;
            hud.Refresh();
            Expect(hud, 0, 1, 2, "disabled core contributes zero but keeps its lifetime");
            allyCore.enabled = true;
            hud.Refresh();
            Expect(hud, 1, 1, 2, "re-enabled core recovers on explicit refresh");
            var allyLife = ally.GetComponent<Unit_Life>();
            allyLife.enabled = false;
            hud.Refresh();
            Expect(hud, 0, 1, 2, "disabled life contributes zero");
            allyLife.enabled = true;
            hud.Refresh();
            Expect(hud, 1, 1, 2, "re-enabled life recovers on explicit refresh");

            hud.gameObject.SetActive(false);
            ally.gameObject.SetActive(false);
            ally.gameObject.SetActive(true);
            allyCore.Initialize(null);
            Check(hud.TryRegister(ally), "new lifetime can register while HUD is hidden");
            hud.gameObject.SetActive(true);
            Expect(hud, 1, 1, 2, "hidden explicit registration replaces stale lifetime without duplicate");

            var temporary = new GameObject("UI Count Validation Temporary");
            try
            {
                var status = temporary.AddComponent<Unit_RuntimeStatus>();
                MvpHudBuilder.Assign(status, "_unitData", allyCore.RuntimeStatus.UnitData);
                temporary.AddComponent<Unit_Life>();
                var core = temporary.AddComponent<Unit_Core>();
                var source = temporary.AddComponent<RuntimeUnitInfoSource>();
                Check(!hud.TryRegister(source), "uninitialized registration rejected without initializing unit");
                Check(temporary.GetComponent<Unit_Life>().MaxHp == 0, "HUD did not initialize gameplay");
                core.Initialize(null);
                Check(hud.TryRegister(source), "later spawned initialized unit registers");
                Expect(hud, 2, 1, 3, "dynamic spawn contributes once");
                hud.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(temporary);
                await UniTask.NextFrame();
                hud.gameObject.SetActive(true);
                Expect(hud, 1, 1, 2, "destroyed object while hidden pruned safely");
            }
            finally
            {
                if (temporary != null) UnityEngine.Object.Destroy(temporary);
                hud.gameObject.SetActive(true);
                hud.Refresh();
            }

            Check(selection.TrySelect(ally), "selection still usable beside count HUD");
            string selectionId = selection.SelectionId;
            hud.ClearRegistrations();
            Expect(hud, 0, 0, 0, "clear resets presentation registrations");
            Check(selection.SelectionId == selectionId && ally.GetComponent<Unit_Life>().CurrentHp == 100,
                "clear does not change selection or gameplay life");
            allyCore.TakeDamage(new DamageResult(null, null, 10, DamageSourceType.BasicAttack));
            Expect(hud, 0, 0, 0, "removed sources no longer update HUD");
            for (int i = 0; i < 3; i++)
            {
                reset.onClick.Invoke();
                Expect(hud, 1, 1, 2, "sample respawn registers each lifetime once");
                enemyCore.TakeDamage(new DamageResult(null, null, 999, DamageSourceType.BasicAttack));
                Expect(hud, 1, 0, 2, "repeated respawn/death stays consistent");
            }
            reset.onClick.Invoke();
            hud.Initialize(allyText, enemyText);
            hud.Initialize(allyText, enemyText);
            Expect(hud, 1, 1, 2, "repeated view initialization preserves registrations");
            try { hud.Initialize(null, enemyText); Check(false, "null view should throw"); }
            catch (ArgumentNullException) { Check(true, "null view rejected"); }
            Expect(hud, 1, 1, 2, "bad view initialization leaves valid state unchanged");
            Check(!allyText.raycastTarget && !enemyText.raycastTarget, "count labels do not add interactive blockers");
            Canvas.ForceUpdateCanvases();
            foreach (var label in hud.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Check(label.font.HasCharacters(label.text, out uint[] missing, false, true), "count glyphs: " + label.name);
                Check(!label.isTextOverflowing, "count text fits: " + label.name);
            }
            Debug.Log($"[UI/MvpRuntimeUnitCountValidation] PASS: {_checks} Play Mode checks with actual Unit_Core/Unit_Life; registered units only, no battle outcome logic.");
        }

        private static void Expect(RuntimeUnitCountHud hud, int allies, int enemies, int registered, string description)
        {
            Check(hud.AliveAllies == allies && hud.AliveEnemies == enemies && hud.RegisteredCount == registered,
                description + $" (actual {hud.AliveAllies}/{hud.AliveEnemies}/{hud.RegisteredCount})");
        }

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Runtime unit counts: " + description);
        }
    }
}
