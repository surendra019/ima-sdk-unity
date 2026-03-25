using UnityEngine;

/// <summary>
/// IMAAdManager_UsageExample
///
/// Attach this to any GameObject in your scene to see how to subscribe to
/// IMAAdManager events from code. You do NOT need this file to use the plugin —
/// it is provided purely as a reference / starting point.
///
/// All events can also be wired through the Inspector without any code:
/// select the IMAAdManager GameObject and expand any of the Event sections.
/// </summary>
public class IMAAdManager_UsageExample : MonoBehaviour
{
    [Tooltip("Drag the GameObject that has IMAAdManager attached here.")]
    [SerializeField] private IMAAdManager adManager;

    [Tooltip("Optional: show a loading screen while the ad plays.")]
    [SerializeField] private GameObject loadingScreen;

    [Header("Settings")]
    [Tooltip("If checked, an ad will be requested automatically when the scene starts.")]
    [SerializeField] private bool requestAdOnStart = false;
    // ── Subscribe ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (adManager == null)
        {
            Debug.LogError("[UsageExample] adManager reference is not set.");
            return;
        }

        // Lifecycle
        adManager.ContentPauseRequested += OnContentPause;
        adManager.ContentResumeRequested += OnContentResume;
        adManager.AdLoaded += OnAdLoaded;
        adManager.AdStarted += OnAdStarted;
        adManager.AdPaused += OnAdPaused;
        adManager.AdResumed += OnAdResumed;
        adManager.AdCompleted += OnAdCompleted;
        adManager.AllAdsCompleted += OnAllAdsCompleted;

        // Progress
        adManager.AdProgress += OnAdProgress;
        adManager.AdFirstQuartile += OnFirstQuartile;
        adManager.AdMidpoint += OnMidpoint;
        adManager.AdThirdQuartile += OnThirdQuartile;

        // Interaction
        adManager.AdClicked += OnAdClicked;
        adManager.AdSkipped += OnAdSkipped;
        adManager.AdSkippableStateChanged += OnSkippableStateChanged;
        adManager.AdIconViewed += OnAdIconViewed;


        // Errors
        adManager.AdLoaderError += OnLoaderError;
        adManager.AdManagerError += OnManagerError;

        // Strict-gap
        adManager.AdBlockedByGap += OnAdBlockedByGap;
    }

    private void OnDisable()
    {
        if (adManager == null) return;

        adManager.ContentPauseRequested -= OnContentPause;
        adManager.ContentResumeRequested -= OnContentResume;
        adManager.AdLoaded -= OnAdLoaded;
        adManager.AdStarted -= OnAdStarted;
        adManager.AdPaused -= OnAdPaused;
        adManager.AdResumed -= OnAdResumed;
        adManager.AdCompleted -= OnAdCompleted;
        adManager.AllAdsCompleted -= OnAllAdsCompleted;

        adManager.AdProgress -= OnAdProgress;
        adManager.AdFirstQuartile -= OnFirstQuartile;
        adManager.AdMidpoint -= OnMidpoint;
        adManager.AdThirdQuartile -= OnThirdQuartile;

        adManager.AdClicked -= OnAdClicked;
        adManager.AdSkipped -= OnAdSkipped;
        adManager.AdSkippableStateChanged -= OnSkippableStateChanged;
        adManager.AdIconViewed -= OnAdIconViewed;


        adManager.AdLoaderError -= OnLoaderError;
        adManager.AdManagerError -= OnManagerError;

        adManager.AdBlockedByGap -= OnAdBlockedByGap;
    }

    private void Start()
    {
        if (requestAdOnStart)
        {
            Debug.Log("[Example] Auto-requesting ad on Start.");
            RequestDefaultAd();
        }
    }
    // ── Example — request an ad with a custom tag via button / UI ─────────────

    public void RequestCustomAd(string customTagUrl)
    {
        adManager.RequestAd(customTagUrl);
    }

    public void RequestDefaultAd()
    {
        adManager.RequestAd();   // uses DefaultAdTagUrl from Inspector
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    // ▸ IMPORTANT: pause ALL game audio / timescale / input here.
    private void OnContentPause()
    {
        Debug.Log("[Example] Content pause — muting game audio.");
        AudioListener.pause = true;
        // Time.timeScale = 0f;   // uncomment if you want to freeze the game
    }

    // ▸ IMPORTANT: resume game audio / timescale / input here.
    private void OnContentResume()
    {
        Debug.Log("[Example] Content resume — unmuting game audio.");
        AudioListener.pause = false;
        // Time.timeScale = 1f;
    }

    private void OnAdLoaded()
    {
        Debug.Log("[Example] Ad loaded — creative is ready.");
        if (loadingScreen) loadingScreen.SetActive(false);
    }

    private void OnAdStarted()
    {
        Debug.Log("[Example] Ad started playing.");
    }

    private void OnAdPaused()
    {
        Debug.Log("[Example] Ad paused (user may have clicked through).");
    }

    private void OnAdResumed()
    {
        Debug.Log("[Example] Ad resumed.");
    }

    private void OnAdCompleted()
    {
        Debug.Log("[Example] Individual ad completed.");
    }

    private void OnAllAdsCompleted()
    {
        Debug.Log("[Example] All ads finished — granting reward, etc.");
        // e.g. RewardManager.Instance.GrantReward();
    }

    // Progress — msg format: "currentMs/durationMs"
    private void OnAdProgress(string msg)
    {
        var parts = msg.Split('/');
        if (parts.Length == 2 &&
            long.TryParse(parts[0], out long current) &&
            long.TryParse(parts[1], out long duration) &&
            duration > 0)
        {
            float pct = current / (float)duration * 100f;
            Debug.Log($"[Example] Ad progress: {pct:F1}%");
        }
    }

    private void OnFirstQuartile() => Debug.Log("[Example] 25% played.");
    private void OnMidpoint() => Debug.Log("[Example] 50% played.");
    private void OnThirdQuartile() => Debug.Log("[Example] 75% played.");

    private void OnAdClicked()
    {
        Debug.Log("[Example] Ad clicked — user is leaving the app temporarily.");
    }

    private void OnAdSkipped()
    {
        Debug.Log("[Example] Ad skipped.");
    }

    // msg = "true" when the skip button appears
    private void OnSkippableStateChanged(string msg)
    {
        bool canSkip = msg == "true";
        Debug.Log($"[Example] Skip button visible: {canSkip}");
        // e.g. skipButton.SetActive(canSkip);
    }

    private void OnAdIconViewed()
    {
        Debug.Log("[Example] Ad icon viewed.");
    }



    private void OnLoaderError(string msg)
    {
        Debug.LogError($"[Example] Ad loader error: {msg}");
        // Show fallback UI, retry logic, etc.
    }

    private void OnManagerError(string msg)
    {
        Debug.LogError($"[Example] Ad manager error: {msg}");
    }

    // msg = remaining cooldown in seconds
    private void OnAdBlockedByGap(string msg)
    {
        Debug.LogWarning($"[Example] Ad request blocked — retry in {msg}s.");
        // e.g. show a "come back later" message to the player
    }
}