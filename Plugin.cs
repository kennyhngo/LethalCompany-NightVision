using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using NightVision.Patches;

namespace NightVision
{
    public class PLUGIN_INFO
    {
        public const string PLUGIN_GUID = "Ken.NightVision";
        public const string PLUGIN_NAME = "Toggleable Night Vision";
        public const string PLUGIN_VERSION = "1.1.3";
    }

    [BepInPlugin(PLUGIN_INFO.PLUGIN_GUID, PLUGIN_INFO.PLUGIN_NAME, PLUGIN_INFO.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Harmony _harmony = new Harmony(PLUGIN_INFO.PLUGIN_GUID);
        internal static ManualLogSource mls;

        // config entries
        private static ConfigEntry<string> cfgKeyBind;
        public static ConfigEntry<bool> defaultToggle;
        public static ConfigEntry<float> intensity;
        public static ConfigEntry<bool> diversityFullDarkness;
        public static ConfigEntry<float> diversityFullDarknessIntensity;

        private void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_INFO.PLUGIN_GUID);
            mls.LogInfo("NightVision loaded");

            cfgKeyBind = Config.Bind("Toggle Button", "Key", "C",
                                     "Button to toggle night vision mode in the bunker.");
            defaultToggle = Config.Bind("Toggle Button", "Default Behavior", false, 
                                        "Whether night vision is on or off by default when you load up the game.");
            
            intensity = Config.Bind("Numeric Values", "Intensity", 7500f,
                                    "Intensity of the night vision when toggled. [Originally was 100000]");
            diversityFullDarkness = Config.Bind("Mod Compatability", "Diversity - Full Darkness", false,
                                                "Change default brightness when night vision is off.\nThis mod overrides the settings in Diversity config.");
            diversityFullDarknessIntensity = Config.Bind("Mod Compatability", "Diversity - Full Darkness Intensity", 1f,
                                                         "How intense the darkness will be. Set values between 0-1.");

            var insertKeyAction = new InputAction(binding: $"<Keyboard>/{cfgKeyBind.Value}");
            insertKeyAction.performed += OnInsertKeyPressed;
            insertKeyAction.Enable();

            NightVisionPatch.toggled = defaultToggle.Value;

            _harmony.PatchAll(typeof(Plugin));
            _harmony.PatchAll(typeof(NightVisionPatch));
        }

        private void OnInsertKeyPressed(InputAction.CallbackContext obj)
        {
            PlayerControllerB player = StartOfRound.Instance?.localPlayerController;
            if (player == null || player.inTerminalMenu || player.isTypingChat) return;

            NightVisionPatch.toggled = !NightVisionPatch.toggled;
            mls.LogInfo($"Night mode {(NightVisionPatch.toggled ? "enabled" : "disabled")}");
        }
    }
}

namespace NightVision.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB), "SetNightVisionEnabled")]
    internal class NightVisionPatch
    {
        internal static bool toggled;

        [HarmonyPrefix]
        private static void Prefix(PlayerControllerB __instance)
        {
            if (toggled)
            {
                __instance.nightVision.intensity = Plugin.intensity.Value;
                __instance.nightVision.range = 100000f;
                __instance.nightVision.shadowStrength = 0f;
                __instance.nightVision.shadows = 0;
                __instance.nightVision.shape = (LightShape)2;
            }
            else
            {
                float clamp = Plugin.diversityFullDarkness.Value ? Mathf.Clamp(1f - Plugin.diversityFullDarknessIntensity.Value, 0f, 1f) : 1f;
                __instance.nightVision.intensity = 366.9317f * clamp;
                __instance.nightVision.range = 12f;
                __instance.nightVision.shadowStrength = 1f;
                __instance.nightVision.shadows = 0;
                __instance.nightVision.shape = 0;
            }
        }
    }
}
