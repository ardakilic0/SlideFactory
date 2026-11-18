using System.Collections;
using UnityEngine;

public class Portal : MonoBehaviour
{
    [SerializeField] private int portalID = 0;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer portalRenderer;

    [Header("Idle Juice")]
    [SerializeField] private float idleScaleAmount = 0.025f;
    [SerializeField] private float idleSpeed = 2.2f;

    [Header("Teleport Pulse")]
    [SerializeField] private float pulseScale = 1.16f;
    [SerializeField] private float pulseOutDuration = 0.07f;
    [SerializeField] private float pulseReturnDuration = 0.13f;

    [Header("Teleport Flash")]
    [SerializeField]
    [Range(0f, 1f)]
    private float flashAmount = 0.22f;

    private Vector3 baseScale;
    private Color baseColor;

    private Coroutine pulseRoutine;
    private bool pulseActive;

    private float idleTimer;

    public int PortalID => portalID;

    public Cell CurrentCell { get; private set; }

    private void Awake()
    {
        if (portalRenderer != null)
        {
            baseScale =
                portalRenderer.transform.localScale;
        }

        UpdateVisual();
    }

    private void Update()
    {
        if (portalRenderer == null)
            return;

        if (pulseActive)
            return;

        idleTimer += Time.deltaTime * idleSpeed;

        float wave =
            (Mathf.Sin(idleTimer) + 1f) * 0.5f;

        float multiplier =
            Mathf.Lerp(
                1f - idleScaleAmount,
                1f + idleScaleAmount,
                wave
            );

        portalRenderer.transform.localScale =
            new Vector3(
                baseScale.x * multiplier,
                baseScale.y * multiplier,
                baseScale.z
            );
    }

    // =========================================================
    // ID
    // =========================================================

    public void SetPortalID(int id)
    {
        portalID = id;

        gameObject.name =
            "Portal_" + id;

        UpdateVisual();
    }

    // =========================================================
    // PLACE
    // =========================================================

    public void Place(Cell cell)
    {
        CurrentCell = cell;

        transform.position =
            cell.transform.position;

        cell.SetPortal(this);
    }

    // =========================================================
    // VISUAL
    // =========================================================

    private void UpdateVisual()
    {
        if (portalRenderer == null)
            return;

        baseColor =
            PortalColorPalette.GetColor(
                portalID
            );

        portalRenderer.color =
            baseColor;
    }

    // =========================================================
    // TELEPORT JUICE
    // =========================================================

    public void PlayTeleportPulse()
    {
        if (portalRenderer == null)
            return;

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }

        pulseRoutine =
            StartCoroutine(
                TeleportPulseRoutine()
            );
    }

    private IEnumerator TeleportPulseRoutine()
    {
        pulseActive = true;

        Transform visual =
            portalRenderer.transform;

        Vector3 startScale =
            visual.localScale;

        Vector3 pulseTargetScale =
            new Vector3(
                baseScale.x * pulseScale,
                baseScale.y * pulseScale,
                baseScale.z
            );

        Color flashColor =
            Color.Lerp(
                baseColor,
                Color.white,
                flashAmount
            );

        // -----------------------------------------------------
        // ENERGY POP
        // -----------------------------------------------------

        float elapsed = 0f;

        while (elapsed < pulseOutDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    pulseOutDuration
                );

            float eased =
                EaseOutQuad(t);

            visual.localScale =
                Vector3.Lerp(
                    startScale,
                    pulseTargetScale,
                    eased
                );

            portalRenderer.color =
                Color.Lerp(
                    baseColor,
                    flashColor,
                    eased
                );

            yield return null;
        }

        // -----------------------------------------------------
        // SETTLE
        // -----------------------------------------------------

        elapsed = 0f;

        while (elapsed < pulseReturnDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    pulseReturnDuration
                );

            float eased =
                EaseOutBack(t);

            visual.localScale =
                Vector3.LerpUnclamped(
                    pulseTargetScale,
                    baseScale,
                    eased
                );

            portalRenderer.color =
                Color.Lerp(
                    flashColor,
                    baseColor,
                    t
                );

            yield return null;
        }

        visual.localScale =
            baseScale;

        portalRenderer.color =
            baseColor;

        pulseActive = false;
        pulseRoutine = null;
    }

    // =========================================================
    // RESET
    // =========================================================

    private void OnDisable()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        pulseActive = false;

        if (portalRenderer != null)
        {
            portalRenderer.transform.localScale =
                baseScale;

            portalRenderer.color =
                baseColor;
        }
    }

    // =========================================================
    // EASING
    // =========================================================

    private float EaseOutQuad(float t)
    {
        return 1f -
               (1f - t) *
               (1f - t);
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f +
               c3 * Mathf.Pow(t - 1f, 3f) +
               c1 * Mathf.Pow(t - 1f, 2f);
    }
}