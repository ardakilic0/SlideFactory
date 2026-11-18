using UnityEngine;

public class GridCameraController : MonoBehaviour
{
    public static GridCameraController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Board Fit")]
    [Range(0.5f, 1f)]
    [SerializeField] private float screenWidthUsage = 0.88f;

    [Range(0.5f, 1f)]
    [SerializeField] private float screenHeightUsage = 0.75f;

    [SerializeField] private float cellSize = 1.1f;

    [Header("Position")]
    [SerializeField] private float cameraYOffset = 0f;

    private void Awake()
    {
        Instance = this;

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    public void FitToGrid(int width, int height)
    {
        if (targetCamera == null)
        {
            Debug.LogError("GridCameraController: Camera bulunamadı!");
            return;
        }

        if (!targetCamera.orthographic)
        {
            Debug.LogError(
                "GridCameraController: Camera Orthographic olmalı!"
            );
            return;
        }

        float gridWorldWidth = width * cellSize;
        float gridWorldHeight = height * cellSize;

        float aspect = targetCamera.aspect;

        float sizeForHeight =
            gridWorldHeight /
            (2f * screenHeightUsage);

        float sizeForWidth =
            gridWorldWidth /
            (2f * aspect * screenWidthUsage);

        targetCamera.orthographicSize =
            Mathf.Max(sizeForHeight, sizeForWidth);

        Vector3 cameraPosition =
            targetCamera.transform.position;

        cameraPosition.x = 0f;
        cameraPosition.y = cameraYOffset;

        targetCamera.transform.position =
            cameraPosition;
    }
}