using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelCompleteAnimator : MonoBehaviour
{
    [Header("Main")]
    [SerializeField] private Image dimBackground;
    [SerializeField] private RectTransform completeContent;
    [SerializeField] private Image panelBG;


    [Header("Hero")]
    [SerializeField] private RectTransform successBadge;
    [SerializeField] private RectTransform sparkleGoldL;
    [SerializeField] private RectTransform sparkleGoldR;
    [SerializeField] private RectTransform sparkleWhiteL;
    [SerializeField] private RectTransform sparkleWhiteR;


    [Header("Effects")]
    [SerializeField] private LevelCompleteConfetti confetti;


    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform levelRow;


    [Header("Buttons")]
    [SerializeField] private RectTransform nextLevelButton;
    [SerializeField] private RectTransform mainMenuButton;


    [Header("Button Shake")]
    [SerializeField] private float buttonShakeAngle = 1.3f;
    [SerializeField] private float buttonShakeCycles = 2.5f;


    [Header("Sparkle Pulse")]
    [SerializeField] private float sparkleMinScale = 0.82f;
    [SerializeField] private float sparkleMaxScale = 1.22f;
    [SerializeField] private float sparklePulseDuration = 0.22f;
    [SerializeField] private float sparklePulsePause = 0.12f;


    private CanvasGroup panelCanvasGroup;
    private CanvasGroup titleCanvasGroup;
    private CanvasGroup levelRowCanvasGroup;
    private CanvasGroup mainMenuCanvasGroup;

    private Coroutine animationRoutine;
    private Coroutine sparklePulseRoutine;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        panelCanvasGroup =
            GetOrAddCanvasGroup(
                panelBG != null
                    ? panelBG.gameObject
                    : null
            );


        titleCanvasGroup =
            GetOrAddCanvasGroup(
                titleText != null
                    ? titleText.gameObject
                    : null
            );


        levelRowCanvasGroup =
            GetOrAddCanvasGroup(
                levelRow != null
                    ? levelRow.gameObject
                    : null
            );


        mainMenuCanvasGroup =
            GetOrAddCanvasGroup(
                mainMenuButton != null
                    ? mainMenuButton.gameObject
                    : null
            );
    }


    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(
                animationRoutine
            );
        }


        if (sparklePulseRoutine != null)
        {
            StopCoroutine(
                sparklePulseRoutine
            );
        }


        HapticsManager.LevelComplete();


        animationRoutine =
            StartCoroutine(
                PlaySequence()
            );
    }


    // =========================================================
    // DISABLE
    // =========================================================

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(
                animationRoutine
            );

            animationRoutine =
                null;
        }


        if (sparklePulseRoutine != null)
        {
            StopCoroutine(
                sparklePulseRoutine
            );

            sparklePulseRoutine =
                null;
        }
    }


    // =========================================================
    // MAIN SEQUENCE
    // =========================================================

    private IEnumerator PlaySequence()
    {
        PrepareInitialState();


        // =====================================================
        // 1 — Arka plan hafif kararır
        // =====================================================

        yield return FadeImage(
            dimBackground,
            0f,
            0.38f,
            0.14f
        );


        // =====================================================
        // 2 — Success Badge
        // =====================================================

        yield return Pop(
            successBadge,
            0.32f,
            1.18f
        );


        // =====================================================
        // 3 — Kutlama
        // =====================================================

        if (confetti != null)
        {
            confetti.PlayBurst();
        }


        StartCoroutine(
            Pop(
                sparkleGoldL,
                0.16f,
                1.15f
            )
        );


        StartCoroutine(
            Pop(
                sparkleGoldR,
                0.16f,
                1.15f
            )
        );


        yield return
            new WaitForSecondsRealtime(
                0.05f
            );


        StartCoroutine(
            Pop(
                sparkleWhiteL,
                0.14f,
                1.15f
            )
        );


        StartCoroutine(
            Pop(
                sparkleWhiteR,
                0.14f,
                1.15f
            )
        );


        yield return
            new WaitForSecondsRealtime(
                0.10f
            );


        // =====================================================
        // 4 — Panel
        // ESKİ ANİMASYON AYNI
        // =====================================================

        yield return FadeAndScale(
            panelBG != null
                ? panelBG.rectTransform
                : null,
            panelCanvasGroup,
            0.90f,
            1f,
            0.22f
        );


        // =====================================================
        // 5 — Başlık
        // =====================================================

        yield return FadeAndScale(
            titleText != null
                ? titleText.rectTransform
                : null,
            titleCanvasGroup,
            0.88f,
            1f,
            0.18f
        );


        // =====================================================
        // 6 — Level satırı
        // =====================================================

        yield return FadeAndScale(
            levelRow,
            levelRowCanvasGroup,
            0.94f,
            1f,
            0.14f
        );


        // =====================================================
        // 7 — Next Level
        // ESKİ POP + HAFİF SHAKE
        // =====================================================

        yield return PopWithShake(
            nextLevelButton,
            0.22f,
            1.10f
        );


        // =====================================================
        // 8 — Main Menu
        // ESKİ FADE/SCALE + HAFİF SHAKE
        // =====================================================

        yield return FadeAndScaleWithShake(
            mainMenuButton,
            mainMenuCanvasGroup,
            0.94f,
            1f,
            0.14f
        );


        animationRoutine =
            null;


        // Her şey geldikten sonra
        // sparkle pulse devam eder.
        sparklePulseRoutine =
            StartCoroutine(
                PulseSparkles()
            );
    }


    // =========================================================
    // INITIAL STATE
    // =========================================================

    private void PrepareInitialState()
    {
        // CompleteContent komple
        // scale edilmiyor.
        if (completeContent != null)
        {
            completeContent.localScale =
                Vector3.one;
        }


        if (dimBackground != null)
        {
            Color color =
                dimBackground.color;

            color.a =
                0f;

            dimBackground.color =
                color;
        }


        // Önce sadece badge görünür.
        SetScale(
            successBadge,
            0f
        );


        // Panel başlangıçta tamamen gizli.
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha =
                0f;
        }


        if (panelBG != null)
        {
            panelBG
                .rectTransform
                .localScale =
                Vector3.one *
                0.90f;
        }


        SetScale(
            sparkleGoldL,
            0f
        );

        SetScale(
            sparkleGoldR,
            0f
        );

        SetScale(
            sparkleWhiteL,
            0f
        );

        SetScale(
            sparkleWhiteR,
            0f
        );


        if (titleCanvasGroup != null)
        {
            titleCanvasGroup.alpha =
                0f;
        }


        if (levelRowCanvasGroup != null)
        {
            levelRowCanvasGroup.alpha =
                0f;
        }


        SetScale(
            nextLevelButton,
            0f
        );


        if (nextLevelButton != null)
        {
            nextLevelButton.localRotation =
                Quaternion.identity;
        }


        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha =
                0f;
        }


        if (mainMenuButton != null)
        {
            mainMenuButton.localScale =
                Vector3.one *
                0.94f;

            mainMenuButton.localRotation =
                Quaternion.identity;
        }
    }


    // =========================================================
    // SPARKLES
    // =========================================================

    private IEnumerator PulseSparkles()
    {
        while (true)
        {
            yield return PulseSparklePair(
                sparkleGoldL,
                sparkleGoldR,
                sparkleWhiteL,
                sparkleWhiteR
            );


            yield return
                new WaitForSecondsRealtime(
                    sparklePulsePause
                );


            yield return PulseSparklePair(
                sparkleWhiteL,
                sparkleWhiteR,
                sparkleGoldL,
                sparkleGoldR
            );


            yield return
                new WaitForSecondsRealtime(
                    sparklePulsePause
                );
        }
    }


    private IEnumerator PulseSparklePair(
        RectTransform activeL,
        RectTransform activeR,
        RectTransform passiveL,
        RectTransform passiveR)
    {
        float halfDuration =
            sparklePulseDuration *
            0.5f;


        float elapsed =
            0f;


        while (elapsed <
               halfDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    halfDuration
                );


            float eased =
                EaseOutQuad(t);


            float activeScale =
                Mathf.Lerp(
                    sparkleMinScale,
                    sparkleMaxScale,
                    eased
                );


            float passiveScale =
                Mathf.Lerp(
                    1f,
                    sparkleMinScale,
                    eased
                );


            SetScale(
                activeL,
                activeScale
            );

            SetScale(
                activeR,
                activeScale
            );


            SetScale(
                passiveL,
                passiveScale
            );

            SetScale(
                passiveR,
                passiveScale
            );


            yield return null;
        }


        elapsed =
            0f;


        while (elapsed <
               halfDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    halfDuration
                );


            float eased =
                EaseOutQuad(t);


            float activeScale =
                Mathf.Lerp(
                    sparkleMaxScale,
                    1f,
                    eased
                );


            float passiveScale =
                Mathf.Lerp(
                    sparkleMinScale,
                    1f,
                    eased
                );


            SetScale(
                activeL,
                activeScale
            );

            SetScale(
                activeR,
                activeScale
            );


            SetScale(
                passiveL,
                passiveScale
            );

            SetScale(
                passiveR,
                passiveScale
            );


            yield return null;
        }


        SetScale(
            activeL,
            1f
        );

        SetScale(
            activeR,
            1f
        );

        SetScale(
            passiveL,
            1f
        );

        SetScale(
            passiveR,
            1f
        );
    }


    // =========================================================
    // NORMAL POP
    // =========================================================

    private IEnumerator Pop(
        RectTransform target,
        float duration,
        float overshootScale)
    {
        if (target == null)
            yield break;


        float firstDuration =
            duration *
            0.65f;


        float secondDuration =
            duration *
            0.35f;


        yield return Scale(
            target,
            Vector3.zero,
            Vector3.one *
            overshootScale,
            firstDuration,
            EaseOutBack
        );


        yield return Scale(
            target,
            Vector3.one *
            overshootScale,
            Vector3.one,
            secondDuration,
            EaseOutQuad
        );
    }


    // =========================================================
    // NEXT BUTTON POP + SHAKE
    // =========================================================

    private IEnumerator PopWithShake(
        RectTransform target,
        float duration,
        float overshootScale)
    {
        if (target == null)
            yield break;


        Quaternion restRotation =
            target.localRotation;


        float firstDuration =
            duration *
            0.65f;


        float secondDuration =
            duration *
            0.35f;


        float elapsed =
            0f;


        while (elapsed <
               firstDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    firstDuration
                );


            float eased =
                EaseOutBack(t);


            target.localScale =
                Vector3.LerpUnclamped(
                    Vector3.zero,
                    Vector3.one *
                    overshootScale,
                    eased
                );


            ApplyButtonShake(
                target,
                restRotation,
                t
            );


            yield return null;
        }


        target.localScale =
            Vector3.one *
            overshootScale;


        target.localRotation =
            restRotation;


        elapsed =
            0f;


        while (elapsed <
               secondDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    secondDuration
                );


            target.localScale =
                Vector3.LerpUnclamped(
                    Vector3.one *
                    overshootScale,
                    Vector3.one,
                    EaseOutQuad(t)
                );


            yield return null;
        }


        target.localScale =
            Vector3.one;


        target.localRotation =
            restRotation;
    }


    // =========================================================
    // NORMAL FADE + SCALE
    // =========================================================

    private IEnumerator FadeAndScale(
        RectTransform target,
        CanvasGroup canvasGroup,
        float startScale,
        float endScale,
        float duration)
    {
        if (target == null)
            yield break;


        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                0f;
        }


        target.localScale =
            Vector3.one *
            startScale;


        float elapsed =
            0f;


        while (elapsed <
               duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );


            float eased =
                EaseOutQuad(t);


            target.localScale =
                Vector3.one *
                Mathf.Lerp(
                    startScale,
                    endScale,
                    eased
                );


            if (canvasGroup != null)
            {
                canvasGroup.alpha =
                    eased;
            }


            yield return null;
        }


        target.localScale =
            Vector3.one *
            endScale;


        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                1f;
        }
    }


    // =========================================================
    // MAIN MENU FADE + SCALE + SHAKE
    // =========================================================

    private IEnumerator FadeAndScaleWithShake(
        RectTransform target,
        CanvasGroup canvasGroup,
        float startScale,
        float endScale,
        float duration)
    {
        if (target == null)
            yield break;


        Quaternion restRotation =
            target.localRotation;


        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                0f;
        }


        target.localScale =
            Vector3.one *
            startScale;


        float elapsed =
            0f;


        while (elapsed <
               duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );


            float eased =
                EaseOutQuad(t);


            target.localScale =
                Vector3.one *
                Mathf.Lerp(
                    startScale,
                    endScale,
                    eased
                );


            if (canvasGroup != null)
            {
                canvasGroup.alpha =
                    eased;
            }


            ApplyButtonShake(
                target,
                restRotation,
                t
            );


            yield return null;
        }


        target.localScale =
            Vector3.one *
            endScale;


        target.localRotation =
            restRotation;


        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                1f;
        }
    }


    // =========================================================
    // BUTTON SHAKE
    // =========================================================

    private void ApplyButtonShake(
        RectTransform target,
        Quaternion restRotation,
        float t)
    {
        float fade =
            1f - t;


        float angle =
            Mathf.Sin(
                t *
                Mathf.PI *
                2f *
                buttonShakeCycles
            ) *
            buttonShakeAngle *
            fade;


        target.localRotation =
            restRotation *
            Quaternion.Euler(
                0f,
                0f,
                angle
            );
    }


    // =========================================================
    // SCALE
    // =========================================================

    private IEnumerator Scale(
        RectTransform target,
        Vector3 from,
        Vector3 to,
        float duration,
        System.Func<float, float> easing)
    {
        if (target == null)
            yield break;


        target.localScale =
            from;


        float elapsed =
            0f;


        while (elapsed <
               duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );


            float eased =
                easing(t);


            target.localScale =
                Vector3.LerpUnclamped(
                    from,
                    to,
                    eased
                );


            yield return null;
        }


        target.localScale =
            to;
    }


    // =========================================================
    // IMAGE FADE
    // =========================================================

    private IEnumerator FadeImage(
        Image image,
        float from,
        float to,
        float duration)
    {
        if (image == null)
            yield break;


        Color color =
            image.color;


        color.a =
            from;


        image.color =
            color;


        float elapsed =
            0f;


        while (elapsed <
               duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );


            color.a =
                Mathf.Lerp(
                    from,
                    to,
                    EaseOutQuad(t)
                );


            image.color =
                color;


            yield return null;
        }


        color.a =
            to;


        image.color =
            color;
    }


    // =========================================================
    // CANVAS GROUP
    // =========================================================

    private CanvasGroup GetOrAddCanvasGroup(
        GameObject target)
    {
        if (target == null)
            return null;


        CanvasGroup canvasGroup =
            target.GetComponent<CanvasGroup>();


        if (canvasGroup == null)
        {
            canvasGroup =
                target.AddComponent<CanvasGroup>();
        }


        return canvasGroup;
    }


    // =========================================================
    // SCALE HELPER
    // =========================================================

    private void SetScale(
        RectTransform target,
        float scale)
    {
        if (target != null)
        {
            target.localScale =
                Vector3.one *
                scale;
        }
    }


    // =========================================================
    // EASING
    // =========================================================

    private float EaseOutQuad(
        float t)
    {
        return
            1f -
            (1f - t) *
            (1f - t);
    }


    private float EaseOutBack(
        float t)
    {
        const float c1 =
            1.70158f;


        const float c3 =
            c1 + 1f;


        return
            1f +
            c3 *
            Mathf.Pow(
                t - 1f,
                3f
            ) +
            c1 *
            Mathf.Pow(
                t - 1f,
                2f
            );
    }
}