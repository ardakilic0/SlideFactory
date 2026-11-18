using System.Collections;
using UnityEngine;

public class TeleportBoxJuice : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform visualRoot;

    [Header("Disappear")]
    [SerializeField] private float disappearDuration = 0.10f;

    [Header("Appear")]
    [SerializeField] private float appearDuration = 0.12f;
    [SerializeField] private float settleDuration = 0.07f;
    [SerializeField] private float appearOvershoot = 1.08f;

    private MovementJuice movementJuice;
    private Coroutine animationRoutine;

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        movementJuice =
            GetComponent<MovementJuice>();
    }

    public void Play(
        Vector3 entryWorldPosition,
        Vector3 entryScale,
        Vector3 exitLocalPosition,
        Vector3 exitScale)
    {
        if (visualRoot == null)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine =
            StartCoroutine(
                TeleportRoutine(
                    entryWorldPosition,
                    entryScale,
                    exitLocalPosition,
                    exitScale
                )
            );
    }

    private IEnumerator TeleportRoutine(
        Vector3 entryWorldPosition,
        Vector3 entryScale,
        Vector3 exitLocalPosition,
        Vector3 exitScale)
    {
        if (movementJuice != null)
        {
            movementJuice
                .SetExternalScaleControl(true);
        }

        // =====================================================
        // DISAPPEAR AT ENTRY PORTAL
        // =====================================================

        visualRoot.position =
            entryWorldPosition;

        visualRoot.localScale =
            entryScale;

        float elapsed = 0f;

        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    disappearDuration
                );

            float eased =
                EaseInQuad(t);

            // Parent Box aslında çıkış portalında olsa bile
            // görseli giriş portalında sabit tutuyoruz.
            visualRoot.position =
                entryWorldPosition;

            visualRoot.localScale =
                Vector3.Lerp(
                    entryScale,
                    Vector3.zero,
                    eased
                );

            yield return null;
        }

        visualRoot.localScale =
            Vector3.zero;

        // =====================================================
        // MOVE VISUAL TO EXIT PORTAL
        // =====================================================

        visualRoot.localPosition =
            exitLocalPosition;

        Vector3 overshootScale =
            exitScale *
            appearOvershoot;

        // =====================================================
        // APPEAR
        // =====================================================

        elapsed = 0f;

        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    appearDuration
                );

            float eased =
                EaseOutQuad(t);

            visualRoot.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    overshootScale,
                    eased
                );

            yield return null;
        }

        // =====================================================
        // SETTLE
        // =====================================================

        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    settleDuration
                );

            float eased =
                EaseOutQuad(t);

            visualRoot.localScale =
                Vector3.Lerp(
                    overshootScale,
                    exitScale,
                    eased
                );

            yield return null;
        }

        visualRoot.localPosition =
            exitLocalPosition;

        visualRoot.localScale =
            exitScale;

        if (movementJuice != null)
        {
            movementJuice
                .SetExternalScaleControl(false);
        }

        animationRoutine = null;
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        if (movementJuice != null)
        {
            movementJuice
                .SetExternalScaleControl(false);
        }
    }

    private float EaseInQuad(float t)
    {
        return t * t;
    }

    private float EaseOutQuad(float t)
    {
        return 1f -
               (1f - t) *
               (1f - t);
    }
}