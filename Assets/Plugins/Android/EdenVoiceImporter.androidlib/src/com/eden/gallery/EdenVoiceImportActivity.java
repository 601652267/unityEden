package com.eden.gallery;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.OpenableColumns;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

public final class EdenVoiceImportActivity extends Activity {
    public static final String EXTRA_CALLBACK_OBJECT = "eden.callbackObject";
    public static final String EXTRA_CALLBACK_METHOD = "eden.callbackMethod";

    private static final int REQUEST_OPEN_ARCHIVE = 7318;
    private static final int BUFFER_SIZE = 1024 * 1024;

    private String callbackObject;
    private String callbackMethod;
    private boolean pickerStarted;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Intent launchIntent = getIntent();
        callbackObject = launchIntent.getStringExtra(EXTRA_CALLBACK_OBJECT);
        callbackMethod = launchIntent.getStringExtra(EXTRA_CALLBACK_METHOD);
        if (savedInstanceState != null) {
            pickerStarted = savedInstanceState.getBoolean("pickerStarted", false);
        }
        if (!pickerStarted) {
            pickerStarted = true;
            openArchivePicker();
        }
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        outState.putBoolean("pickerStarted", pickerStarted);
        super.onSaveInstanceState(outState);
    }

    private void openArchivePicker() {
        try {
            Intent picker = new Intent(
                Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT
                    ? Intent.ACTION_OPEN_DOCUMENT
                    : Intent.ACTION_GET_CONTENT);
            picker.addCategory(Intent.CATEGORY_OPENABLE);
            picker.setType("application/zip");
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT) {
                picker.putExtra(
                    "android.intent.extra.MIME_TYPES",
                    new String[] {
                        "application/zip",
                        "application/x-zip-compressed",
                        "application/octet-stream"
                    });
            }
            picker.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivityForResult(
                Intent.createChooser(picker, "Select voice ZIP archive"),
                REQUEST_OPEN_ARCHIVE);
        } catch (Exception exception) {
            sendMessage("ERROR:" + exception.getMessage());
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_OPEN_ARCHIVE) {
            return;
        }
        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            sendMessage("CANCELLED");
            finish();
            return;
        }

        final Uri uri = data.getData();
        final Context appContext = getApplicationContext();
        final ContentResolver resolver = appContext.getContentResolver();
        final File destination = new File(
            appContext.getCacheDir(),
            "eden_voice_import_" + System.currentTimeMillis() + ".zip");
        final String targetObject = callbackObject;
        final String targetMethod = callbackMethod;
        sendMessage("COPYING");
        finish();

        Thread copyThread = new Thread(new Runnable() {
            @Override
            public void run() {
                InputStream input = null;
                FileOutputStream output = null;
                try {
                    long totalBytes = queryContentSize(resolver, uri);
                    input = resolver.openInputStream(uri);
                    if (input == null) {
                        throw new IllegalStateException("Could not open selected archive");
                    }
                    output = new FileOutputStream(destination, false);
                    byte[] buffer = new byte[BUFFER_SIZE];
                    long copiedBytes = 0L;
                    long lastProgressTime = 0L;
                    int read;
                    while ((read = input.read(buffer)) >= 0) {
                        if (read == 0) {
                            continue;
                        }
                        output.write(buffer, 0, read);
                        copiedBytes += read;
                        long now = System.currentTimeMillis();
                        if (now - lastProgressTime >= 500L) {
                            UnityPlayer.UnitySendMessage(
                                targetObject,
                                targetMethod,
                                "COPY_PROGRESS:" + copiedBytes + ":" + totalBytes);
                            lastProgressTime = now;
                        }
                    }
                    output.flush();
                    UnityPlayer.UnitySendMessage(
                        targetObject,
                        targetMethod,
                        "FILE:" + destination.getAbsolutePath());
                } catch (Exception exception) {
                    destination.delete();
                    UnityPlayer.UnitySendMessage(
                        targetObject,
                        targetMethod,
                        "ERROR:" + exception.getMessage());
                } finally {
                    try {
                        if (input != null) {
                            input.close();
                        }
                    } catch (Exception ignored) {
                    }
                    try {
                        if (output != null) {
                            output.close();
                        }
                    } catch (Exception ignored) {
                    }
                }
            }
        }, "EdenVoiceArchiveCopy");
        copyThread.start();
    }

    private static long queryContentSize(ContentResolver resolver, Uri uri) {
        Cursor cursor = null;
        try {
            cursor = resolver.query(
                uri,
                new String[] { OpenableColumns.SIZE },
                null,
                null,
                null);
            if (cursor != null && cursor.moveToFirst() && !cursor.isNull(0)) {
                return cursor.getLong(0);
            }
        } catch (Exception ignored) {
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }
        return -1L;
    }

    private void sendMessage(String message) {
        if (callbackObject == null || callbackMethod == null) {
            return;
        }
        UnityPlayer.UnitySendMessage(callbackObject, callbackMethod, message);
    }
}
