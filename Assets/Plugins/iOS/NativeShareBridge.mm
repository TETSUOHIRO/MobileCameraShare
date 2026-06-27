#import <UIKit/UIKit.h>

static NSString* NativeShareMakeString(const char* cString)
{
    if (cString == NULL)
    {
        return @"";
    }

    return [NSString stringWithUTF8String:cString];
}

static UIViewController* NativeShareGetTopViewController()
{
    UIWindow* keyWindow = nil;

    if (@available(iOS 13.0, *))
    {
        NSSet<UIScene*>* scenes = [UIApplication sharedApplication].connectedScenes;

        for (UIScene* scene in scenes)
        {
            if (scene.activationState == UISceneActivationStateForegroundActive &&
                [scene isKindOfClass:[UIWindowScene class]])
            {
                UIWindowScene* windowScene = (UIWindowScene*)scene;

                for (UIWindow* window in windowScene.windows)
                {
                    if (window.isKeyWindow)
                    {
                        keyWindow = window;
                        break;
                    }
                }
            }

            if (keyWindow != nil)
            {
                break;
            }
        }
    }

    if (keyWindow == nil)
    {
        keyWindow = [UIApplication sharedApplication].keyWindow;
    }

    if (keyWindow == nil)
    {
        return nil;
    }

    UIViewController* controller = keyWindow.rootViewController;

    while (controller.presentedViewController != nil)
    {
        controller = controller.presentedViewController;
    }

    return controller;
}

// プレビューを開いた時点で先に作っておく共有シート(初回の遅延対策)
static UIActivityViewController* gPrewarmedActivityVC = nil;
static NSString* gPrewarmedPath = nil;

static UIActivityViewController* NativeShareBuildActivityVC(
    NSString* pathString,
    NSString* textString,
    NSString* subjectString
)
{
    NSMutableArray* items = [NSMutableArray array];

    UIImage* image = nil;

    if (pathString.length > 0 &&
        [[NSFileManager defaultManager] fileExistsAtPath:pathString])
    {
        image = [UIImage imageWithContentsOfFile:pathString];
    }

    if (image != nil)
    {
        // iOSで「画像を保存」を出すため、テキストを混ぜず UIImage だけを渡す。
        [items addObject:image];
    }
    else
    {
        // 画像化できなかった場合だけテキストを共有する
        if (textString.length > 0)
        {
            [items addObject:textString];
        }

        if (pathString.length > 0 &&
            [[NSFileManager defaultManager] fileExistsAtPath:pathString])
        {
            [items addObject:[NSURL fileURLWithPath:pathString]];
        }
    }

    if (items.count == 0)
    {
        return nil;
    }

    UIActivityViewController* vc =
        [[UIActivityViewController alloc]
            initWithActivityItems:items
            applicationActivities:nil];

    if (subjectString.length > 0)
    {
        [vc setValue:subjectString forKey:@"subject"];
    }

    return vc;
}

static void NativeSharePresent(UIActivityViewController* vc)
{
    if (vc == nil)
    {
        return;
    }

    UIViewController* controller = NativeShareGetTopViewController();

    if (controller == nil)
    {
        return;
    }

    UIPopoverPresentationController* popover = vc.popoverPresentationController;

    if (popover != nil)
    {
        popover.sourceView = controller.view;
        popover.sourceRect = CGRectMake(
            controller.view.bounds.size.width * 0.5,
            controller.view.bounds.size.height * 0.5,
            1,
            1
        );
        popover.permittedArrowDirections = 0;
    }

    [controller presentViewController:vc animated:YES completion:nil];
}

extern "C"
{
    // 共有シートを先に生成しておく(プレビュー表示時に呼ぶ想定)。
    void _NativeShareBridge_PrewarmShare(
        const char* filePath,
        const char* text,
        const char* subject
    )
    {
        NSString* pathString = NativeShareMakeString(filePath);
        NSString* textString = NativeShareMakeString(text);
        NSString* subjectString = NativeShareMakeString(subject);

        dispatch_async(dispatch_get_main_queue(), ^
        {
            gPrewarmedActivityVC =
                NativeShareBuildActivityVC(pathString, textString, subjectString);
            gPrewarmedPath = pathString;
        });
    }

    void _NativeShareBridge_ShareImage(
        const char* filePath,
        const char* text,
        const char* subject
    )
    {
        NSString* pathString = NativeShareMakeString(filePath);
        NSString* textString = NativeShareMakeString(text);
        NSString* subjectString = NativeShareMakeString(subject);

        dispatch_async(dispatch_get_main_queue(), ^
        {
            UIActivityViewController* vc = nil;

            // プレビューで先に作っておいたものがあれば再利用する
            if (gPrewarmedActivityVC != nil &&
                gPrewarmedPath != nil &&
                [gPrewarmedPath isEqualToString:pathString])
            {
                vc = gPrewarmedActivityVC;
            }

            gPrewarmedActivityVC = nil;
            gPrewarmedPath = nil;

            if (vc == nil)
            {
                vc = NativeShareBuildActivityVC(pathString, textString, subjectString);
            }

            NativeSharePresent(vc);
        });
    }

    void _NativeShareBridge_SaveImageToGallery(const char* filePath)
    {
        NSString* pathString = NativeShareMakeString(filePath);

        dispatch_async(dispatch_get_main_queue(), ^
        {
            if (pathString.length == 0 ||
                ![[NSFileManager defaultManager] fileExistsAtPath:pathString])
            {
                NSLog(@"NativeShareBridge: 保存対象のファイルがありません: %@", pathString);
                return;
            }

            UIImage* image = [UIImage imageWithContentsOfFile:pathString];

            if (image == nil)
            {
                NSLog(@"NativeShareBridge: 画像を読み込めませんでした: %@", pathString);
                return;
            }

            // カメラロールへ保存する。
            // Info.plist に NSPhotoLibraryAddUsageDescription が必要。
            UIImageWriteToSavedPhotosAlbum(image, nil, nil, nil);
        });
    }
}
