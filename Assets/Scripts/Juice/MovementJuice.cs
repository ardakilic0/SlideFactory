using System.Collections;
using UnityEngine;

public class MovementJuice : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform visualRoot;


    // =========================================================
    // NORMAL MOVE
    // =========================================================

    [Header("Normal Move Feel")]
    [SerializeField] private float stretchAmount = 0.035f;
    [SerializeField] private float squashAmount = 0.02f;
    [SerializeField] private float scaleLerpSpeed = 18f;


    // =========================================================
    // HEAVY MOVE
    // =========================================================

    [Header("Heavy Move Feel")]
    [SerializeField] private float heavyStretchAmount = 0.025f;
    [SerializeField] private float heavySquashAmount = 0.015f;
    [SerializeField] private float heavyScaleLerpSpeed = 14f;


    // =========================================================
    // CONVEYOR
    // =========================================================

    [Header("Conveyor Feel")]
    [SerializeField] private float conveyorStretchAmount = 0.08f;
    [SerializeField] private float conveyorSquashAmount = 0.03f;
    [SerializeField] private float conveyorScaleLerpSpeed = 22f;


    // =========================================================
    // PUSH IMPACT
    // =========================================================

    [Header("Push Impact - Normal")]
    [SerializeField] private float normalPushSquash = 0.82f;
    [SerializeField] private float normalPushStretch = 1.10f;


    [Header("Push Impact - Heavy")]
    [SerializeField] private float heavyPushSquash = 0.72f;
    [SerializeField] private float heavyPushStretch = 1.15f;


    [Header("Push Impact Timing")]
    [SerializeField] private float pushImpactDuration = 0.05f;
    [SerializeField] private float pushSettleDuration = 0.08f;


    // =========================================================
    // MOVEMENT DETECTION
    // =========================================================

    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold = 0.002f;

    [Tooltip(
        "Arka arkaya cell hareketlerinde scale'in " +
        "her cell arasında normale dönmesini engeller."
    )]
    [SerializeField] private float stopHoldTime = 0.06f;


    private Box box;

    private Vector3 previousPosition;
    private Vector3 restScale;

    private float stillTime;

    private bool externalScaleControl;
    private bool pushImpactActive;
    private bool conveyorMode;

    private Coroutine pushImpactRoutine;


    private bool IsHeavyBox =>
        box != null &&
        box.Type == BoxType.Heavy;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot =
                transform;
        }


        box =
            GetComponent<Box>();


        restScale =
            visualRoot.localScale;


        previousPosition =
            transform.position;
    }


    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        previousPosition =
            transform.position;


        stillTime =
            0f;


        externalScaleControl =
            false;


        pushImpactActive =
            false;


        conveyorMode =
            false;


        if (visualRoot != null)
        {
            visualRoot.localScale =
                restScale;
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (visualRoot == null)
            return;


        // Buffer / Goal / başka sistem scale'i
        // kontrol ediyorsa karışma.
        if (externalScaleControl ||
            pushImpactActive)
        {
            previousPosition =
                transform.position;

            return;
        }


        Vector3 delta =
            transform.position -
            previousPosition;


        bool isMoving =
            delta.sqrMagnitude >
            movementThreshold *
            movementThreshold;


        if (isMoving)
        {
            // Hareket devam ediyor.
            // Cell sınırında tekrar normale dönme.
            stillTime =
                0f;


            ApplyMoveStretch(
                delta
            );
        }
        else
        {
            stillTime +=
                Time.deltaTime;


            // ÖNEMLİ:
            //
            // Bir sonraki queued hareket 1-2 frame
            // içinde başlayacaksa scale'i normale
            // döndürmüyoruz.
            //
            // Böylece her cell sınırında
            // "oturma / titreme" oluşmuyor.
            if (stillTime >=
                stopHoldTime)
            {
                ReturnToRestScale();
            }
        }


        previousPosition =
            transform.position;
    }


    // =========================================================
    // CONVEYOR
    // =========================================================

    public void SetConveyorMode(
        bool enabled)
    {
        conveyorMode =
            enabled;


        if (enabled)
        {
            stillTime =
                0f;
        }
    }


    // =========================================================
    // PUSH IMPACT
    // =========================================================

    public void PlayPushImpact(
        Vector2Int direction,
        bool heavy)
    {
        if (visualRoot == null)
            return;


        if (pushImpactRoutine != null)
        {
            StopCoroutine(
                pushImpactRoutine
            );


            pushImpactRoutine =
                null;
        }


        stillTime =
            0f;


        visualRoot.localScale =
            restScale;


        pushImpactRoutine =
            StartCoroutine(
                PushImpactRoutine(
                    direction,
                    heavy
                )
            );
    }


    private IEnumerator PushImpactRoutine(
        Vector2Int direction,
        bool heavy)
    {
        pushImpactActive =
            true;


        Vector3 impactScale =
            restScale;


        float squash =
            heavy
                ? heavyPushSquash
                : normalPushSquash;


        float stretch =
            heavy
                ? heavyPushStretch
                : normalPushStretch;


        bool horizontal =
            Mathf.Abs(direction.x) >
            Mathf.Abs(direction.y);


        if (horizontal)
        {
            impactScale.x =
                restScale.x *
                squash;


            impactScale.y =
                restScale.y *
                stretch;
        }
        else
        {
            impactScale.y =
                restScale.y *
                squash;


            impactScale.x =
                restScale.x *
                stretch;
        }


        float elapsed =
            0f;


        // -----------------------------------------------------
        // IMPACT
        // -----------------------------------------------------

        while (elapsed <
               pushImpactDuration)
        {
            elapsed +=
                Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    pushImpactDuration
                );


            visualRoot.localScale =
                Vector3.Lerp(
                    restScale,
                    impactScale,
                    EaseOutQuad(t)
                );


            yield return null;
        }


        visualRoot.localScale =
            impactScale;


        elapsed =
            0f;


        // -----------------------------------------------------
        // SETTLE
        // -----------------------------------------------------

        while (elapsed <
               pushSettleDuration)
        {
            elapsed +=
                Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    pushSettleDuration
                );


            visualRoot.localScale =
                Vector3.Lerp(
                    impactScale,
                    restScale,
                    EaseOutQuad(t)
                );


            yield return null;
        }


        visualRoot.localScale =
            restScale;


        pushImpactActive =
            false;


        pushImpactRoutine =
            null;
    }


    // =========================================================
    // EXTERNAL SCALE
    // =========================================================

    public void SetExternalScaleControl(
        bool enabled)
    {
        externalScaleControl =
            enabled;


        if (enabled)
        {
            stillTime =
                0f;
        }
        else
        {
            previousPosition =
                transform.position;


            stillTime =
                0f;
        }
    }


    public void SetRestScale(
        Vector3 newRestScale)
    {
        restScale =
            newRestScale;
    }


    public void SetRestScaleAndApply(
        Vector3 newRestScale)
    {
        restScale =
            newRestScale;


        if (visualRoot != null)
        {
            visualRoot.localScale =
                restScale;
        }
    }


    public Vector3 GetRestScale()
    {
        return restScale;
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void ApplyMoveStretch(
        Vector3 delta)
    {
        if (delta.sqrMagnitude <=
            0.000001f)
        {
            return;
        }


        Vector3 direction =
            delta.normalized;


        bool horizontal =
            Mathf.Abs(direction.x) >
            Mathf.Abs(direction.y);


        float currentStretch;
        float currentSquash;
        float currentLerpSpeed;


        if (conveyorMode)
        {
            currentStretch =
                conveyorStretchAmount;


            currentSquash =
                conveyorSquashAmount;


            currentLerpSpeed =
                conveyorScaleLerpSpeed;
        }
        else if (IsHeavyBox)
        {
            currentStretch =
                heavyStretchAmount;


            currentSquash =
                heavySquashAmount;


            currentLerpSpeed =
                heavyScaleLerpSpeed;
        }
        else
        {
            currentStretch =
                stretchAmount;


            currentSquash =
                squashAmount;


            currentLerpSpeed =
                scaleLerpSpeed;
        }


        Vector3 targetScale =
            restScale;


        if (horizontal)
        {
            targetScale.x =
                restScale.x *
                (1f + currentStretch);


            targetScale.y =
                restScale.y *
                (1f - currentSquash);
        }
        else
        {
            targetScale.y =
                restScale.y *
                (1f + currentStretch);


            targetScale.x =
                restScale.x *
                (1f - currentSquash);
        }


        visualRoot.localScale =
            Vector3.Lerp(
                visualRoot.localScale,
                targetScale,
                Mathf.Clamp01(
                    Time.deltaTime *
                    currentLerpSpeed
                )
            );
    }


    // =========================================================
    // RETURN TO REST
    // =========================================================

    private void ReturnToRestScale()
    {
        float currentLerpSpeed =
            IsHeavyBox
                ? heavyScaleLerpSpeed
                : scaleLerpSpeed;


        visualRoot.localScale =
            Vector3.Lerp(
                visualRoot.localScale,
                restScale,
                Mathf.Clamp01(
                    Time.deltaTime *
                    currentLerpSpeed
                )
            );


        // Çok yaklaştıysa direkt sabitle.
        if (Vector3.SqrMagnitude(
                visualRoot.localScale -
                restScale) <
            0.000001f)
        {
            visualRoot.localScale =
                restScale;
        }
    }


    // =========================================================
    // DISABLE
    // =========================================================

    private void OnDisable()
    {
        if (pushImpactRoutine != null)
        {
            StopCoroutine(
                pushImpactRoutine
            );


            pushImpactRoutine =
                null;
        }


        pushImpactActive =
            false;


        conveyorMode =
            false;


        stillTime =
            0f;


        if (visualRoot != null)
        {
            visualRoot.localScale =
                restScale;
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
}