package com.eden.gallery;

import android.app.Activity;
import android.content.Intent;

import com.unity3d.player.UnityPlayer;

public final class EdenVoiceImportBridge {
    private EdenVoiceImportBridge() {
    }

    public static void open(final String callbackObject, final String callbackMethod) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            UnityPlayer.UnitySendMessage(callbackObject, callbackMethod, "ERROR:Android activity is unavailable");
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    Intent intent = new Intent(activity, EdenVoiceImportActivity.class);
                    intent.putExtra(EdenVoiceImportActivity.EXTRA_CALLBACK_OBJECT, callbackObject);
                    intent.putExtra(EdenVoiceImportActivity.EXTRA_CALLBACK_METHOD, callbackMethod);
                    activity.startActivity(intent);
                } catch (Exception exception) {
                    UnityPlayer.UnitySendMessage(
                        callbackObject,
                        callbackMethod,
                        "ERROR:" + exception.getMessage());
                }
            }
        });
    }
}
