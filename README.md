# Mailto Picker

A small Windows utility that intercepts `mailto:` links, asks which Gmail account
you want to send from, and opens Gmail's compose window in your default browser.
No tray icon, no background service: it starts, does one job, exits.

Once you teach it a rule — *anything at this domain goes from my work account* —
it stops asking for those and just opens them.

- [Build](#build)
- [Setting up](#setting-up)
- [The picker](#the-picker)
- [Rules](#rules)
- [Config](#config)
- [Moving the app](#moving-the-app)
- [Files](#files)
- [Licence](#licence)

Two companion documents: [DESIGN.md](DESIGN.md) for why the app behaves as it
does, and [PLATFORM-NOTES.md](PLATFORM-NOTES.md) for what had to be learned about
Windows and Gmail to make it work.

## Build

Requires the .NET 8 SDK.

```bash
dotnet publish MailtoPicker.csproj -c Release -o publish
```

That produces a single `publish\MailtoPicker.exe`. Copy that one file wherever
you want it; nothing else goes with it.

It needs the **.NET 8 desktop runtime** installed. It is not self-contained,
which would have made it around a hundred megabytes for a mailto shim.

The icon is `ico\app.ico`, seven sizes built from the PNGs beside it.

## Setting up

Run `MailtoPicker.exe` with no arguments. On a first run it asks for an account
straight away rather than inventing one. Each account has:

- **Display name** — anything you like; this is what the picker lists.
- **Gmail address** — the account to send from.

Then use **Set as default mail handler**, which is usually all it takes. If
Windows refuses, the app walks you through doing it by hand; the route that works
is Settings > Apps > Default apps, typing `MAILTO` into *"Set a default for a
file type or link type"*, and using the `MAILTO` row that appears. The
application list further down that page is the intuitive place to look and the
wrong one.

That button is a toggle. Once the app is the handler it reads **Stop handling
mail links**, which releases the association and removes its registry entries.
Windows then falls through to whatever is next — as with any app that gives up an
association, it cannot be handed back to whoever held it before.

While the app is not the handler it offers to become one on every launch, since
that is the only thing it does. Stopping deliberately is remembered, and it stops
asking.

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

## Config

`%AppData%\MailtoPicker\config.json`

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

## Moving the app

Move it and start it once in its new home. The registry entries name an absolute
path, and the app repoints them at itself whenever the running copy is not the
one on record — whichever copy you last started wins.

The repair happens on *start*, so you have to open the app yourself. A mail link
cannot launch a path that is not there, and so cannot trigger it.

This does mean running a copy out of a build folder takes the registration too.
When that happens the settings window says so and names the copy that has been
orphaned.

### Registry keys

All under `HKEY_CURRENT_USER`, no admin rights needed:

- `Software\Classes\mailto` (+ `shell\open\command`, `DefaultIcon`)
- `Software\Classes\MailtoPicker.Url.Mailto` — the ProgID that Capabilities and
  Windows' own `UserChoice` both point at
- `Software\MailtoPicker\Capabilities` (+ `UrlAssociations\mailto`)
- `Software\RegisteredApplications` → value `MailtoPicker`

**Stop handling mail links** removes all of them. Registering stashes whatever
`Software\Classes\mailto` held beforehand and stopping puts it back, so adding
and removing this app leaves an existing handler untouched.

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
| `RegistrationPrompt.cs` | The offer to become the handler, and the walkthrough |

The forms are hand-written, with no `.Designer.cs` files, but follow the shape
the designer emits because WinForms depends on it — see
[PLATFORM-NOTES.md](PLATFORM-NOTES.md#winforms) before changing any layout code.

## Licence

[BSD 3-Clause](LICENSE).

The paper plane icon in `ico\` is original to this project and covered by the
same licence.
