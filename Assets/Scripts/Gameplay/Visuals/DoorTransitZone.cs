using UnityEngine;

public class DoorTransitZone : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Door door;

    [Header("Transit Effect")]
    [SerializeField] private float effectRadius = 0.70f;
    [SerializeField] private float fullEffectRadius = 0.05f;

    private Box trackedBox;
    private BoxDoorVisual trackedVisual;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (door == null)
        {
            door =
                GetComponent<Door>();
        }
    }

    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (door == null ||
            door.CurrentCell == null)
        {
            return;
        }

        Box doorCellBox =
            door.CurrentCell.Box;

        // -----------------------------------------------------
        // YENİ BOX KAPI CELL'İNE GİRİYOR
        // -----------------------------------------------------

        if (doorCellBox != null &&
            doorCellBox != trackedBox)
        {
            if (trackedVisual != null)
            {
                trackedVisual
                    .ResetDoorTransit();
            }

            trackedBox =
                doorCellBox;

            trackedVisual =
                trackedBox
                    .GetComponent<BoxDoorVisual>();
        }

        // -----------------------------------------------------
        // TAKİP EDİLEN BOX YOK
        // -----------------------------------------------------

        if (trackedBox == null)
            return;

        if (trackedVisual == null)
        {
            trackedVisual =
                trackedBox
                    .GetComponent<BoxDoorVisual>();

            if (trackedVisual == null)
            {
                trackedBox = null;
                return;
            }
        }

        // -----------------------------------------------------
        // KAPI MERKEZİNE UZAKLIK
        // -----------------------------------------------------

        float distance =
            Vector2.Distance(
                trackedBox.transform.position,
                transform.position
            );

        // -----------------------------------------------------
        // DISTANCE -> EFFECT 0-1
        // -----------------------------------------------------

        float amount =
            1f -
            Mathf.InverseLerp(
                fullEffectRadius,
                effectRadius,
                distance
            );

        amount =
            Mathf.Clamp01(amount);

        trackedVisual
            .SetDoorTransit01(
                amount
            );

        // -----------------------------------------------------
        // BOX KAPIDAN TAMAMEN ÇIKTI MI?
        // -----------------------------------------------------

        bool stillDoorCellBox =
            door.CurrentCell.Box ==
            trackedBox;

        if (!stillDoorCellBox &&
            distance >= effectRadius)
        {
            trackedVisual
                .ResetDoorTransit();

            trackedBox = null;
            trackedVisual = null;
        }
    }

    // =========================================================
    // SAFETY
    // =========================================================

    private void OnDisable()
    {
        if (trackedVisual != null)
        {
            trackedVisual
                .ResetDoorTransit();
        }

        trackedBox = null;
        trackedVisual = null;
    }
}