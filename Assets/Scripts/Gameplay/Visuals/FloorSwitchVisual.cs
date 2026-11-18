using System.Collections;
using UnityEngine;

public class FloorSwitchVisual : MonoBehaviour
{
    [Header("Renderers")]
    [SerializeField] private SpriteRenderer defaultRenderer;
    [SerializeField] private SpriteRenderer pressedRenderer;

    [Header("Juice")]
    [SerializeField] private float transitionDuration = 0.08f;
    [SerializeField] private float pressedScale = 0.96f;

    private Vector3 normalScale;
    private Coroutine transitionRoutine;

    private void Awake()
    {
        normalScale = transform.localScale;

        SetAlpha(defaultRenderer, 1f);
        SetAlpha(pressedRenderer, 0f);
    }

    public void SetPressed(bool pressed, bool instant = false)
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        if (instant)
        {
            SetAlpha(defaultRenderer, pressed ? 0f : 1f);
            SetAlpha(pressedRenderer, pressed ? 1f : 0f);

            transform.localScale =
                pressed ? normalScale * pressedScale : normalScale;

            return;
        }

        transitionRoutine = StartCoroutine(AnimatePressedState(pressed));
    }

    private IEnumerator AnimatePressedState(bool pressed)
    {
        float defaultStartAlpha = defaultRenderer.color.a;
        float pressedStartAlpha = pressedRenderer.color.a;

        Vector3 startScale = transform.localScale;
        Vector3 targetScale =
            pressed ? normalScale * pressedScale : normalScale;

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            SetAlpha(
                defaultRenderer,
                Mathf.Lerp(
                    defaultStartAlpha,
                    pressed ? 0f : 1f,
                    t
                )
            );

            SetAlpha(
                pressedRenderer,
                Mathf.Lerp(
                    pressedStartAlpha,
                    pressed ? 1f : 0f,
                    t
                )
            );

            transform.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        SetAlpha(defaultRenderer, pressed ? 0f : 1f);
        SetAlpha(pressedRenderer, pressed ? 1f : 0f);
        transform.localScale = targetScale;

        transitionRoutine = null;
    }

    private void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}