using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Shows a VideoPlayer -> RenderTexture -> RawImage only after video frames arrive.
/// Crucially, restarts preparation every time its panel is enabled, not only once in Start().
/// Replace the previous VideoFirstFrameFix.cs in place; keep its .meta file unchanged.
/// </summary>
public sealed class VideoFirstFrameFix : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage rawImage;

    [Header("Diagnostics")]
    [SerializeField, Min(1f)] private float firstFrameTimeoutSeconds = 8f;
    [SerializeField] private bool logTransitions = true;

    private Color visibleColor = Color.white;
    private bool waitingForFrame;
    private bool revealQueued;
    private bool timeoutLogged;
    private float prepareStartedAt;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
        if (rawImage == null)
            rawImage = GetComponent<RawImage>();

        if (rawImage != null)
            visibleColor = rawImage.color;
    }

    private void OnEnable()
    {
        if (videoPlayer == null || rawImage == null)
        {
            Debug.LogError("[VideoFirstFrameFix] VideoPlayer/RawImage reference is missing.", this);
            return;
        }

        SetVisible(false);
        waitingForFrame = true;
        revealQueued = false;
        timeoutLogged = false;
        prepareStartedAt = Time.unscaledTime;

        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.frameReady += OnFrameReady;
        videoPlayer.errorReceived += OnError;
        videoPlayer.sendFrameReadyEvents = true;

        // Do not start preparation before the entire panel activation settles.
        StartCoroutine(PrepareOnNextFrame());
        Log("OnEnable - waiting to prepare");
    }

    private IEnumerator PrepareOnNextFrame()
    {
        yield return null;
        if (!isActiveAndEnabled || videoPlayer == null)
            yield break;

        videoPlayer.Stop();
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.sendFrameReadyEvents = true;
        prepareStartedAt = Time.unscaledTime;

        Log("Prepare requested");
        videoPlayer.Prepare();
    }

    private void OnPrepared(VideoPlayer player)
    {
        if (!isActiveAndEnabled || player != videoPlayer)
            return;

        Log("prepareCompleted -> Play");
        player.Play();
    }

    private void OnFrameReady(VideoPlayer player, long frameIndex)
    {
        if (!isActiveAndEnabled || player != videoPlayer || !waitingForFrame)
            return;

        Log("frameReady: " + frameIndex);
        QueueReveal();
    }

    private void Update()
    {
        if (!waitingForFrame || videoPlayer == null)
            return;

        // Fallback for platforms where frameReady is missed.
        // Do not reveal on isPrepared alone: a prepared video may not have rendered.
        if (!revealQueued && videoPlayer.isPlaying && videoPlayer.frame >= 1)
        {
            Log("Decoded frame detected without frameReady event (fallback)");
            QueueReveal();
        }

        if (!timeoutLogged && Time.unscaledTime - prepareStartedAt >= firstFrameTimeoutSeconds)
        {
            timeoutLogged = true;
            Debug.LogWarning(
                "[VideoFirstFrameFix] First frame timeout. " +
                "active=" + gameObject.activeInHierarchy +
                ", prepared=" + videoPlayer.isPrepared +
                ", playing=" + videoPlayer.isPlaying +
                ", frame=" + videoPlayer.frame +
                ", clip=" + (videoPlayer.clip != null ? videoPlayer.clip.name : "NULL") +
                ", target=" + (videoPlayer.targetTexture != null ? videoPlayer.targetTexture.name : "NULL"),
                this);
        }
    }

    private void QueueReveal()
    {
        if (revealQueued)
            return;

        revealQueued = true;
        StartCoroutine(RevealNextFrame());
    }

    private IEnumerator RevealNextFrame()
    {
        // Let Unity finish the decoded frame / UI update before making it visible.
        yield return null;
        if (!isActiveAndEnabled || videoPlayer == null || !waitingForFrame)
            yield break;

        waitingForFrame = false;
        SetVisible(true);
        videoPlayer.sendFrameReadyEvents = false;
        videoPlayer.frameReady -= OnFrameReady;
        Log("RawImage visible; video playing=" + videoPlayer.isPlaying);
    }

    private void OnError(VideoPlayer player, string message)
    {
        if (player != videoPlayer)
            return;
        Debug.LogError("[VideoFirstFrameFix] VideoPlayer error: " + message, this);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        waitingForFrame = false;
        revealQueued = false;

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepared;
            videoPlayer.frameReady -= OnFrameReady;
            videoPlayer.errorReceived -= OnError;
            videoPlayer.sendFrameReadyEvents = false;
            videoPlayer.Stop();
        }

        SetVisible(false);
        Log("OnDisable - stopped");
    }

    private void SetVisible(bool visible)
    {
        if (rawImage == null)
            return;
        Color color = visibleColor;
        color.a = visible ? visibleColor.a : 0f;
        rawImage.color = color;
    }

    private void Log(string message)
    {
        if (logTransitions)
            Debug.Log("[VideoFirstFrameFix] " + gameObject.name + ": " + message, this);
    }
}
