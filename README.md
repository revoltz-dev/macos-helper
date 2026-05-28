# macOS Helper

A Windows application that downloads official macOS installers straight from Apple and creates bootable USB drives to install or reinstall macOS on a Mac.

Everything in a single window — no Terminal, no scripts, no Hackintosh tutorials required.

---

## What it does

- **Detects your Mac model** from its `Model Identifier` (About This Mac → System Information) and tells you the latest macOS version that's compatible.
- **Downloads the official installer from Apple** — picking from a full catalog ranging from OS X Lion all the way to macOS Tahoe (the latest).
- **Automatically filters** versions to those compatible with your Mac, so you don't end up downloading something that won't even install.
- **Creates the bootable USB** in one click. The drive works directly on the Mac: hold ⌥ Option at startup and pick the installer.

---

## Who it's for

- People with a **Mac that has no system**, a wiped disk, or a broken macOS install, and need to reinstall.
- People who only have a **Windows PC** at hand and can't use the traditional Mac method to make a USB.
- People who want to **roll back to an older macOS** (downgrade) using a freshly built USB.
- People who want to **try out betas** (Public, Customer Seed, or Developer Beta).

---

## How to use

1. **Open** `MacOSHelper.exe`. It will ask for administrator permission — that's required because it writes directly to the USB drive.
2. Click **Detect Mac**, paste the output of the `system_profiler` command (or just the `Model Identifier`), and see which macOS version is compatible.
3. Click **Catalog** → **Load Catalog**. Choose between Public Release, Public Beta, Customer Seed, or Developer Beta.
4. Pick the macOS version you want and click **Download**. The download is resumable — if it drops, just resume it.
5. Plug in the USB drive (minimum **16 GB**) and go back to the main screen.
6. Pick the USB in the **USB Drive** dropdown, pick the downloaded **Installer**, and click **Create Bootable USB**.
7. Confirm. In a few minutes the USB is ready. **Everything on the USB will be wiped.**

---

## On the Mac

1. Power on the Mac holding the **⌥ Option (Alt)** key.
2. Pick the **macOS installer** icon from the boot menu.
3. Use **Disk Utility** to erase and format the disk as APFS (or HFS+ on older Macs).
4. Go back and choose **Install macOS**.

### If you see "The recovery server could not be contacted"

This error is caused by an Apple bug in High Sierra (10.13) — they updated the server certificates, but the Recovery environment's security framework can no longer validate HTTPS. **Simple fix:** open **Utilities → Terminal** and paste this command (one line):

```
nvram IASUCatalogURL="http://swscan.apple.com/content/catalogs/others/index-10.13-10.12-10.11-10.10-10.9-mountainlion-lion-snowleopard-leopard.merged-1.sucatalog"
```

The trick is swapping `https://` for `http://` (no `S`), which skips the broken SSL check. Close Terminal and click **Reinstall macOS** again.

For other macOS versions the approach is the same — just adjust the `10.13` in the URL to the version you're installing (e.g., `10.14` for Mojave).

---

## Requirements

- **Windows 10 or 11** (64-bit)
- **Administrator permission** (it's prompted automatically)
- **USB drive of 16 GB or larger** (all data on it will be wiped)
- Internet connection to download the installer (4–14 GB depending on the version)

---

## Download

Grab the latest build from **[Releases](../../releases)** — it's a single `.exe`, no installation needed.

---

## Notes

- The app **does not run on macOS** — it's specifically for when you only have Windows around.
- Full bootable USB creation works up to **macOS Catalina (10.15)**. Big Sur and newer (11+) can be downloaded, but USB creation isn't supported for those versions yet. For Apple Silicon Macs, booting from a USB installer isn't supported by Apple itself — use the Mac's built-in Internet Recovery instead.
- Installers are saved in a `Downloads/` folder next to the `.exe`, so you can reuse them without downloading again.

