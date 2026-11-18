using UnityEngine;

public class Conveyor : MonoBehaviour
{
    [SerializeField] private ConveyorDirection direction;

    [Header("Visual")]
    [SerializeField] private Transform visualTransform;

    public ConveyorDirection Direction => direction;

    public Cell CurrentCell { get; private set; }

    public Vector2Int DirectionVector
    {
        get
        {
            switch (direction)
            {
                case ConveyorDirection.Up:
                    return Vector2Int.up;

                case ConveyorDirection.Down:
                    return Vector2Int.down;

                case ConveyorDirection.Left:
                    return Vector2Int.left;

                case ConveyorDirection.Right:
                    return Vector2Int.right;
            }

            return Vector2Int.zero;
        }
    }

    private void Awake()
    {
        UpdateVisual();
    }

    public void SetDirection(ConveyorDirection newDirection)
    {
        direction = newDirection;

        UpdateVisual();
    }

    public void Place(Cell cell)
    {
        CurrentCell = cell;

        transform.position =
            cell.transform.position;

        cell.SetConveyor(this);
    }

    private void UpdateVisual()
    {
        if (visualTransform == null)
            return;

        float zRotation = 0f;

        switch (direction)
        {
            case ConveyorDirection.Up:
                zRotation = 0f;
                break;

            case ConveyorDirection.Right:
                zRotation = -90f;
                break;

            case ConveyorDirection.Down:
                zRotation = 180f;
                break;

            case ConveyorDirection.Left:
                zRotation = 90f;
                break;
        }

        visualTransform.localRotation =
            Quaternion.Euler(0f, 0f, zRotation);
    }
}