using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 撮影画像のプレビュー用ダイアログ。
/// 「保存」か「シェア」を選んで実行する。
/// Resources/Canvas_preview.prefab のルートに付与して使う。
/// </summary>
public class CanvasPreview : MonoBehaviour
{
    [Header("プレビュー画像 (未設定なら子の RawImage を自動取得)")]
    [SerializeField] private RawImage previewImage;

    [Header("保存ボタン (未設定なら btn_save を自動取得)")]
    [SerializeField] private Button saveButton;

    [Header("シェアボタン (未設定なら btn_share を自動取得)")]
    [SerializeField] private Button shareButton;

    [Header("ポラロイド枠 (Frame.png を割り当て。未設定なら枠なし全画面表示)")]
    [SerializeField] private Sprite frameSprite;

    [Header("写真を枠内いっぱいに切り抜く (OFF=写真全体を枠内に収める)")]
    [SerializeField] private bool cropPhotoToFrame = true;

    [Header("カード(枠)の表示領域。画面に対する余白の割合")]
    [SerializeField] private float cardMarginX = 0.04f;
    [SerializeField] private float cardTopAnchor = 0.97f;
    [SerializeField] private float cardBottomAnchor = 0.20f;

    private string filePath;
    private Texture2D previewTexture;
    private bool ownsTexture;
    private string shareText;
    private string shareSubject;

    private bool actionRunning;
    private Action onClosed;

    /// <summary>
    /// プレハブを生成してプレビューを表示する。
    /// </summary>
    /// <param name="filePath">保存・共有する PNG ファイルのパス</param>
    /// <param name="texture">プレビュー表示用テクスチャ</param>
    /// <param name="ownsTexture">true ならダイアログを閉じる時に texture を破棄する</param>
    /// <param name="onClosed">ダイアログが閉じた時に呼ばれるコールバック</param>
    /// <param name="prefab">使用するプレハブ。null なら Resources/Canvas_preview を読み込む</param>
    public static CanvasPreview Show(
        string filePath,
        Texture2D texture,
        string shareText,
        string shareSubject,
        bool ownsTexture = true,
        Action onClosed = null,
        GameObject prefab = null)
    {
        GameObject source = prefab;

        if (source == null)
        {
            source = Resources.Load<GameObject>("Canvas_preview");
        }

        if (source == null)
        {
            Debug.LogError("CanvasPreview: Canvas_preview プレハブが見つかりません。");
            return null;
        }

        GameObject instance = Instantiate(source);

        CanvasPreview preview = instance.GetComponent<CanvasPreview>();

        if (preview == null)
        {
            Debug.LogError("CanvasPreview: プレハブに CanvasPreview コンポーネントがありません。");
            Destroy(instance);
            return null;
        }

        preview.Setup(filePath, texture, shareText, shareSubject, ownsTexture, onClosed);

        return preview;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (previewImage == null)
        {
            previewImage = GetComponentInChildren<RawImage>(true);
        }

        if (saveButton == null)
        {
            saveButton = FindOrAddButton("btn_save");
        }

        if (shareButton == null)
        {
            shareButton = FindOrAddButton("btn_share");
        }
    }

    private Button FindOrAddButton(string targetName)
    {
        Transform target = FindDeep(transform, targetName);

        if (target == null)
        {
            Debug.LogWarning("CanvasPreview: " + targetName + " が見つかりません。");
            return null;
        }

        // プレハブの btn_save / btn_share は Image だけで Button が無いので補う
        Button button = target.GetComponent<Button>();

        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        return button;
    }

    private static Transform FindDeep(Transform parent, string targetName)
    {
        if (parent.name == targetName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), targetName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void Setup(
        string filePath,
        Texture2D texture,
        string shareText,
        string shareSubject,
        bool ownsTexture,
        Action onClosed)
    {
        this.filePath = filePath;
        this.previewTexture = texture;
        this.shareText = shareText;
        this.shareSubject = shareSubject;
        this.ownsTexture = ownsTexture;
        this.onClosed = onClosed;

        // シェアを押す前に共有シートを先読みしておき、初回表示の遅延を減らす
        NativeShareBridge.PrewarmShare(filePath, shareText, shareSubject);

        if (previewImage != null && texture != null)
        {
            previewImage.texture = texture;
            // プレハブの初期色を白に戻して素の画像を表示する
            previewImage.color = Color.white;

            BuildPolaroidLayout(texture);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveClicked);
            saveButton.onClick.AddListener(OnSaveClicked);
        }

        if (shareButton != null)
        {
            shareButton.onClick.RemoveListener(OnShareClicked);
            shareButton.onClick.AddListener(OnShareClicked);
        }
    }

    /// <summary>
    /// ポラロイド風の枠(Frame.png)を組み、その窓の中に撮影画像を配置する。
    /// プレハブの RectTransform 値には依存せず、すべてコード側で構築する。
    /// </summary>
    private void BuildPolaroidLayout(Texture2D texture)
    {
        RectTransform root = (RectTransform)transform;

        // 枠が無ければ従来どおり全画面に収める(フォールバック)
        if (frameSprite == null)
        {
            FillParentWithPhoto((RectTransform)previewImage.transform.parent, texture);
            return;
        }

        // ボタンの上に、左右に余白をとった表示領域を作る
        RectTransform cardArea = CreateChild("CardArea", root);
        cardArea.anchorMin = new Vector2(cardMarginX, cardBottomAnchor);
        cardArea.anchorMax = new Vector2(1f - cardMarginX, cardTopAnchor);
        cardArea.offsetMin = Vector2.zero;
        cardArea.offsetMax = Vector2.zero;

        // カード(枠)= 撮影画像の縦横比に合わせる。
        // フレームは 9 スライスなので、白縁の太さは一定のまま縦長/横長に伸びる。
        RectTransform card = CreateChild("Card", cardArea);
        SetCenterAnchors(card);
        AspectRatioFitter cardFitter = card.gameObject.AddComponent<AspectRatioFitter>();
        cardFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        cardFitter.aspectRatio =
            texture.height > 0 ? (float)texture.width / texture.height : 1f;

        // 9 スライス境界(ピクセル)を UI 上の余白サイズに換算する。
        // border = (x:左, y:下, z:右, w:上)
        Vector4 border = frameSprite.border;
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        float refPpu = rootCanvas != null ? rootCanvas.referencePixelsPerUnit : 100f;
        float spritePpu = frameSprite.pixelsPerUnit > 0f ? frameSprite.pixelsPerUnit : 100f;
        float k = refPpu / spritePpu; // pixelsPerUnitMultiplier は 1 とする
        float left = border.x * k;
        float bottom = border.y * k;
        float right = border.z * k;
        float top = border.w * k;

        // 窓(マスク)。カード全体から白縁ぶんだけ内側に寄せる
        RectTransform window = CreateChild("Window", card);
        window.anchorMin = Vector2.zero;
        window.anchorMax = Vector2.one;
        window.offsetMin = new Vector2(left, bottom);
        window.offsetMax = new Vector2(-right, -top);
        window.gameObject.AddComponent<RectMask2D>();

        // 写真を窓の子に移し、縦横比フィット
        RectTransform photo = previewImage.rectTransform;
        photo.SetParent(window, false);
        SetCenterAnchors(photo);
        previewImage.raycastTarget = false;

        AspectRatioFitter photoFitter = previewImage.GetComponent<AspectRatioFitter>();
        if (photoFitter == null)
        {
            photoFitter = previewImage.gameObject.AddComponent<AspectRatioFitter>();
        }
        photoFitter.aspectMode = cropPhotoToFrame
            ? AspectRatioFitter.AspectMode.EnvelopeParent   // 窓いっぱいに切り抜く
            : AspectRatioFitter.AspectMode.FitInParent;     // 写真全体を収める
        if (texture.height > 0)
        {
            photoFitter.aspectRatio = (float)texture.width / texture.height;
        }

        // 枠(Frame)を写真の背面に敷く。窓のグレーは写真が覆い、白縁とロゴが残る
        RectTransform frame = CreateChild("Frame", card);
        frame.SetSiblingIndex(0); // Window より後ろ(先頭)へ
        StretchFull(frame);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.sprite = frameSprite;
        frameImage.type = Image.Type.Sliced;        // 9 スライスで白縁を一定幅に保つ
        frameImage.pixelsPerUnitMultiplier = 1f;
        frameImage.preserveAspect = false;
        frameImage.raycastTarget = false;
    }

    private void FillParentWithPhoto(RectTransform parent, Texture2D texture)
    {
        RectTransform rt = previewImage.rectTransform;
        rt.localScale = Vector3.one;
        StretchFull(rt);

        AspectRatioFitter fitter = previewImage.GetComponent<AspectRatioFitter>();
        if (fitter == null)
        {
            fitter = previewImage.gameObject.AddComponent<AspectRatioFitter>();
        }
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        if (texture.height > 0)
        {
            fitter.aspectRatio = (float)texture.width / texture.height;
        }
    }

    private static RectTransform CreateChild(string childName, Transform parent)
    {
        GameObject go = new GameObject(childName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetCenterAnchors(RectTransform rt)
    {
        rt.localScale = Vector3.one;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private void OnSaveClicked()
    {
        if (actionRunning)
        {
            return;
        }

        actionRunning = true;
        SetButtonsInteractable(false);

        NativeShareBridge.SaveImageToGallery(filePath);

        Close();
    }

    private void OnShareClicked()
    {
        if (actionRunning)
        {
            return;
        }

        actionRunning = true;
        SetButtonsInteractable(false);

        NativeShareBridge.ShareImage(filePath, shareText, shareSubject);

        Close();
    }

    private void SetButtonsInteractable(bool value)
    {
        if (saveButton != null)
        {
            saveButton.interactable = value;
        }

        if (shareButton != null)
        {
            shareButton.interactable = value;
        }
    }

    /// <summary>
    /// ダイアログを閉じる。所有していれば texture も破棄する。
    /// </summary>
    public void Close()
    {
        if (onClosed != null)
        {
            Action callback = onClosed;
            onClosed = null;
            callback.Invoke();
        }

        if (ownsTexture && previewTexture != null)
        {
            if (previewImage != null && previewImage.texture == previewTexture)
            {
                previewImage.texture = null;
            }

            Destroy(previewTexture);
            previewTexture = null;
        }

        Destroy(gameObject);
    }
}
