using UnityEngine;

public class Goal : MonoBehaviour
{
    [SerializeField] private int goalID = 0;

    private MatchMarker matchMarker;

    public int GoalID => goalID;

    public Cell CurrentCell { get; private set; }

    private void Awake()
    {
        matchMarker =
            GetComponentInChildren<MatchMarker>(true);

        if (matchMarker != null)
        {
            matchMarker.SetID(goalID);
        }
    }

    public void Place(Cell cell)
    {
        CurrentCell = cell;

        transform.position =
            cell.transform.position;
    }

    public bool CanAccept(Box box)
    {
        return box.BoxID == goalID;
    }

    public void SetGoalID(int id)
    {
        goalID = id;

        if (matchMarker != null)
        {
            matchMarker.SetID(goalID);
        }
    }
}