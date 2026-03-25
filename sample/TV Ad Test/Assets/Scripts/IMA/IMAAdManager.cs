using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// IMAAdManager — Unity bootstrap and event hub for the Google IMA SDK Android plugin.
///
/// ───────────────────────────────────────────────────────────────────────────
/// QUICK SETUP
/// ───────────────────────────────────────────────────────────────────────────
///  1. Attach this script to a GameObject in your scene.
///     The GameObject MUST be named "IMAAdManager" (default) or match whatever
///     you type in the "Callback Target Name" inspector field, because Android
///     uses UnitySendMessage("<name>", ...) to fire events.
///
///  2. Drop your ad tag URL into "Default Ad Tag Url" in the Inspector.
///     You can also call RequestAd(url) from code at any time.
///
///  3. Wire up any UnityEvents in the Inspector, or subscribe to the C# events
///     in code (see Events section below).
///
///  4. Build for Android (IL2CPP or Mono). The plugin is no-op in the Editor.
///
/// ───────────────────────────────────────────────────────────────────────────
/// STRICT GAP
/// ───────────────────────────────────────────────────────────────────────────
///  Enable "Use Strict Gap" and set "Strict Gap Seconds" to prevent ads from
///  being requested again within N seconds of the previous ad session ending.
///  The plugin fires OnAdBlockedByGap (with remaining seconds as the message)
///  when a request is rejected by the cooldown.
///
/// ───────────────────────────────────────────────────────────────────────────
/// EVENTS REFERENCE
/// ───────────────────────────────────────────────────────────────────────────
///  All events below map 1-to-1 with the UnitySendMessage method names sent
///  by UnityPlugin.java.  Each event is also exposed as a UnityEvent in the
///  Inspector so you can wire handlers without any code.
///
///  Lifecycle:
///    OnAdLoaded              — Creative loaded; playback about to begin.
///    OnAdStarted             — Playback started.
///    OnAdPaused              — Playback paused.
///    OnAdResumed             — Playback resumed.
///    OnAdCompleted           — Single ad in a pod finished.
///    OnAllAdsCompleted       — Entire ad session finished.
///    OnContentPauseRequested — Your game should pause / mute.
///    OnContentResumeRequested— Your game should resume / unmute.
///
///  Progress:
///    OnAdProgress(msg)       — ~4 Hz while playing. msg = "currentMs/durationMs"
///    OnAdFirstQuartile       — 25 % played.
///    OnAdMidpoint            — 50 % played.
///    OnAdThirdQuartile       — 75 % played.
///
///  Interaction:
///    OnAdClicked             — User tapped the ad.
///    OnAdSkipped             — User tapped Skip.
///    OnAdSkippableStateChanged(msg) — msg = "true" or "false"
///    OnAdIconViewed          — Ad icon was viewed.
///
///  Volume:
///    OnAdVolumeChanged(msg)  — System volume changed. msg = 0-100 as string.
///
///  Errors:
///    OnAdLoaderError(msg)    — AdsLoader failed. msg = error description.
///    OnAdManagerError(msg)   — AdsManager failed. msg = error description.
///
///  Strict-Gap:
///    OnAdBlockedByGap(msg)   — Request rejected. msg = remaining seconds.
/// </summary>
[AddComponentMenu("Ads/IMA Ad Manager")]
public class IMAAdManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — General
    // ─────────────────────────────────────────────────────────────────────────

    [Header("General")]

    [Tooltip("Must match the name of this GameObject so Android can route " +
             "UnitySendMessage callbacks to it correctly.")]
    [SerializeField] private string callbackTargetName = "IMAAdManager";

    [Tooltip("Ad tag URL used when RequestAd() is called without an explicit URL. " +
             "Supports VAST and VMAP tags.")]
    [SerializeField]
    private string defaultAdTagUrl =
        "https://pubads.g.doubleclick.net/gampad/ads?iu=/21775744923/external/" +
        "vmap_ad_samples&sz=640x480&cust_params=sample_ar%3Dpremidpost&" +
        "ciu_szs=300x250&gdfp_req=1&ad_rule=1&output=vmap&unviewed_position_start=1" +
        "&env=vp&impl=s&correlator=";

    [Tooltip("Request and show an ad automatically when the scene starts.")]
    [SerializeField] private bool requestOnStart = false;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Strict Gap
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Strict Gap")]

    [Tooltip("When enabled, ads cannot be requested again until at least " +
             "StrictGapSeconds have elapsed since the previous ad session ended.")]
    [SerializeField] private bool useStrictGap = false;

    [Tooltip("Minimum number of seconds that must pass between ad sessions " +
             "when Strict Gap is enabled.  Has no effect when UseStrictGap is false.")]
    [SerializeField, Min(1)] private int strictGapSeconds = 60;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — UnityEvents (wire in the Inspector, no code required)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Lifecycle Events")]
    [SerializeField] private UnityEvent onAdLoaded = new UnityEvent();
    [SerializeField] private UnityEvent onAdStarted = new UnityEvent();
    [SerializeField] private UnityEvent onAdPaused = new UnityEvent();
    [SerializeField] private UnityEvent onAdResumed = new UnityEvent();
    [SerializeField] private UnityEvent onAdCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onAllAdsCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onContentPauseRequested = new UnityEvent();
    [SerializeField] private UnityEvent onContentResumeRequested = new UnityEvent();

    [Header("Progress Events")]
    [SerializeField] private UnityEvent<string> onAdProgress = new UnityEvent<string>();
    [SerializeField] private UnityEvent onAdFirstQuartile = new UnityEvent();
    [SerializeField] private UnityEvent onAdMidpoint = new UnityEvent();
    [SerializeField] private UnityEvent onAdThirdQuartile = new UnityEvent();

    [Header("Interaction Events")]
    [SerializeField] private UnityEvent onAdClicked = new UnityEvent();
    [SerializeField] private UnityEvent onAdSkipped = new UnityEvent();
    [SerializeField] private UnityEvent<string> onAdSkippableStateChanged = new UnityEvent<string>();
    [SerializeField] private UnityEvent onAdIconViewed = new UnityEvent();


    [Header("Error Events")]
    [SerializeField] private UnityEvent<string> onAdLoaderError = new UnityEvent<string>();
    [SerializeField] private UnityEvent<string> onAdManagerError = new UnityEvent<string>();

    [Header("Strict-Gap Event")]
    [SerializeField] private UnityEvent<string> onAdBlockedByGap = new UnityEvent<string>();

    // ─────────────────────────────────────────────────────────────────────────
    //  C# Events (subscribe from code, fired in addition to UnityEvents)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fired when the ad creative has been loaded.</summary>
    public event Action AdLoaded;

    /// <summary>Fired when ad playback begins.</summary>
    public event Action AdStarted;

    /// <summary>Fired when ad playback is paused.</summary>
    public event Action AdPaused;

    /// <summary>Fired when ad playback resumes after a pause.</summary>
    public event Action AdResumed;

    /// <summary>Fired when a single ad in the pod finishes.</summary>
    public event Action AdCompleted;

    /// <summary>Fired when all ads in the session have finished.</summary>
    public event Action AllAdsCompleted;

    /// <summary>Fired when the ad is about to play; pause/mute your game here.</summary>
    public event Action ContentPauseRequested;

    /// <summary>Fired when the ad session is over; resume your game here.</summary>
    public event Action ContentResumeRequested;

    /// <summary>
    /// Fired ~4 times per second during ad playback.
    /// Argument: "currentMs/durationMs" — split on '/' to get both values.
    /// </summary>
    public event Action<string> AdProgress;

    /// <summary>Fired when 25 % of the ad has played.</summary>
    public event Action AdFirstQuartile;

    /// <summary>Fired when 50 % of the ad has played.</summary>
    public event Action AdMidpoint;

    /// <summary>Fired when 75 % of the ad has played.</summary>
    public event Action AdThirdQuartile;

    /// <summary>Fired when the user taps the ad (click-through).</summary>
    public event Action AdClicked;

    /// <summary>Fired when the user taps the Skip button.</summary>
    public event Action AdSkipped;

    /// <summary>
    /// Fired when the skippable state of the ad changes.
    /// Argument: "true" when the skip button appears, "false" when it disappears.
    /// </summary>
    public event Action<string> AdSkippableStateChanged;

    /// <summary>Fired when an ad icon is viewed.</summary>
    public event Action AdIconViewed;


    /// <summary>Fired when the AdsLoader encounters an error. Argument: error message.</summary>
    public event Action<string> AdLoaderError;

    /// <summary>Fired when the AdsManager encounters an error. Argument: error message.</summary>
    public event Action<string> AdManagerError;

    /// <summary>
    /// Fired when RequestAd() is rejected because the strict-gap cooldown
    /// is still active.  Argument: remaining seconds as a string.
    /// </summary>
    public event Action<string> AdBlockedByGap;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private — Plugin handle
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _plugin;
#endif

    // ─────────────────────────────────────────────────────────────────────────
    //  Properties — runtime read access to Inspector values
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The ad tag URL that will be used by RequestAd() when called with no argument.</summary>
    public string DefaultAdTagUrl
    {
        get => defaultAdTagUrl;
        set => defaultAdTagUrl = value;
    }

    /// <summary>Whether the strict-gap cooldown is currently active.</summary>
    public bool UseStrictGap
    {
        get => useStrictGap;
        set
        {
            useStrictGap = value;
            ApplyStrictGap();
        }
    }

    /// <summary>Minimum seconds between ad sessions when strict gap is enabled.</summary>
    public int StrictGapSeconds
    {
        get => strictGapSeconds;
        set
        {
            strictGapSeconds = Mathf.Max(1, value);
            ApplyStrictGap();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Ensure this GameObject's name matches the callback target so that
        // UnitySendMessage can find it.
        if (gameObject.name != callbackTargetName)
        {
            Debug.LogWarning(
                $"[IMAAdManager] GameObject name is '{gameObject.name}' but " +
                $"callbackTargetName is '{callbackTargetName}'. Renaming the " +
                $"GameObject to match, otherwise Android callbacks will be lost.");
            gameObject.name = callbackTargetName;
        }
    }

    private void Start()
    {
        InitializePlugin();

        if (requestOnStart)
            RequestAd();
    }

    private void OnDestroy()
    {
        DestroyPlugin();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Request and display an ad using the <see cref="DefaultAdTagUrl"/>.</summary>
    public void RequestAd() => RequestAd(defaultAdTagUrl);

    /// <summary>Request and display an ad using the supplied <paramref name="adTagUrl"/>.</summary>
    public void RequestAd(string adTagUrl)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_plugin == null)
        {
            Debug.LogError("[IMAAdManager] Plugin not initialized.");
            return;
        }
        Debug.Log($"[IMAAdManager] Requesting ad: {adTagUrl}");
        _plugin.Call("requestAd", adTagUrl);
#else
        Debug.Log($"[IMAAdManager] RequestAd() — Editor stub. URL: {adTagUrl}");
#endif
    }

    /// <summary>Programmatically pause a running ad.</summary>
    public void PauseAd()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _plugin?.Call("pauseAd");
#endif
    }

    /// <summary>Resume a paused ad.</summary>
    public void ResumeAd()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _plugin?.Call("resumeAd");
#endif
    }

    /// <summary>
    /// Skip the current ad.  Has no effect if the ad is not yet skippable.
    /// Listen for <see cref="AdSkippableStateChanged"/> to know when skipping is available.
    /// </summary>
    public void SkipAd()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _plugin?.Call("skipAd");
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Callbacks — invoked by UnitySendMessage from Java
    //  Method names MUST match exactly what UnityPlugin.java sends.
    // ─────────────────────────────────────────────────────────────────────────

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdLoaded(string _)
    {
        Debug.Log("[IMAAdManager] OnAdLoaded");
        onAdLoaded.Invoke();
        AdLoaded?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdStarted(string _)
    {
        Debug.Log("[IMAAdManager] OnAdStarted");
        onAdStarted.Invoke();
        AdStarted?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdPaused(string _)
    {
        Debug.Log("[IMAAdManager] OnAdPaused");
        onAdPaused.Invoke();
        AdPaused?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdResumed(string _)
    {
        Debug.Log("[IMAAdManager] OnAdResumed");
        onAdResumed.Invoke();
        AdResumed?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdCompleted(string _)
    {
        Debug.Log("[IMAAdManager] OnAdCompleted");
        onAdCompleted.Invoke();
        AdCompleted?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAllAdsCompleted(string _)
    {
        Debug.Log("[IMAAdManager] OnAllAdsCompleted");
        onAllAdsCompleted.Invoke();
        AllAdsCompleted?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnContentPauseRequested(string _)
    {
        Debug.Log("[IMAAdManager] OnContentPauseRequested — pause your game here.");
        onContentPauseRequested.Invoke();
        ContentPauseRequested?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnContentResumeRequested(string _)
    {
        Debug.Log("[IMAAdManager] OnContentResumeRequested — resume your game here.");
        onContentResumeRequested.Invoke();
        ContentResumeRequested?.Invoke();
    }

    // ── Progress ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Android callback] Do not rename — called by UnitySendMessage.
    /// <paramref name="msg"/> format: "currentMs/durationMs".
    /// </summary>
    public void OnAdProgress(string msg)
    {
        onAdProgress.Invoke(msg);
        AdProgress?.Invoke(msg);
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdFirstQuartile(string _)
    {
        Debug.Log("[IMAAdManager] OnAdFirstQuartile (25%)");
        onAdFirstQuartile.Invoke();
        AdFirstQuartile?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdMidpoint(string _)
    {
        Debug.Log("[IMAAdManager] OnAdMidpoint (50%)");
        onAdMidpoint.Invoke();
        AdMidpoint?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdThirdQuartile(string _)
    {
        Debug.Log("[IMAAdManager] OnAdThirdQuartile (75%)");
        onAdThirdQuartile.Invoke();
        AdThirdQuartile?.Invoke();
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdClicked(string _)
    {
        Debug.Log("[IMAAdManager] OnAdClicked");
        onAdClicked.Invoke();
        AdClicked?.Invoke();
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdSkipped(string _)
    {
        Debug.Log("[IMAAdManager] OnAdSkipped");
        onAdSkipped.Invoke();
        AdSkipped?.Invoke();
    }

    /// <summary>
    /// [Android callback] Do not rename — called by UnitySendMessage.
    /// <paramref name="msg"/> is "true" when skip becomes available, "false" when it disappears.
    /// </summary>
    public void OnAdSkippableStateChanged(string msg)
    {
        Debug.Log($"[IMAAdManager] OnAdSkippableStateChanged: canSkip={msg}");
        onAdSkippableStateChanged.Invoke(msg);
        AdSkippableStateChanged?.Invoke(msg);
    }

    /// <summary>[Android callback] Do not rename — called by UnitySendMessage.</summary>
    public void OnAdIconViewed(string _)
    {
        Debug.Log("[IMAAdManager] OnAdIconViewed");
        onAdIconViewed.Invoke();
        AdIconViewed?.Invoke();
    }



    // ── Errors ────────────────────────────────────────────────────────────────

    /// <summary>
    /// [Android callback] Do not rename — called by UnitySendMessage.
    /// <paramref name="msg"/> contains the error description.
    /// </summary>
    public void OnAdLoaderError(string msg)
    {
        Debug.LogError($"[IMAAdManager] OnAdLoaderError: {msg}");
        onAdLoaderError.Invoke(msg);
        AdLoaderError?.Invoke(msg);
    }

    /// <summary>
    /// [Android callback] Do not rename — called by UnitySendMessage.
    /// <paramref name="msg"/> contains the error description.
    /// </summary>
    public void OnAdManagerError(string msg)
    {
        Debug.LogError($"[IMAAdManager] OnAdManagerError: {msg}");
        onAdManagerError.Invoke(msg);
        AdManagerError?.Invoke(msg);
    }

    // ── Strict-Gap ────────────────────────────────────────────────────────────

    /// <summary>
    /// [Android callback] Do not rename — called by UnitySendMessage.
    /// <paramref name="msg"/> is the remaining cooldown in seconds as a string.
    /// </summary>
    public void OnAdBlockedByGap(string msg)
    {
        Debug.LogWarning($"[IMAAdManager] OnAdBlockedByGap — retry in {msg}s.");
        onAdBlockedByGap.Invoke(msg);
        AdBlockedByGap?.Invoke(msg);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void InitializePlugin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            const string pluginClass = "com.example.googleads.UnityPlugin";

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // Pass the Activity via the static method before instantiation
            using (var cls = new AndroidJavaClass(pluginClass))
                cls.CallStatic("receiveUnityActivity", activity);

            _plugin = new AndroidJavaObject(pluginClass);

            // Tell the plugin which GameObject to send callbacks to
            _plugin.Call("setCallbackTarget", callbackTargetName);

            // Apply strict-gap settings
            ApplyStrictGap();

            Debug.Log("[IMAAdManager] Plugin initialized successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[IMAAdManager] Initialization failed: {e.Message}");
        }
#else
        Debug.Log("[IMAAdManager] Running in Editor — Android plugin is a stub.");
#endif
    }

    private void ApplyStrictGap()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _plugin?.Call("setStrictGap", useStrictGap, strictGapSeconds);
        Debug.Log($"[IMAAdManager] StrictGap applied: enabled={useStrictGap}, gap={strictGapSeconds}s");
#endif
    }

    private void DestroyPlugin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            _plugin?.Call("destroy");
            _plugin?.Dispose();
            _plugin = null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IMAAdManager] Error during destroy: {e.Message}");
        }
#endif
    }
}