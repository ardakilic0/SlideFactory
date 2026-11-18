using UnityEngine;

public class GameplayBackgroundController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer staticRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;

    [Header("Glow Animation")]
    [SerializeField] private float glowDuration = 16f;

    [Range(0f, 0.05f)]
    [SerializeField] private float glowScaleAmount = 0.012f;

    [Range(0f, 1f)]
    [SerializeField] private float glowMinAlpha = 0.78f;

    [Range(0f, 1f)]
    [SerializeField] private float glowMaxAlpha = 1f;

    private Vector3 glowBaseScale;

    private float lastCameraAspect;
    private float lastOrthographicSize;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Start()
    {
        FitBackgrounds();
        CacheCameraValues();
    }

    private void Update()
    {
        CheckCameraChanges();
        AnimateGlow();
    }

    // =========================================================
    // GLOW
    // =========================================================

    private void AnimateGlow()
    {
        if (glowRenderer == null)
            return;

        float wave =
            (Mathf.Sin(
                Time.unscaledTime *
                Mathf.PI *
                2f /
                glowDuration
            ) + 1f) * 0.5f;

        float scaleMultiplier =
            Mathf.Lerp(
                1f,
                1f + glowScaleAmount,
                wave
            );

        glowRenderer.transform.localScale =
            new Vector3(
                glowBaseScale.x * scaleMultiplier,
                glowBaseScale.y * scaleMultiplier,
                1f
            );

        Color glowColor =
            glowRenderer.color;

        glowColor.a =
            Mathf.Lerp(
                glowMinAlpha,
                glowMaxAlpha,
                wave
            );

        glowRenderer.color =
            glowColor;
    }

    // =========================================================
    // CAMERA CHECK
    // =========================================================

    private void CheckCameraChanges()
    {
        if (targetCamera == null)
            return;

        bool aspectChanged =
            Mathf.Abs(
                targetCamera.aspect -
                lastCameraAspect
            ) > 0.001f;

        bool sizeChanged =
            Mathf.Abs(
                targetCamera.orthographicSize -
                lastOrthographicSize
            ) > 0.001f;

        if (!aspectChanged && !sizeChanged)
            return;

        FitBackgrounds();
        CacheCameraValues();
    }

    private void CacheCameraValues()
    {
        if (targetCamera == null)
            return;

        lastCameraAspect =
            targetCamera.aspect;

        lastOrthographicSize =
            targetCamera.orthographicSize;
    }

    // =========================================================
    // BACKGROUND FIT
    // =========================================================

    private void FitBackgrounds()
    {
        FitRendererToCamera(staticRenderer);
        FitRendererToCamera(glowRenderer);

        if (glowRenderer != null)
        {
            glowBaseScale =
                glowRenderer.transform.localScale;
        }
    }

    private void FitRendererToCamera(
        SpriteRenderer renderer)
    {
        if (renderer == null ||
            renderer.sprite == null ||
            targetCamera == null ||
            !targetCamera.orthographic)
        {
            return;
        }

        float cameraHeight =
            targetCamera.orthographicSize * 2f;

        float cameraWidth =
            cameraHeight *
            targetCamera.aspect;

        Vector2 spriteSize =
            renderer.sprite.bounds.size;

        // X ve Y ayrı ayrı ekrana oturuyor.
        float scaleX =
            cameraWidth /
            spriteSize.x;

        float scaleY =
            cameraHeight /
            spriteSize.y;

        renderer.transform.localScale =
            new Vector3(
                scaleX,
                scaleY,
                1f
            );

        // Kamera 0,0 ise tam 0,0'a oturur.
        renderer.transform.position =
            new Vector3(
                targetCamera.transform.position.x,
                targetCamera.transform.position.y,
                0f
            );
    }
}