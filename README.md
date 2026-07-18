# DistroClip

DistroClip is a small always-on-top Windows clipboard for music distribution work. It searches your release folders, reads contract and Easy Song PDFs locally, and gives you copyable text plus draggable artwork and audio.

## Start

Double-click `DistroClip.exe`. Run it normally, not as administrator; Windows can block drag-and-drop from an administrator app into a normal browser.

This build is a single 5.7 MB executable for this PC. It requires Windows 10/11 x64 and the Microsoft .NET 8 Desktop Runtime x64, which is already installed here. If another PC asks for .NET, install the **.NET 8 Desktop Runtime x64** (not only the ordinary .NET Runtime) from <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>.

## Use

1. Type any part of an artist name or track title. Accents and common special letters are search-friendly, so `saimoo` finds `Saimöö`, `pharo` finds `PHARØ`, and `ae` finds `Æ`.
2. Choose a result with the mouse, or use the arrow keys and Enter.
3. Click a track title, name chip, or credits block to copy it. The small copy button beside a name row copies the whole comma-separated line.
4. Drag the artwork or WAV card directly onto a distributor's browser file field. A simple click copies the file to the Windows clipboard; double-clicking opens it.
5. Click the release card at the top to open the release folder in Explorer — its tooltip shows the full path. Use the contract icon beside the release heading to open its PDF.

The window starts at the right edge of the screen and can be dragged by its title bar. Its position is remembered. Hover over the thin bottom edge when you want to see the current index status and release count.

An amber **COVER** badge appears when a root-level Easy Song Proof of Licensing PDF is present. Click the badge to open that license, or drag the badge onto a distributor's browser file field to hand over the license PDF, exactly like the artwork and WAV cards. Songwriter names then appear from its `By ...` block. Warnings are shown when a PDF is unreadable or a choice needs verification.

For covers, an **Original artist** section fills in automatically. The license itself never names the original performer, so DistroClip looks up the licensed song title and songwriters on MusicBrainz and Wikidata and shows the original artist as a copyable chip (hover it to see the matched song and first release date). Each song is looked up once and cached in `original-artists.json`, so revisiting a release is instant and offline. If the lookup cannot identify the song, a *Search the web* button opens a browser search instead. Always sanity-check the result before submitting it.

## Search folders

The default parent folders are:

- `Y:\Unreleased Ext Drive`
- `Y:\Unreleased Ext Drive\[P Tracks`
- `Y:\Releases Ext Drive`

Use the gear button to add or remove parent folders. Each immediate child of a configured folder is treated as a release folder. Use the refresh button after changing files while the app is open.

DistroClip inspects release-root files only. It never enters a directory whose name is exactly `Old`, case-insensitively. Nested WAVs and images are therefore not mistaken for current deliverables.

The WAV card draws the master's real waveform: the faint outline is peak level, the solid core is RMS energy, so song sections and over-squashed masters are visible at a glance. Beside it are integrated loudness (LUFS, BS.1770; sample peak and 4x-oversampled true peak live in its tooltip) and, bottom-right, the leading/trailing silence below −60 dBFS as `head/tail` seconds. Amber means "look closer": silence longer than this library's norm, track length outside 2:00–3:00, a master quieter than −12 LUFS, or true peak over 0 dBTP. Red means samples at full scale — clipping — and the affected waveform bars are drawn red too.

Artwork is fingerprinted in the background (a perceptual hash per release, cached in `artwork-hashes.json` — the first pass over a large library takes a few minutes, then it's incremental). If a release's cover is a near-duplicate of another release's — same file, re-export, resize, or light edit — a **≈ SIMILAR** badge appears on the artwork card; hover it to see which release it matches and click it to open that folder. The threshold was calibrated against this library so unrelated covers never trigger it.

**Ctrl+click** the waveform to listen from that spot. While playing, plain clicks and the mouse wheel seek, and the small pause glyph (or another Ctrl+click) pauses. Once paused, plain clicks return to normal: copy the file, or drag it to a browser. Playback stops automatically when you switch releases.

## Master and artwork rules

- WAV versions are compared as integer tuples: `M2` beats `M1`, and `M1.10` beats `M1.2`.
- At the same version, the bare mix is preferred over Instrumental, Acapella, TV Mix, Extended Mix, or Radio Edit.
- A WAV with no M-version is not selected automatically; the app warns you instead.
- Square, high-resolution artwork is preferred. Social banners, revenue-share request images, mockups, and V Layers files are excluded.

## Assign a keyboard shortcut

1. First move the DistroClip folder to a permanent local location. Right-click `DistroClip.exe`, choose **Show more options**, then **Send to > Desktop (create shortcut)**.
2. Right-click the new desktop shortcut and choose **Properties**.
3. Click **Shortcut key**, press the key combination you want (for example `Ctrl+Alt+R`), then click **OK**.

DistroClip is single-instance. Launching it through the shortcut while it is already open closes it; launching it again opens it. This makes one shortcut act as an open/close toggle.

## Local files

Settings, the crash log, and the original-artist cache are stored under `%LocalAppData%\DistroClip`. No release, contract, or usage data is uploaded anywhere by the app. The one exception: the first time a cover release is opened, its licensed song title and songwriter names are sent to MusicBrainz (musicbrainz.org) and Wikidata to identify the original recording; the answer is cached locally so the question is asked only once per song.

## Troubleshooting

- If a drive is disconnected, hover over the bottom edge to see the unavailable-folder status. Reconnect it and press refresh.
- If drag-and-drop does not work, close the app and browser, then reopen both normally (not as administrator).
- If a legal name contains a PDF-reading warning, open the linked contract and verify it before copying.
