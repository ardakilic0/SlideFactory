using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonJuice : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [Header("Scale")]
    [SerializeField]
    private float pressedScale = 0.94f;

    [SerializeField]
    private float releaseOvershootScale = 1.03f;

    [Header("Timing")]
    [SerializeField]
    private float pressDuration = 0.06f;

    [SerializeField]
    private float releaseDuration = 0.12f;

    [Header("Automatic Button Colors")]
    [SerializeField]
    private bool autoButtonColors = true;

    [Range(0f, 0.5f)]
    [SerializeField]
    private float pressedDarkenAmount = 0.1f;

    [Range(0f, 0.2f)]
    [SerializeField]
    private float highlightedDarkenAmount = 0.04f;

    [Header("Optional")]
    [SerializeField]
    private RectTransform scaleTarget;

    private Button button;

    private Vector3 baseScale;

    private Coroutine scaleRoutine;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        button =
            GetComponent<Button>();

        if (scaleTarget == null)
        {
            scaleTarget =
                transform as RectTransform;
        }

        if (scaleTarget != null)
        {
            baseScale =
                scaleTarget.localScale;
        }
        else
        {
            baseScale =
                Vector3.one;
        }

        SetupAutomaticColors();
    }

    // =========================================================
    // AUTOMATIC COLORS
    // =========================================================

    private void SetupAutomaticColors()
    {
        if (!autoButtonColors ||
            button == null ||
            button.targetGraphic == null)
        {
            return;
        }

        Color baseColor =
            button.targetGraphic.color;

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            baseColor;

        colors.highlightedColor =
            DarkenColor(
                baseColor,
                highlightedDarkenAmount
            );

        colors.pressedColor =
            DarkenColor(
                baseColor,
                pressedDarkenAmount
            );

        colors.selectedColor =
            colors.highlightedColor;

        colors.disabledColor =
            new Color(
                baseColor.r,
                baseColor.g,
                baseColor.b,
                baseColor.a * 0.5f
            );

        colors.colorMultiplier =
            1f;

        colors.fadeDuration =
            0.06f;

        button.colors =
            colors;
    }

    // =========================================================
    // DARKEN COLOR
    // =========================================================

    private Color DarkenColor(
        Color color,
        float amount)
    {
        float multiplier =
            1f - amount;

        return new Color(
            color.r * multiplier,
            color.g * multiplier,
            color.b * multiplier,
            color.a
        );
    }

    // =========================================================
    // POINTER DOWN
    // =========================================================

    public void OnPointerDown(
        PointerEventData eventData)
    {
        if (!CanAnimate())
        {
            return;
        }

        StartScaleAnimation(
            baseScale * pressedScale,
            pressDuration,
            false
        );
    }

    // =========================================================
    // POINTER UP
    // =========================================================

    public void OnPointerUp(
        PointerEventData eventData)
    {
        if (!CanAnimate())
        {
            return;
        }

        StartReleaseAnimation();
    }

    // =========================================================
    // POINTER EXIT
    // =========================================================

    public void OnPointerExit(
        PointerEventData eventData)
    {
        if (!CanAnimate())
        {
            return;
        }

        StartReleaseAnimation();
    }

    // =========================================================
    // SCALE ANIMATION
    // =========================================================

    private void StartScaleAnimation(
        Vector3 targetScale,
        float duration,
        bool useBackEase)
    {
        if (scaleTarget == null)
        {
            return;
        }

        if (scaleRoutine != null)
        {
            StopCoroutine(
                scaleRoutine
            );
        }

        scaleRoutine =
            StartCoroutine(
                ScaleRoutine(
                    scaleTarget.localScale,
                    targetScale,
                    duration,
                    useBackEase
                )
            );
    }

    // =========================================================
    // RELEASE
    // =========================================================

    private void StartReleaseAnimation()
    {
        if (scaleTarget == null)
        {
            return;
        }

        if (scaleRoutine != null)
        {
            StopCoroutine(
                scaleRoutine
            );
        }

        scaleRoutine =
            StartCoroutine(
                ReleaseRoutine()
            );
    }

    private IEnumerator ReleaseRoutine()
    {
        Vector3 overshoot =
            baseScale *
            releaseOvershootScale;

        float firstDuration =
            releaseDuration *
            0.55f;

        float secondDuration =
            releaseDuration *
            0.45f;

        yield return ScaleRoutine(
            scaleTarget.localScale,
            overshoot,
            firstDuration,
            true
        );

        yield return ScaleRoutine(
            overshoot,
            baseScale,
            secondDuration,
            false
        );

        scaleRoutine = null;
    }

    // =========================================================
    // SCALE ROUTINE
    // =========================================================

    private IEnumerator ScaleRoutine(
        Vector3 from,
        Vector3 to,
        float duration,
        bool useBackEase)
    {
        float elapsed =
            0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );

            float eased =
                useBackEase
                    ? EaseOutBack(t)
                    : EaseOutQuad(t);

            scaleTarget.localScale =
                Vector3.LerpUnclamped(
                    from,
                    to,
                    eased
                );

            yield return null;
        }

        scaleTarget.localScale =
            to;
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private bool CanAnimate()
    {
        if (scaleTarget == null)
        {
            return false;
        }

        if (button != null &&
            !button.interactable)
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // DISABLE
    // =========================================================

    private void OnDisable()
    {
        if (scaleRoutine != null)
        {
            StopCoroutine(
                scaleRoutine
            );

            scaleRoutine = null;
        }

        if (scaleTarget != null)
        {
            scaleTarget.localScale =
                baseScale;
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