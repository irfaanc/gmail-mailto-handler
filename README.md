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

Run `MailtoPicker.exe` with no arguments to open settings. On a first run it
asks for an account straight away rather than inventing one, since it cannot do
anything without at least one. Each entry has:

- **Display name** — anything you like; this is what the picker lists.
- **Gmail address** — the account to send from.

The address is handed to Gmail as the `authuser` parameter, which picks the
mailbox by name:

```
https://mail.google.com/mail/u/0/?authuser=you@gmail.com&to=...&tf=cm
```

An earlier version selected the account with the `N` in `/mail/u/N/` instead.
That number is not a property of your account — it is a position in the list of
accounts signed into that browser profile, ordered by sign-in sequence. Sign out
of one, sign back in in a different order, or open the link in another browser,
and every N shifts. Storing it meant storing a pointer into someone else's
mutable list, and when it drifted the app silently composed from the wrong
account. It was removed rather than kept as a fallback: it is no more likely to
work than the address, so it bought nothing but a second path to maintain.

An address cannot drift. If it is wrong or that account is not signed in, Google
shows its account chooser, which is a visible failure before you have typed
anything rather than a surprise after you hit send.

Note `authuser` overrides the path, so the URL pins the path to `u/0`, which
always exists. Putting the stored index there would reintroduce the drift.

## Becoming the mail handler

The app writes its own registry entries silently on every launch. That part is
unguarded and reversible, and with no installer the first run *is* the
installation, so it is not worth a dialog.

Being registered only makes the app *selectable*, though. What decides where
mail links actually go is `UserChoice`:

```
HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\mailto\UserChoice
```

That value cannot be forged. Windows stores a `Hash` alongside it, derived from
the account SID, the protocol and the ProgID by an undocumented algorithm, and
discards any entry whose hash does not match. So an app cannot point UserChoice
at itself.

It can, however, *remove* it — and with no recorded choice Windows falls through
to `HKCU\Software\Classes\mailto`, which this app owns. That is what the **Set as
default mail handler** button does, and what the startup prompt offers whenever
something else holds the association. The prompt names the incumbent, because
this replaces the user's current mail app.

Two caveats:

- **It is not guaranteed to work.** UCPD, the User Choice Protection Driver,
  blocks these keys for the protocols it covers, and coverage varies by Windows
  build. It is running on the development machine yet does not cover `mailto`
  there — which is not evidence it never will. The result is therefore read back
  from the registry rather than inferred from the delete succeeding, and if it
  was refused the app falls back to walking you through Settings by hand.
- **Settings is the fallback, not the primary path.** If you do go that route:
  Settings > Apps > Default apps, type MAILTO into *"Set a default for a file
  type or link type"*, and use the MAILTO row that appears. The application list
  further down that page is the intuitive place to look and the wrong one.

The offer repeats on every launch while the app is not the handler. Handling
mail links is the only thing it does, so remembering a "no" would leave a
permanently useless app on the machine.

When a `mailto:` link is clicked, the picker appears:

- **Enter**, or clicking the highlighted row, opens Gmail immediately.
- Clicking a different row selects it; click again (or press Enter) to send.
- **Esc** cancels and nothing happens.

## Rules

The highlighted account is decided by a rule, if one matches the recipient.
Otherwise it is the first account in the list, which is why that list is
reorderable: the top entry is the default.

There is deliberately no "last account you used" memory. It would make the
default depend on invisible state left over from an unrelated message.

### Creating them

From the picker, not from an editor. Alongside the accounts there is a
**Remember** box offering to always use the chosen account for that address, or
for its whole domain. It defaults to remembering nothing, so a rule only ever
exists because it was asked for, and it resets on every launch so it cannot
quietly mint rules for later recipients.

Choosing again **replaces** an existing rule. That is also how a rule is edited:
send to that recipient again, pick the right account, and remember again.

### How they are matched

An exact address rule always beats a domain rule covering the same recipient.
Precedence is by specificity, so rules never need ordering and there is nothing
to drag up and down.

The recipient matched against is the first `To` address, or the first `Cc`/`Bcc`
if there is no `To`. A link can carry several recipients across different
domains, so this is a choice rather than a fact; the picker names the rule that
fired, so a surprising highlight can be traced.

### Seeing and removing them

**Rules...** in the settings window lists them, with a Remove button. There is no
add or edit there on purpose: the picker already does both, and a second
authoring path would only be a way for the two to disagree.

Editing an account's address repoints its rules. Deleting an account deletes the
rules aiming at it, and says how many before doing it.

## Config

`%AppData%\MailtoPicker\config.json`

```json
{
  "Accounts": [
    { "Name": "Personal", "EmailAddress": "you@gmail.com" },
    { "Name": "Work", "EmailAddress": "you@company.com" }
  ],
  "Rules": [
    { "Kind": "Domain",  "Match": "company.com",      "EmailAddress": "you@company.com" },
    { "Kind": "Address", "Match": "friend@company.com", "EmailAddress": "you@gmail.com" }
  ]
}
```

`Kind` is written by name rather than as a number so the file stays
hand-editable. Rules point at accounts by address, matching how accounts are
identified everywhere else.

An entry with no `EmailAddress` is dropped on load rather than left in the
picker to fail at send time, so a hand-edited config that is missing one just
means the settings window asks for a real account.

Created empty on first run if absent, and the settings window then asks for the
first account. It is deliberately not seeded with a guessed entry: a plausible
looking account nobody chose, with an index that may well be wrong, fails later
in a way that is hard to connect back to the cause.

The last account cannot be removed, for the same reason the first one is
demanded. If the file is corrupt the app says so and offers to reset it rather
than failing silently.

## Files

| File | Role |
| --- | --- |
| `Program.cs` | Entry point, argument handling, error dialogs |
| `MailtoRequest.cs` | RFC 2368 parsing and Gmail compose URL building |
| `EmailAddresses.cs` | Pulling a bare address out of a recipient field |
| `Config.cs` | `config.json` load/save, rules and matching |
| `PickerForm.cs` | The account chooser, and where rules are created |
| `SettingsForm.cs` | Account list editor |
| `AccountDialog.cs` | Add/edit one account |
| `RulesDialog.cs` | List and remove rules |
| `Registration.cs` | HKCU registry entries, self-healing, the Settings deep link |
| `RegistrationPrompt.cs` | The startup offer and post-registration walkthrough |

## If you move the app

Every registry entry stores an absolute path, so moving or renaming the folder
breaks the association. The app repairs that itself: on each launch it compares
the registered path against the running exe and rewrites the entries if needed.

Because a mailto link can no longer launch a missing exe, the repair usually
happens the next time you start the app directly — open it once from its new
home and links start working again.

Three deliberate limits on when it fires:

- **Only if you already registered.** An app that was never registered stays
  unregistered; it will not quietly add itself on first run.
- **Only if the registered exe is gone from disk**, not merely different from
  the running one. A path that still resolves is a working registration for
  another copy, and running a build-output copy must not silently steal it. Click
  Register to move it deliberately.
- **Never over another app's handler.** `Software\Classes\mailto` is rewritten
  only while it still names this app. The app's own ProgID is always safe to fix,
  and it is the one that matters: when you have selected this app in Default
  apps, Windows stores the ProgID *name* and resolves the exe through that key.

The settings window shows the current state in a line above the buttons, so a
repair is visible rather than silent.

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
