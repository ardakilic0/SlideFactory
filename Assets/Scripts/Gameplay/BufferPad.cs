using UnityEngine;

public class BufferPad : MonoBehaviour
{
    [Header("Visual References")]
    [SerializeField] private SpriteRenderer staticFrameRenderer;
    [SerializeField] private Transform movingShutter;

    [Header("Shutter")]
    [SerializeField] private float shutterOpenYOffset = 0.48f;

    private Vector3 shutterClosedLocalPosition;

    private Box trackedBox;

    public Cell CurrentCell { get; private set; }

    private void Awake()
    {
        if (movingShutter != null)
        {
            shutterClosedLocalPosition =
                movingShutter.localPosition;
        }
    }

    // =========================================================
    // PLACE
    // =========================================================

    public void Place(Cell cell)
    {
        CurrentCell = cell;

        transform.position =
            cell.transform.position;

        cell.SetBufferPad(this);
    }

    // =========================================================
    // STATIC FRAME BOUNDS
    // =========================================================

    public Bounds GetStaticFrameWorldBounds()
    {
        if (staticFrameRenderer != null)
        {
            return staticFrameRenderer.bounds;
        }

        return new Bounds(
            transform.position,
            Vector3.one
        );
    }

    // =========================================================
    // SHUTTER
    // =========================================================

    public void SetShutterOpen01(float open01)
    {
        if (movingShutter == null)
            return;

        open01 =
            Mathf.Clamp01(open01);

        Vector3 openPosition =
            shutterClosedLocalPosition +
            Vector3.up *
            shutterOpenYOffset;

        movingShutter.localPosition =
            Vector3.Lerp(
                shutterClosedLocalPosition,
                openPosition,
                open01
            );
    }

    public void OpenShutterInstant()
    {
        SetShutterOpen01(1f);
    }

    public void CloseShutterInstant()
    {
        SetShutterOpen01(0f);
    }

    // =========================================================
    // TRACK BOX
    // =========================================================

    public void TrackBox(Box box)
    {
        trackedBox = box;

        if (trackedBox == null)
            return;

        trackedBox.SetBufferVisual01(1f);

        OpenShutterInstant();
    }

    public void ReleaseBox(Box box)
    {
        if (trackedBox == box)
        {
            trackedBox = null;
        }

        CloseShutterInstant();
    }

    // Eski BufferManager sürümleri ile
    // uyumluluk için bırakıyoruz.
    public void SetExternalControl(bool value)
    {
    }
}