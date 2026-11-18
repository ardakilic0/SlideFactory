
using UnityEngine;

public class Cell : MonoBehaviour
{
    public Vector2Int Coordinates { get; private set; }

    public Box Box { get; private set; }
    public Wall Wall { get; private set; }
    public Goal Goal { get; private set; }

    public Door Door { get; private set; }
    public FloorSwitch FloorSwitch { get; private set; }
    public PressurePlate PressurePlate { get; private set; }

    public Portal Portal { get; private set; }
    public Conveyor Conveyor { get; private set; }
    public BufferPad BufferPad { get; private set; }

    public bool IsOccupied => Box != null;
    public bool HasWall => Wall != null;
    public bool HasGoal => Goal != null;
    public bool HasDoor => Door != null;
    public bool HasSwitch => FloorSwitch != null;
    public bool HasPressurePlate => PressurePlate != null;
    public bool HasPortal => Portal != null;
    public bool HasConveyor => Conveyor != null;
    public bool HasBufferPad => BufferPad != null;

    public bool IsBlocked
    {
        get
        {
            if (HasWall) return true;
            // Door gameplay state changes immediately, regardless of animation.
            if (HasDoor && !Door.IsOpen) return true;
            return false;
        }
    }

    public void Initialize(Vector2Int coordinates)
    {
        Coordinates = coordinates;
        Box = null;
        Wall = null;
        Goal = null;
        Door = null;
        FloorSwitch = null;
        PressurePlate = null;
        Portal = null;
        Conveyor = null;
        BufferPad = null;
    }

    public void SetBox(Box box)
    {
        Box = box;
        // Trigger pressure plate state immediately as grid occupancy changes.
        if (PressurePlate != null)
            PressurePlate.SetPressed(Box != null);

        // If a door logically closed while occupied, finish closing only
        // after the occupant moves out. Do not trap or clip through a box.
        if (Door != null)
            Door.OnCellOccupancyChanged();
    }

    public void SetWall(Wall wall) { Wall = wall; }
    public void SetGoal(Goal goal) { Goal = goal; }
    public void SetDoor(Door door) { Door = door; }
    public void SetFloorSwitch(FloorSwitch floorSwitch) { FloorSwitch = floorSwitch; }
    public void SetPortal(Portal portal) { Portal = portal; }
    public void SetConveyor(Conveyor conveyor) { Conveyor = conveyor; }
    public void SetBufferPad(BufferPad bufferPad) { BufferPad = bufferPad; }

    public void SetPressurePlate(PressurePlate pressurePlate)
    {
        PressurePlate = pressurePlate;
        if (PressurePlate != null)
            PressurePlate.SetPressed(Box != null);
    }
}
