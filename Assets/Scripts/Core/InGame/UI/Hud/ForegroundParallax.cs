using Game.Cameras;
using UnityEngine;

/// <summary>
/// Orthographic 2D foreground parallax.
///
/// - Never writes to any camera or CameraArea.
/// - Chooses ONE uniform foreground scale for the expected camera movement/zoom
///   envelope (and whenever screen aspect / bounds settings change).
/// - During normal play, ONLY the foreground position changes; zoom is the
///   camera's natural projection, not a per-frame sprite resize.
/// - Horizontal endpoints are exactly aligned at either camera stop.
/// - Vertical motion stays genuinely parallaxed; no per-frame Y edge clamp.
///
/// The foreground must have an axis-aligned SpriteRenderer (Draw Mode: Simple).
/// Best used without a rotated/non-uniformly scaled parent.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(32000)]
public sealed class ForegroundParallax : MonoBehaviour
{
    [Header("Read-only references")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private CameraArea leftArea;
    [SerializeField] private CameraArea rightArea;

    [Header("Horizontal parallax")]
    [Tooltip("Target screen-space movement at the reference zoom. 0.6 = 60%. " +
             "If extra height requires a wider foreground, endpoint alignment takes priority.")]
    [SerializeField, Range(0f, 1f)] private float horizontalParallax = 0.6f;

    [Tooltip("At the left/right camera stops, align the respective sprite edge " +
             "to the screen edge at every zoom. The sprite scale stays fixed.")]
    [SerializeField] private bool exactEdgeAlignmentAtEveryZoom = true;

    [Tooltip("Treat the fixed BaseCamp camera center as the leftmost camera stop.")]
    [SerializeField] private bool leftStopAtBaseAreaCenter = true;

    [Header("Zoom reference")]
    [Tooltip("0 = derive a reference size from both camera areas. The camera " +
             "can still zoom independently; this field does NOT override its zoom.")]
    [SerializeField, Min(0f)] private float referenceOrthographicSize = 0f;

    [Header("Vertical parallax")]
    [Tooltip("Reference image bottom offset, measured in reference screen heights. " +
             "0 = start from the reference screen bottom, then add safety overlap.")]
    [SerializeField, Range(-2f, 2f)] private float bottomOffsetAtReference = 0f;

    [Tooltip("Fraction of normal world-object screen travel. 0.6 = move 60% " +
             "as far on screen when the camera moves vertically.")]
    [SerializeField, Range(0f, 1f)] private float verticalParallax = 0.6f;

    [Header("Fixed-size edge protection")]
    [Tooltip("Pre-size the foreground for the whole camera movement envelope. " +
             "NO runtime scale switching or vertical edge clamp is used.")]
    [SerializeField] private bool keepVerticalEdgesOutsideView = true;

    [Tooltip("Extra overlap beyond the camera edges in screen pixels, at reference zoom.")]
    [SerializeField, Range(0f, 8f)] private float verticalBleedPixels = 2f;

    [Tooltip("Extra WORLD units of vertical room for camera moves beyond the CameraArea " +
             "limits, e.g. scripted cinematics or focus views. This is baked " +
             "into the scale once rather than enforced by following the camera.")]
    [SerializeField, Min(0f)] private float extraVerticalCameraRange = 0f;

    [Tooltip("Rebuild fixed layout on screen aspect or CameraArea bound changes. " +
             "Normal camera zoom/pan never triggers rebuilding.")]
    [SerializeField] private bool rebuildWhenBoundsChange = true;

    private SpriteRenderer spriteRendererCache;
    private Vector3 authoredLocalScale;
    private Sprite authoredSprite;
    private bool initialized;
    private bool warned;
    private float layoutAspect;
    private Rect layoutLeftBounds;
    private Rect layoutRightBounds;
    private float layoutReferenceSize;
    private float worldSpriteWidth;
    private float referenceCameraY;
    private float anchoredWorldBottom;

    private void Reset()
    {
        worldCamera = Camera.main;
    }

    private void Awake()
    {
        spriteRendererCache = GetComponent<SpriteRenderer>();
        authoredLocalScale = transform.localScale;
    }

    private void LateUpdate()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
        if (spriteRendererCache == null)
            spriteRendererCache = GetComponent<SpriteRenderer>();

        if (worldCamera == null || !worldCamera.orthographic ||
            leftArea == null || rightArea == null ||
            spriteRendererCache == null || spriteRendererCache.sprite == null)
        {
            if (!warned)
            {
                Debug.LogWarning("[ForegroundParallax] Assign Main Camera, both " +
                                 "CameraAreas, and a SpriteRenderer Sprite.", this);
                warned = true;
            }
            return;
        }
        warned = false;

        float aspect = Mathf.Max(0.001f, worldCamera.aspect);
        Rect leftBounds = leftArea.Bounds;
        Rect rightBounds = rightArea.Bounds;
        Sprite sprite = spriteRendererCache.sprite;

        if (!initialized || sprite != authoredSprite ||
            Mathf.Abs(aspect - layoutAspect) > 0.0001f ||
            (rebuildWhenBoundsChange &&
             (!SameRect(leftBounds, layoutLeftBounds) ||
              !SameRect(rightBounds, layoutRightBounds))))
        {
            BuildFixedLayout(sprite, aspect, leftBounds, rightBounds);
        }
        if (!initialized)
            return;

        // No scale writes in ordinary camera pan/zoom: Unity's own orthographic
        // projection naturally magnifies the foreground as the camera zooms in.
        float size = Mathf.Max(0.0001f, worldCamera.orthographicSize);
        float halfWidth = size * aspect;
        Vector3 cameraPosition = worldCamera.transform.position;
        float leftStop = LeftStop(leftBounds, halfWidth);
        float rightStop = Mathf.Max(leftStop, RightStop(rightBounds, halfWidth));

        float screenLeft = cameraPosition.x - halfWidth;
        float screenRight = cameraPosition.x + halfWidth;
        float desiredWorldLeft;

        if (exactEdgeAlignmentAtEveryZoom)
        {
            float span = rightStop - leftStop;
            float progress = span > 0.00001f
                ? Mathf.Clamp01((cameraPosition.x - leftStop) / span)
                : 0.5f;

            // Left stop: sprite's left edge == screen left.
            // Right stop: sprite's right edge == screen right.
            desiredWorldLeft = Mathf.Lerp(
                screenLeft, screenRight - worldSpriteWidth, progress);
        }
        else
        {
            float refHalfWidth = layoutReferenceSize * aspect;
            float leftStopRef = LeftStop(leftBounds, refHalfWidth);
            desiredWorldLeft = leftStopRef - refHalfWidth +
                (1f - horizontalParallax) * (cameraPosition.x - leftStopRef);
        }

        // Horizontal safety clamp only; with a suitable worldSpriteWidth it
        // does nothing except when a cinematic leaves the designed bounds.
        desiredWorldLeft = Mathf.Clamp(
            desiredWorldLeft,
            screenRight - worldSpriteWidth,
            screenLeft);

        // Crucial sign: if the camera goes UP by +10 and verticalParallax=0.6,
        // world foreground goes UP only +4, thus on SCREEN it goes DOWN by 6.
        // Never clamp to screenTop/screenBottom per frame: doing so would
        // force the foreground to chase the camera and ruin parallax.
        float desiredWorldBottom = anchoredWorldBottom +
            (1f - verticalParallax) * (cameraPosition.y - referenceCameraY);

        float worldScaleX = transform.lossyScale.x;
        float worldScaleY = transform.lossyScale.y;
        float leftLocal = worldScaleX >= 0f
            ? sprite.bounds.min.x : sprite.bounds.max.x;
        float bottomLocal = worldScaleY >= 0f
            ? sprite.bounds.min.y : sprite.bounds.max.y;

        Vector3 result = transform.position;
        result.x = desiredWorldLeft - leftLocal * worldScaleX;
        result.y = desiredWorldBottom - bottomLocal * worldScaleY;
        transform.position = result;
    }

    private void BuildFixedLayout(Sprite sprite, float aspect, Rect leftBounds,
        Rect rightBounds)
    {
        initialized = false;

        // Reference and safety zoom: the largest expected camera viewport.
        float leftMaxSize = MaximumSize(leftBounds, aspect);
        float rightMaxSize = MaximumSize(rightBounds, aspect);
        float maxExpectedSize = Mathf.Max(leftMaxSize, rightMaxSize);
        float refSize = referenceOrthographicSize > 0f
            ? referenceOrthographicSize : maxExpectedSize;
        maxExpectedSize = Mathf.Max(maxExpectedSize, refSize);

        float refHalfWidth = refSize * aspect;
        float refLeftStop = LeftStop(leftBounds, refHalfWidth);
        float refRightStop = Mathf.Max(refLeftStop,
                                      RightStop(rightBounds, refHalfWidth));
        float targetWidth = 2f * refHalfWidth +
            horizontalParallax * (refRightStop - refLeftStop);
        targetWidth = Mathf.Max(targetWidth, 2f * maxExpectedSize * aspect);

        // Authoring dimensions are preserved: ONE uniform X/Y multiplier.
        float parentScaleX = transform.parent == null
            ? 1f : Mathf.Abs(transform.parent.lossyScale.x);
        float parentScaleY = transform.parent == null
            ? 1f : Mathf.Abs(transform.parent.lossyScale.y);
        float baseWidth = sprite.bounds.size.x *
            Mathf.Abs(authoredLocalScale.x) * parentScaleX;
        float baseHeight = sprite.bounds.size.y *
            Mathf.Abs(authoredLocalScale.y) * parentScaleY;
        if (baseWidth < 0.00001f || baseHeight < 0.00001f)
        {
            Debug.LogError("[ForegroundParallax] Sprite or initial scale has " +
                           "zero size; cannot build the layout.", this);
            return;
        }

        referenceCameraY = leftBounds.center.y;
        float preferredBottom = referenceCameraY - refSize +
            bottomOffsetAtReference * 2f * refSize;

        // At each area's largest valid orthographic size, its camera-center
        // Y range is the tightest. For 0<=p<=1, these extrema also cover all
        // more zoomed-in views (including the camera's clamp behavior).
        // Use each area's OWN maximum size so we don't needlessly blow up the
        // image to cover camera positions/sizes that can never occur together.
        float bleed = verticalBleedPixels * (2f * maxExpectedSize) /
                      Mathf.Max(1, worldCamera.pixelHeight);
        float minAllowedBottom = preferredBottom;
        float highestRequiredTopRelativeToBase = 0f;

        CameraCenterYRange(leftBounds, Mathf.Max(leftMaxSize,
                           referenceOrthographicSize), out float leftMinY,
                           out float leftMaxY);
        CameraCenterYRange(rightBounds, rightMaxSize, out float rightMinY,
                           out float rightMaxY);

        if (keepVerticalEdgesOutsideView)
        {
            float leftS = Mathf.Max(leftMaxSize, referenceOrthographicSize);
            float rightS = rightMaxSize;
            leftMinY -= extraVerticalCameraRange;
            leftMaxY += extraVerticalCameraRange;
            rightMinY -= extraVerticalCameraRange;
            rightMaxY += extraVerticalCameraRange;

            // The image bottom never crosses the camera viewport bottom.
            float limitLeft = verticalParallax * leftMinY - leftS +
                (1f - verticalParallax) * referenceCameraY - bleed;
            float limitRight = verticalParallax * rightMinY - rightS +
                (1f - verticalParallax) * referenceCameraY - bleed;
            minAllowedBottom = Mathf.Min(preferredBottom,
                Mathf.Min(limitLeft, limitRight));

            // The image top never crosses the camera viewport top.
            float requiredLeft = verticalParallax * leftMaxY + leftS +
                (1f - verticalParallax) * referenceCameraY + bleed -
                minAllowedBottom;
            float requiredRight = verticalParallax * rightMaxY + rightS +
                (1f - verticalParallax) * referenceCameraY + bleed -
                minAllowedBottom;
            highestRequiredTopRelativeToBase = Mathf.Max(requiredLeft,
                                                         requiredRight);
        }

        float multiplier = targetWidth / baseWidth;
        if (keepVerticalEdgesOutsideView)
            multiplier = Mathf.Max(multiplier,
                highestRequiredTopRelativeToBase / baseHeight);

        // Only called at startup or when screen aspect / camera AREA changes,
        // never merely because zoom or camera position changes.
        transform.localScale = new Vector3(
            authoredLocalScale.x * multiplier,
            authoredLocalScale.y * multiplier,
            authoredLocalScale.z);
        worldSpriteWidth = baseWidth * multiplier;
        anchoredWorldBottom = minAllowedBottom;
        layoutAspect = aspect;
        layoutLeftBounds = leftBounds;
        layoutRightBounds = rightBounds;
        layoutReferenceSize = refSize;
        authoredSprite = sprite;
        initialized = true;
    }

    private static void CameraCenterYRange(Rect bounds, float size,
        out float minY, out float maxY)
    {
        // Same vertical clamp as the project's CameraBoundsUtility:
        // an area smaller than the viewport locks the camera at its center.
        if (bounds.height <= size * 2f)
        {
            minY = bounds.center.y;
            maxY = bounds.center.y;
        }
        else
        {
            minY = bounds.yMin + size;
            maxY = bounds.yMax - size;
        }
    }

    private float LeftStop(Rect bounds, float halfWidth)
    {
        if (leftStopAtBaseAreaCenter || bounds.width <= 2f * halfWidth)
            return bounds.center.x;
        return bounds.xMin + halfWidth;
    }

    private static float RightStop(Rect bounds, float halfWidth)
    {
        return bounds.width <= 2f * halfWidth
            ? bounds.center.x
            : bounds.xMax - halfWidth;
    }

    private static float MaximumSize(Rect bounds, float aspect)
    {
        return Mathf.Max(0.01f,
            Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * aspect)));
    }

    private static bool SameRect(Rect a, Rect b)
    {
        return Mathf.Abs(a.xMin - b.xMin) < 0.0001f &&
               Mathf.Abs(a.yMin - b.yMin) < 0.0001f &&
               Mathf.Abs(a.width - b.width) < 0.0001f &&
               Mathf.Abs(a.height - b.height) < 0.0001f;
    }

    private void OnValidate()
    {
        horizontalParallax = Mathf.Clamp01(horizontalParallax);
        verticalParallax = Mathf.Clamp01(verticalParallax);
        referenceOrthographicSize = Mathf.Max(0f, referenceOrthographicSize);
        verticalBleedPixels = Mathf.Clamp(verticalBleedPixels, 0f, 8f);
        extraVerticalCameraRange = Mathf.Max(0f, extraVerticalCameraRange);
        initialized = false;
    }
}
