using System;
using System.Reflection;
using UnityModManagerNet;
using Harmony12;
using UnityEngine;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

namespace LocoAnalogueControlMod
{
    static class Main
    {
        private const string configName = "config.json";

        public static UnityModManager.ModEntry mod;
        public static Config config = new Config();

        private static LocomotiveRemoteController LocoRoCo;
        private static string configPath = null;
        private static bool listenersSetup = false;
        private static bool hasFocus, hasFocusPrev = false;
        private static float throttleVal, reverserVal, trainBrakeVal, independentBrakeVal = 0.0f;
        private static float throttleValPrev, reverserValPrev, trainBrakeValPrev, independentBrakeValPrev = 0.0f;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            mod = modEntry;
            configPath = mod.Path + configName;

            mod.OnUpdate = OnUpdate;
            mod.OnToggle = OnToggle;

            LoadConfig();

            return true;
        }

        static void LoadConfig()
        {
            if (System.IO.File.Exists(configPath))
            {
                mod.Logger.Log(string.Format("Loading {0}", configPath));
                try { config = JsonConvert.DeserializeObject<Config>(System.IO.File.ReadAllText(configPath)); }
                catch (Exception ex) { mod.Logger.Log(string.Format("Could not load {0}. {1}", configPath, ex)); }
            }
            else
            {
                mod.Logger.Log(string.Format("Could not find {0}. Creating it.", configPath));
                try { System.IO.File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented)); }
                catch (Exception ex) { mod.Logger.Log(string.Format("Could not write {0}. {1}", configPath, ex)); }
            }
        }

        static bool OnToggle(UnityModManager.ModEntry _, bool startMod)
        {
            // UnityModManager.ModEntry.Enabled is changed automatically
            // Can we set/unset UnityModManager.ModEntry.OnUpdate dynamically instead?
            if (startMod) LoadConfig();

            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry mod, float delta)
        {
            // Can we set/unset UnityModManager.ModEntry.OnUpdate dynamically instead?
            if (!mod.Enabled) return;
            if (!listenersSetup)
            {
                // Gotta wait until we are loaded until registering the listeners
                if (LoadingScreenManager.IsLoading || !WorldStreamingInit.IsLoaded || !InventoryStartingItems.itemsLoaded) return;

                Grabber grab = PlayerManager.PlayerTransform.GetComponentInChildren<Grabber>();
                grab.Grabbed += OnItemGrabbedRightNonVR;
                grab.Released += OnItemUngrabbedRightNonVR;
                SingletonBehaviour<Inventory>.Instance.ItemAddedToInventory += OnItemAddedToInventory;

                mod.Logger.Log("Listeners have been set up.");
                listenersSetup = true;
            }

            // For some reason the axis defaults to 50% on loss of focus. Stop any inputs when that happens
            hasFocusPrev = hasFocus;
            hasFocus = Application.isFocused;

            if (hasFocus)
            {
                // Update the current values as regaining focus also sets axis to 50%
                // We might be able to use the Application.focusChanged event instead
                if (!hasFocusPrev && hasFocus)
                {
                    throttleVal = config.Throttle.GetValue();
                    reverserVal = config.Reverser.GetValue();
                    trainBrakeVal = config.TrainBrake.GetValue();
                    independentBrakeVal = config.IndependentBrake.GetValue();
                }

                // Get remote or local loco
                LocoControllerBase locoController = null;
                if (LocoRoCo != null)
                {
                    // Go get some private fields from the currently held locomotive remote
                    bool isPoweredOn = (bool)typeof(LocomotiveRemoteController).GetField("isPoweredOn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(LocoRoCo); ;
                    float lostSignalSecondsLeft = (float)typeof(LocomotiveRemoteController).GetField("lostSignalSecondsLeft", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(LocoRoCo);

                    // Implement the logic for understanding if the pairedLocomotive is valid. This is normally done in the LocomotiveRemoteController.Transmit method but is easier to do it here so we can re-use the analogue input logic.
                    if (isPoweredOn && lostSignalSecondsLeft == 0f)
                    {
                        locoController = (LocoControllerBase)typeof(LocomotiveRemoteController).GetField("pairedLocomotive", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(LocoRoCo);
                    }
                }
                else
                {
                    locoController = PlayerManager.Car?.GetComponent<LocoControllerBase>();
                }

                // Do the actual updating
                // Clean up this mess of code
                if (locoController != null || LocoRoCo != null)
                {
                    // Write a method you pleb
                    if (config.Throttle != null && !config.Throttle.AxisName.Equals(""))
                    {
                        // Only update when input has changed to prevent locking out any other controls to these axis
                        throttleValPrev = throttleVal;
                        throttleVal = config.Throttle.GetValue();
                        if (Math.Abs(throttleValPrev - throttleVal) > 0.001f)
                        {
                            locoController?.SetThrottle(throttleVal);
                            if (config.Throttle.Debug) Main.mod.Logger.Log(string.Format("Axis: {0}, Value: {1}", config.Throttle.AxisName, throttleVal));
                        }
                    }

                    if (config.Reverser != null && !config.Reverser.AxisName.Equals(""))
                    {
                        reverserValPrev = reverserVal;
                        reverserVal = config.Reverser.GetValue();
                        if (Math.Abs(reverserValPrev - reverserVal) > 0.001f)
                        {
                            locoController?.SetReverser(reverserVal);
                            if (config.Reverser.Debug) Main.mod.Logger.Log(string.Format("Axis: {0}, Value: {1}", config.Reverser.AxisName, reverserVal));
                        }
                    }

                    if (config.TrainBrake != null && !config.TrainBrake.AxisName.Equals(""))
                    {
                        trainBrakeValPrev = trainBrakeVal;
                        trainBrakeVal = config.TrainBrake.GetValue();
                        if (Math.Abs(trainBrakeValPrev - trainBrakeVal) > 0.001f)
                        {
                            locoController?.SetBrake(trainBrakeVal);
                            if (config.TrainBrake.Debug) Main.mod.Logger.Log(string.Format("Axis: {0}, Value: {1}", config.TrainBrake.AxisName, trainBrakeVal));
                        }
                    }

                    if (config.IndependentBrake != null && !config.IndependentBrake.AxisName.Equals(""))
                    {
                        independentBrakeValPrev = independentBrakeVal;
                        independentBrakeVal = config.IndependentBrake.GetValue();
                        if (Math.Abs(independentBrakeValPrev - independentBrakeVal) > 0.001f)
                        {
                            locoController?.SetIndependentBrake(independentBrakeVal);
                            if (config.IndependentBrake.Debug) Main.mod.Logger.Log(string.Format("Axis: {0}, Value: {1}", config.IndependentBrake.AxisName, independentBrakeVal));
                        }
                    }
                }
            }
        }

        // Need to know when we have grabbed a Locomotive Remote
        // Actual Grab Handlers
        static void OnItemGrabbedRight(InventoryItemSpec iis) { LocoRoCo = iis?.GetComponent<LocomotiveRemoteController>(); }
        static void OnItemUngrabbedRight(InventoryItemSpec iis) { LocoRoCo = null; }
        // Grab Listeners
        static void OnItemAddedToInventory(GameObject o, int _) { OnItemUngrabbedRight(o.GetComponent<InventoryItemSpec>()); }
        static void OnItemGrabbedRightNonVR(GameObject o) { OnItemGrabbedRight(o.GetComponent<InventoryItemSpec>()); }
        static void OnItemUngrabbedRightNonVR(GameObject o) { OnItemUngrabbedRight(o.GetComponent<InventoryItemSpec>()); }
    }

    // Probably a better way to write this
    public class Config
    {
        public Axis Throttle { get; set; } = new Axis();
        public Axis Reverser { get; set; } = new Axis();
        public Axis TrainBrake { get; set; } = new Axis();
        public Axis IndependentBrake { get; set; } = new Axis();

        public class Axis
        {
            public string AxisName { get; set; } = "";
            public bool Inversed { get; set; } = false;
            public bool FullRange { get; set; } = false;
            public bool Debug { get; set; } = false;

            public float GetValue()
            {
                // Should use a mapping from the AxisName to an axis number cause this axis entered might not exist
                float value = UnityEngine.Input.GetAxisRaw(AxisName);
                if (Inversed) value = -value;
                if (FullRange) value = (value + 1f) / 2f;

                return value;
            }
        }
    }
}