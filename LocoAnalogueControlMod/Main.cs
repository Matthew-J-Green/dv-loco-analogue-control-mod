using System;
using System.Reflection;
using UnityModManagerNet;
using Harmony12;
using UnityEngine;
using Newtonsoft.Json;

namespace LocoAnalogueControlMod
{
    static class Main
    {
        private const string configName = "config.json";

        public static UnityModManager.ModEntry mod;
        public static Config config = new Config();

        public static bool hasFocus, hasFocusPrev = false;

        private static LocomotiveRemoteController HoldingLocoRoCo;
        private static string configPath = null;
        private static bool listenersSetup = false;

        private static Input throttleInput = new Input();
        private static Input reverserInput = new Input();
        private static Input trainBrakeInput = new Input();
        private static Input independentBrakeInput = new Input();
        private static Input fireDoorInput = new Input();
        private static Input InjectorInput = new Input();
        private static Input DraftInput = new Input();
        private static Input BlowerInput = new Input();
        private static Input SanderValveInput = new Input();
        private static Input SteamReleaseInput = new Input();
        private static Input WaterDumpInput = new Input();
        private static Input WhistleInput = new Input();

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            mod = modEntry;
            configPath = mod.Path + configName;

            mod.OnUpdate = OnUpdate;
            mod.OnToggle = OnToggle;

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
                // Get remote or local loco
                LocoControllerBase locoController = null;
                if (HoldingLocoRoCo != null)
                {
                    // Go get some private fields from the currently held locomotive remote
                    bool isPoweredOn = (bool)typeof(LocomotiveRemoteController).GetField("isPoweredOn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(HoldingLocoRoCo); ;
                    float lostSignalSecondsLeft = (float)typeof(LocomotiveRemoteController).GetField("lostSignalSecondsLeft", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(HoldingLocoRoCo);

                    // Implement the logic for understanding if the pairedLocomotive is valid.
                    // This is normally done in the LocomotiveRemoteController.Transmit method but is easier to do it here so we can re-use the analogue input logic.
                    if (isPoweredOn && lostSignalSecondsLeft == 0f)
                    {
                        locoController = (LocoControllerBase)typeof(LocomotiveRemoteController).GetField("pairedLocomotive", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(HoldingLocoRoCo);
                    }
                }
                else
                {
                    locoController = PlayerManager.Car?.GetComponent<LocoControllerBase>();
                }

                // Do the actual updating
                if (locoController != null)
                {
                    throttleInput.SetItem(config.Throttle, locoController.SetThrottle);
                    reverserInput.SetItem(config.Reverser, locoController.SetReverser);
                    trainBrakeInput.SetItem(config.TrainBrake, locoController.SetBrake);
                    independentBrakeInput.SetItem(config.IndependentBrake, locoController.SetIndependentBrake);

                    if (locoController.GetType().Name.Equals("LocoControllerSteam"))
                    {
                        // All the steam loco stuff really doesnt like being set
                        // Visuals dont update but the logic is correct ;(
                        LocoControllerSteam locoControllerSteam = locoController as LocoControllerSteam;

                        // Whistle resets every sim tick so just override it
                        WhistleInput.SetItem(config.Whistle, locoControllerSteam.SetWhistle, 0f);

                        fireDoorInput.SetItem(config.FireDoor, locoControllerSteam.SetFireDoorOpen);
                        InjectorInput.SetItem(config.Injector, locoControllerSteam.SetInjector);
                        DraftInput.SetItem(config.Draft, locoControllerSteam.SetDraft);
                        BlowerInput.SetItem(config.Blower, locoControllerSteam.SetBlower);
                        SanderValveInput.SetItem(config.SanderValve, locoControllerSteam.SetSanderValve);
                        SteamReleaseInput.SetItem(config.SteamRelease, locoControllerSteam.SetSteamReleaser);
                        WaterDumpInput.SetItem(config.WaterDump, locoControllerSteam.SetWaterDump);
                    }
                }
            }
        }

        // Need to know when we have grabbed a Locomotive Remote
        // Actual Grab Handlers
        static void OnItemGrabbedRight(InventoryItemSpec iis) { HoldingLocoRoCo = iis?.GetComponent<LocomotiveRemoteController>(); }
        static void OnItemUngrabbedRight() { HoldingLocoRoCo = null; }
        // Grab Listeners
        static void OnItemAddedToInventory(GameObject o, int _) { OnItemUngrabbedRight(); }
        static void OnItemGrabbedRightNonVR(GameObject o) { OnItemGrabbedRight(o.GetComponent<InventoryItemSpec>()); }
        static void OnItemUngrabbedRightNonVR(GameObject o) { OnItemUngrabbedRight(); }
    }

    public class Input
    {
        private float currentInput, previousInput = 0.0f;
        private bool inDeadZone, inDeadZonePrev, inDeadZoneChanged = false;

        public void SetItem(Config.Axis inputAxis, Action<float> setLocoAxis, float minDelta = 0.005f)
        {
            if (inputAxis != null && !inputAxis.AxisName.Equals(""))
            {
                // Update the current values as regaining focus also sets axis to 50%
                if (!Main.hasFocusPrev && Main.hasFocus)
                {
                    previousInput = GetValue(inputAxis);
                    inDeadZoneChanged = false;
                }

                currentInput = GetValue(inputAxis);

                // Only update item if we have changed by an amount or we enter/exit deadzones
                // This allows other input methods to still be used
                // minDelta should probably be configurable in case user inputs are more jittery when left in a fixed position
                if ((Math.Abs(previousInput - currentInput) > minDelta) || inDeadZoneChanged)
                {
                    setLocoAxis(currentInput);
                    if (inputAxis.Debug) Main.mod.Logger.Log(string.Format("Axis: {0}, Value: {1}", inputAxis.AxisName, currentInput));
                    previousInput = currentInput;
                }
            }
        }

        // Might be better to use direct input for better control over attached devices
        private float GetValue(Config.Axis axis)
        {
            // Should use a mapping from the AxisName to an axis number cause this axis entered might not exist
            float value = UnityEngine.Input.GetAxisRaw(axis.AxisName) * axis.Scaling;

            InDeadZone(axis, value);
            DeadZoneScaling(axis, ref value);

            if (axis.FullRange) value = (value + 1f) / 2f;

            return value;
        }

        private void DeadZoneScaling(Config.Axis axis, ref float value)
        {
            // Deadzone scaling
            if (Math.Abs(value) <= axis.DeadZoneCentral)
            {
                value = 0f;
            }
            else
            {
                float range = 1f - axis.DeadZoneCentral - axis.DeadZoneEnds;
                if (range == 0) value = 0;
                if (value > 0) value = Math.Min(1f, (value - axis.DeadZoneCentral) / range);
                else value = Math.Max(-1f, (value + axis.DeadZoneCentral) / range);
            }
        }

        private void InDeadZone(Config.Axis axis, float value)
        {
            inDeadZonePrev = inDeadZone;
            inDeadZone = Math.Abs(value) >= (1f - axis.DeadZoneEnds) || Math.Abs(value) <= axis.DeadZoneCentral;
            inDeadZoneChanged = inDeadZone != inDeadZonePrev;
        }
    }

    // Probably a better way to write this
    public class Config
    {
        public Axis Throttle { get; set; } = new Axis();
        public Axis Reverser { get; set; } = new Axis();
        public Axis TrainBrake { get; set; } = new Axis();
        public Axis IndependentBrake { get; set; } = new Axis();
        public Axis FireDoor { get; set; } = new Axis();
        public Axis Injector { get; set; } = new Axis();
        public Axis Draft { get; set; } = new Axis();
        public Axis Blower { get; set; } = new Axis();
        public Axis SanderValve { get; set; } = new Axis();
        public Axis SteamRelease { get; set; } = new Axis();
        public Axis WaterDump { get; set; } = new Axis();
        public Axis Whistle { get; set; } = new Axis();

        public class Axis
        {
            public string AxisName { get; set; } = "";
            public bool FullRange { get; set; } = false;
            public float Scaling { get; set; } = 1f;
            public float DeadZoneCentral { get; set; } = 0f;
            public float DeadZoneEnds { get; set; } = 0f;
            public bool Debug { get; set; } = false;
        }
    }
}