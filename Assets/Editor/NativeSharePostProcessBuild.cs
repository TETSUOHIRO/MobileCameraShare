using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

/// <summary>
/// iOS ビルド後に Info.plist へ写真ライブラリ関連の用途説明を自動で追加する。
/// これが無いと UIImageWriteToSavedPhotosAlbum 実行時にクラッシュする。
/// </summary>
public static class NativeSharePostProcessBuild
{
    // 必要に応じて文言を変更してください(App Store 審査でも参照されます)
    private const string PhotoAddUsageDescription =
        "撮影した画像を写真ライブラリに保存するために使用します。";

    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
#if UNITY_IOS
        if (buildTarget != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = pathToBuiltProject + "/Info.plist";

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict root = plist.root;

        // 写真への「追加のみ」用途。保存だけならこちらで十分。
        if (root["NSPhotoLibraryAddUsageDescription"] == null)
        {
            root.SetString("NSPhotoLibraryAddUsageDescription", PhotoAddUsageDescription);
        }

        plist.WriteToFile(plistPath);
#endif
    }
}
