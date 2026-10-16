using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OutOfOffice
{
    public enum Tier { Low, Mid, High, Ultra }

    // Device quality tiers (OUT-19). The phone's memory, GPU family and core count pick a tier at boot (iPhones by
    // model generation); each tier sets the 3D render scale, MSAA, shadows, effect density and whether 120 Hz is tried.
    // The UI always draws at full resolution, so lower tiers soften the board, never the text. A hot phone steps down
    // a tier and drops to 60 fps until it cools; the player can also pick a tier in Settings.
    public static class Quality
    {
        public struct Preset
        {
            public float RenderScale, ShadowDistance, Fx;
            public int Msaa, Shadows;
            public bool HighRefresh;
        }

        public static Preset For(Tier tier)
        {
            switch (tier)
            {
                case Tier.Low: return new Preset { RenderScale = 0.85f, Msaa = 2, Shadows = 512, ShadowDistance = 24f, Fx = 0.65f, HighRefresh = false };
                case Tier.Mid: return new Preset { RenderScale = 0.95f, Msaa = 2, Shadows = 1024, ShadowDistance = 32f, Fx = 0.85f, HighRefresh = false };
                case Tier.High: return new Preset { RenderScale = 1f, Msaa = 4, Shadows = 1024, ShadowDistance = 40f, Fx = 1f, HighRefresh = true };
                default: return new Preset { RenderScale = 1f, Msaa = 4, Shadows = 2048, ShadowDistance = 40f, Fx = 1f, HighRefresh = true };
            }
        }

        public static Tier Detected { get; private set; } = Tier.Ultra;
        public static Tier Chosen { get; private set; } = Tier.Ultra;  // the detected tier, or the player's pick
        public static int Heat { get; private set; }                    // 0 cool, 1 warm (a tier down, 60 fps), 2 hot (menus at 30 too)
        public static Tier Current => (Tier)Mathf.Max(0, (int)Chosen - Mathf.Min(1, Heat));
        public static Preset Now => For(Current);

        public static Tier Detect() => Detect(Application.platform, SystemInfo.systemMemorySize, SystemInfo.graphicsDeviceName, SystemInfo.processorCount, SystemInfo.deviceModel);

        // What tier a device gets. Pure, for the checks.
        public static Tier Detect(RuntimePlatform platform, int memoryMb, string gpu, int cores, string model)
        {
            if (platform == RuntimePlatform.IPhonePlayer) return AppleTier(model ?? "");
            if (platform != RuntimePlatform.Android) return Tier.Ultra;  // the editor and desktops
            Tier byMemory = memoryMb < 3500 ? Tier.Low : memoryMb < 6000 ? Tier.Mid : memoryMb < 10000 ? Tier.High : Tier.Ultra;
            string g = (gpu ?? "").ToLowerInvariant();
            Tier cap = (cores > 0 && cores <= 4) || Weak(g) ? Tier.Low : Strong(g) ? Tier.Ultra : Tier.High;
            return (Tier)Mathf.Min((int)byMemory, (int)cap);
        }

        // Entry-level mobile GPUs: Mali G31/G51/G52/G57 and the old T series, PowerVR, Adreno 3xx-5xx and 60x/61x.
        static bool Weak(string g) =>
            g.Contains("mali-g31") || g.Contains("mali-g51") || g.Contains("mali-g52") || g.Contains("mali-g57") || g.Contains("mali-t") ||
            g.Contains("powervr") || g.Contains("img bxm") || g.Contains("adreno (tm) 3") || g.Contains("adreno (tm) 4") || g.Contains("adreno (tm) 5") ||
            g.Contains("adreno (tm) 60") || g.Contains("adreno (tm) 61");

        // Flagship GPUs: Adreno 7xx/8xx, Immortalis, Xclipse, and the Mali G6x0/G7x0 generation.
        static bool Strong(string g) =>
            g.Contains("adreno (tm) 7") || g.Contains("adreno (tm) 8") || g.Contains("immortalis") || g.Contains("xclipse") ||
            g.Contains("mali-g710") || g.Contains("mali-g715") || g.Contains("mali-g720") || g.Contains("mali-g610") || g.Contains("mali-g615") || g.Contains("mali-g620");

        // iPhone14,x is the iPhone 13 family, iPhone15,x the 14 Pro, and so on; iPads by the same counting.
        static Tier AppleTier(string model)
        {
            int major = 0;
            int digits = model.IndexOfAny("0123456789".ToCharArray());
            if (digits >= 0)
            {
                int comma = model.IndexOf(',', digits);
                int.TryParse(comma > digits ? model.Substring(digits, comma - digits) : model.Substring(digits), out major);
            }
            if (model.StartsWith("iPad")) return major >= 13 ? Tier.Ultra : major >= 8 ? Tier.High : Tier.Mid;
            return major >= 15 ? Tier.Ultra : major >= 13 ? Tier.High : major >= 11 ? Tier.Mid : Tier.Low;
        }

        // At boot: detect, then use the player's pick if there is one (-1 is automatic).
        public static void Start(int pick)
        {
            Detected = Detect();
            Choose(pick);
        }

        public static void Choose(int pick)
        {
            Chosen = pick >= 0 && pick <= (int)Tier.Ultra ? (Tier)pick : Detected;
            Apply();
        }

        // From the phone's thermal status (Android PowerManager: 2 moderate, 3 severe and above).
        public static bool SetHeat(int thermalStatus)
        {
            int heat = thermalStatus >= 3 ? 2 : thermalStatus >= 2 ? 1 : 0;
            if (heat == Heat) return false;
            Heat = heat;
            Apply();
            Analytics.Event("thermal", ("status", thermalStatus), ("tier", Current.ToString()));
            return true;
        }

        // Writes the tier into the render pipeline asset. Not in the editor, where that would edit the asset on disk.
        static void Apply()
        {
            if (Application.isEditor) return;
            var p = Now;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = p.RenderScale;
                urp.msaaSampleCount = p.Msaa;
                urp.mainLightShadowmapResolution = p.Shadows;
                urp.shadowDistance = p.ShadowDistance;
            }
        }
    }

    // What the phone says about heat and battery (OUT-19), polled every few seconds. Android only for now; elsewhere
    // the status stays unknown (-1) and power saving off.
    public static class DeviceState
    {
        public static int ThermalStatus { get; private set; } = -1;
        public static bool PowerSave { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject power;
        static int sdk = -1;
#endif

        public static void Poll()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (power == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        power = activity.Call<AndroidJavaObject>("getSystemService", "power");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                }
                if (power == null) return;
                if (sdk >= 29) ThermalStatus = power.Call<int>("getCurrentThermalStatus");
                PowerSave = power.Call<bool>("isPowerSaveMode");
            }
            catch (System.Exception) { }
#endif
        }
    }
}
