
using UnityEngine;
using UnityEditor;

public static class AnchorToCorners
{
    [MenuItem("Tools/UI/Anchors to Corners")]
    private static void ApplyAnchors()
    {
        Canvas.ForceUpdateCanvases();

        foreach (GameObject obj in Selection.gameObjects)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();

            if (rect == null)
                continue;

            RectTransform parent = rect.parent as RectTransform;

            if (parent == null)
                continue;

            Vector2 parentSize = parent.rect.size;

            if (Mathf.Approximately(parentSize.x, 0f) ||
                Mathf.Approximately(parentSize.y, 0f))
                continue;

            Vector2 min = rect.anchorMin;
            Vector2 max = rect.anchorMax;

            Vector2 offsetMin = rect.offsetMin;
            Vector2 offsetMax = rect.offsetMax;

            // 현재 오브젝트의 모서리를 부모 기준 비율로 계산
            Vector2 newMin = min + new Vector2(
                offsetMin.x / parentSize.x,
                offsetMin.y / parentSize.y
            );

            Vector2 newMax = max + new Vector2(
                offsetMax.x / parentSize.x,
                offsetMax.y / parentSize.y
            );

            Undo.RecordObject(rect, "Anchors to Corners");

            // 현재 모서리에 Anchor 배치
            rect.anchorMin = newMin;
            rect.anchorMax = newMax;

            // Anchor와 UI 모서리 사이의 간격 제거
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 프리팹 변경 사항 기록
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            EditorUtility.SetDirty(rect);
        }
    }
}
