using UnityEngine;
using UnityEngine.Rendering;

public class BoxDoorVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform doorTransitRoot;
    [SerializeField] private SortingGroup transitSortingGroup;

    [Header("Inside Door")]
    [SerializeField] private float insideScale = 0.60f;
    [SerializeField] private float insideYOffset = 0f;

    [Header("Door Sorting")]
    [SerializeField] private string transitSortingLayer = "Default";
    [SerializeField] private int transitSortingOrder = 2;

    [Header("Transit Threshold")]
    [Range(0f, 1f)]
    [SerializeField] private float enterInteriorThreshold = 0.55f;

    private Vector3 normalLocalPosition;
    private Vector3 normalLocalScale;

    private bool interiorModeActive;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (doorTransitRoot == null)
        {
            Transform found =
                transform.Find("DoorTransitRoot");

            if (found != null)
            {
                doorTransitRoot = found;
            }
        }

        if (doorTransitRoot == null)
        {
            Debug.LogError(
                "BoxDoorVisual: DoorTransitRoot bulunamadı!",
                gameObject
            );

            enabled = false;
            return;
        }

        if (transitSortingGroup == null)
        {
            transitSortingGroup =
                doorTransitRoot.GetComponent<SortingGroup>();
        }

        if (transitSortingGroup == null)
        {
            transitSortingGroup =
                doorTransitRoot.gameObject
                    .AddComponent<SortingGroup>();
        }

        normalLocalPosition =
            doorTransitRoot.localPosition;

        normalLocalScale =
            doorTransitRoot.localScale;

        // Normal gameplay sırasında SortingGroup devre dışı.
        // Pressure Plate / Goal gibi sprite'ların sortingini bozmaz.
        transitSortingGroup.enabled = false;

        // Önceki denemelerden mask açık kaldıysa temizle.
        ClearSpriteMasks();
    }

    // =========================================================
    // DOOR TRANSIT
    // =========================================================

    public void SetDoorTransit01(float amount)
    {
        if (doorTransitRoot == null)
            return;

        amount =
            Mathf.Clamp01(amount);

        float smoothAmount =
            Mathf.SmoothStep(
                0f,
                1f,
                amount
            );

        // -----------------------------------------------------
        // SCALE
        // -----------------------------------------------------

        Vector3 insideTargetScale =
            normalLocalScale *
            insideScale;

        doorTransitRoot.localScale =
            Vector3.Lerp(
                normalLocalScale,
                insideTargetScale,
                smoothAmount
            );

        // -----------------------------------------------------
        // POSITION
        // -----------------------------------------------------

        Vector3 insidePosition =
            normalLocalPosition;

        insidePosition.y +=
            insideYOffset;

        doorTransitRoot.localPosition =
            Vector3.Lerp(
                normalLocalPosition,
                insidePosition,
                smoothAmount
            );

        // -----------------------------------------------------
        // SORTING
        // -----------------------------------------------------

        if (amount >= enterInteriorThreshold)
        {
            EnterDoorInterior();
        }
        else
        {
            ExitDoorInterior();
        }
    }

    // =========================================================
    // ENTER DOOR
    // =========================================================

    private void EnterDoorInterior()
    {
        if (interiorModeActive)
            return;

        if (transitSortingGroup != null)
        {
            transitSortingGroup.enabled = true;

            transitSortingGroup.sortingLayerName =
                transitSortingLayer;

            transitSortingGroup.sortingOrder =
                transitSortingOrder;
        }

        interiorModeActive = true;
    }

    // =========================================================
    // EXIT DOOR
    // =========================================================

    private void ExitDoorInterior()
    {
        if (!interiorModeActive)
            return;

        if (transitSortingGroup != null)
        {
            transitSortingGroup.enabled = false;
        }

        interiorModeActive = false;
    }

    // =========================================================
    // MASK CLEANUP
    // =========================================================

    private void ClearSpriteMasks()
    {
        SpriteRenderer[] renderers =
            doorTransitRoot
                .GetComponentsInChildren<SpriteRenderer>(
                    true
                );

        foreach (SpriteRenderer renderer in renderers)
        {
            renderer.maskInteraction =
                SpriteMaskInteraction.None;
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetDoorTransit()
    {
        if (doorTransitRoot != null)
        {
            doorTransitRoot.localPosition =
                normalLocalPosition;

            doorTransitRoot.localScale =
                normalLocalScale;
        }

        ExitDoorInterior();

        if (transitSortingGroup != null)
        {
            transitSortingGroup.enabled = false;
        }

        ClearSpriteMasks();
    }

    // =========================================================
    // SAFETY
    // =========================================================

    private void OnDisable()
    {
        ResetDoorTransit();
    }
}