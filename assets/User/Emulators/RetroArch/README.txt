RetroArch — user overrides
===========================

Files here override the shipped Defaults/Emulators/RetroArch/ data.

File naming: {CoreDisplayName}.xml
  The core display name must match the name RetroArch reports in its .info file.

Each file declares which controller variants a core supports and maps
RetroArch device-type IDs to controller variant names defined in
Controllers/{Platform}.xml. Override a file here if a core supports a variant
not covered by the shipped declaration.

Contributing
------------
If you add or correct a core declaration, please consider submitting it so it
ships as a default for everyone. Open a pull request or issue at:
https://github.com/tmstedman/launchbox-dynamic-controls
