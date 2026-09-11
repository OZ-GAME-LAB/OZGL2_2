// Current date KDH 2026-09-08
// 임시 건설 메뉴. UI 담당이 교체하기 전까지 런타임에 Canvas를 만듭니다.
// 씬 파일을 수정하지 않기 위해, 없으면 플레이 시 생성합니다.
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OZGL.KDH
{
    public class BuildingBuildMenu : MonoBehaviour
    {
        private BuildingBuildController _owner;
        private PlayerWallet _wallet;
        private BuildingSlot _slot;
        private readonly List<Row> _rows = new List<Row>(8);
        private readonly StringBuilder _costBuilder = new StringBuilder(64);

        private GameObject _root;
        private Text _titleText;
        private Text _walletText;
        private GameObject _demolishGo;
        private Transform _rowParent;
        private Font _font;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Bind(BuildingBuildController owner)
        {
            _owner = owner;
            EnsureUi();
            Hide();
        }

        public void Show(BuildingSlot slot, List<BuildingData> candidates, PlayerWallet wallet)
        {
            if (slot == null)
            {
                Debug.LogWarning("[BuildingBuildMenu] Show에 BuildingSlot이 null입니다.", this);
                return;
            }

            EnsureUi();
            _slot = slot;
            SubscribeWallet(wallet);

            if (_titleText != null)
                _titleText.text = slot.IsOccupied ? "교체" : "건설";

            if (_demolishGo != null)
                _demolishGo.SetActive(slot.IsOccupied);

            RefreshWalletText();
            EnsureRowCount(candidates != null ? candidates.Count : 0);
            FillRows(candidates);

            _root.SetActive(true);
        }

        public void Hide()
        {
            _slot = null;
            if (_root != null)
                _root.SetActive(false);
        }

        public void RefreshAffordability()
        {
            if (!IsOpen)
                return;

            RefreshWalletText();

            for (int i = 0; i < _rows.Count; i++)
            {
                if (!_rows[i].Root.activeSelf)
                    continue;

                ApplyRowState(_rows[i]);
            }
        }

        private void OnDisable()
        {
            UnsubscribeWallet();
        }

        private void SubscribeWallet(PlayerWallet wallet)
        {
            if (_wallet == wallet)
                return;

            UnsubscribeWallet();
            _wallet = wallet;
            if (_wallet != null)
                _wallet.Changed += OnWalletChanged;
        }

        private void UnsubscribeWallet()
        {
            if (_wallet == null)
                return;

            _wallet.Changed -= OnWalletChanged;
            _wallet = null;
        }

        private void OnWalletChanged(BuildingResourceType type, int amount)
        {
            RefreshAffordability();
        }

        private void FillRows(List<BuildingData> candidates)
        {
            int count = candidates != null ? candidates.Count : 0;
            if (count == 0)
            {
                Debug.LogWarning("[BuildingBuildMenu] 이 칸에 표시할 건물이 없습니다.", this);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i >= count)
                {
                    _rows[i].Root.SetActive(false);
                    _rows[i].Data = null;
                    continue;
                }

                _rows[i].Root.SetActive(true);
                _rows[i].Data = candidates[i];
                ApplyRowState(_rows[i]);
            }
        }

        private void ApplyRowState(Row row)
        {
            BuildingData data = row.Data;
            if (data == null)
                return;

            bool isCurrent = _owner != null && _owner.IsSameAsCurrent(_slot, data);
            bool canAfford = !isCurrent && _owner != null && _owner.CanAffordCandidate(_slot, data);
            row.Label.text = BuildRowLabel(data, canAfford, isCurrent);
            row.Button.interactable = canAfford;
        }

        private string BuildRowLabel(BuildingData data, bool canAfford, bool isCurrent)
        {
            _costBuilder.Length = 0;
            _costBuilder.Append(data.DisplayName);
            _costBuilder.Append('\n');

            if (!string.IsNullOrEmpty(data.Description))
            {
                _costBuilder.Append(data.Description);
                _costBuilder.Append('\n');
            }

            AppendFeatureSummary(_costBuilder, data);

            if (isCurrent)
            {
                _costBuilder.Append("현재 건물");
                return _costBuilder.ToString();
            }

            bool occupied = _slot != null && _slot.IsOccupied;
            if (occupied)
                AppendNetCost(_costBuilder, data);
            else
                AppendCost(_costBuilder, data.BuildCost);

            if (occupied)
                _costBuilder.Append(canAfford ? "\n교체" : "\n부족");
            else
                _costBuilder.Append(canAfford ? "\n건설" : "\n부족");

            return _costBuilder.ToString();
        }

        private void AppendNetCost(StringBuilder builder, BuildingData data)
        {
            if (_owner == null)
            {
                AppendCost(builder, data.BuildCost);
                return;
            }

            int goldNet = _owner.GetCandidateNet(_slot, data, BuildingResourceType.Gold);
            int gemNet = _owner.GetCandidateNet(_slot, data, BuildingResourceType.Gem);

            bool first = true;
            first = AppendNetPart(builder, BuildingResourceType.Gold, goldNet, first);
            first = AppendNetPart(builder, BuildingResourceType.Gem, gemNet, first);

            if (first)
                builder.Append("차액 없음");
        }

        private static bool AppendNetPart(StringBuilder builder, BuildingResourceType type, int net, bool first)
        {
            if (net == 0)
                return first;

            if (!first)
                builder.Append(" / ");

            if (net > 0)
            {
                builder.Append(type);
                builder.Append(" +");
                builder.Append(net);
            }
            else
            {
                builder.Append("환급 ");
                builder.Append(type);
                builder.Append(' ');
                builder.Append(-net);
            }

            return false;
        }

        private static void AppendCost(StringBuilder builder, BuildingResourceCost[] costs)
        {
            if (costs == null || costs.Length == 0)
            {
                builder.Append("무료");
                return;
            }

            bool first = true;
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].amount <= 0)
                    continue;

                if (!first)
                    builder.Append(" / ");

                builder.Append(costs[i].type);
                builder.Append(' ');
                builder.Append(costs[i].amount);
                first = false;
            }

            if (first)
                builder.Append("무료");
        }

        // Current date KDH 2026-09-11
        // 숫자는 description이 아니라 모듈 설정에서 읽어, 데이터가 어긋나지 않게 합니다.
        private static void AppendFeatureSummary(StringBuilder builder, BuildingData data)
        {
            if (data.HasProduction)
            {
                BuildingProductionSettings production = data.Production;
                builder.Append("웨이브마다 ");
                builder.Append(production.resourceType);
                builder.Append(' ');
                builder.Append(production.amount);
                builder.Append('\n');
            }

            if (data.HasSpawn)
            {
                BuildingSpawnSettings spawn = data.Spawn;
                builder.Append("소환 ");
                builder.Append(spawn.countPerWave);
                builder.Append("기");
                if (spawn.unitPrefab != null)
                {
                    builder.Append(" (");
                    builder.Append(spawn.unitPrefab.name);
                    builder.Append(')');
                }

                builder.Append('\n');
            }
        }

        private void RefreshWalletText()
        {
            if (_walletText == null)
                return;

            if (_wallet == null)
            {
                _walletText.text = "지갑 없음";
                return;
            }

            _costBuilder.Length = 0;
            _costBuilder.Append("보유  Gold ");
            _costBuilder.Append(_wallet.Get(BuildingResourceType.Gold));
            _costBuilder.Append("  Gem ");
            _costBuilder.Append(_wallet.Get(BuildingResourceType.Gem));
            _walletText.text = _costBuilder.ToString();
        }

        private void EnsureRowCount(int needed)
        {
            while (_rows.Count < needed)
                _rows.Add(CreateRow(_rows.Count));
        }

        private Row CreateRow(int index)
        {
            GameObject go = new GameObject("BuildRow_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_rowParent, false);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);

            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 88f;
            layout.preferredHeight = 88f;

            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 6f);
            textRt.offsetMax = new Vector2(-8f, -6f);

            Text label = textGo.GetComponent<Text>();
            label.font = _font;
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            Button button = go.GetComponent<Button>();
            int captured = index;
            button.onClick.AddListener(() => OnRowClicked(captured));

            return new Row
            {
                Root = go,
                Button = button,
                Label = label
            };
        }

        private void OnRowClicked(int index)
        {
            if (_owner == null || _slot == null)
                return;

            if (index < 0 || index >= _rows.Count)
                return;

            BuildingData data = _rows[index].Data;
            if (data == null)
            {
                Debug.LogWarning("[BuildingBuildMenu] 선택한 행에 BuildingData가 없습니다.", this);
                return;
            }

            _owner.TryBuild(_slot, data);
        }

        private void EnsureUi()
        {
            if (_root != null)
                return;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (_font == null)
                Debug.LogWarning("[BuildingBuildMenu] 기본 폰트를 찾지 못해 글자가 안 보일 수 있습니다.", this);

            EnsureEventSystem();

            GameObject canvasGo = new GameObject("BuildingBuildMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRt = _root.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.sizeDelta = new Vector2(360f, 0f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.offsetMin = new Vector2(panelRt.offsetMin.x, 40f);
            panelRt.offsetMax = new Vector2(-24f, -40f);

            _root.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            VerticalLayoutGroup rootLayout = _root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(16, 16, 16, 16);
            rootLayout.spacing = 8f;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlHeight = true;
            rootLayout.childControlWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            _titleText = CreateText("Title", _root.transform, 22, FontStyle.Bold);
            _titleText.text = "건설";
            _titleText.GetComponent<LayoutElement>().preferredHeight = 32f;

            _walletText = CreateText("Wallet", _root.transform, 16, FontStyle.Normal);
            _walletText.GetComponent<LayoutElement>().preferredHeight = 24f;

            _demolishGo = CreateDemolishButton(_root.transform);

            GameObject listGo = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listGo.transform.SetParent(_root.transform, false);
            _rowParent = listGo.transform;

            VerticalLayoutGroup listLayout = listGo.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.childAlignment = TextAnchor.UpperCenter;
            listLayout.childControlHeight = true;
            listLayout.childControlWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childForceExpandWidth = true;

            ContentSizeFitter fitter = listGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement listLayoutElement = listGo.AddComponent<LayoutElement>();
            listLayoutElement.flexibleHeight = 1f;
        }

        private GameObject CreateDemolishButton(Transform parent)
        {
            GameObject go = new GameObject("Demolish", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.45f, 0.16f, 0.16f, 0.95f);

            LayoutElement layout = go.GetComponent<LayoutElement>();
            layout.minHeight = 48f;
            layout.preferredHeight = 48f;

            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text label = textGo.GetComponent<Text>();
            label.font = _font;
            label.fontSize = 16;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "철거 (환불)";

            Button button = go.GetComponent<Button>();
            button.onClick.AddListener(OnDemolishClicked);

            go.SetActive(false);
            return go;
        }

        private void OnDemolishClicked()
        {
            if (_owner == null || _slot == null)
            {
                Debug.LogWarning("[BuildingBuildMenu] 철거할 칸이 없습니다.", this);
                return;
            }

            _owner.TryDemolish(_slot);
        }

        private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle style)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private class Row
        {
            public GameObject Root;
            public Button Button;
            public Text Label;
            public BuildingData Data;
        }
    }
}
