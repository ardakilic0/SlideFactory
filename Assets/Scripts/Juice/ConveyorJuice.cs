using UnityEngine;

public class ConveyorJuice : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("3 oku birlikte içeren tek PNG objesi.")]
    [SerializeField] private Transform arrowStrip;

    [Header("Flow")]
    [SerializeField] private float speed = 0.35f;

    [Tooltip(
        "İki aynı 3-ok strip arasındaki tekrar mesafesi. " +
        "Son ok ile sonraki strip'in ilk oku arasındaki mesafeyi belirler."
    )]
    [SerializeField] private float loopDistance = 0.48f;

    private Transform stripA;
    private Transform stripB;

    private Vector3 centerLocalPosition;

    private void Awake()
    {
        if (arrowStrip == null)
        {
            Debug.LogWarning(
                "ConveyorJuice: Arrow Strip atanmadı.",
                this
            );

            enabled = false;
            return;
        }

        stripA = arrowStrip;

        centerLocalPosition =
            stripA.localPosition;

        GameObject copy =
            Instantiate(
                stripA.gameObject,
                stripA.parent
            );

        copy.name =
            "ArrowStrip_Loop";

        stripB =
            copy.transform;
    }

    private void Update()
    {
        if (stripA == null ||
            stripB == null)
        {
            return;
        }

        /*
         * Tek bir ortak offset kullanıyoruz.
         *
         * Böylece iki strip birbirinden asla
         * bağımsız hareket etmiyor.
         */
        float offset =
            Mathf.Repeat(
                Time.time * speed,
                loopDistance
            );

        /*
         * Her ikisi de SADECE local UP yönünde hareket ediyor.
         *
         * Conveyor.cs zaten Visual parent'ı döndürüyor.
         */
        stripA.localPosition =
            centerLocalPosition +
            Vector3.up *
            offset;

        stripB.localPosition =
            centerLocalPosition +
            Vector3.up *
            (
                offset -
                loopDistance
            );
    }
}