package com.chevir.leaguewatch;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** The 07:00 alarm: runs the league check off the main thread and sets the alarm for the next morning. */
public final class LeagueWatchReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(final Context context, Intent intent) {
        final PendingResult result = goAsync();
        final Context app = context.getApplicationContext();
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    LeagueWatch.check(app);
                } finally {
                    LeagueWatch.arm(app);
                    result.finish();
                }
            }
        }).start();
    }
}
