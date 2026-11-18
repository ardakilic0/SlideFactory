using UnityEngine;

public class Wall : MonoBehaviour
{
    public Cell CurrentCell { get; private set; }

    public void Place(Cell cell)
    {
        CurrentCell = cell;

        transform.position = cell.transform.position;
    }
}