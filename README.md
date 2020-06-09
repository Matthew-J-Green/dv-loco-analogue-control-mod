# dv-loco-analogue-control-mod
Derail Valley mod for analogue control in locomotives

99% Sure this wont work with VR

## Using the Mod

 - Install Unity Mod Manager
 - Copy the `LocoAnalogueControlMod.dll` and `Info.json` into the `Derail Valley\Mods\LocoAnalogueControlMod` folder
 - Run Derail Valley to generate `config.json` in the mod folder
 - Update `config.json` for each input.
	 - `AxisName` corresponds to the axis on your controller

	| Axis Number | Axis Name |
	|--|--|
	| 1 | Oculus_GearVR_LThumbstickX |
	| 2 | Oculus_GearVR_LThumbstickY |
	| 3 | Oculus_GearVR_RThumbstickX |
	| 4 | Oculus_GearVR_RThumbstickY |
	| 5 | Oculus_GearVR_DpadX |
	| 6 | Oculus_GearVR_DpadY |
	| 11 | Oculus_GearVR_LIndexTrigger |
	| 12 | Oculus_GearVR_RIndexTrigger |

	 - `Invert` inverts the axis
	 - `FullRange` takes a -1 to 1 range and maps it to 0 to 1
	 - `Debug` enables printing the value being applied to the mod manager log. Enable this for first time setup.
 - Reboot Derail Valley and hop into a locomotive
 - With the mod manager log window open (`ctrl + F10`) move your controller axis and observe the output in the log window
	 - The reverser axis should be from -1 to 1. All other axis are from 0 to 1
 - Correctly set the `config.json` and reboot Derail Valley