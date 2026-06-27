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
        if (report.summary.platform != BuildTarget.Android)
        {
            return;
        }

        CheckUnityAndroidPackageName();
        CheckNativeSharePackageName();
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
                    "NativeShare の Android Java package 名が初期値のままです。\n\n" +
                    "Android向けにビルドする前に、次の2種類の Package Name を確認してください。\n\n" +
                    "1. Unity Player Settings の Package Name\n" +
                    "   Project Settings > Player > Android > Other Settings > Identification > Package Name\n" +
                    "   例: jp.piccolo.cameraapp\n\n" +
                    "2. NativeShare 側の Java package 名\n" +
                    "   次の仮パッケージ名を、自分のものへ変更してください。\n\n" +
                    "   " + DefaultNativeSharePackageName + "\n\n" +
                    "   例:\n" +
                    "   jp.piccolo.unity.nativeshare\n" +
                    "   jp.piccolo.cameraapp.nativeshare\n\n" +
                    "変更が必要な主な箇所:\n" +
                    "・NativeShareBridge.cs の AndroidJavaClass の文字列\n" +
                    "・NativeShareBridge.java の package 行\n" +
                    "・NativeShareFileProvider.java の package 行\n" +
                    "・AndroidManifest.xml の provider android:name\n" +
                    "・Android 側のフォルダ名\n\n" +
                    "補足:\n" +
                    "Unity Player Settings の Package Name と、NativeShare の Java package 名は、" +
                    "同じである必要はありません。"
                );
            }
        }
    }
}