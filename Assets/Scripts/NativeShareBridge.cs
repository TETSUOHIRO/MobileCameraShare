using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public static class NativeShareBridge
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _NativeShareBridge_ShareImage(
        string filePath,
        string text,
        string subject
    );

    [DllImport("__Internal")]
    private static extern void _NativeShareBridge_SaveImageToGallery(
        string filePath
    );

    [DllImport("__Internal")]
    private static extern void _NativeShareBridge_PrewarmShare(
        string filePath,
        string text,
        string subject
    );
#endif

    /// <summary>
    /// 共有シートを事前に生成しておく(初回表示の遅延対策)。
    /// プレビューを開いた時など、シェアを押す前に呼ぶ。
    /// </summary>
    public static void PrewarmShare(string filePath, string text, string subject)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

#if UNITY_IOS && !UNITY_EDITOR
        _NativeShareBridge_PrewarmShare(filePath, text ?? "", subject ?? "");
#endif
    }

    public static void ShareImage(string filePath, string text, string subject)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogWarning("共有するファイルパスが空です。");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer =
                   new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                using (AndroidJavaClass bridge =
                       new AndroidJavaClass("jp.yourname.unity.nativeshare.NativeShareBridge"))
                {
                    bridge.CallStatic(
                        "shareImage",
                        activity,
                        filePath,
                        text ?? "",
                        subject ?? ""
                    );
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Android共有処理でエラー: " + e);
        }

#elif UNITY_IOS && !UNITY_EDITOR
        _NativeShareBridge_ShareImage(
            filePath,
            text ?? "",
            subject ?? ""
        );

#else
        Debug.Log("Editorではネイティブ共有は実行されません。保存先: " + filePath);
#endif
    }

    /// <summary>
    /// 画像を端末の写真フォルダ(カメラロール / ギャラリー)に保存する。
    /// </summary>
    public static void SaveImageToGallery(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogWarning("保存するファイルパスが空です。");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer =
                   new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                using (AndroidJavaClass bridge =
                       new AndroidJavaClass("jp.yourname.unity.nativeshare.NativeShareBridge"))
                {
                    bridge.CallStatic(
                        "saveImageToGallery",
                        activity,
                        filePath,
                        System.IO.Path.GetFileName(filePath)
                    );
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Androidギャラリー保存処理でエラー: " + e);
        }

#elif UNITY_IOS && !UNITY_EDITOR
        _NativeShareBridge_SaveImageToGallery(filePath);

#else
        Debug.Log("Editorではギャラリー保存は実行されません。保存元: " + filePath);
#endif
    }
}