using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows.WebCam;

namespace NightVision
{
    public class PLUGIN_INFO
    {
        public const string PLUGIN_GUID = "Ken.NightVision";
        public const string PLUGIN_NAME = "Toggleable Night Vision";
        public const string PLUGIN_VERSION = "1.1.0";
    }

    [BepInPlugin(PLUGIN_INFO.PLUGIN_GUID, PLUGIN_INFO.PLUGIN_NAME, PLUGIN_INFO.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Harmony _harmony = new Harmony(PLUGIN_INFO.PLUGIN_GUID);
        internal static ManualLogSource mls;
        private static bool toggled = false;

        private static ConfigEntry<string> cfgKeyBind;
        public static ConfigEntry<float> intensity;
        public static ConfigEntry<bool> diversityFullDarkness;
        public static ConfigEntry<float> diversityFullDarknessIntensity;

        private void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_INFO.PLUGIN_GUID);
            mls.LogInfo("NightVision loaded");

            cfgKeyBind = Config.Bind("Toggle Button", "Key", "C",
                                     "Button to toggle night vision mode in the bunker.");
            intensity = Config.Bind("Numeric Values", "Intensity", 7500f,
                                    "Intensity of the night vision when toggled. [Originally was 100000]");
            diversityFullDarkness = Config.Bind("Mod Compatability", "Diversity - Full Darkness", false,
                                                "Change default brightness when night vision is off.\nThis mod overrides the settings in Diversity config.");
            diversityFullDarknessIntensity = Config.Bind("Mod Compatability", "Diversity - Full Darkness Intensity", 1f,
                                                         "How intense the darkness will be. Set values between 0-1.");

            var insertKeyAction = new InputAction(binding: $"<Keyboard>/{cfgKeyBind.Value}");
            insertKeyAction.performed += OnInsertKeyPressed;
            insertKeyAction.Enable();

            _harmony.PatchAll(typeof(Plugin));
        }

        private void OnInsertKeyPressed(InputAction.CallbackContext obj)
        {
            PlayerControllerB player = StartOfRound.Instance.localPlayerController;
            if (player == null) return;

            toggled = !toggled;
            mls.LogInfo($"Night mode {(toggled ? "enabled" : "disabled")}");
            if (toggled)
            {
                player.nightVision.intensity = intensity.Value;
                player.nightVision.range = 100000f;
                player.nightVision.shadowStrength = 0f;
                player.nightVision.shadows = 0;
                player.nightVision.shape = (LightShape)2;
            }
            else
            {
                float clamp = diversityFullDarkness.Value ? Mathf.Clamp(1f - diversityFullDarknessIntensity.Value, 0f, 1f) : 1f;
                player.nightVision.intensity = 366.9317f * clamp;
                player.nightVision.range = 12f;
                player.nightVision.shadowStrength = 1f;
                player.nightVision.shadows = 0;
                player.nightVision.shape = 0;
            }
        }
    }
}
