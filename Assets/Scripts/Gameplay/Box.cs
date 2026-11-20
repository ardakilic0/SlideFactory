
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class Box : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveDuration = 0.13f;
    [SerializeField] private float conveyorMoveDuration = 0.10f;

    [Header("Box Settings")]
    [SerializeField] private int boxID = 0;
    [SerializeField] private BoxType boxType = BoxType.Normal;
    [SerializeField] private float completedScale = 0.75f;

    [Header("Buffer")]
    [Range(0.4f, 1f)]
    [SerializeField] private float bufferScale = 0.62f;
    [SerializeField] private float bufferVisualYOffset = 0.08f;

    [Header("Heavy Settings")]
    [SerializeField] private int heavyPushLimit = 2;

    [Header("Ice Juice")]
    [SerializeField] private float iceLeanAngle = 3.5f;
    [SerializeField] private float iceWobbleAngle = 1.3f;
    [SerializeField] private float iceWobbleCyclesPerCell = 0.45f;
    [SerializeField] private float iceStopKickAngle = 2.4f;
    [SerializeField] private float iceStopDuration = 0.10f;

    [SerializeField]
    [Range(1f, 2f)]
    private float heavyMoveDurationMultiplier = 1.25f;

    // =========================================================
    // VISUAL REFERENCES
    // =========================================================

    [Header("Visual References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private GameObject matchBadgeObject;

    [Header("Box Type Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite crateSprite;
    [SerializeField] private Sprite iceSprite;
    [SerializeField] private Sprite heavySprite;

    [Header("Temporary Fallback Colors")]
    [SerializeField] private Color crateColor = Color.gray;
    [SerializeField] private Color iceColor = Color.cyan;
    [SerializeField] private Color heavyColor =
        new Color(1f, 0.55f, 0.1f);

    private bool isMoving;
    private bool isCompleted;
    private bool isAutomaticMoving;

    private InvalidMoveJuice invalidMoveJuice;
    private TeleportBoxJuice teleportBoxJuice;
    private GoalCompleteJuice goalCompleteJuice;

    private Vector2Int lastAttemptedDirection;

    private Vector3 normalVisualScale;
    private Vector3 normalVisualLocalPosition;

    private MatchMarker matchMarker;
    private SpriteRenderer matchBadgeRenderer;
    private TMP_Text worldMarkerText;

    // =========================================================
    // PUBLIC
    // =========================================================

    public int BoxID => boxID;
    public BoxType Type => boxType;

    public Cell CurrentCell { get; private set; }

    public float MoveDuration => moveDuration;
    public float BufferScale => bufferScale;

    public bool IsBufferCompatible
    {
        get
        {
            return boxType != BoxType.Crate && !isCompleted;
        }
    }

    // =========================================================
    // UI BUFFER DATA
    // =========================================================

    public Sprite BufferSprite
    {
        get
        {
            return bodyRenderer != null
                ? bodyRenderer.sprite
                : null;
        }
    }

    public Color BufferSpriteColor
    {
        get
        {
            return bodyRenderer != null
                ? bodyRenderer.color
                : Color.white;
        }
    }

    public Sprite BufferBadgeSprite
    {
        get
        {
            return matchBadgeRenderer != null
                ? matchBadgeRenderer.sprite
                : null;
        }
    }

    public Color BufferBadgeColor
    {
        get
        {
            return matchBadgeRenderer != null
                ? matchBadgeRenderer.color
                : Color.white;
        }
    }

    public TMP_FontAsset BufferMarkerFont
    {
        get
        {
            return worldMarkerText != null
                ? worldMarkerText.font
                : null;
        }
    }

    public Color BufferMarkerColor
    {
        get
        {
            return worldMarkerText != null
                ? worldMarkerText.color
                : Color.white;
        }
    }

    public FontStyles BufferMarkerFontStyle
    {
        get
        {
            return worldMarkerText != null
                ? worldMarkerText.fontStyle
                : FontStyles.Normal;
        }
    }

    public TextAlignmentOptions BufferMarkerAlignment
    {
        get
        {
            return worldMarkerText != null
                ? worldMarkerText.alignment
                : TextAlignmentOptions.Center;
        }
    }

    public string BufferMarkerLabel
    {
        get
        {
            if (worldMarkerText != null &&
                !string.IsNullOrEmpty(worldMarkerText.text))
            {
                return worldMarkerText.text;
            }

            return GetIDLabel(boxID);
        }
    }

    public float BufferMarkerFontToRectRatio
    {
        get
        {
            if (worldMarkerText == null)
                return 0.5f;

            float rectHeight =
                worldMarkerText.rectTransform.rect.height;

            if (rectHeight <= 0f)
                return 0.5f;

            return worldMarkerText.fontSize / rectHeight;
        }
    }

    // =========================================================
    // WORLD VISUAL BOUNDS
    // =========================================================

    public Bounds GetBufferBodyWorldBounds()
    {
        if (bodyRenderer != null)
            return bodyRenderer.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    public Bounds GetBufferBadgeWorldBounds()
    {
        if (matchBadgeRenderer != null)
            return matchBadgeRenderer.bounds;

        return new Bounds(transform.position, Vector3.zero);
    }

    public Bounds GetBufferMarkerWorldRectBounds()
    {
        if (worldMarkerText == null)
            return new Bounds(transform.position, Vector3.zero);

        RectTransform rect = worldMarkerText.rectTransform;
        Vector3[] corners = new Vector3[4];

        rect.GetWorldCorners(corners);

        Bounds bounds = new Bounds(
            corners[0],
            Vector3.zero
        );

        for (int i = 1; i < corners.Length; i++)
            bounds.Encapsulate(corners[i]);

        return bounds;
    }

    // =========================================================
    // SELECTION / FAIL QUERY
    // =========================================================

    public bool CanBeSelected
    {
        get
        {
            return
                (boxType == BoxType.Normal ||
                 boxType == BoxType.Ice ||
                 boxType == BoxType.Heavy) &&
                !isMoving &&
                !isCompleted;
        }
    }

    public bool IsBusy
    {
        get
        {
            return isMoving || isAutomaticMoving;
        }
    }

    public bool CanUseBufferTap
    {
        get
        {
            return CanUseBuffer &&
                   CurrentCell != null &&
                   CurrentCell.HasBufferPad;
        }
    }

    // =========================================================
    // CAN MOVE
    // =========================================================

    public bool CanMove(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return false;

        if (GridManager.Instance == null)
            return false;

        if (CurrentCell == null)
            return false;

        if (isMoving || isCompleted || isAutomaticMoving)
            return false;

        if (boxType == BoxType.Crate)
            return false;

        if (boxType == BoxType.Ice)
            return CanIceMove(direction);

        if (boxType == BoxType.Heavy)
            return CanHeavyMove(direction);

        return CanNormalMove(direction);
    }

    public bool CanReactivatePortal()
    {
        if (!CanBeSelected)
            return false;

        if (CurrentCell == null || !CurrentCell.HasPortal)
            return false;

        if (GridManager.Instance == null)
            return false;

        Portal exitPortal =
            GridManager.Instance.GetPairedPortal(
                CurrentCell.Portal
            );

        if (exitPortal == null)
            return false;

        Cell destinationCell = exitPortal.CurrentCell;

        if (destinationCell == null)
            return false;

        if (destinationCell.IsOccupied)
            return false;

        if (destinationCell.IsBlocked)
            return false;

        return true;
    }

    // =========================================================
    // NORMAL MOVE VALIDATION
    // =========================================================

    private bool CanNormalMove(Vector2Int direction)
    {
        Cell targetCell =
            GridManager.Instance.GetNeighbor(
                CurrentCell,
                direction
            );

        if (targetCell == null || targetCell.IsBlocked)
            return false;

        if (!targetCell.IsOccupied)
        {
            return GridManager.Instance
                .CanEnterAfterPlateRelease(
                    targetCell,
                    new Cell[] { CurrentCell }
                );
        }

        Box blockingBox = targetCell.Box;

        if (blockingBox == null || !blockingBox.CanBePushed)
            return false;

        Cell pushedTargetCell =
            GridManager.Instance.GetNeighbor(
                targetCell,
                direction
            );

        if (pushedTargetCell == null ||
            pushedTargetCell.IsBlocked ||
            pushedTargetCell.IsOccupied)
        {
            return false;
        }

        Cell[] departing =
        {
            CurrentCell,
            targetCell
        };

        return
            GridManager.Instance.CanEnterAfterPlateRelease(
                targetCell,
                departing
            ) &&
            GridManager.Instance.CanEnterAfterPlateRelease(
                pushedTargetCell,
                departing
            );
    }

    // =========================================================
    // HEAVY MOVE VALIDATION
    // =========================================================

    private bool CanHeavyMove(Vector2Int direction)
    {
        Cell targetCell =
            GridManager.Instance.GetNeighbor(
                CurrentCell,
                direction
            );

        if (targetCell == null || targetCell.IsBlocked)
            return false;

        if (!targetCell.IsOccupied)
        {
            return GridManager.Instance
                .CanEnterAfterPlateRelease(
                    targetCell,
                    new Cell[] { CurrentCell }
                );
        }

        List<Cell> departingCells =
            new List<Cell> { CurrentCell };

        List<Cell> crateDestinations =
            new List<Cell>();

        Cell checkCell = targetCell;
        int crateCount = 0;

        while (checkCell != null && checkCell.IsOccupied)
        {
            Box foundBox = checkCell.Box;

            if (foundBox == null || !foundBox.CanBePushed)
                return false;

            crateCount++;

            if (crateCount > heavyPushLimit)
                return false;

            departingCells.Add(checkCell);

            Cell crateNext =
                GridManager.Instance.GetNeighbor(
                    checkCell,
                    direction
                );

            crateDestinations.Add(crateNext);
            checkCell = crateNext;
        }

        if (checkCell == null ||
            checkCell.IsBlocked ||
            checkCell.IsOccupied)
        {
            return false;
        }

        if (!GridManager.Instance.CanEnterAfterPlateRelease(
                targetCell,
                departingCells))
        {
            return false;
        }

        foreach (Cell crateDestination in crateDestinations)
        {
            if (!GridManager.Instance.CanEnterAfterPlateRelease(
                    crateDestination,
                    departingCells))
            {
                return false;
            }
        }

        return true;
    }

    // =========================================================
    // ICE MOVE VALIDATION
    // =========================================================

    private bool CanIceMove(Vector2Int direction)
    {
        Cell targetCell =
            GridManager.Instance.GetNeighbor(
                CurrentCell,
                direction
            );

        if (targetCell == null ||
            targetCell.IsBlocked ||
            targetCell.IsOccupied)
        {
            return false;
        }

        // The slide is valid when the first cell can be entered once the Ice
        // leaves its current cell. Further cells only decide where it stops:
        // a door held open by a Pressure Plate under the Ice closes as the Ice
        // leaves, so the Ice stops in front of it (see TryIceMove).
        return GridManager.Instance.CanEnterAfterPlateRelease(
            targetCell,
            new Cell[] { CurrentCell }
        );
    }

    // =========================================================
    // PUSH
    // =========================================================

    public bool CanBePushed
    {
        get
        {
            return boxType == BoxType.Crate && !isMoving;
        }
    }

    // =========================================================
    // BUFFER
    // =========================================================

    public bool CanUseBuffer
    {
        get
        {
            return boxType != BoxType.Crate &&
                   !isMoving &&
                   !isCompleted &&
                   !isAutomaticMoving;
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        invalidMoveJuice =
            GetComponent<InvalidMoveJuice>();

        teleportBoxJuice =
            GetComponent<TeleportBoxJuice>();

        goalCompleteJuice =
            GetComponent<GoalCompleteJuice>();

        normalVisualScale = visualRoot.localScale;
        normalVisualLocalPosition = visualRoot.localPosition;

        matchMarker =
            GetComponentInChildren<MatchMarker>(true);

        if (matchBadgeObject != null)
        {
            matchBadgeRenderer =
                matchBadgeObject.GetComponent<SpriteRenderer>();
        }

        if (matchMarker != null)
        {
            worldMarkerText =
                matchMarker.GetComponent<TMP_Text>();

            matchMarker.SetID(boxID);
        }

        UpdateBodyVisual();
        UpdateMarkerVisibility();
    }

    // Keeps the Box small on a grid BufferPad.
    private void LateUpdate()
    {
        if (visualRoot == null ||
            CurrentCell == null ||
            isMoving ||
            isAutomaticMoving ||
            isCompleted ||
            !IsBufferCompatible ||
            !CurrentCell.HasBufferPad)
        {
            return;
        }

        Vector3 targetScale =
            normalVisualScale * bufferScale;

        Vector3 targetPosition =
            normalVisualLocalPosition +
            Vector3.up * bufferVisualYOffset;

        if ((visualRoot.localScale - targetScale)
            .sqrMagnitude > 0.000001f)
        {
            visualRoot.localScale = targetScale;
        }

        if ((visualRoot.localPosition - targetPosition)
            .sqrMagnitude > 0.000001f)
        {
            visualRoot.localPosition = targetPosition;
        }
    }

    // =========================================================
    // BOX SETTINGS
    // =========================================================

    public void SetBoxID(int id)
    {
        boxID = id;

        if (matchMarker != null)
            matchMarker.SetID(boxID);

        UpdateDebugName();
    }

    public void SetBoxType(BoxType type)
    {
        boxType = type;
        UpdateBodyVisual();
        UpdateMarkerVisibility();
        UpdateDebugName();
    }

    private void UpdateBodyVisual()
    {
        if (bodyRenderer == null)
            return;

        switch (boxType)
        {
            case BoxType.Normal:
                if (normalSprite != null)
                    bodyRenderer.sprite = normalSprite;

                bodyRenderer.color = Color.white;
                break;

            case BoxType.Crate:
                if (crateSprite != null)
                {
                    bodyRenderer.sprite = crateSprite;
                    bodyRenderer.color = Color.white;
                }
                else
                {
                    if (normalSprite != null)
                        bodyRenderer.sprite = normalSprite;

                    bodyRenderer.color = crateColor;
                }
                break;

            case BoxType.Ice:
                if (iceSprite != null)
                {
                    bodyRenderer.sprite = iceSprite;
                    bodyRenderer.color = Color.white;
                }
                else
                {
                    if (normalSprite != null)
                        bodyRenderer.sprite = normalSprite;

                    bodyRenderer.color = iceColor;
                }
                break;

            case BoxType.Heavy:
                if (heavySprite != null)
                {
                    bodyRenderer.sprite = heavySprite;
                    bodyRenderer.color = Color.white;
                }
                else
                {
                    if (normalSprite != null)
                        bodyRenderer.sprite = normalSprite;

                    bodyRenderer.color = heavyColor;
                }
                break;
        }
    }

    // =========================================================
    // MARKER
    // =========================================================

    private void UpdateMarkerVisibility()
    {
        bool shouldShowMarker =
            boxType != BoxType.Crate;

        if (matchMarker != null)
            matchMarker.SetVisible(shouldShowMarker);

        if (matchBadgeObject != null)
            matchBadgeObject.SetActive(shouldShowMarker);
    }

    private string GetIDLabel(int id)
    {
        if (id >= 0 && id < 26)
            return ((char)('A' + id)).ToString();

        return id.ToString();
    }

    private void UpdateDebugName()
    {
        switch (boxType)
        {
            case BoxType.Normal:
                gameObject.name = "Box_" + boxID;
                break;

            case BoxType.Crate:
                gameObject.name = "Crate";
                break;

            case BoxType.Ice:
                gameObject.name = "IceBox_" + boxID;
                break;

            case BoxType.Heavy:
                gameObject.name = "HeavyBox_" + boxID;
                break;
        }
    }

    // =========================================================
    // PLACE
    // =========================================================

    public void Place(Cell newCell)
    {
        if (CurrentCell != null)
        {
            ReleaseFloorSwitchIfNeeded(CurrentCell);
            CurrentCell.SetBox(null);
        }

        CurrentCell = newCell;
        CurrentCell.SetBox(this);
        transform.position = newCell.transform.position;

        RestoreNormalScale();
    }

    // =========================================================
    // BUFFER VISUAL
    // =========================================================

    public void SetBufferVisual01(float t)
    {
        if (visualRoot == null)
            return;

        t = Mathf.Clamp01(t);

        visualRoot.localScale =
            Vector3.Lerp(
                normalVisualScale,
                normalVisualScale * bufferScale,
                t
            );

        Vector3 bufferPosition =
            normalVisualLocalPosition +
            Vector3.up * bufferVisualYOffset;

        visualRoot.localPosition =
            Vector3.Lerp(
                normalVisualLocalPosition,
                bufferPosition,
                t
            );
    }

    private void SetBufferExitVisual01(float t)
    {
        if (visualRoot == null)
            return;

        t = Mathf.Clamp01(t);

        visualRoot.localScale =
            Vector3.Lerp(
                normalVisualScale * bufferScale,
                normalVisualScale,
                t
            );

        Vector3 bufferPosition =
            normalVisualLocalPosition +
            Vector3.up * bufferVisualYOffset;

        visualRoot.localPosition =
            Vector3.Lerp(
                bufferPosition,
                normalVisualLocalPosition,
                t
            );
    }

    public void RestoreNormalScale()
    {
        if (visualRoot == null)
            return;

        visualRoot.localScale = normalVisualScale;
        visualRoot.localPosition = normalVisualLocalPosition;
    }

    // =========================================================
    // GRID BUFFER TRANSITION
    // =========================================================

    private void UpdateGridBufferTransition(
        Cell previousCell,
        Cell targetCell,
        float t)
    {
        bool previousIsBuffer =
            previousCell != null &&
            previousCell.HasBufferPad &&
            IsBufferCompatible;

        bool targetIsBuffer =
            targetCell != null &&
            targetCell.HasBufferPad &&
            IsBufferCompatible;

        if (targetIsBuffer && !previousIsBuffer)
        {
            targetCell.BufferPad.SetShutterOpen01(t);
            SetBufferVisual01(t);
        }

        if (previousIsBuffer && !targetIsBuffer)
        {
            previousCell.BufferPad.SetShutterOpen01(1f - t);
            SetBufferExitVisual01(t);
        }
    }

    private void CompleteGridBufferTransition(
        Cell previousCell,
        Cell targetCell)
    {
        bool previousIsBuffer =
            previousCell != null &&
            previousCell.HasBufferPad &&
            IsBufferCompatible;

        bool targetIsBuffer =
            targetCell != null &&
            targetCell.HasBufferPad &&
            IsBufferCompatible;

        if (targetIsBuffer && !previousIsBuffer)
        {
            SetBufferVisual01(1f);
            targetCell.BufferPad.TrackBox(this);
            HapticsManager.Mechanic();
            return;
        }

        if (previousIsBuffer && !targetIsBuffer)
        {
            previousCell.BufferPad.ReleaseBox(this);
            RestoreNormalScale();
        }
    }

    // =========================================================
    // GRID -> UI
    // =========================================================

    public void EnterBuffer()
    {
        if (!CanUseBuffer)
            return;

        HapticsManager.Mechanic();
        AudioManager.Play(GameSfx.Buffer);

        if (CurrentCell != null)
        {
            ReleaseFloorSwitchIfNeeded(CurrentCell);
            CurrentCell.SetBox(null);
        }

        CurrentCell = null;
        gameObject.SetActive(false);
    }

    // =========================================================
    // UNDO RESTORE
    // =========================================================

    // Same as EnterBuffer, without sound / haptics (Undo restore).
    public void EnterBufferSilently()
    {
        if (CurrentCell != null)
            CurrentCell.SetBox(null);

        CurrentCell = null;
        gameObject.SetActive(false);
    }

    // Removes the Box from its cell without any cell effect.
    public void DetachForRestore()
    {
        if (CurrentCell != null)
            CurrentCell.SetBox(null);

        CurrentCell = null;
    }

    // Puts the Box into its completed (on goal) look at once.
    public void RestoreCompleted()
    {
        isCompleted = true;

        Vector3 finalCompletedScale =
            normalVisualScale * completedScale;

        if (visualRoot != null)
            visualRoot.localScale = finalCompletedScale;

        MovementJuice juice = GetComponent<MovementJuice>();

        if (juice != null)
            juice.SetRestScale(finalCompletedScale);
    }

    // After an instant teleport-like placement: stop the movement
    // juice from reading the jump as a fast move.
    public void ResetMotionJuice()
    {
        MovementJuice juice = GetComponent<MovementJuice>();

        if (juice != null)
            juice.SetExternalScaleControl(false);
    }

    // =========================================================
    // UI -> GRID
    // =========================================================

    public void ReturnToGridBuffer(Cell cell)
    {
        if (cell == null)
            return;

        gameObject.SetActive(true);
        AudioManager.Play(GameSfx.Buffer);
        isMoving = false;
        isAutomaticMoving = false;

        CurrentCell = cell;
        CurrentCell.SetBox(this);
        transform.position = cell.transform.position;

        SetBufferVisual01(1f);
    }

    public void ExitBuffer(Cell cell)
    {
        if (cell == null)
            return;

        gameObject.SetActive(true);
        isMoving = false;
        isAutomaticMoving = false;

        CurrentCell = cell;
        CurrentCell.SetBox(this);
        transform.position = cell.transform.position;

        RestoreNormalScale();
    }

    // =========================================================
    // PLAYER MOVEMENT
    // =========================================================

    public void Move(Vector2Int direction)
    {
        if (isMoving || isCompleted)
            return;

        if (boxType == BoxType.Crate)
            return;

        lastAttemptedDirection = direction;

        // Verify movement before beginning the coroutine.
        if (!CanMove(direction))
        {
            InvalidMove();
            return;
        }

        if (boxType == BoxType.Ice)
        {
            TryIceMove(direction);
            return;
        }

        if (boxType == BoxType.Heavy)
        {
            TryHeavyMove(direction);
            return;
        }

        TryNormalMove(direction);
    }

    private void InvalidMove()
    {
        HapticsManager.Invalid();
        AudioManager.Play(GameSfx.Invalid);

        if (invalidMoveJuice != null)
            invalidMoveJuice.Play(lastAttemptedDirection);
    }

    // =========================================================
    // NORMAL
    // =========================================================

    private void TryNormalMove(Vector2Int direction)
    {
        Cell targetCell =
            GridManager.Instance.GetNeighbor(
                CurrentCell,
                direction
            );

        if (targetCell == null)
        {
            InvalidMove();
            return;
        }

        if (targetCell.IsBlocked)
        {
            InvalidMove();
            return;
        }

        if (targetCell.IsOccupied)
        {
            Box blockingBox = targetCell.Box;

            if (!blockingBox.CanBePushed)
            {
                InvalidMove();
                return;
            }

            Cell pushedTargetCell =
                GridManager.Instance.GetNeighbor(
                    targetCell,
                    direction
                );

            if (pushedTargetCell == null ||
                pushedTargetCell.IsBlocked ||
                pushedTargetCell.IsOccupied)
            {
                InvalidMove();
                return;
            }

            StartCoroutine(
                PushMoveRoutine(
                    targetCell,
                    blockingBox,
                    pushedTargetCell,
                    direction
                )
            );

            return;
        }

        StartCoroutine(MoveRoutine(targetCell));
    }

    // =========================================================
    // HEAVY
    // =========================================================

    private void TryHeavyMove(Vector2Int direction)
    {
        Cell targetCell =
            GridManager.Instance.GetNeighbor(
                CurrentCell,
                direction
            );

        if (targetCell == null || targetCell.IsBlocked)
        {
            InvalidMove();
            return;
        }

        if (!targetCell.IsOccupied)
        {
            StartCoroutine(MoveRoutine(targetCell));
            return;
        }

        List<Box> crates = new List<Box>();
        Cell checkCell = targetCell;

        while (checkCell != null && checkCell.IsOccupied)
        {
            Box foundBox = checkCell.Box;

            if (!foundBox.CanBePushed)
            {
                InvalidMove();
                return;
            }

            crates.Add(foundBox);

            if (crates.Count > heavyPushLimit)
            {
                InvalidMove();
                return;
            }

            checkCell =
                GridManager.Instance.GetNeighbor(
                    checkCell,
                    direction
                );
        }

        if (checkCell == null ||
            checkCell.IsBlocked ||
            checkCell.IsOccupied)
        {
            InvalidMove();
            return;
        }

        StartCoroutine(
            HeavyPushRoutine(
                targetCell,
                crates,
                direction
            )
        );
    }

    // =========================================================
    // ICE
    // =========================================================

    private void TryIceMove(Vector2Int direction)
    {
        Cell checkCell =
            GridManager.Instance.GetNeighbor(
                CurrentCell,
                direction
            );

        if (checkCell == null ||
            checkCell.IsBlocked ||
            checkCell.IsOccupied)
        {
            InvalidMove();
            return;
        }

        Cell finalCell = checkCell;
        int travelledCells = 1;
        Cell[] departing = { CurrentCell };

        while (true)
        {
            Cell nextCell =
                GridManager.Instance.GetNeighbor(
                    finalCell,
                    direction
                );

            if (nextCell == null ||
                nextCell.IsBlocked ||
                nextCell.IsOccupied)
            {
                break;
            }

            // A door kept open only by a plate the Ice is leaving
            // closes during the slide: stop in front of it.
            if (!GridManager.Instance.CanEnterAfterPlateRelease(
                    nextCell,
                    departing))
            {
                break;
            }

            finalCell = nextCell;
            travelledCells++;
        }

        StartCoroutine(
            IceMoveRoutine(
                finalCell,
                travelledCells,
                direction
            )
        );
    }

    // =========================================================
    // STANDARD MOVE
    // =========================================================

    private IEnumerator MoveRoutine(Cell targetCell)
    {
        isMoving = true;
        HapticsManager.Move();
        AudioManager.Play(GameSfx.Move);

        Cell previousCell = CurrentCell;

        previousCell.SetBox(null);
        targetCell.SetBox(this);

        Vector3 startPosition = transform.position;
        Vector3 endPosition = targetCell.transform.position;

        float currentMoveDuration =
            boxType == BoxType.Heavy
                ? moveDuration * heavyMoveDurationMultiplier
                : moveDuration;

        float elapsed = 0f;

        while (elapsed < currentMoveDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(elapsed / currentMoveDuration);

            transform.position =
                Vector3.Lerp(startPosition, endPosition, t);

            UpdateGridBufferTransition(
                previousCell,
                targetCell,
                t
            );

            yield return null;
        }

        transform.position = endPosition;
        CurrentCell = targetCell;

        ReleaseFloorSwitchIfNeeded(previousCell);

        CompleteGridBufferTransition(
            previousCell,
            targetCell
        );

        CheckCellEffects();

        if (!isAutomaticMoving)
            isMoving = false;
    }

    // =========================================================
    // NORMAL PUSH
    // =========================================================

    private IEnumerator PushMoveRoutine(
        Cell targetCell,
        Box pushedBox,
        Cell pushedTargetCell,
        Vector2Int direction)
    {
        isMoving = true;
        pushedBox.isMoving = true;

        HapticsManager.Push();
        AudioManager.Play(GameSfx.Push);

        Cell previousCell = CurrentCell;

        previousCell.SetBox(null);
        targetCell.SetBox(this);
        pushedTargetCell.SetBox(pushedBox);

        Vector3 myStartPosition = transform.position;
        Vector3 myEndPosition = targetCell.transform.position;

        Vector3 pushedStartPosition =
            pushedBox.transform.position;

        Vector3 pushedEndPosition =
            pushedTargetCell.transform.position;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);

            transform.position =
                Vector3.Lerp(
                    myStartPosition,
                    myEndPosition,
                    t
                );

            pushedBox.transform.position =
                Vector3.Lerp(
                    pushedStartPosition,
                    pushedEndPosition,
                    t
                );

            UpdateGridBufferTransition(
                previousCell,
                targetCell,
                t
            );

            yield return null;
        }

        transform.position = myEndPosition;
        pushedBox.transform.position = pushedEndPosition;

        CurrentCell = targetCell;
        pushedBox.CurrentCell = pushedTargetCell;

        ReleaseFloorSwitchIfNeeded(previousCell);
        ReleaseFloorSwitchIfNeeded(targetCell);

        CompleteGridBufferTransition(
            previousCell,
            targetCell
        );

        MovementJuice pushedMovementJuice =
            pushedBox.GetComponent<MovementJuice>();

        if (pushedMovementJuice != null &&
            !pushedTargetCell.HasPortal &&
            !pushedTargetCell.HasConveyor &&
            !pushedTargetCell.HasBufferPad)
        {
            pushedMovementJuice.PlayPushImpact(
                direction,
                false
            );
        }

        CheckCellEffects();
        pushedBox.CheckCellEffects();

        if (!pushedBox.isAutomaticMoving)
            pushedBox.isMoving = false;

        if (!isAutomaticMoving)
            isMoving = false;
    }

    // =========================================================
    // HEAVY PUSH
    // =========================================================

    private IEnumerator HeavyPushRoutine(
        Cell targetCell,
        List<Box> crates,
        Vector2Int direction)
    {
        isMoving = true;

        foreach (Box crate in crates)
            crate.isMoving = true;

        HapticsManager.Push();
        AudioManager.Play(GameSfx.HeavyPush);

        Cell previousCell = CurrentCell;

        List<Cell> crateTargetCells = new List<Cell>();
        List<Vector3> crateStartPositions = new List<Vector3>();
        List<Vector3> crateEndPositions = new List<Vector3>();

        foreach (Box crate in crates)
        {
            Cell crateTarget =
                GridManager.Instance.GetNeighbor(
                    crate.CurrentCell,
                    direction
                );

            crateTargetCells.Add(crateTarget);
            crateStartPositions.Add(crate.transform.position);
            crateEndPositions.Add(crateTarget.transform.position);
        }

        previousCell.SetBox(null);

        foreach (Box crate in crates)
            crate.CurrentCell.SetBox(null);

        targetCell.SetBox(this);

        for (int i = 0; i < crates.Count; i++)
            crateTargetCells[i].SetBox(crates[i]);

        Vector3 myStartPosition = transform.position;
        Vector3 myEndPosition = targetCell.transform.position;

        float heavyMoveDuration =
            moveDuration * heavyMoveDurationMultiplier;

        float elapsed = 0f;

        while (elapsed < heavyMoveDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(elapsed / heavyMoveDuration);

            transform.position =
                Vector3.Lerp(
                    myStartPosition,
                    myEndPosition,
                    t
                );

            for (int i = 0; i < crates.Count; i++)
            {
                crates[i].transform.position =
                    Vector3.Lerp(
                        crateStartPositions[i],
                        crateEndPositions[i],
                        t
                    );
            }

            UpdateGridBufferTransition(
                previousCell,
                targetCell,
                t
            );

            yield return null;
        }

        transform.position = myEndPosition;

        ReleaseFloorSwitchIfNeeded(previousCell);

        for (int i = 0; i < crates.Count; i++)
        {
            ReleaseFloorSwitchIfNeeded(
                crates[i].CurrentCell
            );
        }

        CurrentCell = targetCell;

        for (int i = 0; i < crates.Count; i++)
        {
            crates[i].transform.position =
                crateEndPositions[i];

            crates[i].CurrentCell =
                crateTargetCells[i];
        }

        CompleteGridBufferTransition(
            previousCell,
            targetCell
        );

        if (CameraPunch.Instance != null)
        {
            CameraPunch.Instance.PlayHeavyPush(direction);
        }

        for (int i = 0; i < crates.Count; i++)
        {
            Cell crateCell = crateTargetCells[i];

            MovementJuice crateMovementJuice =
                crates[i].GetComponent<MovementJuice>();

            if (crateMovementJuice != null &&
                !crateCell.HasPortal &&
                !crateCell.HasConveyor &&
                !crateCell.HasBufferPad)
            {
                crateMovementJuice.PlayPushImpact(
                    direction,
                    true
                );
            }
        }

        CheckCellEffects();

        foreach (Box crate in crates)
        {
            crate.CheckCellEffects();

            if (!crate.isAutomaticMoving)
                crate.isMoving = false;
        }

        if (!isAutomaticMoving)
            isMoving = false;
    }

    // =========================================================
    // ICE MOVE
    // =========================================================

    private IEnumerator IceMoveRoutine(
        Cell targetCell,
        int travelledCells,
        Vector2Int direction)
    {
        isMoving = true;
        HapticsManager.Move();
        AudioManager.Play(GameSfx.Slide);

        Cell previousCell = CurrentCell;

        previousCell.SetBox(null);
        targetCell.SetBox(this);

        Vector3 startPosition = transform.position;
        Vector3 endPosition = targetCell.transform.position;

        float totalDuration =
            moveDuration * travelledCells;

        float elapsed = 0f;

        Quaternion startVisualRotation =
            visualRoot != null
                ? visualRoot.localRotation
                : Quaternion.identity;

        float directionSign;

        if (direction.x != 0)
        {
            directionSign =
                direction.x > 0 ? -1f : 1f;
        }
        else
        {
            directionSign =
                direction.y > 0 ? 1f : -1f;
        }

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            float movementT =
                Mathf.Clamp01(elapsed / totalDuration);

            transform.position =
                Vector3.Lerp(
                    startPosition,
                    endPosition,
                    movementT
                );

            if (visualRoot != null)
            {
                float envelope =
                    Mathf.Sin(movementT * Mathf.PI);

                float lean =
                    directionSign *
                    iceLeanAngle *
                    envelope;

                float cycles =
                    Mathf.Max(
                        1f,
                        travelledCells *
                        iceWobbleCyclesPerCell
                    );

                float wobble =
                    Mathf.Sin(
                        movementT *
                        Mathf.PI *
                        2f *
                        cycles
                    ) *
                    iceWobbleAngle *
                    envelope;

                visualRoot.localRotation =
                    startVisualRotation *
                    Quaternion.Euler(
                        0f,
                        0f,
                        lean + wobble
                    );
            }

            if (targetCell.HasBufferPad &&
                IsBufferCompatible)
            {
                float finalSegmentStart =
                    totalDuration - moveDuration;

                float bufferT =
                    Mathf.Clamp01(
                        (elapsed - finalSegmentStart) /
                        moveDuration
                    );

                targetCell.BufferPad
                    .SetShutterOpen01(bufferT);

                SetBufferVisual01(bufferT);
            }

            if (previousCell.HasBufferPad &&
                !targetCell.HasBufferPad &&
                IsBufferCompatible)
            {
                float exitT =
                    Mathf.Clamp01(elapsed / moveDuration);

                previousCell.BufferPad
                    .SetShutterOpen01(1f - exitT);

                SetBufferExitVisual01(exitT);
            }

            yield return null;
        }

        transform.position = endPosition;
        CurrentCell = targetCell;

        ReleaseFloorSwitchIfNeeded(previousCell);

        CompleteGridBufferTransition(
            previousCell,
            targetCell
        );

        bool shouldPlayIceStop =
            !targetCell.HasPortal &&
            !targetCell.HasConveyor &&
            !targetCell.HasBufferPad;

        if (visualRoot != null && shouldPlayIceStop)
        {
            float stopElapsed = 0f;

            while (stopElapsed < iceStopDuration)
            {
                stopElapsed += Time.deltaTime;

                float stopT =
                    Mathf.Clamp01(
                        stopElapsed / iceStopDuration
                    );

                float kick =
                    Mathf.Sin(stopT * Mathf.PI) *
                    -directionSign *
                    iceStopKickAngle;

                visualRoot.localRotation =
                    startVisualRotation *
                    Quaternion.Euler(
                        0f,
                        0f,
                        kick
                    );

                yield return null;
            }
        }

        if (visualRoot != null)
            visualRoot.localRotation = startVisualRotation;

        CheckCellEffects();

        if (!isAutomaticMoving)
            isMoving = false;
    }

    // =========================================================
    // FLOOR SWITCH RELEASE
    // =========================================================

    private void ReleaseFloorSwitchIfNeeded(Cell cell)
    {
        if (cell == null ||
            !cell.HasSwitch ||
            cell.FloorSwitch == null)
        {
            return;
        }

        cell.FloorSwitch.Release();
    }

    // =========================================================
    // CELL EFFECTS
    // =========================================================

    private void CheckCellEffects(bool allowPortal = true)
    {
        if (CurrentCell == null)
            return;

        if (CurrentCell.HasSwitch)
        {
            CurrentCell.FloorSwitch.Activate();
            HapticsManager.Mechanic();
        }

        if (allowPortal && CurrentCell.HasPortal)
        {
            bool teleported =
                TryTeleport(CurrentCell.Portal);

            if (teleported)
            {
                CheckCellEffects(false);
                return;
            }
        }

        CheckGoalCompletion();

        if (isCompleted)
            return;

        if (CurrentCell.HasConveyor && !isAutomaticMoving)
        {
            StartCoroutine(ConveyorRoutine());
        }
    }

    // =========================================================
    // CONVEYOR
    // =========================================================

    private IEnumerator ConveyorRoutine()
    {
        if (CurrentCell == null ||
            !CurrentCell.HasConveyor)
        {
            yield break;
        }

        isAutomaticMoving = true;
        isMoving = true;

        MovementJuice conveyorMovementJuice =
            GetComponent<MovementJuice>();

        HashSet<Cell> visitedConveyorCells =
            new HashSet<Cell>();

        while (CurrentCell != null &&
               CurrentCell.HasConveyor &&
               !isCompleted)
        {
            if (!visitedConveyorCells.Add(CurrentCell))
            {
                Debug.LogWarning("Conveyor loop detected!");
                break;
            }

            Vector2Int direction =
                CurrentCell.Conveyor.DirectionVector;

            Cell targetCell =
                GridManager.Instance.GetNeighbor(
                    CurrentCell,
                    direction
                );

            if (targetCell == null ||
                targetCell.IsBlocked ||
                targetCell.IsOccupied)
            {
                break;
            }

            if (!GridManager.Instance
                .CanEnterAfterPlateRelease(
                    targetCell,
                    new Cell[] { CurrentCell }))
            {
                break;
            }

            Cell previousCell = CurrentCell;

            previousCell.SetBox(null);
            targetCell.SetBox(this);

            Vector3 startPosition = transform.position;
            Vector3 endPosition = targetCell.transform.position;

            if (conveyorMovementJuice != null)
                conveyorMovementJuice.SetConveyorMode(true);

            float elapsed = 0f;

            while (elapsed < conveyorMoveDuration)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(elapsed / conveyorMoveDuration);

                transform.position =
                    Vector3.Lerp(
                        startPosition,
                        endPosition,
                        t
                    );

                UpdateGridBufferTransition(
                    previousCell,
                    targetCell,
                    t
                );

                yield return null;
            }

            transform.position = endPosition;
            CurrentCell = targetCell;

            ReleaseFloorSwitchIfNeeded(previousCell);

            CompleteGridBufferTransition(
                previousCell,
                targetCell
            );

            if (CurrentCell.HasSwitch)
            {
                CurrentCell.FloorSwitch.Activate();
                HapticsManager.Mechanic();
            }

            if (CurrentCell.HasPortal)
            {
                bool teleported =
                    TryTeleport(CurrentCell.Portal);

                if (teleported)
                {
                    CheckGoalCompletion();
                    break;
                }
            }

            CheckGoalCompletion();

            if (isCompleted)
                break;
        }

        if (conveyorMovementJuice != null)
            conveyorMovementJuice.SetConveyorMode(false);

        isAutomaticMoving = false;
        isMoving = false;
    }

    // =========================================================
    // PORTAL
    // =========================================================

    public bool TryReactivatePortal()
    {
        if (!CanBeSelected)
            return false;

        if (CurrentCell == null || !CurrentCell.HasPortal)
            return false;

        bool teleported =
            TryTeleport(CurrentCell.Portal);

        if (!teleported)
            return false;

        CheckCellEffects(false);
        return true;
    }

    private bool TryTeleport(Portal entryPortal)
    {
        Portal exitPortal =
            GridManager.Instance.GetPairedPortal(
                entryPortal
            );

        if (exitPortal == null)
        {
            Debug.LogWarning(
                "Portal pair bulunamadı. ID: " +
                entryPortal.PortalID
            );

            return false;
        }

        Cell destinationCell = exitPortal.CurrentCell;

        if (destinationCell == null ||
            destinationCell.IsOccupied ||
            destinationCell.IsBlocked)
        {
            return false;
        }

        Vector3 entryVisualWorldPosition =
            visualRoot != null
                ? visualRoot.position
                : transform.position;

        Vector3 entryVisualScale =
            visualRoot != null
                ? visualRoot.localScale
                : Vector3.one;

        entryPortal.PlayTeleportPulse();

        Cell sourceCell = CurrentCell;
        sourceCell.SetBox(null);

        CurrentCell = destinationCell;
        CurrentCell.SetBox(this);

        transform.position =
            destinationCell.transform.position;

        ReleaseFloorSwitchIfNeeded(sourceCell);
        exitPortal.PlayTeleportPulse();

        if (sourceCell.HasBufferPad &&
            !destinationCell.HasBufferPad)
        {
            sourceCell.BufferPad.ReleaseBox(this);
            RestoreNormalScale();
        }

        if (destinationCell.HasBufferPad &&
            IsBufferCompatible)
        {
            SetBufferVisual01(1f);
            destinationCell.BufferPad.TrackBox(this);
        }

        Vector3 exitVisualLocalPosition =
            visualRoot != null
                ? visualRoot.localPosition
                : Vector3.zero;

        Vector3 exitVisualScale =
            visualRoot != null
                ? visualRoot.localScale
                : Vector3.one;

        if (teleportBoxJuice != null)
        {
            teleportBoxJuice.Play(
                entryVisualWorldPosition,
                entryVisualScale,
                exitVisualLocalPosition,
                exitVisualScale
            );
        }

        HapticsManager.Mechanic();
        AudioManager.Play(GameSfx.Portal);
        return true;
    }

    // =========================================================
    // GOAL
    // =========================================================

    private void CheckGoalCompletion()
    {
        if (isCompleted)
            return;

        if (boxType == BoxType.Crate)
            return;

        if (CurrentCell == null || !CurrentCell.HasGoal)
            return;

        Goal goal = CurrentCell.Goal;

        if (!goal.CanAccept(this))
            return;

        isCompleted = true;
        HapticsManager.Goal();
        AudioManager.Play(GameSfx.Goal);

        Vector3 finalCompletedScale =
            normalVisualScale * completedScale;

        if (goalCompleteJuice != null)
        {
            goalCompleteJuice.Play(
                finalCompletedScale
            );
        }
        else if (visualRoot != null)
        {
            visualRoot.localScale = finalCompletedScale;
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.CompleteBox(this);
        }
    }
}
