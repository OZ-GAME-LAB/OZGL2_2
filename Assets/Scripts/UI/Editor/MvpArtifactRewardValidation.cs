using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>실제 Play Mode UI + EventSystem에 합성 이벤트를 전달한다. OS 입력은 조작하지 않는다.</summary>
    public static class MvpArtifactRewardValidation
    {
        private static int _checks;

        public static async UniTask RunChecksAsync()
        {
            _checks = 0;
            await UniTask.NextFrame();
            var panel = UnityEngine.Object.FindFirstObjectByType<ArtifactRewardPanel>();
            var sample = UnityEngine.Object.FindFirstObjectByType<MvpArtifactRewardSample>();
            Check(panel != null && sample != null && panel.IsVisible, "sample startup");
            Check(UnityEngine.Object.FindFirstObjectByType<Game.Core.GameFlowController>() == null,
                "isolated scene has no game flow to mutate");
            sample.enabled = false;
            panel.ResetReward();
            var fields = new SerializedObject(panel);
            var confirm = Field<Button>(fields, "_confirmButton");
            var clear = Field<Button>(fields, "_clearButton");
            var confirmText = Field<TMP_Text>(fields, "_confirmText");
            var rewardText = Field<TMP_Text>(fields, "_rewardText");
            var status = Field<TMP_Text>(fields, "_statusText");
            var overlay = Field<GameObject>(fields, "_panelRoot");
            var cards = fields.FindProperty("_cards");
            var buttons = new Button[3];
            for (int i = 0; i < 3; i++) buttons[i] = Row<Button>(cards, i, "Button");
            var open = Field<Button>(new SerializedObject(sample), "_openButton");
            var data = MvpArtifactRewardSample.CreateExample("test-victory-1");
            Check(data.Candidates.Count == 3 && data.AwardedGold == 30 && data.AwardedGems == 0, "supplied values");
            Check(!panel.IsVisible, "reset hides");
            Throws<ArgumentNullException>(() => panel.ShowReward(null), "null snapshot rejected");
            Throws<ArgumentException>(() => new ArtifactRewardViewData("bad", 0, 0), "empty candidate list");
            Throws<ArgumentException>(() => new ArtifactRewardViewData("bad", 0, 0, (ArtifactRewardOffer[])null), "null candidate list");
            Throws<ArgumentException>(() => new ArtifactRewardViewData("bad", 0, 0,
                data.Candidates[0], data.Candidates[0], data.Candidates[2]), "duplicate candidate IDs");
            Throws<ArgumentException>(() => new ArtifactRewardViewData("bad", 0, 0,
                data.Candidates[0], null, data.Candidates[2]), "null candidate");
            Throws<ArgumentException>(() => new ArtifactRewardViewData(" ", 0, 0,
                data.Candidates[0], data.Candidates[1], data.Candidates[2]), "blank reward ID");
            Throws<ArgumentOutOfRangeException>(() => new ArtifactRewardViewData("bad", -1, 0,
                data.Candidates[0], data.Candidates[1], data.Candidates[2]), "negative gold");
            Throws<ArgumentOutOfRangeException>(() => new ArtifactRewardViewData("bad", 0, -1,
                data.Candidates[0], data.Candidates[1], data.Candidates[2]), "negative gems");
            var mutable = new[] { data.Candidates[0], data.Candidates[1], data.Candidates[2] };
            var snapshot = new ArtifactRewardViewData("copy", null, null, mutable);
            mutable[0] = data.Candidates[2];
            Check(snapshot.Candidates[0] == data.Candidates[0], "defensive snapshot copy");

            EventSystem.current.SetSelectedGameObject(open.gameObject);
            panel.ShowReward(data);
            Check(panel.IsVisible && panel.SelectedArtifactId == null, "opens with no automatic choice");
            Check(EventSystem.current.currentSelectedGameObject == buttons[0].gameObject, "focus is card, not forfeit");
            Check(!confirm.interactable && status.text.Contains("연결 대기"), "missing receiver locks confirmation");
            Click(confirm);
            Check(!panel.IsRequestPending, "unbound confirm has no request");
            Check(rewardText.text.Contains("+30") && rewardText.text.Contains("+0"), "zero gems displayed without invented reward");
            Check(rewardText.text.Split('\n').Length == 2, "gold and gems on separate lines as in reference");
            Check(confirmText.text == "모두 포기하고 계속" && !clear.interactable, "forfeit meaning visible");
            var requests = new List<ArtifactRewardRequest>();
            Action<ArtifactRewardRequest> receive = request =>
            {
                requests.Add(request);
                confirm.onClick.Invoke(); // 동기 재진입도 한 요청으로 제한되어야 한다.
            };
            panel.ChoiceRequested += receive;
            Check(confirm.interactable, "binding enables confirmation");
            Click(buttons[1]);
            Check(requests.Count == 0 && panel.SelectedArtifactId == data.Candidates[1].ArtifactId,
                "card selection does not apply/submit");
            Click(buttons[2]);
            for (int i = 0; i < 3; i++)
            {
                Check(Row<GameObject>(cards, i, "Selection").activeSelf == (i == 2), "exclusive highlight " + i);
                Check(Row<Image>(cards, i, "Icon").enabled == false && Row<GameObject>(cards, i, "MissingIcon").activeSelf,
                    "missing artwork fallback " + i);
                Check(Row<TMP_Text>(cards, i, "Effect").text == data.Candidates[i].EffectDescription, "effect text " + i);
            }
            Check(confirmText.text == "선택 확정", "explicit confirm caption");
            Click(clear);
            Check(panel.SelectedArtifactId == null && confirmText.text.Contains("포기"), "can deselect before forfeit");
            Click(buttons[0]);
            await UniTask.Delay(TimeSpan.FromSeconds(.2), ignoreTimeScale: true);
            await CaptureAsync(panel, 1280, 720, "selected");
            await CaptureAsync(panel, 1920, 1080, "selected");
            Click(confirm);
            Check(requests.Count == 1 && panel.IsRequestPending, "exactly one request including reentrant submit");
            Check(requests[0].RewardId == data.RewardId && requests[0].ArtifactId == data.Candidates[0].ArtifactId &&
                !requests[0].IsForfeit, "selected ID request contract");
            Check(!confirm.interactable && !clear.interactable, "pending actions locked");
            for (int i = 0; i < 3; i++)
            {
                Check(!buttons[i].interactable, "pending card locked " + i);
                Click(buttons[i]);
            }
            Click(clear);
            Click(confirm);
            Check(requests.Count == 1 && panel.SelectedArtifactId == data.Candidates[0].ArtifactId, "double input stable");
            Check(!panel.TryResolveRequest(Guid.NewGuid(), true) && panel.IsRequestPending, "foreign response ignored");
            Throws<InvalidOperationException>(() => panel.ShowReward(MvpArtifactRewardSample.CreateExample("unexpected")),
                "cannot replace in-flight reward implicitly");
            panel.HideReward();
            Check(!panel.IsVisible && panel.IsRequestPending, "hide is not cancel");
            panel.ShowReward(data);
            Check(panel.IsVisible && !confirm.interactable && panel.IsRequestPending, "same reward pending restored");
            panel.gameObject.SetActive(false);
            panel.gameObject.SetActive(true);
            Check(panel.IsVisible && panel.IsRequestPending, "disable/enable pending retained");
            Check(panel.TryResolveRequest(requests[0].RequestId, false, "테스트 거절"), "rejection accepted");
            Check(confirm.interactable && status.text.Contains("테스트 거절"), "rejection feedback and retry");
            Check(EventSystem.current.currentSelectedGameObject == buttons[0].gameObject, "rejection restores navigable focus");
            Click(confirm);
            Check(requests.Count == 2 && requests[1].RequestId != requests[0].RequestId, "retry uses new request ID");
            Check(!panel.TryResolveRequest(requests[0].RequestId, true) && panel.IsRequestPending, "late old response ignored");
            Check(panel.TryResolveRequest(requests[1].RequestId, true), "success resolved");
            Check(!panel.IsVisible && !panel.IsRequestPending, "success closes");
            panel.ShowReward(data);
            Check(!panel.IsVisible, "completed same reward cannot be reopened for duplicate claim");
            Check(!panel.TryResolveRequest(requests[1].RequestId, true), "duplicate success ignored");

            var next = MvpArtifactRewardSample.CreateExample("test-victory-2");
            EventSystem.current.SetSelectedGameObject(open.gameObject);
            panel.ShowReward(next);
            Click(confirm);
            Check(requests.Count == 3 && requests[2].IsForfeit && requests[2].ArtifactId == null, "forfeit request is explicit");
            panel.gameObject.SetActive(false);
            Check(panel.TryResolveRequest(requests[2].RequestId, true), "disabled view receives authoritative success");
            panel.gameObject.SetActive(true);
            Check(!panel.IsVisible, "disabled success does not reopen");
            Check(EventSystem.current.currentSelectedGameObject == open.gameObject, "previous valid focus restored");

            panel.ShowReward(MvpArtifactRewardSample.CreateExample("old-run"));
            Click(buttons[1]);
            Click(confirm);
            var stale = requests[3];
            panel.ResetReward();
            panel.ShowReward(snapshot);
            Check(!panel.TryResolveRequest(stale.RequestId, true) && panel.IsVisible, "reset invalidates old UI response");
            Check(panel.SelectedArtifactId == null && rewardText.text.Contains("--"), "new session and unknown currency");
            var alternate = MvpArtifactRewardSample.CreateExample(snapshot.RewardId);
            panel.ShowReward(alternate);
            Check(rewardText.text.Contains("--"), "same reward ID preserves original snapshot");
            for (int i = 0; i < 8; i++)
            {
                panel.gameObject.SetActive(false);
                panel.gameObject.SetActive(true);
            }
            Click(buttons[2]);
            int before = requests.Count;
            Click(confirm);
            Check(requests.Count == before + 1, "repeated enable does not duplicate subscriptions");
            panel.ChoiceRequested -= receive;
            Check(panel.IsRequestPending, "receiver removal does not falsely cancel outstanding request");
            Check(panel.TryResolveRequest(requests[requests.Count - 1].RequestId, false), "response after receiver removed");
            Check(!confirm.interactable && status.text.Contains("다시 시도"), "failure still shown without receiver");
            panel.ResetReward();

            Action<ArtifactRewardRequest> synchronous = request => panel.TryResolveRequest(request.RequestId, true);
            panel.ChoiceRequested += synchronous;
            panel.ShowReward(MvpArtifactRewardSample.CreateExample("sync"));
            // EventSystem 키보드/패드 탐색에 해당하는 합성 move/submit. 실제 장치 입력은 아님.
            ExecuteEvents.Execute(buttons[0].gameObject, new AxisEventData(EventSystem.current)
                { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
            Check(EventSystem.current.currentSelectedGameObject == buttons[1].gameObject, "explicit right navigation");
            ExecuteEvents.Execute(buttons[1].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Check(panel.SelectedArtifactId == data.Candidates[1].ArtifactId, "submit on card only selects");
            ExecuteEvents.Execute(buttons[1].gameObject, new AxisEventData(EventSystem.current)
                { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
            Check(EventSystem.current.currentSelectedGameObject == confirm.gameObject, "down navigation to confirm");
            ExecuteEvents.Execute(confirm.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Check(!panel.IsVisible && !panel.IsRequestPending, "synchronous completion is reentrancy safe");
            panel.ChoiceRequested -= synchronous;

            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            try
            {
                panel.ShowReward(new ArtifactRewardViewData("icon", 0, null,
                    new ArtifactRewardOffer("icon", "실제 이미지 슬롯 검사", "희귀", "제공된 Sprite를 그대로 표시합니다.", sprite, Color.cyan),
                    data.Candidates[1], data.Candidates[2]));
                Check(Row<Image>(cards, 0, "Icon").sprite == sprite && Row<Image>(cards, 0, "Icon").enabled &&
                    !Row<GameObject>(cards, 0, "MissingIcon").activeSelf, "provided icon replaces fallback");
                Check(Row<TMP_Text>(cards, 0, "Rarity").color == Color.cyan, "provided rarity color");
                panel.HideReward();
                Click(buttons[0]);
                Check(panel.SelectedArtifactId == null, "hidden card cannot select");
            }
            finally
            {
                panel.ResetReward();
                UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(texture);
            }
            panel.ChoiceRequested += receive;
            panel.ShowReward(MvpArtifactRewardSample.CreateExample("final-preview"));
            await UniTask.Delay(TimeSpan.FromSeconds(.3), ignoreTimeScale: true);
            Check(confirm.interactable, "forfeit enabled with receiver");
            await WaitForEnabledTint(confirm);
            Canvas.ForceUpdateCanvases();
            var raycasts = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                { position = new Vector2(5, 5) }, raycasts);
            Check(raycasts.Count > 0 && raycasts[0].gameObject == overlay, "fullscreen overlay blocks background pointer");
            await CaptureAsync(panel, 1280, 720, "unselected");
            await CaptureAsync(panel, 1920, 1080, "unselected");
            var events = EventSystem.current;
            events.enabled = false;
            Check(EventSystem.current == null, "event system can disappear before panel during teardown");
            panel.gameObject.SetActive(false);
            Check(!panel.IsVisible, "hide tolerates missing event system");
            panel.gameObject.SetActive(true);
            Check(panel.IsVisible, "UI still works before event system returns");
            events.enabled = true;
            panel.ResetReward();
            panel.ChoiceRequested -= receive;
            await RunPagingChecksAsync(panel);
            sample.enabled = true;
            sample.ShowNextReward();
            Debug.Log("[UI/MvpArtifactRewardValidation] PASS " + _checks + " checks. No gameplay changes; synthetic UI input only.");
        }

        private static void Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }

        private static async UniTask RunPagingChecksAsync(ArtifactRewardPanel panel)
        {
            var fields = new SerializedObject(panel);
            var cards = fields.FindProperty("_cards");
            var previous = Field<Button>(fields, "_previousPageButton");
            var next = Field<Button>(fields, "_nextPageButton");
            var confirm = Field<Button>(fields, "_confirmButton");
            var clear = Field<Button>(fields, "_clearButton");
            var pageText = Field<TMP_Text>(fields, "_pageText");
            var instruction = Field<TMP_Text>(fields, "_instructionText");
            var status = Field<TMP_Text>(fields, "_statusText");
            var buttons = new[] { Row<Button>(cards, 0, "Button"), Row<Button>(cards, 1, "Button"), Row<Button>(cards, 2, "Button") };
            var requests = new List<ArtifactRewardRequest>();
            Action<ArtifactRewardRequest> receive = requests.Add;
            panel.ChoiceRequested += receive;
            try
            {
                foreach (int count in new[] { 1, 2, 3, 4, 5, 7, 25 })
                {
                    panel.ResetReward();
                    requests.Clear();
                    var data = MvpArtifactRewardSample.CreateExample("count-" + count, count);
                    panel.ShowReward(data);
                    int pages = (count + 2) / 3;
                    Check(data.Candidates.Count == count && panel.PageCount == pages, count + " candidates retained, no truncation");
                    Check(panel.CurrentPageIndex == 0 && panel.SelectedArtifactId == null, "new reward resets page and selection");
                    Check(instruction.text.Contains("총 " + count + "개"), "count-aware instruction");
                    Check(pageText.gameObject.activeSelf == (pages > 1) && next.gameObject.activeSelf == (pages > 1), "paging only when needed");
                    previous.onClick.Invoke();
                    Check(panel.CurrentPageIndex == 0, "cannot move before first page");
                    for (int p = 0; p < pages; p++)
                    {
                        Check(panel.CurrentPageIndex == p && pageText.text == (p + 1) + " / " + pages, "correct page indicator");
                        Check(previous.interactable == (p > 0) && next.interactable == (p < pages - 1), "paging bounds");
                        Check(previous.navigation.selectOnRight == buttons[0] &&
                            next.navigation.selectOnLeft == buttons[Math.Min(3, count - p * 3) - 1],
                            "side paging navigation returns to the adjacent card");
                        for (int i = 0; i < 3; i++)
                        {
                            int index = p * 3 + i;
                            Check(buttons[i].gameObject.activeSelf == (index < count), "unused card slots hidden");
                            if (index < count)
                            {
                                Check(Row<TMP_Text>(cards, i, "Name").text == data.Candidates[index].DisplayName,
                                    "visible slot maps to global candidate " + index);
                                Click(buttons[i]);
                                Check(panel.SelectedArtifactId == data.Candidates[index].ArtifactId, "selection uses global index");
                                for (int j = 0; j < 3; j++)
                                    Check(Row<GameObject>(cards, j, "Selection").activeSelf == (j == i), "single highlight on current page");
                            }
                            else
                            {
                                string selected = panel.SelectedArtifactId;
                                buttons[i].onClick.Invoke();
                                Check(panel.SelectedArtifactId == selected, "hidden slot callback cannot select stale candidate");
                            }
                        }
                        Check(requests.Count == 0, "browsing and selection do not submit");
                        string beforePageChange = panel.SelectedArtifactId;
                        next.onClick.Invoke();
                        Check(panel.CurrentPageIndex == Math.Min(p + 1, pages - 1) && panel.SelectedArtifactId == beforePageChange,
                            "next page preserves selection and stays in range");
                    }
                    // Navigate away from the selected last offer, then submit from the first page.
                    while (panel.CurrentPageIndex > 0) Click(previous);
                    Check(panel.SelectedArtifactId == data.Candidates[count - 1].ArtifactId &&
                        status.text.Contains(data.Candidates[count - 1].DisplayName), "off-page selected offer remains explicit");
                    Click(confirm);
                    Check(requests.Count == 1 && requests[0].ArtifactId == data.Candidates[count - 1].ArtifactId &&
                        requests[0].RewardId == data.RewardId, "off-page confirmation sends correct identity");
                    next.onClick.Invoke();
                    previous.onClick.Invoke();
                    Check(panel.CurrentPageIndex == 0 && !next.interactable && !previous.interactable,
                        "pending request locks page navigation");
                    Check(panel.TryResolveRequest(requests[0].RequestId, false, "페이지 재시도"), "page selection request can be rejected");
                    Check(panel.SelectedArtifactId == data.Candidates[count - 1].ArtifactId, "rejection retains off-page selection");
                    Click(clear);
                    Click(confirm);
                    Check(requests.Count == 2 && requests[1].IsForfeit, "clear from any page then forfeit");
                    Check(panel.TryResolveRequest(requests[1].RequestId, true) && !panel.IsVisible, "forfeit completion closes variable reward");
                }

                panel.ResetReward();
                var five = MvpArtifactRewardSample.CreateExample("five-reopen", 5);
                panel.ShowReward(five);
                EventSystem.current.SetSelectedGameObject(buttons[2].gameObject);
                ExecuteEvents.Execute(buttons[2].gameObject, new AxisEventData(EventSystem.current)
                    { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
                Check(EventSystem.current.currentSelectedGameObject == next.gameObject, "keyboard navigation reaches next page");
                ExecuteEvents.Execute(next.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Check(panel.CurrentPageIndex == 1 && EventSystem.current.currentSelectedGameObject == buttons[0].gameObject,
                    "last-page disabled next button returns focus to a visible card");
                Click(buttons[1]);
                panel.HideReward();
                panel.ShowReward(five);
                Check(panel.CurrentPageIndex == 1 && panel.SelectedArtifactId == five.Candidates[4].ArtifactId,
                    "hide/show retains global selection and page");
                panel.gameObject.SetActive(false);
                panel.gameObject.SetActive(true);
                Check(panel.CurrentPageIndex == 1 && panel.SelectedArtifactId == five.Candidates[4].ArtifactId,
                    "disable/enable retains global selection and page");
                panel.ShowReward(MvpArtifactRewardSample.CreateExample(five.RewardId, 1));
                Check(panel.PageCount == 2 && instruction.text.Contains("총 5개"), "same reward snapshot cannot silently shrink");
                await WaitForEnabledTint(confirm);
                await CaptureAsync(panel, 1280, 720, "five-page2");
                await CaptureAsync(panel, 1920, 1080, "five-page2");
                EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
                ExecuteEvents.Execute(buttons[0].gameObject, new AxisEventData(EventSystem.current)
                    { moveDir = MoveDirection.Left }, ExecuteEvents.moveHandler);
                Check(EventSystem.current.currentSelectedGameObject == previous.gameObject, "keyboard navigation reaches previous page");
                ExecuteEvents.Execute(previous.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Check(panel.CurrentPageIndex == 0 && EventSystem.current.currentSelectedGameObject == buttons[0].gameObject,
                    "first page focuses visible card when previous becomes disabled");
                Check(panel.SelectedArtifactId == five.Candidates[4].ArtifactId, "keyboard paging never changes chosen artifact");
                Click(confirm);
                var stale = requests[requests.Count - 1];
                panel.ResetReward();
                panel.ShowReward(MvpArtifactRewardSample.CreateExample("new-single", 1));
                Check(!panel.TryResolveRequest(stale.RequestId, true) && panel.IsVisible && panel.CurrentPageIndex == 0,
                    "reset from multi-page to single invalidates old pending response");
                Check(!buttons[1].gameObject.activeSelf && !buttons[2].gameObject.activeSelf &&
                    !next.gameObject.activeSelf && panel.SelectedArtifactId == null, "single candidate has no stale cards or selection");
                await WaitForEnabledTint(confirm);
                await CaptureAsync(panel, 1280, 720, "single");
                await CaptureAsync(panel, 1920, 1080, "single");
            }
            finally
            {
                panel.ResetReward();
                panel.ChoiceRequested -= receive;
            }
        }

        private static async UniTask WaitForEnabledTint(Button button)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (button.targetGraphic.canvasRenderer.GetColor().a < .99f)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new InvalidOperationException("Enabled button tint did not recover: " +
                        button.targetGraphic.canvasRenderer.GetColor());
                await UniTask.NextFrame();
            }
            Check(true, "enabled action is visually enabled after transition");
        }

        private static T Field<T>(SerializedObject fields, string name) where T : UnityEngine.Object =>
            (T)fields.FindProperty(name).objectReferenceValue;

        private static T Row<T>(SerializedProperty cards, int index, string field) where T : UnityEngine.Object =>
            (T)cards.GetArrayElementAtIndex(index).FindPropertyRelative(field).objectReferenceValue;

        private static void Check(bool condition, string message)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Artifact reward: " + message);
        }

        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { Check(true, message); return; }
            Check(false, message);
        }

        private static async UniTask CaptureAsync(ArtifactRewardPanel panel, int width, int height, string suffix)
        {
            Check(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "graphics required for layout validation");
            await UniTask.NextFrame();
            // Existing HUD validator also uses a fresh native Canvas per snapshot:
            // same-frame hide/show can otherwise leave stale offscreen render batches.
            // Clone the view subtree only, never its controller or gameplay components.
            var sourceCanvas = panel.GetComponent<Canvas>();
            bool sourceEnabled = sourceCanvas.enabled;
            var cameraObject = new GameObject("Artifact Snapshot Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(15, 22, 31, 255);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            var root = new GameObject("Artifact Snapshot Canvas", typeof(RectTransform), typeof(Canvas));
            root.SetActive(false);
            var sourceView = Field<GameObject>(new SerializedObject(panel), "_panelRoot");
            var view = UnityEngine.Object.Instantiate(sourceView, root.transform, false);
            var canvas = root.GetComponent<Canvas>();
            var target = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                sourceCanvas.enabled = false;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.scaleFactor = Mathf.Sqrt((width / 1920f) * (height / 1080f));
                root.SetActive(true);
                Canvas.ForceUpdateCanvases();
                foreach (var label in canvas.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Check(label.font.HasCharacters(label.text, out uint[] missing, false, true), "glyphs " + label.name);
                    Check(!label.isTextOverflowing, width + " text fits " + label.name);
                }
                Canvas.ForceUpdateCanvases();
                CheckReferenceLayout(view.transform);
                var corners = new Vector3[4];
                foreach (var button in canvas.GetComponentsInChildren<Button>())
                {
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToScreenPoint(corner);
                        Check(point.x >= 0 && point.x <= width && point.y >= 0 && point.y <= height,
                            width + " button bounds " + button.name);
                    }
                }
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                for (int i = 0; i < 3; i++)
                {
                    var card = view.transform.Find("RewardCard/Candidate" + i);
                    if (!card.gameObject.activeInHierarchy) continue;
                    var point = camera.WorldToScreenPoint(card.TransformPoint(new Vector3(10, 10, 0)));
                    var pixel = pixels.GetPixel((int)point.x, (int)point.y);
                    Check(pixel.g > .12f && pixel.b > .16f, "snapshot actually rendered candidate " + i);
                }
                Directory.CreateDirectory("Logs/ArtifactRewardValidation");
                File.WriteAllBytes("Logs/ArtifactRewardValidation/reward-" + width + "x" + height + "-" + suffix + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                canvas.worldCamera = null;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
                sourceCanvas.enabled = sourceEnabled;
            }
        }

        private static void CheckReferenceLayout(Transform view)
        {
            var board = view.Find("RewardCard");
            var boardRect = WorldRect(board);
            var title = WorldRect(board.Find("Title"));
            var rewards = WorldRect(board.Find("Reward"));
            var confirm = WorldRect(board.Find("Confirm"));
            var clear = WorldRect(board.Find("Clear"));
            Check(Mathf.Abs(title.center.x - boardRect.center.x) < .01f, "reference centered victory heading");
            Check(Mathf.Abs(rewards.center.x - boardRect.center.x) < .01f, "reference centered currency summary");
            Check(title.yMin > rewards.yMax, "reference heading above rewards");
            Check(Mathf.Abs(confirm.center.x - boardRect.center.x) < .01f, "reference centered primary action");
            Check(clear.xMax < confirm.xMin, "secondary clear action separated from primary action");
            Rect previous = default;
            for (int i = 0; i < 3; i++)
            {
                var candidate = board.Find("Candidate" + i);
                if (!candidate.gameObject.activeInHierarchy) continue;
                var rect = WorldRect(candidate);
                var icon = WorldRect(candidate.Find("IconFrame"));
                var name = WorldRect(candidate.Find("Name"));
                var rarity = WorldRect(candidate.Find("Rarity"));
                var effect = WorldRect(candidate.Find("Effect"));
                Check(rect.height > rect.width, "reference portrait card " + i);
                Check(rewards.yMin > rect.yMax && confirm.yMax < rect.yMin, "reference reward/card/action vertical order " + i);
                Check(icon.yMin > name.yMax && name.yMin > rarity.yMax && rarity.yMin > effect.yMax,
                    "reference image/name/rarity/effect order without overlap " + i);
                foreach (var content in new[] { icon, name, rarity, effect })
                    Check(content.xMin >= rect.xMin && content.xMax <= rect.xMax &&
                        content.yMin >= rect.yMin && content.yMax <= rect.yMax, "card content fits " + i);
                if (i > 0) Check(previous.xMax < rect.xMin, "candidate cards do not overlap");
                previous = rect;
            }
            var first = WorldRect(board.Find("Candidate0"));
            var last = WorldRect(board.Find("Candidate2"));
            Check(WorldRect(board.Find("PreviousPage")).xMax < first.xMin &&
                WorldRect(board.Find("NextPage")).xMin > last.xMax, "paging is outside candidate card area");
        }

        private static Rect WorldRect(Transform transform)
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
