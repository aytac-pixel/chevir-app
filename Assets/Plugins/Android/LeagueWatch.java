package com.chevir.leaguewatch;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.SystemClock;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * Checks the learner's place in the weekly league about every 30 minutes, also while Chevir is closed, and shows a
 * notification when it changed. Asks the database with a device token (no sign-in needed): league_place_for_device.
 * Started and configured from C# (ReminderService).
 */
public final class LeagueWatch {
    static final String PREFS = "chevir_league_watch";
    static final String CHANNEL = "cevir_league";
    private static final int REQUEST = 4711;
    private static final int NOTIFICATION_ID = 4712;

    private LeagueWatch() {
    }

    /** Saves what the check needs and arms the repeating alarm. Texts come from the app in its interface language. */
    public static void start(Context context, String baseUrl, String anonKey, String token,
                             String titleUp, String titleDown, String bodyUp, String bodyDown, String channelName,
                             boolean azerbaijaniOrdinal) {
        prefs(context).edit()
                .putString("url", baseUrl)
                .putString("key", anonKey)
                .putString("token", token)
                .putString("titleUp", titleUp)
                .putString("titleDown", titleDown)
                .putString("bodyUp", bodyUp)
                .putString("bodyDown", bodyDown)
                .putString("channelName", channelName)
                .putBoolean("azOrdinal", azerbaijaniOrdinal)
                .putBoolean("on", true)
                .apply();
        arm(context);
    }

    /** Stops the checks (league messages off, or signed out). */
    public static void stop(Context context) {
        prefs(context).edit().putBoolean("on", false).apply();
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarms != null) alarms.cancel(pending(context));
    }

    /**
     * Stores the place the app has just seen and returns the one stored before for the same week (0 when none), so the
     * app and the background check never announce the same change twice.
     */
    public static synchronized int exchange(Context context, String week, int rank) {
        SharedPreferences p = prefs(context);
        int previous = week.equals(p.getString("week", "")) ? p.getInt("rank", 0) : 0;
        p.edit().putString("week", week).putInt("rank", rank).apply();
        return previous;
    }

    static void arm(Context context) {
        if (!prefs(context).getBoolean("on", false)) return;
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarms == null) return;
        alarms.setInexactRepeating(AlarmManager.ELAPSED_REALTIME_WAKEUP,
                SystemClock.elapsedRealtime() + AlarmManager.INTERVAL_FIFTEEN_MINUTES,
                AlarmManager.INTERVAL_HALF_HOUR, pending(context));
    }

    private static PendingIntent pending(Context context) {
        Intent intent = new Intent(context, LeagueWatchReceiver.class);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= 23) flags |= PendingIntent.FLAG_IMMUTABLE;
        return PendingIntent.getBroadcast(context, REQUEST, intent, flags);
    }

    static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    /** One check (on a background thread). */
    static void check(Context context) {
        SharedPreferences p = prefs(context);
        if (!p.getBoolean("on", false)) return;
        String url = p.getString("url", "");
        String key = p.getString("key", "");
        String token = p.getString("token", "");
        if (url.isEmpty() || key.isEmpty() || token.isEmpty()) return;

        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection) new URL(url + "/rest/v1/rpc/league_place_for_device").openConnection();
            connection.setConnectTimeout(15000);
            connection.setReadTimeout(15000);
            connection.setRequestMethod("POST");
            connection.setDoOutput(true);
            connection.setRequestProperty("apikey", key);
            connection.setRequestProperty("Authorization", "Bearer " + key);
            connection.setRequestProperty("Content-Type", "application/json");
            byte[] body = new JSONObject().put("p_token", token).toString().getBytes("UTF-8");
            OutputStream out = connection.getOutputStream();
            out.write(body);
            out.close();
            if (connection.getResponseCode() != 200) return;

            String text = read(connection.getInputStream()).trim();
            JSONObject row;
            if (text.startsWith("[")) {
                JSONArray rows = new JSONArray(text);
                if (rows.length() == 0) return;
                row = rows.getJSONObject(0);
            } else {
                row = new JSONObject(text);
            }

            String week = row.optString("week", "");
            int rank = row.optInt("rank", 0);
            int players = row.optInt("players", 0);
            if (week.isEmpty() || rank <= 0 || players < 2) return;
            int previous = exchange(context, week, rank);
            if (previous == 0 || previous == rank) return;
            notify(context, rank < previous, rank);
        } catch (Exception ignored) {
            // offline or the database is not ready: try again at the next alarm
        } finally {
            if (connection != null) connection.disconnect();
        }
    }

    private static String read(InputStream in) throws java.io.IOException {
        ByteArrayOutputStream buffer = new ByteArrayOutputStream();
        byte[] chunk = new byte[4096];
        int n;
        while ((n = in.read(chunk)) > 0) buffer.write(chunk, 0, n);
        in.close();
        return buffer.toString("UTF-8");
    }

    /** Azerbaijani ordinal like the app's: 1-ci, 3-c\u00fc, 6-c\u0131, 9-cu, 10-cu, 40-c\u0131, 100-c\u00fc. */
    static String ordinalAz(int n) {
        String[] units = {"", "ci", "ci", "c\u00fc", "c\u00fc", "ci", "c\u0131", "ci", "ci", "cu"};
        String[] tens = {"", "cu", "ci", "cu", "c\u0131", "ci", "c\u0131", "ci", "ci", "c\u0131"};
        int u = n % 10, t = (n / 10) % 10, h = (n / 100) % 10;
        String suffix = u != 0 ? units[u] : t != 0 ? tens[t] : h != 0 ? "c\u00fc" : "ci";
        return n + "-" + suffix;
    }

    private static void notify(Context context, boolean up, int rank) {
        SharedPreferences p = prefs(context);
        String title = p.getString(up ? "titleUp" : "titleDown", "Chevir");
        String body = p.getString(up ? "bodyUp" : "bodyDown", "{0}").replace("{0}",
                p.getBoolean("azOrdinal", false) ? ordinalAz(rank) : Integer.toString(rank));

        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null) return;
        Notification.Builder builder;
        if (Build.VERSION.SDK_INT >= 26) {
            if (manager.getNotificationChannel(CHANNEL) == null) {
                manager.createNotificationChannel(new NotificationChannel(CHANNEL, p.getString("channelName", "Liqa"),
                        NotificationManager.IMPORTANCE_DEFAULT));
            }
            builder = new Notification.Builder(context, CHANNEL);
        } else {
            builder = new Notification.Builder(context);
        }

        Intent open = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
        if (open != null) {
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= 23) flags |= PendingIntent.FLAG_IMMUTABLE;
            builder.setContentIntent(PendingIntent.getActivity(context, NOTIFICATION_ID, open, flags));
        }

        builder.setSmallIcon(context.getApplicationInfo().icon)
                .setContentTitle(title)
                .setContentText(body)
                .setStyle(new Notification.BigTextStyle().bigText(body))
                .setAutoCancel(true);
        try {
            manager.notify(NOTIFICATION_ID, builder.build());
        } catch (SecurityException ignored) {
            // notifications not allowed on this phone
        }
    }
}
