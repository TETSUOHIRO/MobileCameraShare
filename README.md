# MobileCameraShare

スマホ(iOS / Android)のカメラで撮影し、**プレビュー(ポラロイド風の白縁つき)→ 端末への保存 / ネイティブ共有**までを行う Unity サンプルプロジェクトです。

書籍 **「Unityで神になる本」** のサンプルとして公開しています。本を購入してくださった方は、MIT ライセンスの範囲で自由に使ってください。

## 動作環境

- Unity 6000.3.17f1(Unity 6.3)
- iOS / Android 実機(カメラ・写真ライブラリを使うため、実機での確認が必要です)

## 主な機能

- `WebCamTexture` によるカメラプレビュー(前面 / 背面、向き・ミラー補正つき)
- シャッターで画面をキャプチャ
- 撮影画像を**プレビュー表示**(昔のプリント風に白縁を合成。`Frame.png` の 9 スライスで縁幅を一定に保ったまま撮影アスペクト比に追従)
- **保存**:端末の写真フォルダ(iOS=カメラロール / Android=Pictures)へ保存
- **共有**:OS のネイティブ共有シート(iOS=UIActivityViewController / Android=ACTION_SEND)
- iOS は共有シートを事前生成して初回表示の待ちを軽減

## 構成

| 場所 | 役割 |
|------|------|
| `Assets/Scripts/MobileCameraStarter.cs` | カメラ起動・権限・キャプチャ・プレビュー呼び出し |
| `Assets/Scripts/CanvasPreview.cs` | プレビューダイアログ(枠の合成・保存/共有ボタン) |
| `Assets/Scripts/NativeShareBridge.cs` | ネイティブ共有 / ギャラリー保存の C# 窓口 |
| `Assets/Plugins/iOS/NativeShareBridge.mm` | iOS ネイティブ実装 |
| `Assets/Plugins/Android/.../NativeShareBridge.java` ほか | Android ネイティブ実装(FileProvider 含む) |
| `Assets/Editor/NativeSharePostProcessBuild.cs` | iOS ビルド後に写真追加の用途説明を Info.plist へ自動付与 |
| `Assets/Editor/NativeSharePackageNameGuard.cs` | Android ビルド前にパッケージ名の設定漏れを検出 |

## ⚠️ Android でビルドする前に(重要)

ネイティブ共有のために、仮の Java パッケージ名 `jp.yourname.unity.nativeshare` を使っています。**そのままだと Android ビルド時にガードが働いてビルドが止まります**(`NativeSharePackageNameGuard`)。次の2つを自分用に設定してください。

1. **アプリの Package Name**
   Project Settings > Player > Android > Other Settings > Identification > Package Name
   例: `jp.example.cameraapp`

2. **NativeShare の Java パッケージ名**(`jp.yourname.unity.nativeshare` を自分のものへ)
   - `Assets/Plugins/Android/jp/yourname/unity/nativeshare/` フォルダ名
   - `NativeShareBridge.java` / `NativeShareFileProvider.java` の `package` 行
   - `AndroidManifest.xml` の `provider android:name`
   - `NativeShareBridge.cs` の `AndroidJavaClass("jp.yourname.unity.nativeshare.NativeShareBridge")` の文字列

> アプリの Package Name と NativeShare の Java パッケージ名は、同じである必要はありません。

`AndroidManifest.xml` は Application Entry = **GameActivity** 前提です。Player Settings で Activity 方式に変えた場合は、マニフェストの起動アクティビティを `com.unity3d.player.UnityPlayerActivity` / テーマ `@style/UnityThemeSelector` に変更してください。

## iOS について

- 写真保存に必要な `NSPhotoLibraryAddUsageDescription` は、ビルド後処理(`NativeSharePostProcessBuild.cs`)で Info.plist に自動追加されます。文言を変えたい場合はそのスクリプトを編集してください。
- カメラ用途の説明(`NSCameraUsageDescription`)は Player Settings の「Camera Usage Description」で設定してください。
- iOS は `jp.yourname.unity.nativeshare` を実際には使いませんが、テンプレートの仮名のまま残っていると**ビルド時に警告(非ブロック)**でリネームを促します(`NativeSharePackageNameGuard`)。Android はビルドを停止します。

## ライセンス

MIT License([LICENSE](LICENSE) を参照)。
