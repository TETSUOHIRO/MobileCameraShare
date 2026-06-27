using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class NativeSharePackageNameGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private const string DefaultNativeSharePackageName = "jp.yourname.unity.nativeshare";

    public void OnPreprocessBuild(BuildReport report)
    {
        BuildTarget platform = report.summary.platform;

        if (platform == BuildTarget.Android)
        {
            CheckUnityAndroidPackageName();
            CheckNativeSharePackageName();
        }
        else if (platform == BuildTarget.iOS)
        {
            // iOS 自体は jp.yourname を使わないが、テンプレートの仮名のまま
            // 公開されないよう、リネームを促す。
            CheckNativeSharePackageName();
        }
    }

    private void CheckUnityAndroidPackageName()
    {
        string applicationId =
            PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);

        if (string.IsNullOrEmpty(applicationId))
        {
            throw new BuildFailedException(
                "Android の Package Name が空です。\n\n" +
                "Project Settings > Player > Android > Other Settings > Identification > Package Name に、" +
                "自分のアプリ用の Package Name を設定してください。\n\n" +
                "例:\n" +
                "jp.piccolo.cameraapp"
            );
        }

        if (
            applicationId.StartsWith("com.DefaultCompany") ||
            applicationId.Contains("DefaultCompany") ||
            applicationId.Contains("ProductName")
        )
        {
            throw new BuildFailedException(
                "Android の Package Name が初期値のままです。\n\n" +
                "Project Settings > Player > Android > Other Settings > Identification > Package Name を、" +
                "自分のアプリ用のものへ変更してください。\n\n" +
                "現在の値:\n" +
                applicationId + "\n\n" +
                "例:\n" +
                "jp.piccolo.cameraapp"
            );
        }
    }

    private void CheckNativeSharePackageName()
    {
        string assetsPath = Application.dataPath;

        string[] targetFiles =
        {
            Path.Combine(assetsPath, "Scripts/NativeShareBridge.cs"),
            Path.Combine(assetsPath, "Plugins/Android/AndroidManifest.xml"),
            Path.Combine(assetsPath, "Plugins/Android/jp/yourname/unity/nativeshare/NativeShareBridge.java"),
            Path.Combine(assetsPath, "Plugins/Android/jp/yourname/unity/nativeshare/NativeShareFileProvider.java")
        };

        foreach (string filePath in targetFiles)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            string text = File.ReadAllText(filePath);

            if (text.Contains(DefaultNativeSharePackageName))
            {
                throw new BuildFailedException(
                    "NativeShare の Java package 名が初期値(テンプレートの仮名)のままです。\n\n" +
                    "公開・配布の前に、次の仮パッケージ名を自分のものへ変更してください。\n\n" +
                    "   " + DefaultNativeSharePackageName + "\n\n" +
                    "   例:\n" +
                    "   jp.piccolo.unity.nativeshare\n" +
                    "   jp.piccolo.cameraapp.nativeshare\n\n" +
                    "変更が必要な主な箇所:\n" +
                    "・NativeShareBridge.cs の AndroidJavaClass の文字列\n" +
                    "・NativeShareBridge.java の package 行\n" +
                    "・NativeShareFileProvider.java の package 行\n" +
                    "・AndroidManifest.xml の provider android:name\n" +
                    "・Android 側のフォルダ名(jp/yourname/unity/nativeshare)\n\n" +
                    "補足:\n" +
                    "・この package 名は Android のネイティブ実装で使われます。\n" +
                    "  iOS のビルド自体には必須ではありませんが、公開前のリネームを推奨します。\n" +
                    "・アプリの Package Name(Player Settings)と、NativeShare の Java\n" +
                    "  package 名は、同じである必要はありません。"
                );
            }
        }
    }
}