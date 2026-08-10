# Mailto Picker

A small Windows utility that intercepts `mailto:` links, asks which Gmail
account you want to send from, and opens Gmail's compose window in your default
browser. No tray icon, no background service: it starts, does one job, exits.

## Build

Requires the .NET 8 SDK.

```bash
dotnet publish MailtoPicker.csproj -c Release -o publish
```

That produces `publish\MailtoPicker.exe`. Put the `publish` folder somewhere
permanent before registering it: the registry entries point at the exact exe
path, so moving it afterwards breaks the association.

## Use

Run `MailtoPicker.exe` with no arguments to open settings. Add one entry per
Gmail account:

- **Display name** — anything you like; this is what the picker lists.
- **Account index** — the `N` in `https://mail.google.com/mail/u/N/`. It is the
  order Google signed the accounts in, starting at 0. To find yours, open Gmail
  for each account and read the number out of the address bar.

Then click **Register as mailto handler...** and, when prompted, open
Settings > Apps > Default apps and set MAILTO to "Mailto Picker". Windows blocks
apps from making themselves the default handler (the UserChoice key is protected
by UCPD.sys), so that one manual step is unavoidable.

When a `mailto:` link is clicked, the picker appears with the account you used
last already highlighted:

- **Enter**, or clicking the highlighted row, opens Gmail immediately.
- Clicking a different row selects it; click again (or press Enter) to send.
- **Esc** cancels and nothing happens.

## Config

`%AppData%\MailtoPicker\config.json`

```json
{
  "Accounts": [
    { "Name": "Personal", "AccountIndex": 0 },
    { "Name": "Work", "AccountIndex": 1 }
  ],
  "LastUsedAccount": "Work"
}
```

Written on first run if absent. If the file is corrupt the app says so and
offers to reset it rather than failing silently.

## Files

| File | Role |
| --- | --- |
| `Program.cs` | Entry point, argument handling, error dialogs |
| `MailtoRequest.cs` | RFC 2368 parsing and Gmail compose URL building |
| `Config.cs` | `config.json` load/save |
| `PickerForm.cs` | The account chooser |
| `SettingsForm.cs` | Account list editor |
| `AccountDialog.cs` | Add/edit one account |
| `Registration.cs` | HKCU registry entries and the Settings deep link |

## Note on the forms

There are no `.Designer.cs` files: the three forms are written by hand. They
still follow the shape the designer emits, because WinForms depends on it.

Each constructor wraps its body in `SuspendLayout()` / `ResumeLayout(false)` and
declares `AutoScaleDimensions` + `AutoScaleMode` inside that block. That is not
decoration. Assigning `AutoScaleMode` runs the scaling pass *immediately*, so
declaring it before the controls exist scales an empty form, stamps
`AutoScaleDimensions` to the current value, and leaves every later measurement
treated as already-scaled — the window then keeps its literal 96 DPI pixel sizes
while the fonts grow, and the layout comes out cramped and clipped. Suspending
layout defers the pass until the controls and `ClientSize` are set.

All coordinates in the constructors are therefore in 96 DPI units.
`AutoScaleMode.Dpi` is used rather than the more common `Font`, because `Font`
scales against a baseline the designer normally *measures* on the machine that
generated the file. With no designer there is nothing to measure, and an
invented baseline scales the axes by different amounts; 96 DPI is exact and
uniform by definition.

Auto-scaling only ever touches `Location`, `Size` and `Font`. Two things here
fall outside that and are scaled by hand off `LogicalToDeviceUnits`, from both
`OnLoad` and `OnDpiChanged` so they survive a drag to a different-DPI monitor:

- `ListView` column widths in the settings window.
- The picker's list height, which has to be a whole number of rows. `ItemHeight`
  follows the font, so a scaled pixel height drifts out of step with it.

## Registry keys written

All under `HKEY_CURRENT_USER`, no admin rights needed:

- `Software\Classes\mailto` (+ `shell\open\command`, `DefaultIcon`)
- `Software\Classes\MailtoPicker.Url.Mailto` (the ProgID the Capabilities entry
  points at)
- `Software\MailtoPicker\Capabilities` (+ `UrlAssociations\mailto`)
- `Software\RegisteredApplications` → value `MailtoPicker`

The **Unregister** button in settings removes all of them. Registering first
stashes whatever `Software\Classes\mailto` held beforehand, and unregistering
puts it back, so installing and removing this app leaves an existing handler
untouched. It never writes the `UserChoice` key that decides the actual default
— Windows blocks that at the driver level, and trying would be pointless.
