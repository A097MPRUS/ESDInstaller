# Changelog

## 0.1.15 test release — 2026-09-19

Automated checks passed, but these changes have not yet been tested through a full Windows installation. Try them in a disposable virtual machine first.

### All editions

- When an installation stops, the real reason is now shown instead of a generic "stopped with exit code" message.
- Worker exit codes are explained in plain words, for example when administrator rights were not granted.

### Windows 10/11

- An unanswered administrator prompt is now reported as the installer not starting, instead of "unexpected error".

### Windows 7 and Windows 8/8.1

- Fixed the installation process occasionally closing without any message partway through an installation.
- DiskPart errors are now reported with their actual text, and a warning is logged if the destination does not look formatted as requested.
- A damaged or incomplete ISO is now reported as possibly damaged instead of as an unsupported format.

### Windows 8/8.1

- The chosen edition's Windows version is no longer left out of the approved installation plan.

## 0.1.14 test release — 2026-09-16

Automated checks passed, but these changes have not yet been tested through a full Windows installation. Try them in a disposable virtual machine first.

### All editions

- Stops the app from erasing the partition that contains your installation image.
- Checks that the installer uses exactly the installation choices you approved.
- Improved checks for unavailable, read-only, or unsuitable installation partitions.
- Update downloads that stop responding or are unexpectedly large now fail instead of hanging, and the downloaded update is checked again right before it starts.
- Fixed update checks getting stuck after the computer's clock changes, and separate update downloads overwriting each other.
- Stopped update prompts from interrupting a Windows installation.
- Installation commands that stop responding are now stopped instead of leaving the installation waiting forever.

### Windows 10/11

- Fixed repeated clicks and disk refreshes leaving the app with outdated selections.
- Improved handling of file-opening errors and cancelled operations.

### Windows 7 and Windows 8/8.1

- Fixed two different ISO files with the same name possibly sharing extracted files; a damaged or changed extracted image is now detected before installation.
- Fixed repeated clicks, refreshes and image selections leaving outdated selections, and a possible crash when reading disks failed.
- Improved drive-letter checks so installation stops if the destination cannot be confirmed.

Keep the image on a separate local drive from the destination. WIM and ESD files (and any image on Windows 10/11) on network drives are blocked. No new languages or interface redesign are included. The installers remain unsigned.

## Unreleased

### Fixed

- Fixed ordinary Windows 7 NTFS partitions being classified as EFI system partitions because `Installable File System` contains the word `System`.
- Fixed the Windows 7 BitLocker check so a fully decrypted volume with protection off is not blocked merely because it is BitLocker-capable.
- Fixed Windows 7 ISO/WIM/ESD drag-and-drop by handling supported file drops at the page preview level.
- Replaced the Windows 7 edition-list deployment glyph with a cleaner Windows 7-era image mark.
- Updated Windows 7 executable and installer metadata to identify `A097MPRUS`.

### Added

- Added optional update notifications to the Windows 10/11, Windows 8/8.1, and Windows 7 editions without changing their existing installation workflows or visual designs.
- Added quiet startup checks limited to once every 24 hours, an enabled-by-default automatic-check setting, and a manual **Check for Updates** action.
- Added platform-specific update manifests, semantic-version comparison, real download progress, cancellation, HTTPS enforcement, and mandatory SHA-256 verification before an installer can run.
- Added localized update interface text to all 42 application languages.
- Added Arabic, Hebrew, Persian, Afrikaans, Hungarian, Portuguese, Czech, Cyrillic Uyghur, Turkish, Thai, Korean, Japanese, Georgian, Azerbaijani, Traditional Chinese, Norwegian Nynorsk, Kyrgyz, Italian, Romanian, and Icelandic to both the application and installer in every supported edition.
- Renamed the existing Norwegian entry to Norwegian Bokmål and retained the existing Spanish and Simplified Chinese translations.
- Added right-to-left application layout handling for Arabic, Hebrew, and Persian.

## Latest release — 2026-08-10

### Branding

- Renamed the product to **ESD Installer** across the Windows 10/11, Windows 8/8.1, and Windows 7 editions.
- Renamed application and worker executables, assemblies, namespaces, projects, settings paths, logs, installer identities, shortcuts, and release artifacts consistently.

### Fixed

- Fixed the Windows 10/11 **Review Installation** button so it navigates reliably and records navigation failures in the log.
- Fixed the Windows 10/11 installer omitting the elevated installation worker required to perform deployment.
- Fixed overlapping Back, selected-disk summary, and Next controls on the Windows 8/8.1 destination page.
- Fixed Windows 8/8.1 WIM/ESD inspection failures caused by incorrect native wimlib architecture packaging.
- Added the required down-level Universal CRT files to the Windows 8/8.1 installer to prevent missing `api-ms-win-crt-*.dll` startup errors.
- Fixed clipped headings in localized setup and uninstall wizard pages.

### Languages added

The following languages were added to both the application and installer in every supported edition:

- Norwegian
- Finnish
- Swedish
- Mongolian (Cyrillic)
- Armenian
- Kazakh
- Bashkir
- Tatar
- Crimean Tatar
- Abkhazian
- Ossetian
