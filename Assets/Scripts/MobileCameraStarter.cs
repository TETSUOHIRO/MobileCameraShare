using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class MobileCameraStarter : MonoBehaviour
{
    [Header("カメラ映像を表示する RawImage")]
    public RawImage cameraView;

    [Header("シェアボタンのGameObject")]
    public GameObject shareButtonObject;

    [Header("シェアボタンのOnClickを自動登録する")]
    public bool autoAddShareButtonOnClick = true;

    [Header("キャプチャ時にシェアボタンを隠す")]
    public bool hideShareButtonWhileCapturing = true;

    [Header("前面カメラを使う")]
    public bool useFrontCamera = false;

    [Header("希望する解像度とFPSを使う")]
    public bool useRequestedResolution = true;

    [Header("希望する解像度とFPS")]
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;

    [Header("シェア設定")]
    public string shareText = "カメラ画像をシェアします";
    public string shareSubject = "Camera Screenshot";
    public string shareFilePrefix = "camera_share";

    [Header("プレビュー用 Canvas_preview プレハブ (未設定なら Resources から読み込み)")]
    public GameObject canvasPreviewPrefab;

    [Header("キャプチャ時に一時的に隠すUI")]
    public GameObject[] hideWhileCapturing;

    private WebCamTexture webCamTexture;
    private AspectRatioFitter aspectRatioFitter;
    private Button shareButton;

    private bool isStartingCamera = false;
    private bool isCapturing = false;
    private bool isCaptureReady = false;

    private int lastWidth = -1;
    private int lastHeight = -1;
    private int lastRotation = -1;
    private bool lastMirrored = false;

#if UNITY_ANDROID
    private PermissionCallbacks permissionCallbacks;
#endif

    private IEnumerator Start()
    {
        SetupCameraView();
        SetupShareButton();

        SetShareButtonInteractable(false);

        yield return RequestCameraPermission();
    }

    private void SetupCameraView()
    {
        if (cameraView == null)
        {
            Debug.LogWarning("Camera View に RawImage が設定されていません。");
            return;
        }

        aspectRatioFitter = cameraView.GetComponent<AspectRatioFitter>();

        if (aspectRatioFitter == null)
        {
            aspectRatioFitter = cameraView.gameObject.AddComponent<AspectRatioFitter>();
        }

        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        // カメラ背景として使うだけならタッチ判定は不要
        cameraView.raycastTarget = false;
    }

    private void SetupShareButton()
    {
        if (shareButtonObject == null)
        {
            Debug.LogWarning("Share Button Object が設定されていません。");
            return;
        }

        shareButton = shareButtonObject.GetComponent<Button>();

        if (shareButton == null)
        {
            Debug.LogWarning("Share Button Object に Button コンポーネントがありません。");
            return;
        }

        if (autoAddShareButtonOnClick)
        {
            shareButton.onClick.RemoveListener(CapturePreview);
            shareButton.onClick.AddListener(CapturePreview);
        }
    }

    private void SetShareButtonInteractable(bool value)
    {
        if (shareButton != null)
        {
            shareButton.interactable = value;
        }
    }

    private IEnumerator RequestCameraPermission()
    {
#if UNITY_IOS
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            StartCoroutine(StartCamera());
        }
        else
        {
            Debug.LogWarning("iOS: カメラの使用が許可されませんでした。");
            SetShareButtonInteractable(false);
        }

#elif UNITY_ANDROID
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            StartCoroutine(StartCamera());
        }
        else
        {
            permissionCallbacks = new PermissionCallbacks();

            permissionCallbacks.PermissionGranted += OnAndroidCameraPermissionGranted;
            permissionCallbacks.PermissionDenied += OnAndroidCameraPermissionDenied;
            permissionCallbacks.PermissionDeniedAndDontAskAgain += OnAndroidCameraPermissionDeniedAndDontAskAgain;

            Permission.RequestUserPermission(Permission.Camera, permissionCallbacks);
        }

        yield return null;

#else
        StartCoroutine(StartCamera());
        yield return null;
#endif
    }

#if UNITY_ANDROID
    private void OnAndroidCameraPermissionGranted(string permissionName)
    {
        if (permissionName == Permission.Camera)
        {
            Debug.Log("Android: カメラ権限が許可されました。");
            StartCoroutine(StartCamera());
        }
    }

    private void OnAndroidCameraPermissionDenied(string permissionName)
    {
        Debug.LogWarning("Android: カメラ権限が拒否されました。");
        SetShareButtonInteractable(false);
    }

    private void OnAndroidCameraPermissionDeniedAndDontAskAgain(string permissionName)
    {
        Debug.LogWarning("Android: カメラ権限が拒否され、今後表示しない設定になりました。");
        SetShareButtonInteractable(false);
    }
#endif

    private IEnumerator StartCamera()
    {
        if (isStartingCamera)
        {
            yield break;
        }

        isStartingCamera = true;
        isCaptureReady = false;
        SetShareButtonInteractable(false);

        if (cameraView == null)
        {
            Debug.LogWarning("Camera View に RawImage が設定されていません。");
            isStartingCamera = false;
            yield break;
        }

        StopCamera();

        yield return null;

        WebCamDevice[] devices = WebCamTexture.devices;

        Debug.Log("使用可能なカメラ数: " + devices.Length);

        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log(
                "Camera[" + i + "] " +
                devices[i].name +
                " / Front: " + devices[i].isFrontFacing
            );
        }

        if (devices.Length == 0)
        {
            Debug.LogWarning("使用できるカメラが見つかりません。");
            isStartingCamera = false;
            SetShareButtonInteractable(false);
            yield break;
        }

        string selectedCameraName = devices[0].name;

        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].isFrontFacing == useFrontCamera)
            {
                selectedCameraName = devices[i].name;
                break;
            }
        }

        Debug.Log("使用するカメラ: " + selectedCameraName);

        if (useRequestedResolution)
        {
            webCamTexture = new WebCamTexture(
                selectedCameraName,
                requestedWidth,
                requestedHeight,
                requestedFPS
            );
        }
        else
        {
            // Androidで黒画面になる場合は、まずこちらを試す
            webCamTexture = new WebCamTexture(selectedCameraName);
        }

        cameraView.texture = webCamTexture;
        webCamTexture.Play();

        float timeout = 5f;

        while ((webCamTexture.width <= 16 || webCamTexture.height <= 16) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Debug.Log(
            "WebCamTexture width: " + webCamTexture.width +
            " / height: " + webCamTexture.height +
            " / rotation: " + webCamTexture.videoRotationAngle +
            " / mirrored: " + webCamTexture.videoVerticallyMirrored
        );

        if (webCamTexture.width <= 16 || webCamTexture.height <= 16)
        {
            Debug.LogWarning("カメラ映像のフレームが取得できていません。");
            isStartingCamera = false;
            isCaptureReady = false;
            SetShareButtonInteractable(false);
            yield break;
        }

        ApplyCameraTextureSettings();

        isStartingCamera = false;
        isCaptureReady = true;

        SetShareButtonInteractable(true);

        Debug.Log("カメラを開始しました。キャプチャ準備完了。");
    }

    private void Update()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            return;
        }

        if (
            webCamTexture.width != lastWidth ||
            webCamTexture.height != lastHeight ||
            webCamTexture.videoRotationAngle != lastRotation ||
            webCamTexture.videoVerticallyMirrored != lastMirrored
        )
        {
            ApplyCameraTextureSettings();
        }
    }

    private void ApplyCameraTextureSettings()
    {
        if (cameraView == null || webCamTexture == null)
        {
            return;
        }

        int rotationAngle = webCamTexture.videoRotationAngle;

        cameraView.rectTransform.localEulerAngles =
            new Vector3(0f, 0f, -rotationAngle);

        if (webCamTexture.videoVerticallyMirrored)
        {
            cameraView.uvRect = new Rect(0f, 1f, 1f, -1f);
        }
        else
        {
            cameraView.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        float aspectRatio = (float)webCamTexture.width / webCamTexture.height;

        if (aspectRatioFitter != null)
        {
            aspectRatioFitter.aspectRatio = aspectRatio;
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        lastWidth = webCamTexture.width;
        lastHeight = webCamTexture.height;
        lastRotation = webCamTexture.videoRotationAngle;
        lastMirrored = webCamTexture.videoVerticallyMirrored;
    }

    // 旧名互換: ボタンの onClick が CaptureAndShare を参照していても動くようにする
    public void CaptureAndShare()
    {
        CapturePreview();
    }

    public void CapturePreview()
    {
        if (!isCaptureReady)
        {
            Debug.LogWarning("まだキャプチャの準備ができていません。");
            return;
        }

        if (isCapturing)
        {
            return;
        }

        StartCoroutine(CaptureAndPreviewCoroutine());
    }

    private IEnumerator CaptureAndPreviewCoroutine()
    {
        isCapturing = true;
        SetShareButtonInteractable(false);

        bool shareButtonPreviousActive = true;

        if (shareButtonObject != null && hideShareButtonWhileCapturing)
        {
            shareButtonPreviousActive = shareButtonObject.activeSelf;
            shareButtonObject.SetActive(false);
        }

        bool[] previousActiveStates = null;

        if (hideWhileCapturing != null && hideWhileCapturing.Length > 0)
        {
            previousActiveStates = new bool[hideWhileCapturing.Length];

            for (int i = 0; i < hideWhileCapturing.Length; i++)
            {
                if (hideWhileCapturing[i] != null)
                {
                    previousActiveStates[i] = hideWhileCapturing[i].activeSelf;
                    hideWhileCapturing[i].SetActive(false);
                }
            }
        }

        yield return new WaitForEndOfFrame();

        Texture2D screenshot = new Texture2D(
            Screen.width,
            Screen.height,
            TextureFormat.RGB24,
            false
        );

        screenshot.ReadPixels(
            new Rect(0f, 0f, Screen.width, Screen.height),
            0,
            0
        );

        screenshot.Apply();

        byte[] pngData = screenshot.EncodeToPNG();

        string fileName =
            shareFilePrefix + "_" +
            System.DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".png";

        string filePath = Path.Combine(Application.temporaryCachePath, fileName);

        File.WriteAllBytes(filePath, pngData);

        // screenshot はここでは破棄しない。プレビュー表示に使い、
        // ダイアログを閉じる時に CanvasPreview 側で破棄する。

        if (hideWhileCapturing != null && previousActiveStates != null)
        {
            for (int i = 0; i < hideWhileCapturing.Length; i++)
            {
                if (hideWhileCapturing[i] != null)
                {
                    hideWhileCapturing[i].SetActive(previousActiveStates[i]);
                }
            }
        }

        if (shareButtonObject != null && hideShareButtonWhileCapturing)
        {
            shareButtonObject.SetActive(shareButtonPreviousActive);
        }

        Debug.Log("プレビュー用画像を保存しました: " + filePath);

        // 撮影が終わったらプレビューダイアログを開く。
        // 保存 / シェアの実行はダイアログ側のボタンから行う。
        CanvasPreview preview = CanvasPreview.Show(
            filePath,
            screenshot,
            shareText,
            shareSubject,
            true,             // テクスチャの所有権をプレビューに渡す
            OnPreviewClosed,  // 閉じたら撮影ボタンを戻す
            canvasPreviewPrefab
        );

        if (preview == null)
        {
            // プレビューを開けなかった場合のフォールバック
            Destroy(screenshot);
            isCapturing = false;

            if (isCaptureReady)
            {
                SetShareButtonInteractable(true);
            }

            yield break;
        }

        // プレビュー表示中はボタンを無効のままにし、
        // 閉じられた時に OnPreviewClosed で復帰させる。
        isCapturing = false;
    }

    private void OnPreviewClosed()
    {
        if (isCaptureReady)
        {
            SetShareButtonInteractable(true);
        }
    }

    public void StopCamera()
    {
        isCaptureReady = false;
        SetShareButtonInteractable(false);

        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }

            webCamTexture = null;
        }

        if (cameraView != null)
        {
            cameraView.texture = null;
        }
    }

    private void OnDestroy()
    {
        StopCamera();

        if (shareButton != null && autoAddShareButtonOnClick)
        {
            shareButton.onClick.RemoveListener(CapturePreview);
        }
    }
}