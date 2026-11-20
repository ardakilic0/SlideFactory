
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    // Tutorials / popups can block board input.
    public static bool BlockBoardInput;

    [Header("Swipe")]
    [SerializeField] private float swipeScreenRatio = 0.045f;
    [SerializeField] private float minSwipePixels = 28f;
    [SerializeField] private float maxSwipePixels = 65f;

    [Header("Fast Input Queue")]
    [SerializeField] private int maxQueuedMoves = 6;
    [SerializeField] private float queuedMoveLifeTime = 1.25f;

    private Vector2 pointerDownScreenPosition;
    private bool pointerActive;
    private Box pointerBox;

    private readonly List<MoveCommand> moveQueue =
        new List<MoveCommand>();

    private struct MoveCommand
    {
        public Box box;
        public Vector2Int direction;
        public float expireTime;

        public MoveCommand(
            Box box,
            Vector2Int direction,
            float expireTime)
        {
            this.box = box;
            this.direction = direction;
            this.expireTime = expireTime;
        }
    }

    private void Awake()
    {
        Instance = this;
        BlockBoardInput = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void CancelAll()
    {
        CancelPointer();
        ClearQueue();
    }

    private void Update()
    {
        if (BlockBoardInput)
        {
            CancelPointer();
            ClearQueue();
            return;
        }

        if (LevelManager.Instance != null &&
            LevelManager.Instance.IsInputLocked)
        {
            CancelPointer();
            ClearQueue();
            return;
        }

        if (SettingsController.Instance != null &&
            SettingsController.Instance.IsOpen)
        {
            CancelPointer();
            ClearQueue();
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
                BeginPointer(touch.position);

            if (touch.phase == TouchPhase.Ended)
                EndPointer(touch.position);

            if (touch.phase == TouchPhase.Canceled)
                CancelPointer();

            return;
        }

        if (Input.GetMouseButtonDown(0))
            BeginPointer(Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
            EndPointer(Input.mousePosition);
    }

    private void LateUpdate()
    {
        if (LevelManager.Instance != null &&
            LevelManager.Instance.IsInputLocked)
        {
            ClearQueue();
            return;
        }

        ProcessMoveQueue();
    }

    private void BeginPointer(Vector2 screenPosition)
    {
        if (IsOverInteractiveUI(screenPosition))
        {
            CancelPointer();
            return;
        }

        Box hitBox =
            FindBoxAtScreenPosition(screenPosition);

        if (hitBox == null || hitBox.Type == BoxType.Crate)
        {
            CancelPointer();
            return;
        }

        pointerBox = hitBox;
        pointerDownScreenPosition = screenPosition;
        pointerActive = true;
    }

    private void EndPointer(Vector2 screenPosition)
    {
        if (!pointerActive || pointerBox == null)
        {
            CancelPointer();
            return;
        }

        Vector2 delta =
            screenPosition - pointerDownScreenPosition;

        Vector2Int direction = GetDirection(delta);

        // TAP
        if (direction == Vector2Int.zero)
        {
            // Portal re-activation (free) or grid buffer -> UI buffer.
            GameActions.TryTap(pointerBox);

            CancelPointer();
            return;
        }

        // SWIPE
        QueueMove(pointerBox, direction);
        CancelPointer();
    }

    private void CancelPointer()
    {
        pointerActive = false;
        pointerBox = null;
    }

    private void QueueMove(
        Box box,
        Vector2Int direction)
    {
        if (box == null || direction == Vector2Int.zero)
            return;

        if (moveQueue.Count >= maxQueuedMoves)
            return;

        moveQueue.Add(
            new MoveCommand(
                box,
                direction,
                Time.unscaledTime + queuedMoveLifeTime
            )
        );
    }

    private void ProcessMoveQueue()
    {
        if (moveQueue.Count == 0)
            return;

        for (int i = moveQueue.Count - 1; i >= 0; i--)
        {
            MoveCommand command = moveQueue[i];

            if (command.box == null ||
                Time.unscaledTime > command.expireTime)
            {
                moveQueue.RemoveAt(i);
            }
        }

        if (moveQueue.Count == 0)
            return;

        for (int i = 0; i < moveQueue.Count; i++)
        {
            MoveCommand command = moveQueue[i];

            if (command.box == null)
                continue;

            if (HasEarlierCommandForBox(i, command.box))
                continue;

            if (!command.box.CanBeSelected)
                continue;

            moveQueue.RemoveAt(i);

            if (!command.box.gameObject.activeInHierarchy)
            {
                i = -1;
                continue;
            }

            GameActions.TrySwipe(
                command.box,
                command.direction
            );

            i = -1;

            if (LevelManager.Instance != null &&
                LevelManager.Instance.IsInputLocked)
            {
                ClearQueue();
                return;
            }
        }
    }

    private bool HasEarlierCommandForBox(
        int currentIndex,
        Box box)
    {
        for (int i = 0; i < currentIndex; i++)
        {
            if (moveQueue[i].box == box)
                return true;
        }

        return false;
    }

    private void ClearQueue()
    {
        moveQueue.Clear();
    }

    private Vector2Int GetDirection(Vector2 delta)
    {
        float shortestSide =
            Mathf.Min(Screen.width, Screen.height);

        float swipeDistance =
            Mathf.Clamp(
                shortestSide * swipeScreenRatio,
                minSwipePixels,
                maxSwipePixels
            );

        if (delta.magnitude < swipeDistance)
            return Vector2Int.zero;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x > 0f
                ? Vector2Int.right
                : Vector2Int.left;
        }

        return delta.y > 0f
            ? Vector2Int.up
            : Vector2Int.down;
    }

    private Box FindBoxAtScreenPosition(
        Vector2 screenPosition)
    {
        if (Camera.main == null)
            return null;

        Vector2 worldPosition =
            Camera.main.ScreenToWorldPoint(
                screenPosition
            );

        Collider2D[] hits =
            Physics2D.OverlapPointAll(worldPosition);

        Box closestBox = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            Box hitBox = hit.GetComponent<Box>();

            if (hitBox == null)
                hitBox = hit.GetComponentInParent<Box>();

            if (hitBox == null)
                continue;

            if (hitBox.Type == BoxType.Crate)
                continue;

            float distance =
                Vector2.Distance(
                    worldPosition,
                    hitBox.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestBox = hitBox;
            }
        }

        return closestBox;
    }

    private bool IsOverInteractiveUI(
        Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData =
            new PointerEventData(
                EventSystem.current
            );

        eventData.position = screenPosition;

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(
            eventData,
            results
        );

        foreach (RaycastResult result in results)
        {
            Selectable selectable =
                result.gameObject
                    .GetComponentInParent<Selectable>();

            if (selectable != null &&
                selectable.interactable)
            {
                return true;
            }
        }

        return false;
    }
}
