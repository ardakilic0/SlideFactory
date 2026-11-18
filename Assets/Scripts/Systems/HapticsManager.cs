using System.Runtime.InteropServices;
using UnityEngine;

public static class HapticsManager
{
    private const string HapticsKey = "HapticsEnabled";

    private static bool initialized;
    private static bool enabled;


#if UNITY_IOS && !UNITY_EDITOR

    [DllImport("__Internal")]
    private static extern void _HapticsImpact(int style);

    [DllImport("__Internal")]
    private static extern void _HapticsSelection();

    [DllImport("__Internal")]
    private static extern void _HapticsNotification(int type);

#endif


    // =========================================================
    // SETTINGS
    // =========================================================

    public static bool IsEnabled
    {
        get
        {
            if (!initialized)
            {
                enabled =
                    PlayerPrefs.GetInt(
                        HapticsKey,
                        1
                    ) == 1;

                initialized =
                    true;
            }

            return enabled;
        }
    }


    public static void SetEnabled(
        bool value)
    {
        enabled =
            value;

        initialized =
            true;
    }


    // =========================================================
    // UI
    // =========================================================

    // UI butonları / toggle'lar /
    // hafif mekanikler.
    public static void UI()
    {
        if (!IsEnabled)
            return;


#if UNITY_IOS && !UNITY_EDITOR

        _HapticsSelection();

#elif UNITY_ANDROID && !UNITY_EDITOR

        // EFFECT_TICK
        AndroidPredefined(
            2,
            12,
            70
        );

#endif
    }


    // =========================================================
    // NORMAL MOVE
    // =========================================================

    public static void Move()
    {
        if (!IsEnabled)
            return;


#if UNITY_IOS && !UNITY_EDITOR

        // Light
        _HapticsImpact(0);

#elif UNITY_ANDROID && !UNITY_EDITOR

        AndroidPredefined(
            2,
            12,
            65
        );

#endif
    }


    // =========================================================
    // PUSH
    // =========================================================

    // Crate push gibi biraz daha
    // fiziksel hareket.
    public static void Push()
    {
        if (!IsEnabled)
            return;


#if UNITY_IOS && !UNITY_EDITOR

        // Medium
        _HapticsImpact(1);

#elif UNITY_ANDROID && !UNITY_EDITOR

        // EFFECT_CLICK
        AndroidPredefined(
            0,
            18,
            100
        );

#endif
    }


    // =========================================================
    // MECHANIC
    // =========================================================

    // Switch / Portal / Buffer.
    public static void Mechanic()
    {
        if (!IsEnabled)
            return;


#if UNITY_IOS && !UNITY_EDITOR

        _HapticsImpact(0);

#elif UNITY_ANDROID && !UNITY_EDITOR

        AndroidPredefined(
            2,
            14,
            80
        );

#endif
    }


    // =========================================================
    // GOAL
    // =========================================================

    // Box doğru Goal'a oturduğunda.
    public static void Goal()
    {
        if (!IsEnabled)
            return;


#if UNITY_IOS && !UNITY_EDITOR

        _HapticsImpact(1);

#elif UNITY_ANDROID && !UNITY_EDITOR

        AndroidPredefined(
            0,
            22,
            120
        );

#endif
    }


    // =========================================================
    // LEVEL COMPLETE
    // =========================================================

    public static void LevelComplete()
    {
        if (!IsEnabled)
            return;


#if UNITY_IOS && !UNITY_EDITOR

        // Success notification.
        _HapticsNotification(0);

#elif UNITY_ANDROID && !UNITY_EDITOR

        // EFFECT_DOUBLE_CLICK
        AndroidPredefined(
            1,
            35,
            150
        );

#endif
    }


    // =========================================================
    // INVALID MOVE
    // =========================================================

    public static void Invalid()
    {
        if (!IsEnabled)
            return;


#if UNITY_IOS && !UNITY_EDITOR

        // Heavy
        _HapticsImpact(2);

#elif UNITY_ANDROID && !UNITY_EDITOR

        AndroidPredefined(
            0,
            12,
            110
        );

#endif
    }


    // =========================================================
    // ANDROID
    // =========================================================

#if UNITY_ANDROID && !UNITY_EDITOR

    private static void AndroidPredefined(
        int predefinedEffect,
        long fallbackDuration,
        int fallbackAmplitude)
    {
        try
        {
            using AndroidJavaClass unityPlayer =
                new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer"
                );


            using AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity"
                );


            using AndroidJavaObject vibrator =
                activity.Call<AndroidJavaObject>(
                    "getSystemService",
                    "vibrator"
                );


            if (vibrator == null)
                return;


            using AndroidJavaClass version =
                new AndroidJavaClass(
                    "android.os.Build$VERSION"
                );


            int sdkInt =
                version.GetStatic<int>(
                    "SDK_INT"
                );


            // Android 10+
            if (sdkInt >= 29)
            {
                using AndroidJavaClass vibrationEffect =
                    new AndroidJavaClass(
                        "android.os.VibrationEffect"
                    );


                using AndroidJavaObject effect =
                    vibrationEffect
                        .CallStatic<AndroidJavaObject>(
                            "createPredefined",
                            predefinedEffect
                        );


                vibrator.Call(
                    "vibrate",
                    effect
                );
            }

            // Android 8+
            else if (sdkInt >= 26)
            {
                using AndroidJavaClass vibrationEffect =
                    new AndroidJavaClass(
                        "android.os.VibrationEffect"
                    );


                using AndroidJavaObject effect =
                    vibrationEffect
                        .CallStatic<AndroidJavaObject>(
                            "createOneShot",
                            fallbackDuration,
                            fallbackAmplitude
                        );


                vibrator.Call(
                    "vibrate",
                    effect
                );
            }

            // Eski Android cihazlar.
            else
            {
                Handheld.Vibrate();
            }
        }
        catch
        {
            // Haptic desteklenmiyorsa
            // gameplay'i etkilemesin.
        }
    }

#endif
}