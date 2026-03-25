package com.example.googleads;

import android.app.Activity;
import android.content.Context;
import android.media.AudioManager;
import android.util.Log;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;
import android.widget.VideoView;

import com.google.ads.interactivemedia.v3.api.AdDisplayContainer;
import com.google.ads.interactivemedia.v3.api.AdEvent;
import com.google.ads.interactivemedia.v3.api.AdsLoader;
import com.google.ads.interactivemedia.v3.api.AdsManager;
import com.google.ads.interactivemedia.v3.api.AdsRenderingSettings;
import com.google.ads.interactivemedia.v3.api.AdsRequest;
import com.google.ads.interactivemedia.v3.api.ImaSdkFactory;
import com.google.ads.interactivemedia.v3.api.ImaSdkSettings;
import com.google.ads.interactivemedia.v3.api.player.VideoProgressUpdate;

// UnityPlayer is provided at runtime by the Unity engine — it is NOT on the
// compile-time classpath of a standalone Android library module.  We resolve
// it via reflection so the module compiles cleanly without unity-classes.jar
// being present, while still calling UnitySendMessage correctly on device.
import java.lang.reflect.Method;

/**
 * UnityPlugin — Google IMA SDK bridge for Unity (Android)
 *
 * Sends the following UnitySendMessage events to the GameObject named in
 * setCallbackTarget() (default: "IMAAdManager"):
 *
 *  ── Lifecycle ──────────────────────────────────────────────────────────────
 *  OnAdLoaded              Ad creative has been loaded and is ready to play.
 *  OnAdStarted             Ad playback has begun.
 *  OnAdPaused              Ad playback was paused (e.g. user clicked through).
 *  OnAdResumed             Ad playback resumed after a pause.
 *  OnAdCompleted           A single ad finished playing.
 *  OnAllAdsCompleted       All ads in the pod / VMAP have finished.
 *  OnContentPauseRequested The game should pause; ad is about to play.
 *  OnContentResumeRequested The game should resume; ad session is done.
 *
 *  ── Progress ───────────────────────────────────────────────────────────────
 *  OnAdProgress            Fired ~4× per second while an ad plays.
 *                          Message: "<currentMs>/<durationMs>"
 *  OnAdFirstQuartile       Playhead crossed 25 % of the ad.
 *  OnAdMidpoint            Playhead crossed 50 % of the ad.
 *  OnAdThirdQuartile       Playhead crossed 75 % of the ad.
 *
 *  ── Interaction ────────────────────────────────────────────────────────────
 *  OnAdClicked             User tapped the ad (click-through).
 *  OnAdSkipped             User tapped the Skip button.
 *  OnAdSkippableStateChanged  Skip button appeared or disappeared.
 *                          Message: "true" or "false"
 *
 *  ── Volume / icon ──────────────────────────────────────────────────────────
 *  OnAdVolumeChanged       System volume changed while an ad was active.
 *                          Message: volume 0-100 as string.
 *  OnAdIconViewed          An ad icon was viewed.
 *
 *  ── Errors ─────────────────────────────────────────────────────────────────
 *  OnAdLoaderError         The AdsLoader failed. Message: error description.
 *  OnAdManagerError        The AdsManager failed. Message: error description.
 *
 *  ── Strict-Gap ─────────────────────────────────────────────────────────────
 *  OnAdBlockedByGap        requestAd() was called while the strict-gap
 *                          cooldown was still active.
 *                          Message: remaining seconds as string.
 */
public class UnityPlugin {

    // ─────────────────────────────────────────────────────────────────────────
    //  Constants
    // ─────────────────────────────────────────────────────────────────────────
    private static final String TAG = "IMA_Unity";

    /** Default Unity GameObject that receives UnitySendMessage callbacks. */
    private static final String DEFAULT_CALLBACK_TARGET = "IMAAdManager";

    // ─────────────────────────────────────────────────────────────────────────
    //  Static / shared state
    // ─────────────────────────────────────────────────────────────────────────
    private static Activity unityActivity;

    // ─────────────────────────────────────────────────────────────────────────
    //  IMA objects
    // ─────────────────────────────────────────────────────────────────────────
    private ImaSdkFactory sdkFactory;
    private AdsLoader adsLoader;
    private AdsManager adsManager;

    // ─────────────────────────────────────────────────────────────────────────
    //  UI elements
    // ─────────────────────────────────────────────────────────────────────────
    private VideoView videoPlayer;
    private FrameLayout adContainer;
    private VideoAdPlayerAdapter videoAdPlayerAdapter;

    // ─────────────────────────────────────────────────────────────────────────
    //  Configuration
    // ─────────────────────────────────────────────────────────────────────────

    /** Name of the Unity GameObject that will receive all callbacks. */
    private String callbackTarget = DEFAULT_CALLBACK_TARGET;

    /**
     * When > 0, enforces a minimum gap (in seconds) between ad sessions.
     * Any requestAd() call made before the gap has elapsed is silently
     * dropped and OnAdBlockedByGap is sent instead.
     */
    private int strictGapSeconds = 0;

    /** Epoch-millisecond timestamp of when the last ad session ended. */
    private long lastAdEndTimeMs = 0L;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /** Called from Unity once to pass the Android Activity. */
    public static void receiveUnityActivity(Activity activity) {
        unityActivity = activity;
    }

    public UnityPlugin() {
        sdkFactory = ImaSdkFactory.getInstance();
    }

    /**
     * Override the Unity GameObject that receives all UnitySendMessage calls.
     * Must match the name of the GameObject that has IMAAdManager attached.
     */
    public void setCallbackTarget(String gameObjectName) {
        callbackTarget = gameObjectName;
    }

    /**
     * Enable/disable the strict-gap feature and set the gap duration.
     *
     * @param enabled        Pass true to enable, false to disable.
     * @param gapSeconds     Minimum seconds between the end of one ad session
     *                       and the start of the next. Ignored when disabled.
     */
    public void setStrictGap(boolean enabled, int gapSeconds) {
        strictGapSeconds = enabled ? gapSeconds : 0;
        Log.d(TAG, "StrictGap: enabled=" + enabled + " gap=" + gapSeconds + "s");
    }

    /**
     * Request and display an ad from the given VAST / VMAP tag URL.
     *
     * Respects the strict-gap cooldown when enabled; fires OnAdBlockedByGap
     * and returns immediately if the cooldown has not yet elapsed.
     */
    public void requestAd(final String adTagUrl) {
        if (unityActivity == null) {
            Log.e(TAG, "Activity is null. Call receiveUnityActivity first.");
            return;
        }

        // ── Strict-gap check ──────────────────────────────────────────────
        if (strictGapSeconds > 0 && lastAdEndTimeMs > 0) {
            long elapsedMs  = System.currentTimeMillis() - lastAdEndTimeMs;
            long requiredMs = strictGapSeconds * 1000L;
            if (elapsedMs < requiredMs) {
                long remainingSeconds = (requiredMs - elapsedMs + 999) / 1000;
                Log.w(TAG, "Ad blocked by strict gap. Remaining: " + remainingSeconds + "s");
                sendCallback("OnAdBlockedByGap", String.valueOf(remainingSeconds));
                return;
            }
        }

        unityActivity.runOnUiThread(() -> {
            try {
                ensurePlayerInitialized();

                AdDisplayContainer container =
                        ImaSdkFactory.createAdDisplayContainer(adContainer, videoAdPlayerAdapter);

                if (adsLoader == null) {
                    ImaSdkSettings settings = sdkFactory.createImaSdkSettings();
                    adsLoader = sdkFactory.createAdsLoader(unityActivity, settings, container);
                    setupAdsLoaderListeners();
                }

                AdsRequest request = sdkFactory.createAdsRequest();
                request.setAdTagUrl(adTagUrl);
                request.setContentProgressProvider(() -> VideoProgressUpdate.VIDEO_TIME_NOT_READY);

                adsLoader.requestAds(request);
                Log.d(TAG, "Ad requested: " + adTagUrl);

            } catch (Exception e) {
                Log.e(TAG, "requestAd error: " + e.getMessage());
                e.printStackTrace();
            }
        });
    }

    /** Programmatically pause a running ad (mirrors what IMA does on click-through). */
    public void pauseAd() {
        if (adsManager != null) {
            unityActivity.runOnUiThread(() -> adsManager.pause());
        }
    }

    /** Resume a paused ad. */
    public void resumeAd() {
        if (adsManager != null) {
            unityActivity.runOnUiThread(() -> adsManager.resume());
        }
    }

    /** Skip the current ad (only effective if the ad is skippable). */
    public void skipAd() {
        if (adsManager != null) {
            unityActivity.runOnUiThread(() -> adsManager.skip());
        }
    }

    /** Tear down all IMA resources and remove the overlay from Unity's view. */
    public void destroy() {
        if (adsManager != null) {
            adsManager.destroy();
            adsManager = null;
        }
        if (adContainer != null) {
            unityActivity.runOnUiThread(() -> {
                ViewGroup root = unityActivity.findViewById(android.R.id.content);
                root.removeView(adContainer);
                adContainer  = null;
                videoPlayer  = null;
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ensurePlayerInitialized() {
        if (videoPlayer != null) return;

        adContainer = new FrameLayout(unityActivity);
        adContainer.setBackgroundColor(0xFF000000);

        videoPlayer = new VideoView(unityActivity);
        FrameLayout.LayoutParams playerParams = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT);
        adContainer.addView(videoPlayer, playerParams);

        ViewGroup root = unityActivity.findViewById(android.R.id.content);
        root.addView(adContainer, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));

        adContainer.setVisibility(View.GONE);

        AudioManager audioManager =
                (AudioManager) unityActivity.getSystemService(Context.AUDIO_SERVICE);
        videoAdPlayerAdapter = new VideoAdPlayerAdapter(videoPlayer, audioManager);
    }

    private void setupAdsLoaderListeners() {

        // ── Loader-level error ────────────────────────────────────────────
        adsLoader.addAdErrorListener(event -> {
            String msg = event.getError().getMessage();
            Log.e(TAG, "AdsLoader error: " + msg);
            sendCallback("OnAdLoaderError", msg);
            markAdSessionEnded();
            resumeContent();
        });

        // ── Ads loaded successfully ───────────────────────────────────────
        adsLoader.addAdsLoadedListener(event -> {
            adsManager = event.getAdsManager();

            // Manager-level error
            adsManager.addAdErrorListener(errEvent -> {
                String msg = errEvent.getError().getMessage();
                Log.e(TAG, "AdsManager error: " + msg);
                sendCallback("OnAdManagerError", msg);
                markAdSessionEnded();
                resumeContent();
            });

            // Ad events
            adsManager.addAdEventListener(adEvent -> {
                Log.i(TAG, "AdEvent: " + adEvent.getType());
                handleAdEvent(adEvent);
            });

            AdsRenderingSettings renderSettings = sdkFactory.createAdsRenderingSettings();
            adsManager.init(renderSettings);
        });
    }

    /**
     * Central dispatcher for all IMA {@link AdEvent.AdEventType} values.
     * Every case fires a corresponding UnitySendMessage callback.
     */
    private void handleAdEvent(AdEvent adEvent) {
        switch (adEvent.getType()) {

            // ── Lifecycle ─────────────────────────────────────────────────
            case LOADED:
                sendCallback("OnAdLoaded", "");
                adsManager.start();
                break;

            case STARTED:
                sendCallback("OnAdStarted", "");
                break;

            case PAUSED:
                sendCallback("OnAdPaused", "");
                break;

            case RESUMED:
                sendCallback("OnAdResumed", "");
                break;

            case COMPLETED:
                sendCallback("OnAdCompleted", "");
                break;

            case ALL_ADS_COMPLETED:
                sendCallback("OnAllAdsCompleted", "");
                markAdSessionEnded();
                resumeContent();
                break;

            case CONTENT_PAUSE_REQUESTED:
                sendCallback("OnContentPauseRequested", "");
                pauseContentForAds();
                break;

            case CONTENT_RESUME_REQUESTED:
                sendCallback("OnContentResumeRequested", "");
                markAdSessionEnded();
                resumeContent();
                break;

            // ── Progress milestones ───────────────────────────────────────
            case FIRST_QUARTILE:
                sendCallback("OnAdFirstQuartile", "");
                break;

            case MIDPOINT:
                sendCallback("OnAdMidpoint", "");
                break;

            case THIRD_QUARTILE:
                sendCallback("OnAdThirdQuartile", "");
                break;

            case AD_PROGRESS:
                // Message format:  "<currentMs>/<durationMs>"
                if (adEvent.getAdData() != null) {
                    String current  = adEvent.getAdData().get("currentTime");
                    String duration = adEvent.getAdData().get("duration");
                    sendCallback("OnAdProgress", current + "/" + duration);
                }
                break;

            // ── Interaction ───────────────────────────────────────────────
            case CLICKED:
                sendCallback("OnAdClicked", "");
                break;

            case SKIPPED:
                sendCallback("OnAdSkipped", "");
                break;

            case SKIPPABLE_STATE_CHANGED:
                // AdsManager does not expose isCurrentAdSkippable().
                // The IMA SDK signals skippability via the "skippableState" key
                // in the ad's data map: "1" = skip available, else not available.
                String skippableRaw = adEvent.getAdData() != null
                        ? adEvent.getAdData().get("skippableState") : null;
                boolean canSkip = "1".equals(skippableRaw);
                sendCallback("OnAdSkippableStateChanged", String.valueOf(canSkip));
                break;

            case ICON_TAPPED:
                sendCallback("OnAdIconViewed", "");
                break;

            // ── Volume ────────────────────────────────────────────────────
//            case VOLUME_CHANGED:
//                if (adEvent.getAdData() != null) {
//                    String vol = adEvent.getAdData().get("playerVolume");
//                    sendCallback("OnAdVolumeChanged", vol != null ? vol : "");
//                }
//                break;

            default:
                break;
        }
    }

    private void pauseContentForAds() {
        unityActivity.runOnUiThread(() -> {
            if (adContainer != null) adContainer.setVisibility(View.VISIBLE);
        });
    }

    private void resumeContent() {
        unityActivity.runOnUiThread(() -> {
            // Stop and clean up video player
            if (videoPlayer != null) {
                videoPlayer.stopPlayback();
                videoPlayer = null;
            }

            // Fully remove the ad container from the view hierarchy
            if (adContainer != null) {
                ViewGroup root = unityActivity.findViewById(android.R.id.content);
                root.removeView(adContainer);
                adContainer = null;
            }

            // Clean up the video adapter
            if (videoAdPlayerAdapter != null) {
                videoAdPlayerAdapter = null;
            }

            // Destroy ads manager
            if (adsManager != null) {
                adsManager.destroy();
                adsManager = null;
            }

            // Reset ads loader so next requestAd() starts fresh
            if (adsLoader != null) {
                adsLoader = null;
            }
        });
    }
    /** Records the timestamp when an ad session ends (for strict-gap tracking). */
    private void markAdSessionEnded() {
        lastAdEndTimeMs = System.currentTimeMillis();
        Log.d(TAG, "Ad session ended. StrictGap cooldown started.");
    }

    /**
     * Sends a UnitySendMessage to the configured callback target.
     *
     * UnityPlayer is resolved via reflection rather than a direct import so that
     * this module compiles cleanly as a standalone Android library without
     * unity-classes.jar on the compile classpath.  At runtime inside a Unity
     * build the class is always present, so the reflective call succeeds.
     */
    private void sendCallback(final String methodName, final String message) {
        Log.d(TAG, "Callback -> " + callbackTarget + "." + methodName + "(" + message + ")");
        try {
            Class<?> unityPlayer = Class.forName("com.unity3d.player.UnityPlayer");
            Method unitySendMessage = unityPlayer.getMethod(
                    "UnitySendMessage", String.class, String.class, String.class);
            unitySendMessage.invoke(null, callbackTarget, methodName, message);
        } catch (ClassNotFoundException e) {
            // Running outside a Unity build (e.g. plain Android test app) — log and skip.
            Log.w(TAG, "UnityPlayer not found — not running inside Unity. Skipping callback.");
        } catch (Exception e) {
            Log.e(TAG, "sendCallback reflection error: " + e.getMessage());
        }
    }
}