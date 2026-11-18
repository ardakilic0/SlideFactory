using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelCompleteConfetti : MonoBehaviour
{
    [Header("Burst")]
    [SerializeField] private int pieceCount = 84;
    [SerializeField] private float totalLifetime = 4.8f;
    [SerializeField] private float burstPhaseDuration = 0.55f;

    [Header("Two Burst Origins")]
    [SerializeField] private float burstOriginX = 210f;
    [SerializeField] private float burstOriginY = 10f;

    [Header("Spawn Spread")]
    [SerializeField] private float spawnSpreadX = 40f;
    [SerializeField] private float spawnSpreadY = 30f;

    [Header("Initial Burst")]
    [SerializeField] private float horizontalSpeedMin = 300f;
    [SerializeField] private float horizontalSpeedMax = 720f;

    [SerializeField] private float verticalSpeedMin = 260f;
    [SerializeField] private float verticalSpeedMax = 540f;

    [Header("Falling")]
    [SerializeField] private float gravity = 310f;
    [SerializeField] private float terminalFallSpeed = 260f;
    [SerializeField] private float horizontalDrag = 1.15f;
    [SerializeField] private float swayAmount = 32f;
    [SerializeField] private float swaySpeedMin = 1.6f;
    [SerializeField] private float swaySpeedMax = 3.2f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeedMin = 100f;
    [SerializeField] private float rotationSpeedMax = 360f;

    [Header("Size")]
    [SerializeField] private Vector2 sizeMin = new Vector2(12f, 20f);
    [SerializeField] private Vector2 sizeMax = new Vector2(27f, 46f);

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeStart = 0.76f;

    private readonly List<GameObject> activePieces = new();

    private static readonly Color[] confettiColors =
    {
        new Color32(48, 200, 216, 255),   // Cyan
        new Color32(255, 181, 46, 255),   // Gold
        new Color32(255, 255, 255, 255),  // White
        new Color32(255, 136, 52, 255)    // Orange
    };

    public void PlayBurst()
    {
        ClearPieces();

        int leftCount = pieceCount / 2;
        int rightCount = pieceCount - leftCount;

        for (int i = 0; i < leftCount; i++)
            CreatePiece(i, -1);

        for (int i = 0; i < rightCount; i++)
            CreatePiece(leftCount + i, 1);
    }

    private void CreatePiece(int index, int side)
    {
        GameObject piece = new GameObject(
            "Confetti_" + index,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        piece.transform.SetParent(transform, false);

        RectTransform rect = piece.GetComponent<RectTransform>();

        float width = Random.Range(sizeMin.x, sizeMax.x);
        float height = Random.Range(sizeMin.y, sizeMax.y);

        if (Random.value < 0.3f)
        {
            float temp = width;
            width = height;
            height = temp;
        }

        rect.sizeDelta = new Vector2(width, height);

        float originX = burstOriginX * side;

        rect.anchoredPosition = new Vector2(
            originX + Random.Range(-spawnSpreadX, spawnSpreadX),
            burstOriginY + Random.Range(-spawnSpreadY, spawnSpreadY)
        );

        rect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Random.Range(0f, 360f)
        );

        Image image = piece.GetComponent<Image>();

        image.color =
            confettiColors[
                Random.Range(0, confettiColors.Length)
            ];

        image.raycastTarget = false;

        activePieces.Add(piece);

        // Her iki patlama merkezi ağırlıklı olarak dışarı doğru açılır.
        float horizontalSpeed =
            Random.Range(horizontalSpeedMin, horizontalSpeedMax) * side;

        // Tam düzenli görünmemesi için açı varyasyonu.
        horizontalSpeed += Random.Range(-120f, 120f);

        float verticalSpeed =
            Random.Range(verticalSpeedMin, verticalSpeedMax);

        Vector2 velocity = new Vector2(
            horizontalSpeed,
            verticalSpeed
        );

        float rotationDirection =
            Random.value < 0.5f ? -1f : 1f;

        float rotationSpeed =
            rotationDirection *
            Random.Range(rotationSpeedMin, rotationSpeedMax);

        float swaySpeed =
            Random.Range(swaySpeedMin, swaySpeedMax);

        float swayOffset =
            Random.Range(0f, Mathf.PI * 2f);

        StartCoroutine(
            AnimatePiece(
                piece,
                rect,
                image,
                velocity,
                rotationSpeed,
                swaySpeed,
                swayOffset
            )
        );
    }

    private IEnumerator AnimatePiece(
        GameObject piece,
        RectTransform rect,
        Image image,
        Vector2 velocity,
        float rotationSpeed,
        float swaySpeed,
        float swayOffset)
    {
        float elapsed = 0f;
        Color originalColor = image.color;

        while (elapsed < totalLifetime)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            bool burstPhase =
                elapsed < burstPhaseDuration;

            // Yukarı çıkan parçalar zamanla aşağı dönmeye başlar.
            velocity.y -= gravity * dt;

            velocity.y = Mathf.Max(
                velocity.y,
                -terminalFallSpeed
            );

            // İlk patlamadan sonra yatay hız giderek ölür.
            if (!burstPhase)
            {
                velocity.x = Mathf.Lerp(
                    velocity.x,
                    0f,
                    horizontalDrag * dt
                );
            }

            Vector2 movement =
                velocity * dt;

            // Düşerken hafif sağ-sol süzülme.
            if (!burstPhase)
            {
                float sway =
                    Mathf.Sin(
                        elapsed * swaySpeed + swayOffset
                    ) * swayAmount * dt;

                movement.x += sway;
            }

            rect.anchoredPosition += movement;

            // Patlamadan sonra dönme de biraz sakinleşsin.
            float rotationMultiplier =
                burstPhase ? 1f : 0.55f;

            rect.Rotate(
                0f,
                0f,
                rotationSpeed *
                rotationMultiplier *
                dt
            );

            float normalized =
                Mathf.Clamp01(
                    elapsed / totalLifetime
                );

            // Uzun süre tam görünür kalır,
            // en son bölümde yumuşakça kaybolur.
            if (normalized >= fadeStart)
            {
                float fadeT =
                    Mathf.InverseLerp(
                        fadeStart,
                        1f,
                        normalized
                    );

                float alpha =
                    1f - SmoothStep(fadeT);

                Color color = originalColor;
                color.a = alpha;

                image.color = color;
            }

            yield return null;
        }

        activePieces.Remove(piece);

        if (piece != null)
            Destroy(piece);
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ClearPieces()
    {
        foreach (GameObject piece in activePieces)
        {
            if (piece != null)
                Destroy(piece);
        }

        activePieces.Clear();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearPieces();
    }
}