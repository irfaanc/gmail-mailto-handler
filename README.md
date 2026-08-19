# gmail:to

A small Windows utility that intercepts `mailto:` links, asks which Gmail account
you want to send from, and opens Gmail's compose window in your default browser.
No tray icon, no background service: it starts, does one job, exits.

It supports any Gmail hosted email account, not just @gmail.

Teach it a rule (ex: *anything at this domain goes from my work account*) and it
stops asking for those and just opens them.

## Why

Click a mail link on Windows and it opens whatever Windows picked years ago,
usually Outlook, whether or not you have ever used it.

Pointing it at Gmail instead is the obvious fix, and it works until you have a
second account. Gmail's own handler carries no account selector: it resolves to
`/mail/u/0/`, the first mailbox signed into that browser profile, and composes
there without asking. Slot numbers also shift as accounts are signed in and out,
so the mailbox it picks today is not necessarily the one it picks tomorrow.

The message goes from the wrong address, and you find out after sending, if at
all. This app puts the choice back in front of you, and names the mailbox by
address rather than by position, so the account you pick is the account that
composes.

Service wrapper clients like [Ferdium](https://ferdium.org/) are one common route
to this, since they host Gmail in a desktop window but cannot route a mail link
to a particular service. The underlying problem is the same with or without one.

## Quick start

**1. Download it.** One file, from the
[latest release](../../releases/latest):

| file | runs on | take this if |
| --- | --- | --- |
| `GmailTo.exe` | 32-bit, 64-bit, ARM | **you are not sure** |
| `GmailTo-x64.exe` | 64-bit Windows | you already have the 64-bit .NET runtime |

Both work on a normal 64-bit machine. The only thing the second saves you is a
second runtime download.

**2. Run it.** Windows will say **"Windows protected your PC"**, because the file
is not code-signed. Click **More info**, then **Run anyway**. That prompt is
expected — signing requires a certificate this project does not have.

Run from Downloads, your Desktop, or a temp folder and it copies itself to
`%LocalAppData%\Programs\GmailTo\` on that first run, registers from there,
and hands over to that copy, so the app you go on using is the permanent one. No
admin rights needed. The file you downloaded is left where it is, yours to keep
or bin. If you would rather choose the location, put it somewhere first and it
leaves your choice alone.

**3. Add an account.** It asks straight away. Give it a name and the Gmail
address to send from.

**4. Say yes to becoming the mail handler.** It offers as soon as the account
exists. Two dialogs in a row on a first run is normal.

And that's it. Clicking a `mailto:` link anywhere now brings up the
picker.

### Requirements

**Windows 10 or 11**, and the **.NET Desktop Runtime 8**.

You do not have to check for the runtime first. If it is missing, running the app
produces a Windows dialog with a **Download it now** button that fetches the
right version for your machine. Install it up front from
[dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) if you
would rather — it is the *Desktop Runtime* you want, not the SDK and not the
ASP.NET Core runtime.

## The picker

Clicking a `mailto:` link brings up the picker, unless a [rule](#rules) matches.

- **Enter**, or clicking the highlighted row, opens Gmail immediately.
- Clicking a different row selects it; click again, or press Enter, to send.
- **Esc** cancels and nothing happens.

The highlighted account is whichever a rule names, or the first account in the
list. That list is reorderable, so the top entry is your default.

## Rules

A rule maps a recipient to the account that should write to them, either one
exact address or a whole domain.

### Creating them

In the picker, not in an editor. Beside the accounts is a **Remember** box
offering to always use the chosen account for that address or for its domain. It
defaults to remembering nothing and resets every launch, so a rule only exists
because you asked for it.

Choosing again replaces an existing rule, which is also how you edit one: send to
that recipient again, pick the right account, remember again.

### How they match

An exact address beats a domain rule covering the same recipient, so rules never
need ordering.

Matching uses the first `To` address, or the first `Cc`/`Bcc` if there is no
`To`. A link can carry several recipients across different domains, so the
Remember box names its target — *Always use for company.com* — rather than saying
"this domain".

### When one matches

The picker does not appear. The message opens in the account the rule names, and
a notice in the corner says which account and which rule. It never takes focus,
and fades after a few seconds.

**Nothing is sent.** This opens a Gmail *compose window*, so a rule that fires
wrongly leaves a draft in the wrong account, visible in the browser, with nothing
delivered.

**Hold Shift while clicking a mail link** to force the picker anyway.

### Undoing one

Click the notice, or just run the app, to see the last thing it did without
asking: who it went to, which account, which rule, and when. From there you can
send the same message again from a different account, or remove the rule.

Only the most recent is kept, and it clears once seen, superseded, or once you
send anything manually. If you miss one, the rule that caused it is still listed.

### Seeing and removing them

**Rules...** in settings lists them with a Remove button. There is no add or edit
there: the picker already does both.

Editing an account's address repoints its rules. Deleting an account deletes the
rules aiming at it, and says how many first.

## Settings

Run `GmailTo.exe` with no arguments. Accounts can be added, edited,
reordered and removed, and the button at the bottom toggles between **Set as
default mail handler** and **Stop handling mail links** depending on which state
you are in.

Stopping releases the association and removes the app's registry entries.
Windows then falls through to whatever is next — as with any app that gives up an
association, it cannot be handed back to whoever held it before.

## Config

`%AppData%\GmailTo\config.json`

```json
{
  "Accounts": [
    { "Name": "Personal", "EmailAddress": "you@gmail.com" },
    { "Name": "Work", "EmailAddress": "you@company.com" }
  ],
  "Rules": [
    { "Kind": "Domain",  "Match": "company.com",        "EmailAddress": "you@company.com" },
    { "Kind": "Address", "Match": "friend@company.com", "EmailAddress": "you@gmail.com" }
  ]
}
```

There is no Save button. Every change — accounts, rules, ordering — is written as
it is made, and closing a window never discards anything. Removing an account
asks first and says which rules go with it.

Hand-editing is fine: `Kind` is written by name rather than as a number, and
rules point at accounts by address. An entry with no `EmailAddress` is dropped on
load. A corrupt file is reported, with an offer to reset it.

One other file lives beside it: `last-forward.uri`, holding the link of the last
automatic forward so it can be re-sent. It holds the draft, so it is deleted as
soon as it is used, seen, superseded, or a week old.

## Where it installs, and moving it

Run from Downloads, the Desktop, or a temp folder, it copies itself to
`%LocalAppData%\Programs\GmailTo\` and registers from there. Those are the
places a file sits when nobody has decided where it belongs yet, and a handler
registered from one of them breaks the first time the folder is tidied.

Put the exe somewhere yourself and it stays put. A deliberate location is left
alone, because silently relocating a file you filed on purpose is worse than the
problem being solved.

### Upgrading

Download the newer release and run it from Downloads. It replaces the installed
copy, keeps the registration pointing at the same place, and leaves your accounts
and rules alone. The download is left alone too, as ever.

### Moving it

To move an installed copy, move it and start it once in its new home. The
registry entries name an absolute path, and the app repoints them at itself
whenever the running copy is not the one on record — whichever copy you last
started wins.

The repair happens on *start*, so you have to open the app yourself. A mail link
cannot launch a path that is not there, and so cannot trigger it.

This does mean running a copy out of a build folder takes the registration too.
When that happens the settings window says so and names the copy that has been
orphaned. Swapping the x86 build for the x64 one, or the reverse, works the same
way: start the new one once.

### Registry keys

All under `HKEY_CURRENT_USER`, no admin rights needed:

- `Software\Classes\mailto` (+ `shell\open\command`, `DefaultIcon`)
- `Software\Classes\GmailTo.Url.Mailto` — the ProgID that Capabilities and
  Windows' own `UserChoice` both point at
- `Software\GmailTo\Capabilities` (+ `UrlAssociations\mailto`)
- `Software\RegisteredApplications` → value `GmailTo`

**Stop handling mail links** removes all of them. Registering stashes whatever
`Software\Classes\mailto` held beforehand and stopping puts it back, so adding
and removing this app leaves an existing handler untouched.

### Uninstalling

There is no uninstaller, because there was no installer. Three steps, none of
them needing admin rights:

1. Open settings and click **Stop handling mail links**. That removes every
   registry entry listed above and puts back whatever held
   `Software\Classes\mailto` beforehand.
2. Delete `%LocalAppData%\Programs\GmailTo\`.
3. Delete `%AppData%\GmailTo\`, which holds `config.json` and any saved
   forward.

Step 1 cannot hand the association back to the app that held it before, because
Windows allows `UserChoice` to be deleted but never written. Mail links fall
through to whatever Windows decides is next, which is what happens to any file
association when the app owning it goes away.

## Build

Requires the .NET 8 SDK.

```bash
dotnet publish GmailTo.csproj -c Release -o publish
dotnet publish GmailTo.csproj -c Release -r win-x64 -o publish-x64
```

The first is the x86 build that runs everywhere; the second is the 64-bit one.
Each produces a single self-contained-looking `GmailTo.exe` with everything
bundled in — copy that one file, nothing goes with it.

Neither is actually self-contained: both need the .NET Desktop Runtime, which is
why they are around 290 KB rather than 150 MB.

The icon is `ico\app.ico`, seven sizes built from the PNGs beside it.

## Files

| File | Role |
| --- | --- |
| `Program.cs` | Entry point, argument handling, error dialogs |
| `MailtoRequest.cs` | RFC 2368 parsing and Gmail compose URL building |
| `EmailAddresses.cs` | Pulling a bare address out of a recipient field |
| `AppIcon.cs` | Loading the embedded icon for the windows |
| `Mail.cs` | Opening the Gmail compose window |
| `Config.cs` | `config.json` load/save, rules and matching |
| `RetryStore.cs` | The saved link of the last automatic forward |
| `PickerForm.cs` | The account chooser, and where rules are created |
| `ToastForm.cs` | The corner notice, which must never take focus |
| `ForwardDialog.cs` | What happened, and how to undo it |
| `SettingsForm.cs` | Account list editor |
| `AccountDialog.cs` | Add/edit one account |
| `RulesDialog.cs` | List and remove rules |
| `Registration.cs` | HKCU registry entries and self-healing |
| `SelfInstall.cs` | Copying itself to a permanent home on first run |
| `RegistrationPrompt.cs` | The offer to become the handler, and the walkthrough |

The forms are hand-written, with no `.Designer.cs` files, but follow the shape
the designer emits because WinForms depends on it — see
[PLATFORM-NOTES.md](PLATFORM-NOTES.md#winforms) before changing any layout code.

Two companion documents: [DESIGN.md](DESIGN.md) for why the app behaves as it
does, and [PLATFORM-NOTES.md](PLATFORM-NOTES.md) for what had to be learned about
Windows and Gmail to make it work.

## Licence

[MIT](LICENSE).

The paper plane icon in `ico\` is original to this project and covered by the
same licence.
