using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using NightVision.Patches;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightVision
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PLUGIN_GUID = "Ken.NightVision";
        private const string PLUGIN_NAME = "Toggleable Night Vision";
        private const string PLUGIN_VERSION = "1.0.0";

        public static Harmony _harmony = new Harmony("NightVision");
        internal static ManualLogSource mls;

        private static ConfigEntry<string> cfgKeyBind;
        public static ConfigEntry<float> intensity;

        private void Awake()
        {
            _harmony.PatchAll(typeof(NightVisionPatch));
            mls = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_GUID);
            mls.LogInfo("NightVision loaded");

            cfgKeyBind = Config.Bind("Toggle Button", "Key", "C",
                                     "Button to toggle night vision mode in the bunker.");
            intensity = Config.Bind("Numeric Values", "Intensity", 7500f,
                                    "Intensity of the night vision when toggled. [Originally was 100000]");

            var insertKeyAction = new InputAction(binding: $"<Keyboard>/{cfgKeyBind.Value}");
            insertKeyAction.performed += OnInsertKeyPressed;
            insertKeyAction.Enable();
        }

        private void OnInsertKeyPressed(InputAction.CallbackContext obj)
        {
            NightVisionPatch.toggled = !NightVisionPatch.toggled;
            mls.LogDebug($"Night mode {(NightVisionPatch.toggled ? "enabled" : "disabled")}");
        }
    }
}

namespace NightVision.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class NightVisionPatch
    {
        internal static bool toggled = false;
        private static MethodInfo TargetMethod()
        {
            return typeof(PlayerControllerB).GetMethod("SetNightVisionEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [HarmonyPrefix]
        private static void Prefix(PlayerControllerB __instance)
        {
            if (toggled)
            {
                __instance.nightVision.intensity = NightVision.Plugin.intensity.Value;
                __instance.nightVision.range = 100000f;
                __instance.nightVision.shadowStrength = 0f;
                __instance.nightVision.shadows = (LightShadows)0;
                __instance.nightVision.shape = (LightShape)2;
            }
            else
            {
                __instance.nightVision.intensity = 366.9317f;
                __instance.nightVision.range = 12f;
                __instance.nightVision.shadowStrength = 1f;
                __instance.nightVision.shadows = (LightShadows)0;
                __instance.nightVision.shape = (LightShape)0;
            }
        }
    }
}