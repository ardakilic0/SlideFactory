using System.Collections;
using TMPro;
using UnityEngine;

public class MoveCounterUI : MonoBehaviour
{
    public static MoveCounterUI Instance
    {
        get;
        private set;
    }

    [Header("Reference")]
    [SerializeField]
    private TMP_Text movesValueText;

    [Header("Zero Moves Juice")]
    [SerializeField]
    private Color zeroColor =
        new Color(1f, 0.08f, 0.05f, 1f);

    [SerializeField]
    private float pulseScale = 1.55f;

    [SerializeField]
    private float scaleUpDuration = 0.13f;

    [SerializeField]
    private float scaleDownDuration = 0.18f;

    private RectTransform movesRect;

    private Vector3 normalScale;

    private Color normalColor;
    private Color32 normalFaceColor;

    private Coroutine zeroRoutine;

    private int lastMoves = -999;

    private void Awake()
    {
        Instance = this;

        if (movesValueText == null)
            return;

        movesRect =
            movesValueText.rectTransform;

        normalScale =
            movesRect.localScale;

        normalColor =
            movesValueText.color;

        normalFaceColor =
            movesValueText.faceColor;
    }

    // =========================================================
    // SET MOVES
    // =========================================================

    public void SetMoves(
        int moves,
        bool animateZero = true)
    {
        if (movesValueText == null)
            return;

        movesValueText.text =
            moves.ToString();

        lastMoves =
            moves;

        if (moves > 0)
        {
            StopZeroJuice();

            RestoreNormalState();

            return;
        }

        if (moves == 0)
        {
            StopZeroJuice();

            // Win olup olmadığı henüz bilinmiyorsa
            // sadece normal 0 göster.
            if (!animateZero)
            {
                RestoreNormalState();

                return;
            }

            zeroRoutine =
                StartCoroutine(
                    ZeroMovesRoutine()
                );
        }
    }

    // =========================================================
    // PLAY ZERO JUICE
    // =========================================================

    public void PlayZeroJuice()
    {
        if (movesValueText == null)
            return;

        if (lastMoves != 0)
            return;

        StopZeroJuice();

        zeroRoutine =
            StartCoroutine(
                ZeroMovesRoutine()
            );
    }

    // =========================================================
    // ZERO ANIMATION
    // =========================================================

    private IEnumerator ZeroMovesRoutine()
    {
        Vector3 bigScale =
            normalScale *
            pulseScale;

        Color startFaceColor =
            normalFaceColor;

        // =====================================================
        // SCALE UP + RED
        // =====================================================

        float elapsed = 0f;

        while (elapsed <
               scaleUpDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    scaleUpDuration
                );

            t =
                EaseOut(t);

            movesRect.localScale =
                Vector3.Lerp(
                    normalScale,
                    bigScale,
                    t
                );

            Color currentColor =
                Color.Lerp(
                    startFaceColor,
                    zeroColor,
                    t
                );

            movesValueText.faceColor =
                (Color32)currentColor;

            yield return null;
        }

        movesRect.localScale =
            bigScale;

        movesValueText.faceColor =
            (Color32)zeroColor;

        // =====================================================
        // SCALE DOWN + ORIGINAL COLOR
        // =====================================================

        elapsed = 0f;

        while (elapsed <
               scaleDownDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    scaleDownDuration
                );

            t =
                Smooth(t);

            movesRect.localScale =
                Vector3.Lerp(
                    bigScale,
                    normalScale,
                    t
                );

            Color currentColor =
                Color.Lerp(
                    zeroColor,
                    startFaceColor,
                    t
                );

            movesValueText.faceColor =
                (Color32)currentColor;

            yield return null;
        }

        RestoreNormalState();

        zeroRoutine = null;
    }

    // =========================================================
    // STOP
    // =========================================================

    private void StopZeroJuice()
    {
        if (zeroRoutine == null)
            return;

        StopCoroutine(
            zeroRoutine
        );

        zeroRoutine = null;
    }

    // =========================================================
    // RESTORE
    // =========================================================

    private void RestoreNormalState()
    {
        if (movesRect != null)
        {
            movesRect.localScale =
                normalScale;
        }

        if (movesValueText != null)
        {
            movesValueText.color =
                normalColor;

            movesValueText.faceColor =
                normalFaceColor;
        }
    }

    private float EaseOut(float t)
    {
        t =
            Mathf.Clamp01(t);

        return
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );
    }

    private float Smooth(float t)
    {
        t =
            Mathf.Clamp01(t);

        return
            t *
            t *
            (3f - 2f * t);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}