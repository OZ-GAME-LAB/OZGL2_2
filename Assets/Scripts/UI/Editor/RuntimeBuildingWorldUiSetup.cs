using System;
using System.IO;
using System.Linq;
using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Game.UI.Editor
{
    /// <summary>기존 씬을 보존한 채 월드 입력만 추가한 별도 UI 검증 씬을 생성한다.</summary>
    public static class RuntimeBuildingWorldUiSetup
    {
        public const string ScenePath = "Assets/Scenes/UI/PlayerBuildingWorldInput.unity";

        [MenuItem("Game/UI/Create Building World Input Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) { Debug.Log("[UI] Existing world input scene preserved."); return; }
            if (!File.Exists(RuntimeBuildingUiSetup.ScenePath)) throw new InvalidOperationException("Create building integration scene first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard pending scene edits yourself first.");
            var previous = SceneManager.GetActiveScene();
            if (!AssetDatabase.CopyAsset(RuntimeBuildingUiSetup.ScenePath, ScenePath))
                throw new IOException("World input scene copy failed.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var roots = scene.GetRootGameObjects();
                T Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                var binding = Find<RuntimeBuildingUiBinding>();
                var flow = Find<GameFlowController>();
                var camera = Find<Camera>();
                camera.gameObject.AddComponent<Physics2DRaycaster>();
                camera.orthographicSize = 5;
                roots.Single(r => r.name == "Battlefield Backdrop - UI layout preview").SetActive(false);
                // 이 씬은 건설 입력 검증 전용이다. 가짜 전투/병력 수를 플레이어에게 제시하지 않는다.
                var hud = Find<GameUIController>();
                hud.transform.Find("UnitCounts").gameObject.SetActive(false);
                hud.transform.Find("BottomBar/WaveStart").gameObject.SetActive(false);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PlayerUiBuilder.FontPath);
                if (font == null) throw new InvalidOperationException("Player UI font is required.");
                var slots = roots.SelectMany(r => r.GetComponentsInChildren<BuildingSlot>(true)).OrderBy(s => s.name).ToArray();
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    slot.transform.position = new Vector3(-2 + (i % 2) * 4, 1.4f - (i / 2) * 2.8f, 0);
                    slot.GetComponent<BoxCollider2D>().size = new Vector2(2.8f, 1.8f);
                    // 원본 BuildingSlot은 같은 오브젝트의 SpriteRenderer를 빈 칸 표식으로 숨긴다.
                    // 점유 상태를 계속 표시할 UI 표식은 별도 자식에 두어 소유권을 분리한다.
                    var markerObject = new GameObject("UI Slot Marker", typeof(SpriteRenderer));
                    markerObject.transform.SetParent(slot.transform, false);
                    var marker = markerObject.GetComponent<SpriteRenderer>();
                    marker.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    marker.drawMode = SpriteDrawMode.Sliced;
                    marker.size = new Vector2(2.8f, 1.8f);
                    var labelObject = new GameObject("World Slot Label", typeof(TextMeshPro));
                    labelObject.transform.SetParent(slot.transform, false);
                    labelObject.transform.localPosition = new Vector3(0, 0, -.2f);
                    var label = labelObject.GetComponent<TextMeshPro>();
                    label.font = font;
                    label.fontSize = 3.5f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.rectTransform.sizeDelta = new Vector2(2.6f, .6f);
                    label.color = new Color32(235, 230, 211, 255);
                    label.text = "+ 건설";
                    MvpHudBuilder.Assign(slot.gameObject.AddComponent<RuntimeBuildingSelectionTarget>(),
                        "_binding", binding, "_slot", slot, "_flow", flow);
                    MvpHudBuilder.Assign(slot.gameObject.AddComponent<RuntimeBuildingWorldSlotView>(),
                        "_slot", slot, "_marker", marker, "_label", label);
                }
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("World input scene save failed.");
                Debug.Log("[UI] Created " + ScenePath + ". Construction input fixture, not the team combat map.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
