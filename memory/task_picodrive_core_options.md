---
name: task-picodrive-core-options
description: Future task — read RetroArch core option keys from cfg cascade to detect PicoDrive controller type
metadata:
  type: project
---

Read core option keys (e.g. `picodrive_input1 = "6 button pad"`) from the RetroArch cfg cascade to detect controller variant for PicoDrive and other cores that use core options rather than `input_libretro_device_p1` for controller type selection.

**Why:** PicoDrive (Sega 32X, Sega CD) selects 3-button vs 6-button pad via a core option string, not via `input_libretro_device_p1`. The current plugin mechanism only reads `input_libretro_device_p1` so PicoDrive controller variants are invisible to the plugin.

**How to apply:** Requires extending the cfg cascade reader to also extract core option keys, and a new mechanism in the RetroArch source to map core option values to controller variant names (analogous to how device type IDs map to variants in the core XML files).
