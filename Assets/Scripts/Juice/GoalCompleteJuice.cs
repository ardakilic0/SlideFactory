using System.Collections;
using UnityEngine;

public class GoalCompleteJuice : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform visualRoot;

    [Header("Pop")]
    [SerializeField] private float popScale = 1.12f;
    [SerializeField] private float popDuration = 0.12f;

    [Header("Settle")]
    [SerializeField] private float settleDuration = 0.18f;

    [Header("Optional Rotation")]
    [SerializeField] private float rotationPunch = 3f;

    private MovementJuice movementJuice;
    private Coroutine animationRoutine;

    private Vector3 baseLocalEuler;

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        movementJuice =
            GetComponent<MovementJuice>();

        baseLocalEuler =
            visualRoot.localEulerAngles;
    }

    public void Play(Vector3 completedScale)
    {
        if (visualRoot == null)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine =
            StartCoroutine(
                PlayRoutine(completedScale)
            );
    }

    private IEnumerator PlayRoutine(
        Vector3 completedScale)
    {
        if (movementJuice != null)
            movementJuice.SetExternalScaleControl(true);

        Vector3 startScale =
            visualRoot.localScale;

        Vector3 popTarget =
            startScale * popScale;

        // 1 — kısa başarı pop'u
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / popDuration
                );

            float eased =
                EaseOutBack(t);

            visualRoot.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    popTarget,
                    eased
                );

            float rotation =
                Mathf.Sin(t * Mathf.PI) *
                rotationPunch;

            visualRoot.localEulerAngles =
                baseLocalEuler +
                new Vector3(
                    0f,
                    0f,
                    rotation
                );

            yield return null;
        }

        visualRoot.localScale =
            popTarget;

        // 2 — goal üzerindeki final scale'e oturur
        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / settleDuration
                );

            float eased =
                EaseOutQuad(t);

            visualRoot.localScale =
                Vector3.Lerp(
                    popTarget,
                    completedScale,
                    eased
                );

            visualRoot.localEulerAngles =
                Vector3.Lerp(
                    baseLocalEuler,
                    baseLocalEuler,
                    eased
                );

            yield return null;
        }

        visualRoot.localScale =
            completedScale;

        visualRoot.localEulerAngles =
            baseLocalEuler;

        if (movementJuice != null)
        {
            movementJuice.SetRestScale(
                completedScale
            );

            movementJuice.SetExternalScaleControl(false);
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
    }

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