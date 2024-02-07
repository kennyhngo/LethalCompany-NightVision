using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using NightVision.Patches;
using UnityEngine.Rendering.HighDefinition;

namespace NightVision
{
    public class PLUGIN_INFO
    {
        public const string PLUGIN_GUID = "Ken.NightVision";
        public const string PLUGIN_NAME = "Toggleable Night Vision";
        public const string PLUGIN_VERSION = "2.0.0";
    }

    [BepInPlugin(PLUGIN_INFO.PLUGIN_GUID, PLUGIN_INFO.PLUGIN_NAME, PLUGIN_INFO.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Harmony harmony = new Harmony(PLUGIN_INFO.PLUGIN_GUID);
        internal static ManualLogSource mls;

        // config entries
        private static ConfigEntry<string> cfgKeyBind;
        
        // indoor settings
        public static ConfigEntry<bool> defaultToggle;
        public static ConfigEntry<float> indoorIntensity;
        
        // outdoor settings
        public static ConfigEntry<bool> toggleFog;
        
        // mod compatability
        public static ConfigEntry<bool> diversityFullDarkness;
        public static ConfigEntry<float> diversityFullDarknessIntensity;

        private void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_INFO.PLUGIN_GUID);
            mls.LogInfo("NightVision loaded");
            ConfigureSettings();
            
            var insertKeyAction = new InputAction(binding: $"<Keyboard>/{cfgKeyBind.Value}");
            insertKeyAction.performed += OnInsertKeyPressed;
            insertKeyAction.Enable();

            SetDefaultToggle(defaultToggle.Value);
            harmony.PatchAll(typeof(Plugin));
            harmony.PatchAll(typeof(NightVisionPatch));
            harmony.PatchAll(typeof(NightVisionOutdoors));
            harmony.PatchAll(typeof(PlayerControllerBPatch));
        }

        private void ConfigureSettings()
        {
            // My mod settings
            // General
            cfgKeyBind = Config.Bind("General", "Toggle Key", "C", "Button to toggle night vision mode inside the facility.");
            defaultToggle = Config.Bind("General", "Default Behavior", false, "Whether night vision is on or off by default when you load up the game.");
            
            // Indoors
            indoorIntensity = Config.Bind("Indoor Settings", "Intensity", 7500f, "Intensity of the night vision when toggled. [Originally was 100000]");

            // Outdoors
            toggleFog = Config.Bind("Outdoor Settings", "Toggle Fog", false, "Disable fog when toggling night vision outside.");
            // outdoorIntensity = Config.Bind("Outdoor Settings", "Intensity", 0f, "Intensity of night vision when toggled.");

            // Diversity mod
            diversityFullDarkness = Config.Bind("Mod Compatability", "Diversity - Full Darkness", false, "Change default brightness when night vision is off.\nThis mod overrides the settings in Diversity config.");
            diversityFullDarknessIntensity = Config.Bind("Mod Compatability", "Diversity - Full Darkness Intensity", 1f, "How intense the darkness will be. Set values between 0-1.");
        }

        private void SetDefaultToggle(bool value)
        {
            NightVisionPatch.toggled = value;
            NightVisionOutdoors.toggled = value;
            PlayerControllerBPatch.toggled = value;
        }

        private void OnInsertKeyPressed(InputAction.CallbackContext obj)
        {
            PlayerControllerB player = StartOfRound.Instance?.localPlayerController;
            if (player == null || player.inTerminalMenu || player.isTypingChat) return;

            NightVisionPatch.toggled = !NightVisionPatch.toggled;
            NightVisionOutdoors.toggled = !NightVisionOutdoors.toggled;
            PlayerControllerBPatch.toggled = !PlayerControllerBPatch.toggled;
            mls.LogInfo($"Night mode {(NightVisionPatch.toggled ? "enabled" : "disabled")}");
        }
    }
}


namespace NightVision.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    internal class PlayerControllerBPatch
    {
        internal static Object[] cameras;
        internal static bool toggled;
        internal static int cacheRefreshLimit = 20;
        internal static int cachePasses = 0;

        [HarmonyPostfix]
        private static void StartPrefix(PlayerControllerB __instance)
        {
            if (!Plugin.toggleFog.Value) return;
            if (__instance == null) return;
            if (cachePasses++ >= cacheRefreshLimit)
            {
                cachePasses = 0;
                cameras = null;
            }
            if (cameras == null) cameras = Resources.FindObjectsOfTypeAll(typeof(HDAdditionalCameraData));
            
            foreach (Object cam in cameras)
            {
                HDAdditionalCameraData camera = (HDAdditionalCameraData)(cam is HDAdditionalCameraData ? cam : null);
                if (camera.gameObject.name != "MapCamera")
                {
                    camera.customRenderingSettings = true;
                    camera.renderingPathCustomFrameSettingsOverrideMask.mask[28u] = true;
                    camera.renderingPathCustomFrameSettings.SetEnabled((FrameSettingsField)28, !toggled);
                }
            }
        }
    }


    [HarmonyPatch(typeof(PlayerControllerB), "SetNightVisionEnabled")]
    internal class NightVisionPatch
    {
        internal static bool toggled;

        [HarmonyPrefix]
        private static void Prefix(PlayerControllerB __instance)
        {
            if (toggled)
            {
                __instance.nightVision.intensity = Plugin.indoorIntensity.Value;
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


    [HarmonyPatch(typeof(TimeOfDay), "SetInsideLightingDimness")]
    internal static class NightVisionOutdoors
    {
        internal static bool toggled = false;

        private static float sigmoid(float x, float midpoint, float steepness)
        {
            return 1 / (1 + Mathf.Exp(-steepness * (x - midpoint)));
        }


        [HarmonyPostfix]
        private static void Postfix(TimeOfDay __instance)
        {
            HDAdditionalLightData indirect = __instance.sunIndirect.GetComponent<HDAdditionalLightData>();
            if (toggled)
            {
                indirect.lightDimmer = Mathf.Lerp(100f, 100_000f, sigmoid(__instance.globalTime, 950f, 0.1f));
                indirect.intensity = Mathf.Lerp(2f, 10000f, sigmoid(__instance.globalTime, 950f, 0.1f));
                indirect.shadowDimmer = 0f;
                indirect.useRayTracedShadows = true;
                indirect.lightShadowRadius = 0f;
            }
            else
            {
                indirect.lightDimmer = Mathf.Lerp(indirect.lightDimmer, 1f, 5f * Time.deltaTime);
                indirect.shadowDimmer = 1f;
                indirect.useRayTracedShadows = false;
                indirect.lightShadowRadius = 0.5f;
            }
        }
    }
}
