
using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] private int doorID;

    [Header("Visual References")]
    [SerializeField] private Transform leftLeaf;
    [SerializeField] private Transform rightLeaf;
    [SerializeField] private SpriteRenderer leftLeafRenderer;
    [SerializeField] private SpriteRenderer rightLeafRenderer;
    [SerializeField] private SpriteRenderer connectionIndicatorRenderer;

    [Header("Sliding Door")]
    [SerializeField] private float openDistance = 0.24f;
    [SerializeField] private float openDuration = 0.22f;
    [SerializeField] private float closeDuration = 0.20f;

    [Header("Open Juice")]
    [SerializeField] private float openOvershoot = 0.015f;
    [SerializeField] private float openSettleDuration = 0.06f;

    [Header("Close Juice")]
    [SerializeField] private float closeOvershoot = 0.012f;
    [SerializeField] private float closeSettleDuration = 0.06f;

    private Collider2D doorCollider;
    private Vector3 leftClosedPosition;
    private Vector3 rightClosedPosition;
    private Coroutine animationRoutine;

    public int DoorID => doorID;
    public bool IsOpen { get; private set; }
    public Cell CurrentCell { get; private set; }

    // The door can be logically closed while a Box is inside.
    private bool closeWhenEmpty;

    private void Awake()
    {
        AutoFindReferences();
        doorCollider = GetComponent<Collider2D>();

        if (leftLeaf != null)
            leftClosedPosition = leftLeaf.localPosition;

        if (rightLeaf != null)
            rightClosedPosition = rightLeaf.localPosition;

        if (leftLeafRenderer != null)
        {
            leftLeafRenderer.maskInteraction =
                SpriteMaskInteraction.VisibleInsideMask;
        }

        if (rightLeafRenderer != null)
        {
            rightLeafRenderer.maskInteraction =
                SpriteMaskInteraction.VisibleInsideMask;
        }

        SetDoorPosition(false);
        UpdateConnectionVisual();
    }

    private void AutoFindReferences()
    {
        if (leftLeaf == null)
            leftLeaf = FindChildRecursive(transform, "LeftLeaf");

        if (rightLeaf == null)
            rightLeaf = FindChildRecursive(transform, "RightLeaf");

        if (leftLeafRenderer == null && leftLeaf != null)
            leftLeafRenderer = leftLeaf.GetComponent<SpriteRenderer>();

        if (rightLeafRenderer == null && rightLeaf != null)
            rightLeafRenderer = rightLeaf.GetComponent<SpriteRenderer>();

        if (connectionIndicatorRenderer == null)
        {
            connectionIndicatorRenderer =
                FindRenderer("ConnectionIndicator");
        }
    }

    private Transform FindChildRecursive(
        Transform root,
        string objectName)
    {
        foreach (Transform child in root)
        {
            if (child.name == objectName)
                return child;

            Transform found =
                FindChildRecursive(child, objectName);

            if (found != null)
                return found;
        }

        return null;
    }

    private SpriteRenderer FindRenderer(string objectName)
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.gameObject.name == objectName)
                return renderer;
        }

        return null;
    }

    public void SetDoorID(int id)
    {
        doorID = id;
        gameObject.name = "Door_" + doorID;
        UpdateConnectionVisual();
    }

    public void Place(Cell cell)
    {
        CurrentCell = cell;
        transform.position = cell.transform.position;
        cell.SetDoor(this);
    }

    private void UpdateConnectionVisual()
    {
        if (connectionIndicatorRenderer == null)
            return;

        connectionIndicatorRenderer.color =
            ConnectionColorPalette.GetColor(doorID);
    }

    // =========================================================
    // OPEN
    // =========================================================

    public void Open()
    {
        if (IsOpen)
            return;

        IsOpen = true;
        AudioManager.Play(GameSfx.DoorOpen);
        closeWhenEmpty = false;

        if (doorCollider != null)
            doorCollider.enabled = false;

        if (connectionIndicatorRenderer != null)
            connectionIndicatorRenderer.enabled = false;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        animationRoutine = StartCoroutine(OpenAnimation());
    }

    // =========================================================
    // CLOSE
    // =========================================================

    public void Close()
    {
        // Close gameplay access immediately.
        if (!IsOpen)
            return;

        IsOpen = false;
        AudioManager.Play(GameSfx.DoorClose);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        // Wait for an existing occupant to leave.
        if (CurrentCell != null && CurrentCell.IsOccupied)
        {
            closeWhenEmpty = true;

            if (doorCollider != null)
                doorCollider.enabled = false;

            if (connectionIndicatorRenderer != null)
                connectionIndicatorRenderer.enabled = false;

            return;
        }

        StartClosingVisual();
    }

    // Called from Cell.SetBox().
    public void OnCellOccupancyChanged()
    {
        if (!closeWhenEmpty ||
            IsOpen ||
            CurrentCell == null ||
            CurrentCell.IsOccupied)
        {
            return;
        }

        StartClosingVisual();
    }

    private void StartClosingVisual()
    {
        closeWhenEmpty = false;

        if (doorCollider != null)
            doorCollider.enabled = true;

        if (connectionIndicatorRenderer != null)
            connectionIndicatorRenderer.enabled = true;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        animationRoutine = StartCoroutine(CloseAnimation());
    }

    // =========================================================
    // OPEN ANIMATION
    // =========================================================

    private IEnumerator OpenAnimation()
    {
        if (leftLeaf == null || rightLeaf == null)
            yield break;

        Vector3 leftStart = leftLeaf.localPosition;
        Vector3 rightStart = rightLeaf.localPosition;

        Vector3 leftOpen =
            leftClosedPosition + Vector3.left * openDistance;

        Vector3 rightOpen =
            rightClosedPosition + Vector3.right * openDistance;

        Vector3 leftOvershoot =
            leftOpen + Vector3.left * openOvershoot;

        Vector3 rightOvershoot =
            rightOpen + Vector3.right * openOvershoot;

        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / openDuration
            );

            float eased = EaseOutCubic(t);

            leftLeaf.localPosition =
                Vector3.Lerp(leftStart, leftOvershoot, eased);

            rightLeaf.localPosition =
                Vector3.Lerp(rightStart, rightOvershoot, eased);

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < openSettleDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / openSettleDuration
            );

            float eased = EaseOutQuad(t);

            leftLeaf.localPosition =
                Vector3.Lerp(leftOvershoot, leftOpen, eased);

            rightLeaf.localPosition =
                Vector3.Lerp(rightOvershoot, rightOpen, eased);

            yield return null;
        }

        leftLeaf.localPosition = leftOpen;
        rightLeaf.localPosition = rightOpen;
        animationRoutine = null;
    }

    // =========================================================
    // CLOSE ANIMATION
    // =========================================================

    private IEnumerator CloseAnimation()
    {
        if (leftLeaf == null || rightLeaf == null)
            yield break;

        Vector3 leftStart = leftLeaf.localPosition;
        Vector3 rightStart = rightLeaf.localPosition;

        Vector3 leftOvershoot =
            leftClosedPosition + Vector3.right * closeOvershoot;

        Vector3 rightOvershoot =
            rightClosedPosition + Vector3.left * closeOvershoot;

        float elapsed = 0f;

        while (elapsed < closeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / closeDuration
            );

            float eased = EaseOutCubic(t);

            leftLeaf.localPosition =
                Vector3.Lerp(leftStart, leftOvershoot, eased);

            rightLeaf.localPosition =
                Vector3.Lerp(rightStart, rightOvershoot, eased);

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < closeSettleDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / closeSettleDuration
            );

            float eased = EaseOutQuad(t);

            leftLeaf.localPosition =
                Vector3.Lerp(
                    leftOvershoot,
                    leftClosedPosition,
                    eased
                );

            rightLeaf.localPosition =
                Vector3.Lerp(
                    rightOvershoot,
                    rightClosedPosition,
                    eased
                );

            yield return null;
        }

        leftLeaf.localPosition = leftClosedPosition;
        rightLeaf.localPosition = rightClosedPosition;
        animationRoutine = null;
    }

    // Undo restore: jump to the current logical state, no animation.
    public void SnapToState()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        closeWhenEmpty = false;

        if (doorCollider != null)
            doorCollider.enabled = !IsOpen;

        if (connectionIndicatorRenderer != null)
            connectionIndicatorRenderer.enabled = !IsOpen;

        SetDoorPosition(IsOpen);
    }

    // =========================================================
    // POSITION
    // =========================================================

    private void SetDoorPosition(bool open)
    {
        if (leftLeaf == null || rightLeaf == null)
            return;

        Vector3 leftTarget = leftClosedPosition;
        Vector3 rightTarget = rightClosedPosition;

        if (open)
        {
            leftTarget += Vector3.left * openDistance;
            rightTarget += Vector3.right * openDistance;
        }

        leftLeaf.localPosition = leftTarget;
        rightLeaf.localPosition = rightTarget;
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }
}
