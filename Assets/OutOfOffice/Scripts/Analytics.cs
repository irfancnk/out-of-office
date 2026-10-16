using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OutOfOffice
{
    // Gameplay events for design decisions. This stands in until OUT-7 picks a provider: Sent is the seam that
    // provider subscribes to. Editor and development builds also log each event; release builds don't.
    public static class Analytics
    {
        public static event Action<string, IReadOnlyDictionary<string, object>> Sent;

        public static void Event(string name, params (string Key, object Value)[] fields)
        {
            var data = new Dictionary<string, object>(fields.Length);
            foreach (var (key, value) in fields) data[key] = value;
            Sent?.Invoke(name, data);
            if (!Debug.isDebugBuild) return;
            var line = new StringBuilder("HSEVENT ").Append(name);
            foreach (var (key, value) in fields) line.Append(' ').Append(key).Append('=').Append(value);
            Debug.Log(line.ToString());
        }
    }
}
