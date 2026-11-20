
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Cell cellPrefab;
    [SerializeField] private Box boxPrefab;
    [SerializeField] private Wall wallPrefab;
    [SerializeField] private Goal goalPrefab;
    [SerializeField] private Door doorPrefab;
    [SerializeField] private FloorSwitch floorSwitchPrefab;
    [SerializeField] private Portal portalPrefab;
    [SerializeField] private Conveyor conveyorPrefab;
    [SerializeField] private PressurePlate pressurePlatePrefab;
    [SerializeField] private BufferPad bufferPadPrefab;

    private const float CellSize = 1.1f;

    private Cell[,] grid;
    private int width;
    private int height;

    private readonly Dictionary<int, List<Door>> doorsByID =
        new Dictionary<int, List<Door>>();

    private readonly Dictionary<int, int> activePressurePlates =
        new Dictionary<int, int>();

    private readonly HashSet<int> permanentlyOpenDoors =
        new HashSet<int>();

    public int Width => width;
    public int Height => height;

    private void Awake()
    {
        Instance = this;
    }

    public void GenerateGrid(int gridWidth, int gridHeight)
    {
        width = gridWidth;
        height = gridHeight;

        grid = new Cell[width, height];

        Vector2 offset = new Vector2(
            (width - 1) * CellSize * 0.5f,
            (height - 1) * CellSize * 0.5f
        );

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 position = new Vector3(
                    x * CellSize - offset.x,
                    y * CellSize - offset.y,
                    0f
                );

                Cell spawnedCell = Instantiate(
                    cellPrefab,
                    position,
                    Quaternion.identity,
                    transform
                );

                spawnedCell.Initialize(
                    new Vector2Int(x, y)
                );

                grid[x, y] = spawnedCell;
            }
        }
    }

    public void ClearEverything()
    {
        doorsByID.Clear();
        activePressurePlates.Clear();
        permanentlyOpenDoors.Clear();

        // Children are detached and hidden right away, so a level can
        // be rebuilt in the same frame (Undo) without the old objects
        // still showing up in GetComponentsInChildren.
        List<Transform> children = new List<Transform>();

        foreach (Transform child in transform)
        {
            children.Add(child);
        }

        foreach (Transform child in children)
        {
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        grid = null;
        width = 0;
        height = 0;
    }

    public Cell GetCell(Vector2Int coordinates)
    {
        if (!IsInsideGrid(coordinates))
            return null;

        return grid[coordinates.x, coordinates.y];
    }

    public bool IsInsideGrid(Vector2Int coordinates)
    {
        return
            grid != null &&
            coordinates.x >= 0 &&
            coordinates.x < width &&
            coordinates.y >= 0 &&
            coordinates.y < height;
    }

    public Cell GetNeighbor(
        Cell cell,
        Vector2Int direction)
    {
        if (cell == null)
            return null;

        return GetCell(cell.Coordinates + direction);
    }

    public Vector3 GetWorldPosition(Vector2Int coordinates)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return Vector3.zero;

        return cell.transform.position;
    }

    public Box SpawnBox(Vector2Int coordinates)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return null;

        Box box = Instantiate(
            boxPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        box.Place(cell);
        return box;
    }

    public void CreateWall(Vector2Int coordinates)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        Wall wall = Instantiate(
            wallPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        wall.Place(cell);
        cell.SetWall(wall);
    }

    public void CreateGoal(
        Vector2Int coordinates,
        int goalID)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        Goal goal = Instantiate(
            goalPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        goal.SetGoalID(goalID);
        goal.Place(cell);
        cell.SetGoal(goal);
    }

    public void CreateDoor(
        Vector2Int coordinates,
        int doorID)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        Door door = Instantiate(
            doorPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        door.SetDoorID(doorID);
        door.Place(cell);

        if (!doorsByID.ContainsKey(doorID))
        {
            doorsByID.Add(
                doorID,
                new List<Door>()
            );
        }

        doorsByID[doorID].Add(door);
    }

    public void CreateFloorSwitch(
        Vector2Int coordinates,
        int doorID)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        FloorSwitch floorSwitch = Instantiate(
            floorSwitchPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        floorSwitch.SetTargetDoorID(doorID);
        floorSwitch.Place(cell);
    }

    // =========================================================
    // PERMANENT DOOR
    // =========================================================

    public void OpenDoor(int doorID)
    {
        permanentlyOpenDoors.Add(doorID);
        ApplyDoorState(doorID);
    }

    public bool IsDoorPermanentlyOpen(int doorID)
    {
        return permanentlyOpenDoors.Contains(doorID);
    }

    public List<int> GetPermanentlyOpenDoors()
    {
        return new List<int>(permanentlyOpenDoors);
    }

    // Undo restore: every door jumps to its logical state.
    public void SnapAllDoors()
    {
        foreach (List<Door> doors in doorsByID.Values)
        {
            foreach (Door door in doors)
            {
                if (door != null)
                    door.SnapToState();
            }
        }
    }

    // =========================================================
    // PRESSURE PLATE
    // =========================================================

    public void SetPressurePlateState(
        int doorID,
        bool pressed)
    {
        if (!activePressurePlates.ContainsKey(doorID))
        {
            activePressurePlates.Add(doorID, 0);
        }

        if (pressed)
        {
            activePressurePlates[doorID]++;
        }
        else
        {
            activePressurePlates[doorID] = Mathf.Max(
                0,
                activePressurePlates[doorID] - 1
            );
        }

        ApplyDoorState(doorID);
    }

    // =========================================================
    // PRE-MOVE DOOR CHECK
    // =========================================================

    public bool CanEnterAfterPlateRelease(
        Cell destination,
        IList<Cell> departingCells)
    {
        if (destination == null || destination.IsBlocked)
            return false;

        if (!destination.HasDoor)
            return true;

        int id = destination.Door.DoorID;

        if (permanentlyOpenDoors.Contains(id))
            return true;

        int presses;

        if (!activePressurePlates.TryGetValue(
                id,
                out presses) ||
            presses <= 0)
        {
            return false;
        }

        if (departingCells != null)
        {
            HashSet<PressurePlate> counted =
                new HashSet<PressurePlate>();

            foreach (Cell cell in departingCells)
            {
                if (cell == null || !cell.HasPressurePlate)
                    continue;

                PressurePlate plate = cell.PressurePlate;

                if (plate.IsPressed &&
                    plate.TargetDoorID == id &&
                    counted.Add(plate))
                {
                    presses--;
                }
            }
        }

        return presses > 0;
    }

    // =========================================================
    // UPDATE DOORS
    // =========================================================

    private void ApplyDoorState(int doorID)
    {
        if (!doorsByID.ContainsKey(doorID))
            return;

        bool shouldBeOpen =
            permanentlyOpenDoors.Contains(doorID);

        if (activePressurePlates.ContainsKey(doorID))
        {
            shouldBeOpen |= activePressurePlates[doorID] > 0;
        }

        foreach (Door door in doorsByID[doorID])
        {
            if (shouldBeOpen)
                door.Open();
            else
                door.Close();
        }
    }

    // =========================================================
    // PORTAL
    // =========================================================

    public void CreatePortal(
        Vector2Int coordinates,
        int portalID)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        Portal portal = Instantiate(
            portalPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        portal.SetPortalID(portalID);
        portal.Place(cell);
    }

    public Portal GetPairedPortal(Portal currentPortal)
    {
        if (currentPortal == null)
            return null;

        Portal[] portals =
            GetComponentsInChildren<Portal>();

        foreach (Portal portal in portals)
        {
            if (portal == currentPortal)
                continue;

            if (portal.PortalID == currentPortal.PortalID)
                return portal;
        }

        return null;
    }

    // =========================================================
    // CONVEYOR
    // =========================================================

    public void CreateConveyor(
        Vector2Int coordinates,
        ConveyorDirection direction)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        Conveyor conveyor = Instantiate(
            conveyorPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        conveyor.SetDirection(direction);
        conveyor.Place(cell);
    }

    // =========================================================
    // PRESSURE PLATE SPAWN
    // =========================================================

    public void CreatePressurePlate(
        Vector2Int coordinates,
        int doorID)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        PressurePlate pressurePlate = Instantiate(
            pressurePlatePrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        pressurePlate.SetTargetDoorID(doorID);
        pressurePlate.Place(cell);
    }

    // =========================================================
    // BUFFER PAD
    // =========================================================

    public void CreateBufferPad(Vector2Int coordinates)
    {
        Cell cell = GetCell(coordinates);

        if (cell == null)
            return;

        BufferPad bufferPad = Instantiate(
            bufferPadPrefab,
            cell.transform.position,
            Quaternion.identity,
            transform
        );

        bufferPad.Place(cell);

        if (BufferManager.Instance != null)
        {
            BufferManager.Instance.RegisterPad(bufferPad);
        }
    }
}
