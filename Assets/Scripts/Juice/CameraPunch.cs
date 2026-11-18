using System.Collections;
using UnityEngine;

public class CameraPunch : MonoBehaviour
{
    public static CameraPunch Instance { get; private set; }

    [Header("Heavy Push")]
    [SerializeField] private float heavyKickDistance = 0.06f;
    [SerializeField] private float heavyKickDuration = 0.04f;

    [Range(0f, 0.5f)]
    [SerializeField] private float heavyReboundMultiplier = 0.22f;

    [SerializeField] private float heavyReboundDuration = 0.035f;
    [SerializeField] private float heavySettleDuration = 0.06f;

    private Coroutine punchRoutine;

    private Vector3 baseLocalPosition;

    private bool isPunching;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        StopCurrentPunch();
    }

    public void PlayHeavyPush(
        Vector2Int direction)
    {
        Vector2 punchDirection =
            new Vector2(
                direction.x,
                direction.y
            );

        if (punchDirection.sqrMagnitude <= 0.001f)
            return;

        StartPunch(
            punchDirection.normalized,
            heavyKickDistance,
            heavyKickDuration,
            heavyReboundMultiplier,
            heavyReboundDuration,
            heavySettleDuration
        );
    }

    private void StartPunch(
        Vector2 direction,
        float distance,
        float kickDuration,
        float reboundMultiplier,
        float reboundDuration,
        float settleDuration)
    {
        StopCurrentPunch();

        baseLocalPosition =
            transform.localPosition;

        isPunching = true;

        punchRoutine =
            StartCoroutine(
                PunchRoutine(
                    direction,
                    distance,
                    kickDuration,
                    reboundMultiplier,
                    reboundDuration,
                    settleDuration
                )
            );
    }

    private IEnumerator PunchRoutine(
        Vector2 direction,
        float distance,
        float kickDuration,
        float reboundMultiplier,
        float reboundDuration,
        float settleDuration)
    {
        Vector3 direction3D =
            new Vector3(
                direction.x,
                direction.y,
                0f
            );

        Vector3 kickPosition =
            baseLocalPosition +
            direction3D * distance;

        Vector3 reboundPosition =
            baseLocalPosition -
            direction3D *
            distance *
            reboundMultiplier;

        // İlk küçük darbe.
        yield return MoveCamera(
            baseLocalPosition,
            kickPosition,
            kickDuration
        );

        // Çok küçük ters sekme.
        yield return MoveCamera(
            kickPosition,
            reboundPosition,
            reboundDuration
        );

        // Tekrar normal konuma otur.
        yield return MoveCamera(
            reboundPosition,
            baseLocalPosition,
            settleDuration
        );

        transform.localPosition =
            baseLocalPosition;

        punchRoutine = null;
        isPunching = false;
    }

    private IEnumerator MoveCamera(
        Vector3 from,
        Vector3 to,
        float duration)
    {
        if (duration <= 0f)
        {
            transform.localPosition = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );

            // SmoothStep.
            t =
                t *
                t *
                (3f - 2f * t);

            transform.localPosition =
                Vector3.LerpUnclamped(
                    from,
                    to,
                    t
                );

            yield return null;
        }

        transform.localPosition = to;
    }

    private void StopCurrentPunch()
    {
        if (punchRoutine != null)
        {
            StopCoroutine(
                punchRoutine
            );

            punchRoutine = null;
        }

        if (isPunching)
        {
            transform.localPosition =
                baseLocalPosition;
        }

        isPunching = false;
    }
}