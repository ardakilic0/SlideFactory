using System.Collections;
using UnityEngine;

public class InvalidMoveJuice : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform visualRoot;

    [Header("Bump")]
    [SerializeField] private float distance = 0.10f;
    [SerializeField] private float duration = 0.14f;
    [SerializeField] private float oscillations = 2.5f;

    private Vector3 shakeOrigin;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;
    }

    public void Play(Vector2Int attemptedDirection)
    {
        if (visualRoot == null)
            return;

        // Önceki shake hâlâ sürüyorsa
        // kutuyu doğru dinlenme pozisyonuna döndür.
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            visualRoot.localPosition = shakeOrigin;
        }

        // Her invalid move anındaki gerçek pozisyonu baz alıyoruz.
        // Böylece Buffer gibi VisualRoot'u offsetleyen sistemlerle çakışmaz.
        shakeOrigin = visualRoot.localPosition;

        shakeRoutine = StartCoroutine(
            ShakeRoutine(attemptedDirection)
        );
    }

    private IEnumerator ShakeRoutine(Vector2Int attemptedDirection)
    {
        Vector3 direction = new Vector3(
            attemptedDirection.x,
            attemptedDirection.y,
            0f
        ).normalized;

        if (direction == Vector3.zero)
            direction = Vector3.right;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            float decay = 1f - t;

            float wave = Mathf.Sin(
                t * Mathf.PI * oscillations
            );

            float offset =
                wave *
                distance *
                decay;

            visualRoot.localPosition =
                shakeOrigin +
                direction * offset;

            yield return null;
        }

        visualRoot.localPosition = shakeOrigin;
        shakeRoutine = null;
    }

    private void OnDisable()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;

            if (visualRoot != null)
                visualRoot.localPosition = shakeOrigin;
        }
    }
}