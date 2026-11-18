using System.Collections;
using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [Header("Door Connection")]
    [SerializeField] private int targetDoorID = 0;

    [Header("Visual References")]
    [SerializeField] private Transform plateVisualRoot;
    [SerializeField] private SpriteRenderer defaultRenderer;
    [SerializeField] private SpriteRenderer pressedRenderer;
    [SerializeField] private SpriteRenderer connectionIndicatorRenderer;

    [Header("Pressed State")]
    [SerializeField] private float pressedYOffset = -0.02f;
    [SerializeField] private float pressedScale = 0.94f;

    [Header("Press Juice")]
    [SerializeField] private float impactScale = 0.87f;
    [SerializeField] private float impactYOffsetMultiplier = 1.35f;
    [SerializeField] private float pressImpactDuration = 0.07f;
    [SerializeField] private float pressSettleDuration = 0.08f;

    [Header("Release Juice")]
    [SerializeField] private float visualReleaseDelay = 0.30f;
    [SerializeField] private float releaseOvershootScale = 1.04f;
    [SerializeField] private float releaseOvershootY = 0.008f;
    [SerializeField] private float releaseDuration = 0.12f;

    private bool isPressed;

    private Vector3 plateStartPosition;
    private Vector3 plateStartScale;

    private Coroutine animationRoutine;
    private Coroutine visualReleaseRoutine;

    public int TargetDoorID => targetDoorID;
    public bool IsPressed => isPressed;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        AutoFindVisualReferences();

        if (plateVisualRoot != null)
        {
            plateStartPosition =
                plateVisualRoot.localPosition;

            plateStartScale =
                plateVisualRoot.localScale;
        }

        ResetRendererAlpha(defaultRenderer);
        ResetRendererAlpha(pressedRenderer);

        // Başlangıç: default görünür.
        SetPressedVisual(false);

        UpdateConnectionIndicator();
    }

    // =========================================================
    // AUTO FIND
    // =========================================================

    private void AutoFindVisualReferences()
    {
        if (defaultRenderer == null)
        {
            defaultRenderer =
                FindRenderer("DefaultVisual");
        }

        if (pressedRenderer == null)
        {
            pressedRenderer =
                FindRenderer("PressedVisual");
        }

        if (connectionIndicatorRenderer == null)
        {
            connectionIndicatorRenderer =
                FindRenderer("ConnectionIndicator");
        }

        if (plateVisualRoot == null)
        {
            if (defaultRenderer != null)
            {
                plateVisualRoot =
                    defaultRenderer.transform.parent;
            }
            else if (pressedRenderer != null)
            {
                plateVisualRoot =
                    pressedRenderer.transform.parent;
            }
        }

        if (defaultRenderer != null)
        {
            defaultRenderer.gameObject.SetActive(true);
        }

        if (pressedRenderer != null)
        {
            pressedRenderer.gameObject.SetActive(true);
        }
    }

    private SpriteRenderer FindRenderer(string objectName)
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.gameObject.name == objectName)
            {
                return renderer;
            }
        }

        return null;
    }

    // =========================================================
    // ID
    // =========================================================

    public void SetTargetDoorID(int id)
    {
        targetDoorID = id;

        gameObject.name =
            "PressurePlate_" + targetDoorID;

        UpdateConnectionIndicator();
    }

    // =========================================================
    // CONNECTION INDICATOR
    // =========================================================

    private void UpdateConnectionIndicator()
    {
        if (connectionIndicatorRenderer == null)
            return;

        connectionIndicatorRenderer.color =
            ConnectionColorPalette.GetColor(
                targetDoorID
            );
    }

    // =========================================================
    // PLACE
    // =========================================================

    public void Place(Cell cell)
    {
        if (cell == null)
            return;

        transform.position =
            cell.transform.position;

        cell.SetPressurePlate(this);
    }

    // =========================================================
    // PRESS STATE
    // =========================================================

    public void SetPressed(bool pressed)
    {
        if (isPressed == pressed)
            return;

        isPressed = pressed;

        // -----------------------------------------------------
        // GAMEPLAY STATE
        // -----------------------------------------------------
        // Kapının gerçek mechanic durumu gecikmeden değişir.
        if (GridManager.Instance != null)
        {
            GridManager.Instance.SetPressurePlateState(
                targetDoorID,
                isPressed
            );
        }

        // -----------------------------------------------------
        // VISUAL STATE
        // -----------------------------------------------------

        if (pressed)
        {
            // Release için bekliyorsa iptal et.
            if (visualReleaseRoutine != null)
            {
                StopCoroutine(visualReleaseRoutine);
                visualReleaseRoutine = null;
            }

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            // Pressed sprite anında görünür.
            SetPressedVisual(true);

            animationRoutine =
                StartCoroutine(
                    PressAnimation()
                );
        }
        else
        {
            // Gameplay artık released.
            // Ama oyuncunun görmesi için görsel biraz daha
            // pressed kalacak.

            if (visualReleaseRoutine != null)
            {
                StopCoroutine(visualReleaseRoutine);
            }

            visualReleaseRoutine =
                StartCoroutine(
                    DelayedVisualRelease()
                );
        }
    }

    // =========================================================
    // DELAYED VISUAL RELEASE
    // =========================================================

    private IEnumerator DelayedVisualRelease()
    {
        yield return new WaitForSeconds(
            visualReleaseDelay
        );

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        animationRoutine =
            StartCoroutine(
                ReleaseAnimation()
            );

        visualReleaseRoutine = null;
    }

    // =========================================================
    // PRESS ANIMATION
    // =========================================================

    private IEnumerator PressAnimation()
    {
        if (plateVisualRoot == null)
            yield break;

        Vector3 startPosition =
            plateVisualRoot.localPosition;

        Vector3 startScale =
            plateVisualRoot.localScale;

        Vector3 impactPosition =
            plateStartPosition;

        impactPosition.y +=
            pressedYOffset *
            impactYOffsetMultiplier;

        Vector3 impactTargetScale =
            plateStartScale *
            impactScale;

        float elapsed = 0f;

        // -----------------------------------------------------
        // İlk temas
        // -----------------------------------------------------

        while (elapsed < pressImpactDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    pressImpactDuration
                );

            float eased =
                EaseOutQuad(t);

            plateVisualRoot.localPosition =
                Vector3.Lerp(
                    startPosition,
                    impactPosition,
                    eased
                );

            plateVisualRoot.localScale =
                Vector3.Lerp(
                    startScale,
                    impactTargetScale,
                    eased
                );

            yield return null;
        }

        Vector3 pressedPosition =
            plateStartPosition;

        pressedPosition.y +=
            pressedYOffset;

        Vector3 pressedTargetScale =
            plateStartScale *
            pressedScale;

        elapsed = 0f;

        // -----------------------------------------------------
        // Hafif geri sekip pressed pozisyona oturur
        // -----------------------------------------------------

        while (elapsed < pressSettleDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    pressSettleDuration
                );

            float eased =
                EaseOutBack(t);

            plateVisualRoot.localPosition =
                Vector3.LerpUnclamped(
                    impactPosition,
                    pressedPosition,
                    eased
                );

            plateVisualRoot.localScale =
                Vector3.LerpUnclamped(
                    impactTargetScale,
                    pressedTargetScale,
                    eased
                );

            yield return null;
        }

        plateVisualRoot.localPosition =
            pressedPosition;

        plateVisualRoot.localScale =
            pressedTargetScale;

        animationRoutine = null;
    }

    // =========================================================
    // RELEASE ANIMATION
    // =========================================================

    private IEnumerator ReleaseAnimation()
    {
        if (plateVisualRoot == null)
        {
            SetPressedVisual(false);
            yield break;
        }

        Vector3 startPosition =
            plateVisualRoot.localPosition;

        Vector3 startScale =
            plateVisualRoot.localScale;

        Vector3 overshootPosition =
            plateStartPosition +
            Vector3.up *
            releaseOvershootY;

        Vector3 overshootScale =
            plateStartScale *
            releaseOvershootScale;

        float firstDuration =
            releaseDuration * 0.55f;

        float secondDuration =
            releaseDuration * 0.45f;

        float elapsed = 0f;

        // -----------------------------------------------------
        // Basılı durumdan hafif yukarı çıkar
        // -----------------------------------------------------

        while (elapsed < firstDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    firstDuration
                );

            float eased =
                EaseOutBack(t);

            plateVisualRoot.localPosition =
                Vector3.LerpUnclamped(
                    startPosition,
                    overshootPosition,
                    eased
                );

            plateVisualRoot.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    overshootScale,
                    eased
                );

            yield return null;
        }

        elapsed = 0f;

        // -----------------------------------------------------
        // Normal pozisyona oturur
        // -----------------------------------------------------

        while (elapsed < secondDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    secondDuration
                );

            float eased =
                EaseOutQuad(t);

            plateVisualRoot.localPosition =
                Vector3.Lerp(
                    overshootPosition,
                    plateStartPosition,
                    eased
                );

            plateVisualRoot.localScale =
                Vector3.Lerp(
                    overshootScale,
                    plateStartScale,
                    eased
                );

            yield return null;
        }

        plateVisualRoot.localPosition =
            plateStartPosition;

        plateVisualRoot.localScale =
            plateStartScale;

        // Release tamamlandıktan sonra
        // default sprite geri gelir.
        SetPressedVisual(false);

        animationRoutine = null;
    }

    // =========================================================
    // VISUAL STATE
    // =========================================================

    private void SetPressedVisual(bool pressed)
    {
        if (defaultRenderer != null)
        {
            ResetRendererAlpha(defaultRenderer);

            defaultRenderer.enabled =
                !pressed;
        }

        if (pressedRenderer != null)
        {
            ResetRendererAlpha(pressedRenderer);

            pressedRenderer.enabled =
                pressed;
        }
    }

    private void ResetRendererAlpha(
        SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        Color color =
            renderer.color;

        color.a = 1f;

        renderer.color =
            color;
    }

    // =========================================================
    // EASING
    // =========================================================

    private float EaseOutQuad(float t)
    {
        return
            1f -
            (1f - t) *
            (1f - t);
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return
            1f
            + c3 *
            Mathf.Pow(
                t - 1f,
                3f
            )
            + c1 *
            Mathf.Pow(
                t - 1f,
                2f
            );
    }
}