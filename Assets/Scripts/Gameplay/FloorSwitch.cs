using System.Collections;
using UnityEngine;

public class FloorSwitch : MonoBehaviour
{
    [Header("Door Connection")]
    [SerializeField] private int targetDoorID = 0;

    [Header("Visual References")]
    [SerializeField] private Transform switchVisualRoot;
    [SerializeField] private SpriteRenderer defaultRenderer;
    [SerializeField] private SpriteRenderer pressedRenderer;
    [SerializeField] private SpriteRenderer connectionIndicatorRenderer;

    [Header("Pressed State")]
    [SerializeField] private float pressedYOffset = -0.025f;
    [SerializeField] private float pressedScale = 0.96f;

    [Header("Press Juice")]
    [SerializeField] private float impactScale = 0.88f;
    [SerializeField] private float impactYOffsetMultiplier = 1.35f;
    [SerializeField] private float impactDuration = 0.07f;
    [SerializeField] private float settleDuration = 0.09f;

    [Header("Release")]
    [SerializeField] private float releaseDelay = 0.35f;
    [SerializeField] private float releaseDuration = 0.10f;

    private bool isActivated;

    private Vector3 visualStartPosition;
    private Vector3 visualStartScale;

    private Coroutine animationRoutine;
    private Coroutine releaseRoutine;

    public int TargetDoorID => targetDoorID;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        AutoFindVisualReferences();

        if (switchVisualRoot != null)
        {
            visualStartPosition =
                switchVisualRoot.localPosition;

            visualStartScale =
                switchVisualRoot.localScale;
        }

        // Önceden Inspector'da Alpha 0 yaptıysak temizle.
        ResetRendererAlpha(defaultRenderer);
        ResetRendererAlpha(pressedRenderer);

        // Başlangıç durumu.
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

        if (switchVisualRoot == null)
        {
            if (defaultRenderer != null)
            {
                switchVisualRoot =
                    defaultRenderer.transform.parent;
            }
            else if (pressedRenderer != null)
            {
                switchVisualRoot =
                    pressedRenderer.transform.parent;
            }
        }

        // GameObject daha önce kapatıldıysa aç.
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
            "FloorSwitch_" + targetDoorID;

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

        cell.SetFloorSwitch(this);
    }

    // =========================================================
    // ACTIVATE
    // =========================================================

    public void Activate()
    {
        // Default'a dönmek için bekliyorsa iptal et.
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        if (isActivated)
            return;

        isActivated = true;
        AudioManager.Play(GameSfx.Switch);

        // EN ÖNEMLİ KISIM:
        // Pressed sprite anında görünür.
        SetPressedVisual(true);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        animationRoutine =
            StartCoroutine(
                PressAnimation()
            );

        if (GridManager.Instance != null)
        {
            GridManager.Instance.OpenDoor(
                targetDoorID
            );
        }
    }

    // =========================================================
    // RELEASE
    // =========================================================

    public void Release()
    {
        if (!isActivated)
            return;

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
        }

        releaseRoutine =
            StartCoroutine(
                ReleaseAfterDelay()
            );
    }

    private IEnumerator ReleaseAfterDelay()
    {
        // Box tamamen çıktıktan sonra
        // pressed hali bir süre görünür kalır.
        yield return new WaitForSeconds(
            releaseDelay
        );

        isActivated = false;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        animationRoutine =
            StartCoroutine(
                ReleaseAnimation()
            );

        releaseRoutine = null;
    }

    // =========================================================
    // PRESS ANIMATION
    // =========================================================

    private IEnumerator PressAnimation()
    {
        if (switchVisualRoot == null)
            yield break;

        Vector3 startPosition =
            switchVisualRoot.localPosition;

        Vector3 startScale =
            switchVisualRoot.localScale;

        Vector3 impactPosition =
            visualStartPosition;

        impactPosition.y +=
            pressedYOffset *
            impactYOffsetMultiplier;

        Vector3 impactTargetScale =
            visualStartScale *
            impactScale;

        float elapsed = 0f;

        // -----------------------------------------------------
        // İlk basış
        // -----------------------------------------------------

        while (elapsed < impactDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    impactDuration
                );

            float eased =
                EaseOutQuad(t);

            switchVisualRoot.localPosition =
                Vector3.Lerp(
                    startPosition,
                    impactPosition,
                    eased
                );

            switchVisualRoot.localScale =
                Vector3.Lerp(
                    startScale,
                    impactTargetScale,
                    eased
                );

            yield return null;
        }

        Vector3 pressedPosition =
            visualStartPosition;

        pressedPosition.y +=
            pressedYOffset;

        Vector3 pressedTargetScale =
            visualStartScale *
            pressedScale;

        elapsed = 0f;

        // -----------------------------------------------------
        // Hafif geri sekme
        // -----------------------------------------------------

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    settleDuration
                );

            float eased =
                EaseOutBack(t);

            switchVisualRoot.localPosition =
                Vector3.LerpUnclamped(
                    impactPosition,
                    pressedPosition,
                    eased
                );

            switchVisualRoot.localScale =
                Vector3.LerpUnclamped(
                    impactTargetScale,
                    pressedTargetScale,
                    eased
                );

            yield return null;
        }

        switchVisualRoot.localPosition =
            pressedPosition;

        switchVisualRoot.localScale =
            pressedTargetScale;

        animationRoutine = null;
    }

    // =========================================================
    // RELEASE ANIMATION
    // =========================================================

    private IEnumerator ReleaseAnimation()
    {
        if (switchVisualRoot == null)
        {
            SetPressedVisual(false);
            yield break;
        }

        Vector3 startPosition =
            switchVisualRoot.localPosition;

        Vector3 startScale =
            switchVisualRoot.localScale;

        float elapsed = 0f;

        while (elapsed < releaseDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    releaseDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            switchVisualRoot.localPosition =
                Vector3.Lerp(
                    startPosition,
                    visualStartPosition,
                    eased
                );

            switchVisualRoot.localScale =
                Vector3.Lerp(
                    startScale,
                    visualStartScale,
                    eased
                );

            yield return null;
        }

        switchVisualRoot.localPosition =
            visualStartPosition;

        switchVisualRoot.localScale =
            visualStartScale;

        // Pressed bekleme ve release bittikten sonra
        // default görsele geri dön.
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