using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace OutOfOffice
{
    // Device time corrected by the server's (OUT-35). Until a sync succeeds it is plain device UTC, which Progress
    // still guards against rollbacks. The offset only moves after a successful sync, so a bad network never hurts.
    public class ServerClock : IClock
    {
        public TimeSpan Offset { get; private set; }
        public bool Synced { get; private set; }

        public DateTime UtcNow => DateTime.UtcNow + Offset;
        public DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();  // the device's time zone

        public void Apply(DateTime serverUtc, DateTime deviceUtcAtResponse)
        {
            Offset = serverUtc - deviceUtcAtResponse;
            Synced = true;
        }
    }

    public static class ServerTime
    {
        // A tiny HTTPS endpoint built for connectivity checks; only its Date header is read and nothing is sent.
        // The game's own backend (OUT-5) replaces it.
        public const string Url = "https://www.google.com/generate_204";
        public const int TimeoutSeconds = 5;

        // The HTTP Date header, e.g. "Fri, 16 Oct 2026 12:00:00 GMT", as UTC.
        public static bool TryParseDate(string header, out DateTime utc) =>
            DateTime.TryParseExact(header, "r", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);

        public static IEnumerator Sync(ServerClock clock, Action<bool> done = null)
        {
            using (var request = UnityWebRequest.Head(Url))
            {
                request.timeout = TimeoutSeconds;
                var sent = DateTime.UtcNow;
                yield return request.SendWebRequest();
                var received = DateTime.UtcNow;
                DateTime server = default;
                bool ok = request.result == UnityWebRequest.Result.Success && TryParseDate(request.GetResponseHeader("Date"), out server);
                if (ok)
                {
                    // The header is whole seconds and was stamped about halfway through the round trip.
                    clock.Apply(server + TimeSpan.FromTicks((received - sent).Ticks / 2), received);
                    Analytics.Event("server_time", ("offset_s", (int)clock.Offset.TotalSeconds), ("rtt_ms", (int)(received - sent).TotalMilliseconds));
                }
                else Analytics.Event("server_time_fail", ("error", request.error ?? "no date"));
                done?.Invoke(ok);
            }
        }
    }
}
