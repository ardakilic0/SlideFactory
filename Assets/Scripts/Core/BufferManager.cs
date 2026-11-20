using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BufferManager : MonoBehaviour
{
    public static BufferManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotTexts;

    [Header("UI Dock References")]
    [SerializeField] private RectTransform[] slotStaticFrames;

    [Header("UI Box Visuals")]
    [SerializeField] private RectTransform[] slotBoxVisualRoots;
    [SerializeField] private Image[] slotBoxBodies;

    // Bu artık yeni BadgeBase Image'larını temsil ediyor.
    [SerializeField] private Image[] slotBoxBadges;

    // Yeni Gloss katmanı.
    [SerializeField] private Image[] slotBoxBadgeGlosses;

    [SerializeField] private TMP_Text[] slotBoxIDTexts;

    [Header("UI Shutters")]
    [SerializeField] private RectTransform[] slotShutterTransforms;
    [SerializeField] private float uiShutterOpenYOffset = 90f;

    private Box[] storedBoxes;
    private Vector2[] shutterClosedPositions;

    private BufferPad currentPad;
    private bool isTransitioning;

    // Read-only runtime state used by DeadlockDetector and LevelManager.
    public bool IsTransitioning => isTransitioning;

    public Cell CurrentPadCell =>
        currentPad != null ? currentPad.CurrentCell : null;

    public int SlotCapacity =>
        storedBoxes != null ? storedBoxes.Length : 0;

    public int StoredBoxCount
    {
        get
        {
            if (storedBoxes == null)
                return 0;

            int count = 0;

            for (int i = 0; i < storedBoxes.Length; i++)
            {
                if (storedBoxes[i] != null)
                    count++;
            }

            return count;
        }
    }

    public bool IsBoxStored(Box box)
    {
        if (box == null || storedBoxes == null)
            return false;

        for (int i = 0; i < storedBoxes.Length; i++)
        {
            if (storedBoxes[i] == box)
                return true;
        }

        return false;
    }

    public bool CanStoreBox(Box box)
    {
        if (isTransitioning ||
            box == null ||
            !box.CanUseBuffer ||
            storedBoxes == null ||
            currentPad == null ||
            currentPad.CurrentCell == null)
        {
            return false;
        }

        return
            box.CurrentCell == currentPad.CurrentCell &&
            FindEmptySlot() >= 0;
    }

    public bool CanReturnAnyStoredBox
    {
        get
        {
            if (isTransitioning ||
                StoredBoxCount <= 0 ||
                currentPad == null ||
                currentPad.CurrentCell == null)
            {
                return false;
            }

            Cell returnCell = currentPad.CurrentCell;

            return
                !returnCell.IsOccupied &&
                !returnCell.IsBlocked;
        }
    }

    public bool CanReturnSlot(int slotIndex)
    {
        if (storedBoxes == null ||
            !IsValidSlot(slotIndex) ||
            storedBoxes[slotIndex] == null)
        {
            return false;
        }

        return CanReturnAnyStoredBox;
    }

    public Box GetStoredBox(int slotIndex)
    {
        if (storedBoxes == null || !IsValidSlot(slotIndex))
            return null;

        return storedBoxes[slotIndex];
    }

    public int GetSlotOfBox(Box box)
    {
        if (box == null || storedBoxes == null)
            return -1;

        for (int i = 0; i < storedBoxes.Length; i++)
        {
            if (storedBoxes[i] == box)
                return i;
        }

        return -1;
    }

    public RectTransform GetSlotRect(int slotIndex)
    {
        if (slotButtons == null ||
            slotIndex < 0 ||
            slotIndex >= slotButtons.Length ||
            slotButtons[slotIndex] == null)
        {
            return null;
        }

        return slotButtons[slotIndex].transform as RectTransform;
    }

    // Undo: puts a Box straight into a UI slot, without animation.
    // The Box must currently stand on the active pad cell.
    public void RestoreStoredBox(int slotIndex, Box box)
    {
        if (box == null ||
            currentPad == null ||
            storedBoxes == null ||
            !IsValidSlot(slotIndex))
        {
            return;
        }

        box.SetBufferVisual01(1f);

        ApplyWorldMatchedUILayout(
            slotIndex,
            box
        );

        storedBoxes[slotIndex] = box;

        box.EnterBufferSilently();

        currentPad.CloseShutterInstant();
        currentPad.ReleaseBox(box);

        SetUIShutterOpen01(slotIndex, 1f);
        UpdateUI();
    }

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        Instance = this;

        int slotCount =
            slotButtons != null
                ? slotButtons.Length
                : 0;

        storedBoxes =
            new Box[slotCount];

        int shutterCount =
            slotShutterTransforms != null
                ? slotShutterTransforms.Length
                : 0;

        shutterClosedPositions =
            new Vector2[shutterCount];

        for (int i = 0; i < shutterCount; i++)
        {
            if (slotShutterTransforms[i] == null)
                continue;

            shutterClosedPositions[i] =
                slotShutterTransforms[i]
                    .anchoredPosition;
        }

        for (int i = 0; i < slotCount; i++)
        {
            if (slotButtons[i] == null)
                continue;

            int slotIndex = i;

            slotButtons[i]
                .onClick
                .AddListener(
                    () => GameActions.TryReturnFromBuffer(slotIndex)
                );
        }

        CloseAllUIShuttersInstant();
        UpdateUI();

        if (contentRoot != null)
        {
            contentRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetForLevel()
    {
        StopAllCoroutines();

        isTransitioning = false;

        if (currentPad != null)
        {
            currentPad.CloseShutterInstant();
        }

        currentPad = null;

        if (storedBoxes != null)
        {
            for (int i = 0; i < storedBoxes.Length; i++)
            {
                storedBoxes[i] = null;
            }
        }

        CloseAllUIShuttersInstant();
        UpdateUI();

        if (contentRoot != null)
        {
            contentRoot.SetActive(false);
        }
    }

    // =========================================================
    // REGISTER PAD
    // =========================================================

    public void RegisterPad(BufferPad bufferPad)
    {
        currentPad = bufferPad;

        if (currentPad != null)
        {
            currentPad.CloseShutterInstant();
        }

        if (contentRoot != null)
        {
            contentRoot.SetActive(true);
        }
    }

    // =========================================================
    // GRID -> UI
    // =========================================================

    public void TryStoreBox(Box box)
    {
        if (isTransitioning)
            return;

        if (box == null)
            return;

        if (!box.CanUseBuffer)
            return;

        if (currentPad == null)
            return;

        if (box.CurrentCell != currentPad.CurrentCell)
            return;

        int emptySlot =
            FindEmptySlot();

        if (emptySlot == -1)
        {
            Debug.Log("BUFFER FULL");
            return;
        }

        StartCoroutine(
            StoreToUISlotRoutine(
                emptySlot,
                box
            )
        );
    }

    private IEnumerator StoreToUISlotRoutine(
        int slotIndex,
        Box box)
    {
        isTransitioning = true;

        float duration =
            Mathf.Max(
                0.01f,
                box.MoveDuration
            );

        // Grid Buffer'daki final küçüklüğü/konumu garanti edilir.
        box.SetBufferVisual01(1f);

        currentPad.OpenShutterInstant();

        // Box henüz world'de aktifken gerçek boyutlarını ölç.
        ApplyWorldMatchedUILayout(
            slotIndex,
            box
        );

        // UI shutter açılır.
        yield return AnimateUIShutter(
            slotIndex,
            0f,
            1f,
            duration
        );

        storedBoxes[slotIndex] = box;

        // World Box gizlenir.
        box.EnterBuffer();

        // UI Box görünür.
        UpdateUI();

        // Grid shutter kapanır.
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            currentPad.SetShutterOpen01(
                1f - t
            );

            yield return null;
        }

        currentPad.CloseShutterInstant();
        currentPad.ReleaseBox(box);

        isTransitioning = false;

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.NotifyBoardStateChanged();
        }
    }

    // =========================================================
    // UI -> GRID
    // =========================================================

    public void TryReturnBox(int slotIndex)
    {
        if (isTransitioning)
            return;

        if (!IsValidSlot(slotIndex))
            return;

        Box box =
            storedBoxes[slotIndex];

        if (box == null)
            return;

        if (currentPad == null)
            return;

        Cell returnCell =
            currentPad.CurrentCell;

        if (returnCell == null)
            return;

        if (returnCell.IsOccupied)
        {
            Debug.Log(
                "Grid Buffer occupied!"
            );

            return;
        }

        if (returnCell.IsBlocked)
            return;

        StartCoroutine(
            ReturnToGridRoutine(
                slotIndex,
                box,
                returnCell
            )
        );
    }

    private IEnumerator ReturnToGridRoutine(
        int slotIndex,
        Box box,
        Cell returnCell)
    {
        isTransitioning = true;

        float duration =
            Mathf.Max(
                0.01f,
                box.MoveDuration
            );

        // Grid shutter açılır.
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            currentPad.SetShutterOpen01(t);

            yield return null;
        }

        currentPad.OpenShutterInstant();

        // UI slot boşaltılır.
        storedBoxes[slotIndex] = null;

        // World Box tekrar grid Buffer'a gelir.
        box.ReturnToGridBuffer(
            returnCell
        );

        currentPad.TrackBox(box);

        UpdateUI();

        // UI shutter kapanır.
        yield return AnimateUIShutter(
            slotIndex,
            1f,
            0f,
            duration
        );

        isTransitioning = false;

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.NotifyBoardStateChanged();
        }
    }

    // =========================================================
    // WORLD -> UI VISUAL MATCH
    // =========================================================

    private void ApplyWorldMatchedUILayout(
        int slotIndex,
        Box box)
    {
        if (box == null ||
            currentPad == null)
        {
            return;
        }

        if (slotStaticFrames == null ||
            slotBoxVisualRoots == null)
        {
            return;
        }

        if (slotIndex < 0 ||
            slotIndex >= slotStaticFrames.Length ||
            slotIndex >= slotBoxVisualRoots.Length)
        {
            return;
        }

        RectTransform uiFrame =
            slotStaticFrames[slotIndex];

        RectTransform uiBoxRoot =
            slotBoxVisualRoots[slotIndex];

        if (uiFrame == null ||
            uiBoxRoot == null)
        {
            return;
        }

        RectTransform parent =
            uiBoxRoot.parent
                as RectTransform;

        if (parent == null)
            return;

        // ---------------------------------------------------------
        // GRID/WORLD'DEKİ GERÇEK BOUNDS
        // ---------------------------------------------------------

        Bounds dockBounds =
            currentPad
                .GetStaticFrameWorldBounds();

        Bounds bodyBounds =
            box.GetBufferBodyWorldBounds();

        Bounds badgeBounds =
            box.GetBufferBadgeWorldBounds();

        if (dockBounds.size.x <= 0f ||
            dockBounds.size.y <= 0f ||
            bodyBounds.size.x <= 0f ||
            bodyBounds.size.y <= 0f)
        {
            return;
        }

        // ---------------------------------------------------------
        // UI DOCK
        // ---------------------------------------------------------

        Vector2 uiDockCenter;
        Vector2 uiDockSize;

        if (!TryGetRectInParentSpace(
                uiFrame,
                parent,
                out uiDockCenter,
                out uiDockSize))
        {
            return;
        }

        // ---------------------------------------------------------
        // BOX BODY
        // ---------------------------------------------------------

        float bodyWidthRatio =
            bodyBounds.size.x /
            dockBounds.size.x;

        float bodyHeightRatio =
            bodyBounds.size.y /
            dockBounds.size.y;

        float bodyOffsetXRatio =
            (bodyBounds.center.x -
             dockBounds.center.x) /
            dockBounds.size.x;

        float bodyOffsetYRatio =
            (bodyBounds.center.y -
             dockBounds.center.y) /
            dockBounds.size.y;

        Vector2 uiBodySize =
            new Vector2(
                uiDockSize.x *
                bodyWidthRatio,

                uiDockSize.y *
                bodyHeightRatio
            );

        Vector2 uiBodyCenter =
            uiDockCenter +
            new Vector2(
                uiDockSize.x *
                bodyOffsetXRatio,

                uiDockSize.y *
                bodyOffsetYRatio
            );

        uiBoxRoot.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        uiBoxRoot.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        uiBoxRoot.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        // Önemli:
        // Burada artık ayrıca 0.62 vermiyoruz.
        // Çünkü world'deki gerçek küçülmüş Body bounds'u zaten ölçüldü.
        uiBoxRoot.localScale =
            Vector3.one;

        uiBoxRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            uiBodySize.x
        );

        uiBoxRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            uiBodySize.y
        );

        uiBoxRoot.localPosition =
            new Vector3(
                uiBodyCenter.x,
                uiBodyCenter.y,
                uiBoxRoot.localPosition.z
            );

        // ---------------------------------------------------------
        // BODY IMAGE
        // ---------------------------------------------------------

        if (slotBoxBodies != null &&
            slotIndex < slotBoxBodies.Length &&
            slotBoxBodies[slotIndex] != null)
        {
            RectTransform bodyRect =
                slotBoxBodies[
                    slotIndex
                ].rectTransform;

            SetFullStretch(
                bodyRect
            );
        }

        // ---------------------------------------------------------
        // BADGE BASE
        // ---------------------------------------------------------

        if (slotBoxBadges != null &&
            slotIndex < slotBoxBadges.Length &&
            slotBoxBadges[slotIndex] != null &&
            badgeBounds.size.x > 0f &&
            badgeBounds.size.y > 0f)
        {
            float badgeWidthRatio =
                badgeBounds.size.x /
                bodyBounds.size.x;

            float badgeHeightRatio =
                badgeBounds.size.y /
                bodyBounds.size.y;

            float badgeOffsetXRatio =
                (badgeBounds.center.x -
                 bodyBounds.center.x) /
                bodyBounds.size.x;

            float badgeOffsetYRatio =
                (badgeBounds.center.y -
                 bodyBounds.center.y) /
                bodyBounds.size.y;

            Vector2 badgeSize =
                new Vector2(
                    uiBodySize.x *
                    badgeWidthRatio,

                    uiBodySize.y *
                    badgeHeightRatio
                );

            Vector2 badgePosition =
                new Vector2(
                    uiBodySize.x *
                    badgeOffsetXRatio,

                    uiBodySize.y *
                    badgeOffsetYRatio
                );

            RectTransform badgeBaseRect =
                slotBoxBadges[
                    slotIndex
                ].rectTransform;

            SetCenteredRect(
                badgeBaseRect,
                badgeSize,
                badgePosition
            );

            // -----------------------------------------------------
            // BADGE GLOSS
            // Base ile BİREBİR aynı konum ve boyut.
            // -----------------------------------------------------

            if (slotBoxBadgeGlosses != null &&
                slotIndex < slotBoxBadgeGlosses.Length &&
                slotBoxBadgeGlosses[slotIndex] != null)
            {
                RectTransform glossRect =
                    slotBoxBadgeGlosses[
                        slotIndex
                    ].rectTransform;

                SetCenteredRect(
                    glossRect,
                    badgeSize,
                    badgePosition
                );
            }

            // -----------------------------------------------------
            // ID TEXT
            // -----------------------------------------------------

            if (slotBoxIDTexts != null &&
                slotIndex < slotBoxIDTexts.Length &&
                slotBoxIDTexts[slotIndex] != null)
            {
                TMP_Text idText =
                    slotBoxIDTexts[
                        slotIndex
                    ];

                RectTransform textRect =
                    idText.rectTransform;

                // Daha önce görsel olarak oturttuğumuz değer.
                Vector2 textSize =
                    badgeSize * 0.80f;

                SetCenteredRect(
                    textRect,
                    textSize,
                    badgePosition
                );

                idText.enableAutoSizing =
                    true;

                idText.fontSizeMin =
                    4f;

                idText.fontSizeMax =
                    Mathf.Max(
                        8f,
                        textSize.y
                    );

                idText.alignment =
                    TextAlignmentOptions.Center;

                idText.overflowMode =
                    TextOverflowModes.Overflow;
            }
        }
    }

    // =========================================================
    // UPDATE UI
    // =========================================================

    private void UpdateUI()
    {
        if (storedBoxes == null)
            return;

        for (int i = 0;
             i < storedBoxes.Length;
             i++)
        {
            Box box =
                storedBoxes[i];

            bool hasBox =
                box != null;

            // -----------------------------------------------------
            // ROOT
            // -----------------------------------------------------

            if (slotBoxVisualRoots != null &&
                i < slotBoxVisualRoots.Length &&
                slotBoxVisualRoots[i] != null)
            {
                slotBoxVisualRoots[i]
                    .gameObject
                    .SetActive(hasBox);
            }

            // -----------------------------------------------------
            // BODY
            // -----------------------------------------------------

            if (slotBoxBodies != null &&
                i < slotBoxBodies.Length &&
                slotBoxBodies[i] != null)
            {
                Image bodyImage =
                    slotBoxBodies[i];

                if (!hasBox)
                {
                    bodyImage.enabled = false;
                }
                else
                {
                    bodyImage.sprite =
                        box.BufferSprite;

                    bodyImage.color =
                        box.BufferSpriteColor;

                    bodyImage.preserveAspect =
                        true;

                    bodyImage.raycastTarget =
                        false;

                    bodyImage.enabled =
                        true;
                }
            }

            // -----------------------------------------------------
            // BADGE BASE
            // -----------------------------------------------------

            if (slotBoxBadges != null &&
                i < slotBoxBadges.Length &&
                slotBoxBadges[i] != null)
            {
                Image badgeBase =
                    slotBoxBadges[i];

                if (!hasBox)
                {
                    badgeBase.enabled =
                        false;
                }
                else
                {
                    // Grid Box'taki gerçek yeni
                    // Badge_Base_Neutral sprite'ı.
                    badgeBase.sprite =
                        box.BufferBadgeSprite;

                    // Gridde kullanılan A/B renginin
                    // AYNISI UI'a aktarılıyor.
                    badgeBase.color =
                        box.BufferBadgeColor;

                    badgeBase.preserveAspect =
                        true;

                    badgeBase.raycastTarget =
                        false;

                    badgeBase.enabled =
                        true;
                }
            }

            // -----------------------------------------------------
            // BADGE GLOSS
            // -----------------------------------------------------

            if (slotBoxBadgeGlosses != null &&
                i < slotBoxBadgeGlosses.Length &&
                slotBoxBadgeGlosses[i] != null)
            {
                Image badgeGloss =
                    slotBoxBadgeGlosses[i];

                if (!hasBox)
                {
                    badgeGloss.enabled =
                        false;
                }
                else
                {
                    // Gloss ASLA A/B rengi almıyor.
                    // Figma PNG'sindeki kendi alpha/highlight'ı korunuyor.
                    badgeGloss.color =
                        Color.white;

                    badgeGloss.preserveAspect =
                        true;

                    badgeGloss.raycastTarget =
                        false;

                    badgeGloss.enabled =
                        true;
                }
            }

            // -----------------------------------------------------
            // ID TEXT
            // -----------------------------------------------------

            if (slotBoxIDTexts != null &&
                i < slotBoxIDTexts.Length &&
                slotBoxIDTexts[i] != null)
            {
                TMP_Text idText =
                    slotBoxIDTexts[i];

                if (!hasBox)
                {
                    idText.enabled =
                        false;
                }
                else
                {
                    idText.text =
                        box.BufferMarkerLabel;

                    // Box'ta hangi font varsa
                    // UI Buffer da aynısını kullanır.
                    if (box.BufferMarkerFont != null)
                    {
                        idText.font =
                            box.BufferMarkerFont;
                    }

                    idText.color =
                        box.BufferMarkerColor;

                    idText.fontStyle =
                        box.BufferMarkerFontStyle;

                    idText.alignment =
                        TextAlignmentOptions.Center;

                    idText.raycastTarget =
                        false;

                    idText.enabled =
                        true;
                }
            }

            // -----------------------------------------------------
            // SLOT LABEL
            // -----------------------------------------------------

            if (slotTexts != null &&
                i < slotTexts.Length &&
                slotTexts[i] != null)
            {
                slotTexts[i].text =
                    hasBox
                        ? GetBoxLabel(box)
                        : "EMPTY";
            }
        }
    }

    // =========================================================
    // SHUTTER
    // =========================================================

    private IEnumerator AnimateUIShutter(
        int slotIndex,
        float from,
        float to,
        float duration)
    {
        duration =
            Mathf.Max(
                0.01f,
                duration
            );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float value =
                Mathf.Lerp(
                    from,
                    to,
                    t
                );

            SetUIShutterOpen01(
                slotIndex,
                value
            );

            yield return null;
        }

        SetUIShutterOpen01(
            slotIndex,
            to
        );
    }

    private void SetUIShutterOpen01(
        int slotIndex,
        float open01)
    {
        if (slotShutterTransforms == null)
            return;

        if (slotIndex < 0 ||
            slotIndex >= slotShutterTransforms.Length)
        {
            return;
        }

        if (slotIndex >= shutterClosedPositions.Length)
            return;

        RectTransform shutter =
            slotShutterTransforms[
                slotIndex
            ];

        if (shutter == null)
            return;

        Vector2 closedPosition =
            shutterClosedPositions[
                slotIndex
            ];

        Vector2 openPosition =
            closedPosition +
            Vector2.up *
            uiShutterOpenYOffset;

        shutter.anchoredPosition =
            Vector2.Lerp(
                closedPosition,
                openPosition,
                Mathf.Clamp01(open01)
            );
    }

    private void CloseAllUIShuttersInstant()
    {
        if (slotShutterTransforms == null)
            return;

        for (int i = 0;
             i < slotShutterTransforms.Length;
             i++)
        {
            if (slotShutterTransforms[i] == null)
                continue;

            if (i >= shutterClosedPositions.Length)
                continue;

            slotShutterTransforms[i]
                .anchoredPosition =
                shutterClosedPositions[i];
        }
    }

    // =========================================================
    // RECT HELPERS
    // =========================================================

    private bool TryGetRectInParentSpace(
        RectTransform source,
        RectTransform targetParent,
        out Vector2 center,
        out Vector2 size)
    {
        center =
            Vector2.zero;

        size =
            Vector2.zero;

        if (source == null ||
            targetParent == null)
        {
            return false;
        }

        Vector3[] corners =
            new Vector3[4];

        source.GetWorldCorners(
            corners
        );

        Vector3 bottomLeft =
            targetParent
                .InverseTransformPoint(
                    corners[0]
                );

        Vector3 topRight =
            targetParent
                .InverseTransformPoint(
                    corners[2]
                );

        center =
            new Vector2(
                (bottomLeft.x +
                 topRight.x) * 0.5f,

                (bottomLeft.y +
                 topRight.y) * 0.5f
            );

        size =
            new Vector2(
                Mathf.Abs(
                    topRight.x -
                    bottomLeft.x
                ),

                Mathf.Abs(
                    topRight.y -
                    bottomLeft.y
                )
            );

        return true;
    }

    private void SetFullStretch(
        RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.anchoredPosition =
            Vector2.zero;

        rect.localScale =
            Vector3.one;
    }

    private void SetCenteredRect(
        RectTransform rect,
        Vector2 size,
        Vector2 position)
    {
        if (rect == null)
            return;

        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        rect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        rect.localScale =
            Vector3.one;

        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            size.x
        );

        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            size.y
        );

        rect.anchoredPosition =
            position;
    }

    // =========================================================
    // SLOT HELPERS
    // =========================================================

    private int FindEmptySlot()
    {
        for (int i = 0;
             i < storedBoxes.Length;
             i++)
        {
            if (storedBoxes[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsValidSlot(
        int slotIndex)
    {
        return
            slotIndex >= 0 &&
            slotIndex < storedBoxes.Length;
    }

    private string GetBoxLabel(
        Box box)
    {
        string typeLabel = "";

        switch (box.Type)
        {
            case BoxType.Normal:
                typeLabel = "N";
                break;

            case BoxType.Ice:
                typeLabel = "I";
                break;

            case BoxType.Heavy:
                typeLabel = "H";
                break;
        }

        return
            typeLabel +
            "-" +
            GetIDLabel(
                box.BoxID
            );
    }

    private string GetIDLabel(
        int id)
    {
        if (id >= 0 &&
            id < 26)
        {
            return
                ((char)('A' + id))
                .ToString();
        }

        return id.ToString();
    }
}