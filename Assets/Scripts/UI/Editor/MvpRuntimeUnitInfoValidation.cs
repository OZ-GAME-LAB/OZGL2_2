using System;
using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using Units;
using Units.UnitDatas;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>Runtime HUD Play 검사에서 실행한다. 실제 유닛 컴포넌트와 임시 데이터만 사용한다.</summary>
    public static class MvpRuntimeUnitInfoValidation
    {
        private static int _checks;

        public static async UniTask RunChecksAsync()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run the Runtime HUD Play Mode validation first.");
            _checks = 0;
            var root = new GameObject("Temporary Runtime Unit UI Checks");
            var allyData = CreateData("ui-test-ally", "검증용 전사", UnitTeam.Ally, 100);
            var enemyData = CreateData("ui-test-enemy", "검증용 경비병", UnitTeam.Enemy, 150);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpUnitUiBuilder.UnitPrefabPath);
                Check(prefab != null, "existing unit info prefab available");
                var panelRoot = UnityEngine.Object.Instantiate(prefab, root.transform);
                var panel = panelRoot.GetComponent<UnitInfoPanel>();
                var binding = panelRoot.AddComponent<RuntimeUnitInfoBinding>();
                panel.HideUnitInfo();
                var fields = new SerializedObject(panel);
                var health = Field<TMP_Text>(fields, "_healthText");
                var combat = Field<TMP_Text>(fields, "_combatText");
                var title = Field<TMP_Text>(fields, "_nameText");
                var faction = Field<TMP_Text>(fields, "_factionText");
                var close = Field<Button>(fields, "_closeButton");
                var scroll = Field<ScrollRect>(fields, "_detailsScroll");
                var fill = Field<RectTransform>(fields, "_healthFill");
                var ally = CreateUnit(root.transform, allyData);
                var enemy = CreateUnit(root.transform, enemyData);
                var allyCore = ally.GetComponent<Unit_Core>();
                var allyLife = ally.GetComponent<Unit_Life>();
                var allyStatus = ally.GetComponent<Unit_RuntimeStatus>();
                var enemyCore = enemy.GetComponent<Unit_Core>();
                Check(!ally.TryGetInfo(out _) && !binding.TrySelect(ally), "UI cannot initialize unready unit");
                Check(!panel.HasSelection && allyLife.MaxHp == 0, "failed selection leaves unit and panel unchanged");
                allyCore.Initialize(null);
                Check(ally.TryGetInfo(out var allyInfo) && allyInfo.CurrentHealth == 100, "real core initializes life and data");
                Check(binding.TrySelect(ally) && health.text == "체력 100 / 100", "initial current/max are read from properties");
                string allyId = binding.SelectionId;
                Check(title.text == "검증용 전사" && faction.text == "아군", "data name and real team displayed");
                Check(combat.text.Contains("공격력 12") && combat.text.Contains("방어력 5"), "real status displayed");
                Check(!binding.TrySelect(null) && !binding.TrySelect(enemy) && binding.SelectionId == allyId,
                    "invalid selection preserves previous unit");
                allyCore.TakeDamage(new DamageResult(null, null, 25, DamageSourceType.BasicAttack, false));
                Check(health.text == "체력 75 / 100" && fill.anchorMax.x == 0.75f, "damage old/new payload is not interpreted as current/max");
                allyCore.Heal(10);
                Check(health.text == "체력 85 / 100", "real heal event");
                allyCore.AddShield(20);
                Check(combat.text.Contains("보호막 20"), "real shield event");
                allyCore.TakeDamage(new DamageResult(null, null, 10, DamageSourceType.BasicAttack, false));
                Check(health.text == "체력 85 / 100" && combat.text.Contains("보호막 10"), "shield-only damage updates without HP event");
                allyCore.TakeDamage(new DamageResult(null, null, 15, DamageSourceType.BasicAttack, false));
                Check(health.text == "체력 80 / 100" && combat.text.Contains("보호막 0"), "shield overflow damage");
                var hpBuff = new object();
                allyStatus.AddCombatModifier(new CombatStatModifier(hpBuff, UnitStatType.MaxHp, UnitStatModifierType.Flat, 50));
                Check(health.text == "체력 80 / 150", "max HP growth updates without current HP event");
                allyCore.Heal(100);
                Check(health.text == "체력 150 / 150", "healing uses modified max HP");
                allyStatus.RemoveCombatModifiers(hpBuff);
                Check(health.text == "체력 100 / 100", "max HP decrease follows actual life clamp");
                var attackBuff = new object();
                allyStatus.AddCombatModifier(new CombatStatModifier(attackBuff, UnitStatType.AttackPower, UnitStatModifierType.Flat, 8));
                Check(combat.text.Contains("공격력 20"), "attack modifier updates detail");
                allyStatus.RemoveCombatModifiers(attackBuff);
                Check(combat.text.Contains("공격력 12"), "removed modifier updates detail");

                // 실제 프레임/OnEnable/OnDisable로 검증한다. 생명주기 리플렉션 호출은 사용하지 않는다.
                panelRoot.SetActive(false);
                allyCore.TakeDamage(new DamageResult(null, null, 30, DamageSourceType.BasicAttack, false));
                Check(health.text == "체력 100 / 100" && allyLife.CurrentHp == 70, "hidden panel stops listening without stopping unit");
                Check(!binding.TrySelect(ally), "hidden binding rejects input");
                panelRoot.SetActive(true);
                Check(health.text == "체력 70 / 100" && binding.SelectionId == allyId, "show panel reads latest same-lifetime unit");
                for (int i = 0; i < 3; i++) { panelRoot.SetActive(false); panelRoot.SetActive(true); }
                allyCore.Heal(10);
                Check(health.text == "체력 80 / 100", "repeated activation keeps data current");
                close.onClick.Invoke();
                Check(!panel.HasSelection && binding.SelectionId == null, "user close detaches current unit");
                allyCore.TakeDamage(new DamageResult(null, null, 5, DamageSourceType.BasicAttack, false));
                Check(!panel.HasSelection, "late health change cannot reopen closed view");

                enemyCore.Initialize(null);
                Check(binding.TrySelect(enemy) && faction.text == "적군" && health.text == "체력 150 / 150", "switch to real enemy");
                string enemyId = binding.SelectionId;
                allyCore.TakeDamage(new DamageResult(null, null, 5, DamageSourceType.BasicAttack, false));
                ally.gameObject.SetActive(false);
                Check(binding.SelectionId == enemyId && health.text == "체력 150 / 150", "old unit damage/despawn cannot affect new selection");
                enemyCore.TakeDamage(new DamageResult(null, null, 999, DamageSourceType.BasicAttack, false));
                Check(enemy.GetComponent<Unit_Life>().IsDead && panel.HasSelection && health.text == "체력 0 / 150",
                    "death stays visible until despawn");
                enemy.gameObject.SetActive(false);
                Check(!panel.HasSelection && binding.SelectionId == null, "despawn clears selected unit");

                ally.gameObject.SetActive(true);
                allyCore.Initialize(null);
                Check(binding.TrySelect(ally) && binding.SelectionId != allyId, "pooled object receives new selection identity");
                string pooledId = binding.SelectionId;
                Check(!panel.TryUpdateHealth(allyId, 0, 100), "previous spawn callback is rejected");
                panelRoot.SetActive(false);
                ally.gameObject.SetActive(false);
                ally.gameObject.SetActive(true);
                allyCore.Initialize(null);
                panelRoot.SetActive(true);
                Check(ally.SelectionId != pooledId && !panel.HasSelection && binding.SelectionId == null,
                    "recycled while hidden does not restore stale selection");
                Check(binding.TrySelect(ally), "new lifetime can be explicitly selected");
                binding.Initialize(panel);
                binding.Initialize(panel);
                Check(!panel.HasSelection && binding.TrySelect(ally), "reinitialization safely clears old selection");
                allyCore.TakeDamage(new DamageResult(null, null, 10, DamageSourceType.BasicAttack, false));
                Check(health.text == "체력 90 / 100", "reinitialized binding receives current health");
                panel.HideUnitInfo();
                allyCore.TakeDamage(new DamageResult(null, null, 10, DamageSourceType.BasicAttack, false));
                Check(!panel.HasSelection && binding.SelectionId == null, "externally hidden panel is not resurrected");
                Check(binding.TrySelect(ally), "selection resumes after explicit hide");
                await UniTask.NextFrame();
                Capture(panelRoot, 1280, 720);
                Capture(panelRoot, 1920, 1080);
                var longDescription = new SerializedObject(ally);
                longDescription.FindProperty("_description").stringValue = string.Concat(System.Linq.Enumerable.Repeat("실제 유닛 정보 연결 검사입니다.\n", 30));
                longDescription.ApplyModifiedPropertiesWithoutUndo();
                Check(binding.TrySelect(ally), "optional UI description can be presented");
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Check(scroll.content.rect.height > scroll.viewport.rect.height, "description is scrollable");
                scroll.verticalNormalizedPosition = 0.4f;
                allyCore.TakeDamage(new DamageResult(null, null, 5, DamageSourceType.BasicAttack, false));
                Check(Mathf.Abs(scroll.verticalNormalizedPosition - 0.4f) < 0.001f, "event refresh preserves reading position");
                await UniTask.NextFrame();
                UnityEngine.Object.Destroy(ally.gameObject);
                await UniTask.NextFrame();
                Check(!panel.HasSelection && binding.SelectionId == null, "destroyed selection is cleared by actual lifecycle");
                Check(binding.TrySelect(ActivateUnit(enemy)), "remaining source selectable");
                UnityEngine.Object.Destroy(binding);
                await UniTask.NextFrame();
                Check(!panel.HasSelection, "destroyed binding clears its own view");
                enemyCore.TakeDamage(new DamageResult(null, null, 10, DamageSourceType.BasicAttack, false));
                Check(!panel.HasSelection, "destroyed binding leaves no callbacks");
                Debug.Log($"[UI/MvpRuntimeUnitInfoValidation] PASS: {_checks} checks in actual Play Mode with Unit_Core/Unit_Life/Unit_RuntimeStatus. No AI or combat scene integration claimed.");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                await UniTask.NextFrame();
                UnityEngine.Object.Destroy(allyData);
                UnityEngine.Object.Destroy(enemyData);
            }
        }

        private static RuntimeUnitInfoSource ActivateUnit(RuntimeUnitInfoSource source)
        {
            source.gameObject.SetActive(true);
            source.GetComponent<Unit_Core>().Initialize(null);
            return source;
        }

        private static RuntimeUnitInfoSource CreateUnit(Transform parent, UnitData data)
        {
            var unit = new GameObject(data.UnitName);
            unit.transform.SetParent(parent);
            unit.SetActive(false);
            var status = unit.AddComponent<Unit_RuntimeStatus>();
            MvpHudBuilder.Assign(status, "_unitData", data);
            unit.AddComponent<Unit_Life>();
            unit.AddComponent<Unit_Core>();
            var source = unit.AddComponent<RuntimeUnitInfoSource>();
            unit.SetActive(true);
            return source;
        }

        private static UnitData CreateData(string id, string name, UnitTeam team, float hp)
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            var fields = new SerializedObject(data);
            fields.FindProperty("_unitId").stringValue = id;
            fields.FindProperty("_unitName").stringValue = name;
            fields.FindProperty("_team").intValue = (int)team;
            var types = new[] { UnitStatType.MaxHp, UnitStatType.AttackPower, UnitStatType.Defense, UnitStatType.AttackSpeed, UnitStatType.MoveSpeed };
            var values = new[] { hp, 12f, 5f, 1f, 2f };
            var stats = fields.FindProperty("_stats");
            stats.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++)
            {
                var stat = stats.GetArrayElementAtIndex(i);
                stat.FindPropertyRelative("_statType").intValue = (int)types[i];
                stat.FindPropertyRelative("_value").floatValue = values[i];
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static T Field<T>(SerializedObject fields, string name) where T : UnityEngine.Object =>
            (T)fields.FindProperty(name).objectReferenceValue;

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Runtime unit UI: " + description);
        }

        private static void Capture(GameObject root, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var cameraObject = new GameObject("Runtime Unit UI Capture", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var canvas = root.GetComponent<Canvas>();
            var scaler = root.GetComponent<CanvasScaler>();
            float scale = canvas.scaleFactor;
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 22, 31, 255);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.targetTexture = target;
                scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.scaleFactor = Mathf.Sqrt((width / 1920f) * (height / 1080f));
                Canvas.ForceUpdateCanvases();
                foreach (var label in root.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Check(label.font.HasCharacters(label.text, out uint[] missing, false, true), width + " glyphs: " + label.name);
                    Check(!label.isTextOverflowing, width + " text fits: " + label.name);
                }
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs/RuntimeUnitInfoValidation");
                File.WriteAllBytes($"Logs/RuntimeUnitInfoValidation/unit-{width}x{height}.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.scaleFactor = scale;
                scaler.enabled = true;
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraObject);
            }
        }
    }
}
