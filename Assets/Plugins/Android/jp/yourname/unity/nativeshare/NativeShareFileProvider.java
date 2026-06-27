package jp.yourname.unity.nativeshare;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import android.text.TextUtils;
import android.webkit.MimeTypeMap;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;

public class NativeShareFileProvider extends ContentProvider
{
    public static Uri getUriForFile(Context context, File file) throws IOException
    {
        File canonicalFile = file.getCanonicalFile();

        if (!isFileInAllowedDir(context, canonicalFile))
        {
            throw new SecurityException("許可されていない場所のファイルです: " + canonicalFile);
        }

        String authority = context.getPackageName() + ".native_share_fileprovider";

        return new Uri.Builder()
                .scheme("content")
                .authority(authority)
                .appendPath("share")
                .appendQueryParameter("path", canonicalFile.getAbsolutePath())
                .build();
    }

    private static boolean isFileInAllowedDir(Context context, File file) throws IOException
    {
        String filePath = file.getCanonicalPath();

        File[] allowedRoots = new File[]
        {
                context.getCacheDir(),
                context.getFilesDir(),
                context.getExternalCacheDir()
        };

        for (File root : allowedRoots)
        {
            if (root == null)
            {
                continue;
            }

            String rootPath = root.getCanonicalPath();

            if (filePath.equals(rootPath) || filePath.startsWith(rootPath + File.separator))
            {
                return true;
            }
        }

        return false;
    }

    private File getFileForUri(Uri uri) throws IOException
    {
        Context context = getContext();

        if (context == null)
        {
            throw new IOException("Context が取得できません。");
        }

        String path = uri.getQueryParameter("path");

        if (TextUtils.isEmpty(path))
        {
            throw new FileNotFoundException("path が空です。");
        }

        File file = new File(path).getCanonicalFile();

        if (!isFileInAllowedDir(context, file))
        {
            throw new SecurityException("許可されていないファイルパスです: " + file);
        }

        if (!file.exists())
        {
            throw new FileNotFoundException("ファイルが存在しません: " + file);
        }

        return file;
    }

    @Override
    public boolean onCreate()
    {
        return true;
    }

    @Override
    public String getType(Uri uri)
    {
        try
        {
            File file = getFileForUri(uri);
            String name = file.getName();

            int dotIndex = name.lastIndexOf('.');

            if (dotIndex >= 0 && dotIndex < name.length() - 1)
            {
                String extension = name.substring(dotIndex + 1).toLowerCase();
                String mimeType =
                        MimeTypeMap.getSingleton().getMimeTypeFromExtension(extension);

                if (mimeType != null)
                {
                    return mimeType;
                }
            }
        }
        catch (Exception ignored)
        {
        }

        return "application/octet-stream";
    }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException
    {
        if (mode != null && !mode.equals("r") && !mode.equals("rt"))
        {
            throw new FileNotFoundException("読み取り専用です。");
        }

        try
        {
            File file = getFileForUri(uri);

            return ParcelFileDescriptor.open(
                    file,
                    ParcelFileDescriptor.MODE_READ_ONLY
            );
        }
        catch (IOException e)
        {
            throw new FileNotFoundException(e.getMessage());
        }
    }

    @Override
    public Cursor query(
            Uri uri,
            String[] projection,
            String selection,
            String[] selectionArgs,
            String sortOrder
    )
    {
        try
        {
            File file = getFileForUri(uri);

            String[] columns = projection;

            if (columns == null)
            {
                columns = new String[]
                {
                        OpenableColumns.DISPLAY_NAME,
                        OpenableColumns.SIZE
                };
            }

            Object[] values = new Object[columns.length];

            for (int i = 0; i < columns.length; i++)
            {
                if (OpenableColumns.DISPLAY_NAME.equals(columns[i]))
                {
                    values[i] = file.getName();
                }
                else if (OpenableColumns.SIZE.equals(columns[i]))
                {
                    values[i] = file.length();
                }
                else
                {
                    values[i] = null;
                }
            }

            MatrixCursor cursor = new MatrixCursor(columns, 1);
            cursor.addRow(values);
            return cursor;
        }
        catch (Exception e)
        {
            return null;
        }
    }

    @Override
    public Uri insert(Uri uri, ContentValues values)
    {
        return null;
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs)
    {
        return 0;
    }

    @Override
    public int update(
            Uri uri,
            ContentValues values,
            String selection,
            String[] selectionArgs
    )
    {
        return 0;
    }
}