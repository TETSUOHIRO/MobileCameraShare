package jp.yourname.unity.nativeshare;

import android.app.Activity;
import android.content.ClipData;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;

public class NativeShareBridge
{
    public static void shareImage(
            final Activity activity,
            final String filePath,
            final String text,
            final String subject
    )
    {
        if (activity == null)
        {
            return;
        }

        activity.runOnUiThread(new Runnable()
        {
            @Override
            public void run()
            {
                try
                {
                    if (filePath == null || filePath.length() == 0)
                    {
                        return;
                    }

                    File file = new File(filePath);

                    if (!file.exists())
                    {
                        return;
                    }

                    Uri uri = NativeShareFileProvider.getUriForFile(activity, file);

                    Intent shareIntent = new Intent(Intent.ACTION_SEND);
                    shareIntent.setType("image/png");
                    shareIntent.putExtra(Intent.EXTRA_STREAM, uri);

                    if (text != null && text.length() > 0)
                    {
                        shareIntent.putExtra(Intent.EXTRA_TEXT, text);
                    }

                    if (subject != null && subject.length() > 0)
                    {
                        shareIntent.putExtra(Intent.EXTRA_SUBJECT, subject);
                    }

                    shareIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);

                    shareIntent.setClipData(
                            ClipData.newUri(
                                    activity.getContentResolver(),
                                    file.getName(),
                                    uri
                            )
                    );

                    String chooserTitle =
                            subject != null && subject.length() > 0
                                    ? subject
                                    : "Share";

                    Intent chooser = Intent.createChooser(shareIntent, chooserTitle);

                    activity.startActivity(chooser);
                }
                catch (Exception e)
                {
                    e.printStackTrace();
                }
            }
        });
    }

    public static void saveImageToGallery(
            final Activity activity,
            final String filePath,
            final String displayName
    )
    {
        if (activity == null)
        {
            return;
        }

        activity.runOnUiThread(new Runnable()
        {
            @Override
            public void run()
            {
                try
                {
                    if (filePath == null || filePath.length() == 0)
                    {
                        return;
                    }

                    File file = new File(filePath);

                    if (!file.exists())
                    {
                        return;
                    }

                    String name =
                            displayName != null && displayName.length() > 0
                                    ? displayName
                                    : file.getName();

                    ContentResolver resolver = activity.getContentResolver();

                    ContentValues values = new ContentValues();
                    values.put(MediaStore.Images.Media.DISPLAY_NAME, name);
                    values.put(MediaStore.Images.Media.MIME_TYPE, "image/png");

                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q)
                    {
                        values.put(
                                MediaStore.Images.Media.RELATIVE_PATH,
                                Environment.DIRECTORY_PICTURES
                        );
                        values.put(MediaStore.Images.Media.IS_PENDING, 1);
                    }

                    Uri uri = resolver.insert(
                            MediaStore.Images.Media.EXTERNAL_CONTENT_URI,
                            values
                    );

                    if (uri == null)
                    {
                        return;
                    }

                    OutputStream out = null;
                    InputStream in = null;

                    try
                    {
                        out = resolver.openOutputStream(uri);
                        in = new FileInputStream(file);

                        byte[] buffer = new byte[8192];
                        int length;

                        while ((length = in.read(buffer)) > 0)
                        {
                            out.write(buffer, 0, length);
                        }

                        out.flush();
                    }
                    finally
                    {
                        if (in != null)
                        {
                            in.close();
                        }

                        if (out != null)
                        {
                            out.close();
                        }
                    }

                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q)
                    {
                        values.clear();
                        values.put(MediaStore.Images.Media.IS_PENDING, 0);
                        resolver.update(uri, values, null, null);
                    }
                }
                catch (Exception e)
                {
                    e.printStackTrace();
                }
            }
        });
    }
}