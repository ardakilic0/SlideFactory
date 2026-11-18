using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum LevelFailReason
{
    OutOfMoves,
    Deadlock
}

public class LevelFailUI : MonoBehaviour
{
    public static LevelFailUI Instance
    {
        get;
        private set;
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Main")]
    [SerializeField] private GameObject failPanel;
    [SerializeField] private CanvasGroup failCanvasGroup;
    [SerializeField] private RectTransform failCard;

    [Header("Warning")]
    [SerializeField] private RectTransform warningGlow;
    [SerializeField] private CanvasGroup warningGlowGroup;

    [SerializeField] private RectTransform warningRays;
    [SerializeField] private CanvasGroup warningRaysGroup;

    [SerializeField] private RectTransform warningBeacon;

    [Header("Texts")]
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private CanvasGroup reasonGroup;

    [Header("Buttons")]
    [SerializeField] private CanvasGroup tryAgainGroup;
    [SerializeField] private CanvasGroup mainMenuGroup;

    // =========================================================
    // PANEL
    // =========================================================

    [Header("Panel")]
    [SerializeField] private float panelDuration = 0.24f;
    [SerializeField] private float panelStartScale = 0.86f;
    [SerializeField] private float panelOvershootScale = 1.04f;

    // =========================================================
    // WARNING
    // =========================================================

    [Header("Warning")]
    [SerializeField] private float warningDuration = 0.22f;
    [SerializeField] private float wobbleDuration = 0.24f;

    // =========================================================
    // CONTENT
    // =========================================================

    [Header("Content")]
    [SerializeField] private float textDuration = 0.18f;
    [SerializeField] private float buttonDuration = 0.18f;

    // =========================================================
    // AMBIENT GLOW
    // =========================================================

    [Header("Ambient Glow")]
    [SerializeField] private float glowMinAlpha = 0.38f;
    [SerializeField] private float glowMaxAlpha = 0.68f;
    [SerializeField] private float glowMinScale = 0.97f;
    [SerializeField] private float glowMaxScale = 1.04f;
    [SerializeField] private float glowSpeed = 0.75f;

    // =========================================================
    // STATE
    // =========================================================

    private Coroutine showRoutine;
    private Coroutine ambientRoutine;

    private Vector2 titlePosition;
    private Vector2 reasonPosition;

    private bool isShowing;

    public bool IsShowing => isShowing;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        Instance = this;

        CachePositions();

        // Play başladığında fail ekranı kapalı.
        if (failPanel != null)
        {
            failPanel.SetActive(false);
        }
    }

    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // =========================================================
    // CACHE POSITIONS
    // =========================================================

    private void CachePositions()
    {
        if (titleGroup != null)
        {
            RectTransform rect =
                titleGroup.transform as RectTransform;

            if (rect != null)
            {
                titlePosition =
                    rect.anchoredPosition;
            }
        }

        if (reasonGroup != null)
        {
            RectTransform rect =
                reasonGroup.transform as RectTransform;

            if (rect != null)
            {
                reasonPosition =
                    rect.anchoredPosition;
            }
        }
    }

    // =========================================================
    // SHOW FAIL
    // =========================================================

    public void ShowFail(
        LevelFailReason reason)
    {
        if (failPanel == null)
        {
            Debug.LogWarning(
                "LevelFailUI: FailPanel atanmadı."
            );

            return;
        }

        if (showRoutine != null)
        {
            StopCoroutine(
                showRoutine
            );
        }

        if (ambientRoutine != null)
        {
            StopCoroutine(
                ambientRoutine
            );
        }

        isShowing = true;

        SetReason(
            reason
        );

        failPanel.SetActive(
            true
        );

        ResetAnimationState();

        // Fail gerçekleştiğinde güçlü feedback.
        HapticsManager.Invalid();

        showRoutine =
            StartCoroutine(
                ShowRoutine()
            );
    }

    // =========================================================
    // HIDE FAIL
    // =========================================================

    public void HideFail()
    {
        isShowing = false;

        if (showRoutine != null)
        {
            StopCoroutine(
                showRoutine
            );

            showRoutine = null;
        }

        if (ambientRoutine != null)
        {
            StopCoroutine(
                ambientRoutine
            );

            ambientRoutine = null;
        }

        RestoreNormalState();

        if (failPanel != null)
        {
            failPanel.SetActive(
                false
            );
        }
    }

    // =========================================================
    // REASON
    // =========================================================

    private void SetReason(
        LevelFailReason reason)
    {
        if (reasonText == null)
            return;

        if (reason ==
            LevelFailReason.OutOfMoves)
        {
            reasonText.text =
                "OUT OF MOVES";
        }
        else
        {
            reasonText.text =
                "NO VALID MOVES";
        }
    }

    // =========================================================
    // RESET ANIMATION STATE
    // =========================================================

    private void ResetAnimationState()
    {
        if (failCanvasGroup != null)
        {
            failCanvasGroup.alpha =
                0f;

            failCanvasGroup.interactable =
                false;

            failCanvasGroup.blocksRaycasts =
                false;
        }

        if (failCard != null)
        {
            failCard.localScale =
                Vector3.one *
                panelStartScale;
        }

        if (warningGlow != null)
        {
            warningGlow.localScale =
                Vector3.one *
                0.75f;
        }

        if (warningGlowGroup != null)
        {
            warningGlowGroup.alpha =
                0f;
        }

        if (warningRays != null)
        {
            warningRays.localScale =
                Vector3.one *
                0.88f;
        }

        if (warningRaysGroup != null)
        {
            warningRaysGroup.alpha =
                0f;
        }

        if (warningBeacon != null)
        {
            warningBeacon.localScale =
                Vector3.one *
                0.78f;

            warningBeacon.localRotation =
                Quaternion.identity;
        }

        SetAlpha(
            titleGroup,
            0f
        );

        SetAlpha(
            reasonGroup,
            0f
        );

        SetAlpha(
            tryAgainGroup,
            0f
        );

        SetAlpha(
            mainMenuGroup,
            0f
        );

        RectTransform titleRect =
            titleGroup != null
                ? titleGroup.transform
                    as RectTransform
                : null;

        if (titleRect != null)
        {
            titleRect.anchoredPosition =
                titlePosition +
                Vector2.down *
                12f;
        }

        RectTransform reasonRect =
            reasonGroup != null
                ? reasonGroup.transform
                    as RectTransform
                : null;

        if (reasonRect != null)
        {
            reasonRect.anchoredPosition =
                reasonPosition +
                Vector2.down *
                10f;
        }

        if (tryAgainGroup != null)
        {
            tryAgainGroup
                .transform
                .localScale =
                    Vector3.one *
                    0.90f;
        }

        if (mainMenuGroup != null)
        {
            mainMenuGroup
                .transform
                .localScale =
                    Vector3.one;
        }
    }

    // =========================================================
    // RESTORE NORMAL STATE
    // =========================================================

    private void RestoreNormalState()
    {
        if (failCanvasGroup != null)
        {
            failCanvasGroup.alpha =
                1f;

            failCanvasGroup.interactable =
                true;

            failCanvasGroup.blocksRaycasts =
                true;
        }

        if (failCard != null)
        {
            failCard.localScale =
                Vector3.one;
        }

        if (warningGlow != null)
        {
            warningGlow.localScale =
                Vector3.one;
        }

        if (warningGlowGroup != null)
        {
            warningGlowGroup.alpha =
                1f;
        }

        if (warningRays != null)
        {
            warningRays.localScale =
                Vector3.one;
        }

        if (warningRaysGroup != null)
        {
            warningRaysGroup.alpha =
                1f;
        }

        if (warningBeacon != null)
        {
            warningBeacon.localScale =
                Vector3.one;

            warningBeacon.localRotation =
                Quaternion.identity;
        }

        SetAlpha(
            titleGroup,
            1f
        );

        SetAlpha(
            reasonGroup,
            1f
        );

        SetAlpha(
            tryAgainGroup,
            1f
        );

        SetAlpha(
            mainMenuGroup,
            1f
        );

        RectTransform titleRect =
            titleGroup != null
                ? titleGroup.transform
                    as RectTransform
                : null;

        if (titleRect != null)
        {
            titleRect.anchoredPosition =
                titlePosition;
        }

        RectTransform reasonRect =
            reasonGroup != null
                ? reasonGroup.transform
                    as RectTransform
                : null;

        if (reasonRect != null)
        {
            reasonRect.anchoredPosition =
                reasonPosition;
        }

        if (tryAgainGroup != null)
        {
            tryAgainGroup
                .transform
                .localScale =
                    Vector3.one;
        }
    }

    // =========================================================
    // SHOW ROUTINE
    // =========================================================

    private IEnumerator ShowRoutine()
    {
        // =====================================================
        // PANEL POP + DIM
        // =====================================================

        float elapsed =
            0f;

        while (elapsed <
               panelDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    panelDuration
                );

            if (failCanvasGroup != null)
            {
                failCanvasGroup.alpha =
                    Smooth(
                        Mathf.Clamp01(
                            t * 1.5f
                        )
                    );
            }

            if (failCard != null)
            {
                float scale;

                if (t < 0.70f)
                {
                    float popT =
                        Smooth(
                            t / 0.70f
                        );

                    scale =
                        Mathf.Lerp(
                            panelStartScale,
                            panelOvershootScale,
                            popT
                        );
                }
                else
                {
                    float settleT =
                        Smooth(
                            (t - 0.70f) /
                            0.30f
                        );

                    scale =
                        Mathf.Lerp(
                            panelOvershootScale,
                            1f,
                            settleT
                        );
                }

                failCard.localScale =
                    Vector3.one *
                    scale;
            }

            yield return null;
        }

        if (failCard != null)
        {
            failCard.localScale =
                Vector3.one;
        }

        // =====================================================
        // WARNING FLASH
        // =====================================================

        elapsed =
            0f;

        while (elapsed <
               warningDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    warningDuration
                );

            float smooth =
                Smooth(t);

            if (warningBeacon != null)
            {
                float scale;

                if (t < 0.65f)
                {
                    scale =
                        Mathf.Lerp(
                            0.78f,
                            1.12f,
                            Smooth(
                                t / 0.65f
                            )
                        );
                }
                else
                {
                    scale =
                        Mathf.Lerp(
                            1.12f,
                            1f,
                            Smooth(
                                (t - 0.65f) /
                                0.35f
                            )
                        );
                }

                warningBeacon.localScale =
                    Vector3.one *
                    scale;
            }

            if (warningGlow != null)
            {
                warningGlow.localScale =
                    Vector3.one *
                    Mathf.Lerp(
                        0.75f,
                        1.05f,
                        smooth
                    );
            }

            if (warningGlowGroup != null)
            {
                warningGlowGroup.alpha =
                    t < 0.45f
                        ? Mathf.Lerp(
                            0f,
                            1f,
                            t / 0.45f
                        )
                        : Mathf.Lerp(
                            1f,
                            0.55f,
                            (t - 0.45f) /
                            0.55f
                        );
            }

            if (warningRays != null)
            {
                warningRays.localScale =
                    Vector3.one *
                    Mathf.Lerp(
                        0.88f,
                        1f,
                        smooth
                    );
            }

            if (warningRaysGroup != null)
            {
                warningRaysGroup.alpha =
                    GetRayFlash(t);
            }

            yield return null;
        }

        // =====================================================
        // BEACON JAM WOBBLE
        // =====================================================

        elapsed =
            0f;

        while (elapsed <
               wobbleDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    wobbleDuration
                );

            if (warningBeacon != null)
            {
                warningBeacon.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        GetWobbleAngle(t)
                    );
            }

            yield return null;
        }

        if (warningBeacon != null)
        {
            warningBeacon.localRotation =
                Quaternion.identity;
        }

        // =====================================================
        // TITLE + REASON
        // =====================================================

        elapsed =
            0f;

        RectTransform titleRect =
            titleGroup != null
                ? titleGroup.transform
                    as RectTransform
                : null;

        RectTransform reasonRect =
            reasonGroup != null
                ? reasonGroup.transform
                    as RectTransform
                : null;

        while (elapsed <
               textDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    textDuration
                );

            float titleT =
                Smooth(t);

            float reasonT =
                Mathf.Clamp01(
                    (t - 0.18f) /
                    0.82f
                );

            reasonT =
                Smooth(reasonT);

            SetAlpha(
                titleGroup,
                titleT
            );

            SetAlpha(
                reasonGroup,
                reasonT
            );

            if (titleRect != null)
            {
                titleRect.anchoredPosition =
                    Vector2.Lerp(
                        titlePosition +
                        Vector2.down *
                        12f,
                        titlePosition,
                        titleT
                    );
            }

            if (reasonRect != null)
            {
                reasonRect.anchoredPosition =
                    Vector2.Lerp(
                        reasonPosition +
                        Vector2.down *
                        10f,
                        reasonPosition,
                        reasonT
                    );
            }

            yield return null;
        }

        // =====================================================
        // TRY AGAIN
        // =====================================================

        elapsed =
            0f;

        while (elapsed <
               buttonDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    buttonDuration
                );

            SetAlpha(
                tryAgainGroup,
                Smooth(t)
            );

            if (tryAgainGroup != null)
            {
                float scale;

                if (t < 0.70f)
                {
                    scale =
                        Mathf.Lerp(
                            0.90f,
                            1.035f,
                            Smooth(
                                t / 0.70f
                            )
                        );
                }
                else
                {
                    scale =
                        Mathf.Lerp(
                            1.035f,
                            1f,
                            Smooth(
                                (t - 0.70f) /
                                0.30f
                            )
                        );
                }

                tryAgainGroup
                    .transform
                    .localScale =
                        Vector3.one *
                        scale;
            }

            yield return null;
        }

        if (tryAgainGroup != null)
        {
            tryAgainGroup
                .transform
                .localScale =
                    Vector3.one;
        }

        // =====================================================
        // MAIN MENU
        // =====================================================

        elapsed =
            0f;

        while (elapsed <
               0.14f)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    0.14f
                );

            SetAlpha(
                mainMenuGroup,
                Smooth(t)
            );

            yield return null;
        }

        // =====================================================
        // ENABLE INTERACTION
        // =====================================================

        if (failCanvasGroup != null)
        {
            failCanvasGroup.alpha =
                1f;

            failCanvasGroup.interactable =
                true;

            failCanvasGroup.blocksRaycasts =
                true;
        }

        showRoutine =
            null;

        ambientRoutine =
            StartCoroutine(
                AmbientRoutine()
            );
    }

    // =========================================================
    // AMBIENT
    // =========================================================

    private IEnumerator AmbientRoutine()
    {
        float timer =
            0f;

        while (isShowing)
        {
            timer +=
                Time.unscaledDeltaTime *
                glowSpeed;

            float wave =
                Mathf.Sin(
                    timer *
                    Mathf.PI *
                    2f
                );

            wave =
                wave *
                0.5f +
                0.5f;

            wave =
                Smooth(wave);

            if (warningGlowGroup != null)
            {
                warningGlowGroup.alpha =
                    Mathf.Lerp(
                        glowMinAlpha,
                        glowMaxAlpha,
                        wave
                    );
            }

            if (warningGlow != null)
            {
                float scale =
                    Mathf.Lerp(
                        glowMinScale,
                        glowMaxScale,
                        wave
                    );

                warningGlow.localScale =
                    Vector3.one *
                    scale;
            }

            if (warningRaysGroup != null)
            {
                warningRaysGroup.alpha =
                    Mathf.Lerp(
                        0.48f,
                        0.68f,
                        wave
                    );
            }

            yield return null;
        }
    }

    // =========================================================
    // RAY FLASH
    // =========================================================

    private float GetRayFlash(
        float t)
    {
        if (t < 0.25f)
        {
            return Mathf.Lerp(
                0f,
                1f,
                t / 0.25f
            );
        }

        if (t < 0.50f)
        {
            return Mathf.Lerp(
                1f,
                0.25f,
                (t - 0.25f) /
                0.25f
            );
        }

        if (t < 0.75f)
        {
            return Mathf.Lerp(
                0.25f,
                1f,
                (t - 0.50f) /
                0.25f
            );
        }

        return Mathf.Lerp(
            1f,
            0.62f,
            (t - 0.75f) /
            0.25f
        );
    }

    // =========================================================
    // WOBBLE
    // =========================================================

    private float GetWobbleAngle(
        float t)
    {
        if (t < 0.25f)
        {
            return Mathf.Lerp(
                0f,
                8f,
                Smooth(
                    t / 0.25f
                )
            );
        }

        if (t < 0.50f)
        {
            return Mathf.Lerp(
                8f,
                -6f,
                Smooth(
                    (t - 0.25f) /
                    0.25f
                )
            );
        }

        if (t < 0.75f)
        {
            return Mathf.Lerp(
                -6f,
                3f,
                Smooth(
                    (t - 0.50f) /
                    0.25f
                )
            );
        }

        return Mathf.Lerp(
            3f,
            0f,
            Smooth(
                (t - 0.75f) /
                0.25f
            )
        );
    }

    // =========================================================
    // ALPHA
    // =========================================================

    private void SetAlpha(
        CanvasGroup group,
        float value)
    {
        if (group == null)
            return;

        group.alpha =
            Mathf.Clamp01(
                value
            );
    }

    // =========================================================
    // SMOOTH
    // =========================================================

    private float Smooth(
        float t)
    {
        t =
            Mathf.Clamp01(t);

        return
            t *
            t *
            (3f - 2f * t);
    }

    // =========================================================
    // TRY AGAIN BUTTON
    // =========================================================

    public void OnTryAgainPressed()
    {
        // UI button haptic.
        HapticsManager.UI();

        HideFail();

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance
                .RestartLevel();
        }
    }

    // =========================================================
    // MAIN MENU BUTTON
    // =========================================================

    public void OnMainMenuPressed()
    {
        // UI button haptic.
        HapticsManager.UI();

        SceneManager.LoadScene(
            0
        );
    }

    // =========================================================
    // TEST - OUT OF MOVES
    // =========================================================

    [ContextMenu("TEST - OUT OF MOVES")]
    private void TestOutOfMoves()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Fail testi sadece Play Mode'da çalışır."
            );

            return;
        }

        ShowFail(
            LevelFailReason.OutOfMoves
        );
    }

    // =========================================================
    // TEST - DEADLOCK
    // =========================================================

    [ContextMenu("TEST - NO VALID MOVES")]
    private void TestDeadlock()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Fail testi sadece Play Mode'da çalışır."
            );

            return;
        }

        ShowFail(
            LevelFailReason.Deadlock
        );
    }
}