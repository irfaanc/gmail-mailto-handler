# Design: sender rules and automatic forwarding

**Status: half built.**

| Part | State |
| --- | --- |
| Rules, precedence, creation from the picker | **built** |
| Rules list with delete, in settings | **built** |
| Reason line naming the rule that fired | **built** |
| Global last-used removed | **built** |
| Automatic forwarding (skipping the picker) | not built |
| Shift override | not built |
| The notification | not built |
| Explanation record and retry payload | not built |

Rules currently *preselect* an account. The picker still appears every time.
Everything from "Automatic forwarding" onwards is still a design, and the
reasoning below is kept so it can be picked up without rediscovering it.

## The problem

Choosing which account sends is currently a manual act on every mail link. In
practice the choice is nearly always a function of the recipient: anything at a
work domain goes from the work account. The app should be able to learn that
once and then stop asking.

## Rules

Two kinds, and only two:

| Kind | Matches |
| --- | --- |
| Address | one exact recipient address |
| Domain | every address at a domain |

There is no pattern language. Globs and regular expressions were considered and
rejected (see below). These two cover the realistic cases and neither can be
written wrongly.

### Precedence

By specificity, not by user-defined order:

1. exact address rule
2. domain rule
3. first account in the list

Specificity ordering means rules never need reordering, so the settings window
needs no Move up/down for them. It also makes account *order* meaningful: the
first account is the default when nothing matches, and the accounts list is
already reorderable.

There is deliberately no "last used account" step. See the rejected list.

### Creating rules

Rules are authored from the picker, not from a settings editor. The picker gains
a remember control with three states:

- **Do not remember** (default)
- **Remember this address**
- **Remember this domain**

Chosen alongside the account, so the rule is written at the moment the context
is in front of the user.

- The control resets to "do not remember" on every launch. Sticky behaviour
  would quietly mint rules for every later recipient.
- Choosing again **overwrites** any existing rule for the same address or
  domain. This is also how a rule gets edited: send again, pick the right
  account, remember again.
- Naming the target in the label ("Remember company.com") is preferred
  over a generic "Remember domain", because it makes the multi-recipient
  ambiguity visible rather than silent.

### Deleting rules

The settings window gets a list of rules with a Remove button. **View and delete
only, no add and no edit.** The picker already owns creation and update, so
duplicating that in settings would build a second, competing authoring path.

Known limitation: updating a rule requires actually sending something to that
recipient. To fix a rule in the abstract, delete it and let it be recreated.

## Automatic forwarding

When a rule matches, the picker does not appear. The app opens the Gmail compose
URL directly, shows a notification, and records what it did.

**This is safe because nothing is sent.** The app opens a compose *draft*. A
misfiring rule produces a draft in the wrong account, visible in the browser,
with nothing delivered. That is what makes skipping the confirmation defensible
here when it would not be in a real mail client.

### Shift override

If Shift is held at launch, the picker is shown even when a rule matches. Without
this, a domain rule is a one way door: that domain never shows the picker again,
and the only route back is the explanation screen.

Caveat: this reads live keyboard state a few hundred milliseconds after the click,
so the instruction is "hold Shift through the click", not "press Shift as you
click". It needs to be in the README or nobody will know it exists.

## The notification

Drawn by the app, not a Windows toast. A real toast needs an AppUserModelID and a
Start Menu shortcut, and click to activate additionally needs a registered COM
activator CLSID. That is more machinery than the feature it serves, on an app
with no dependencies and no installer.

Requirements:

- **Must not steal focus.** Override `ShowWithoutActivation => true` so it does
  not activate when shown, and add `WS_EX_NOACTIVATE` to `CreateParams` so it
  does not activate when clicked either.
- Modeless, since `ShowWithoutActivation` does not apply to `ShowDialog`. The
  process therefore needs a real message loop for the seconds it is visible,
  which changes the app's character: today it exits the instant the browser
  launches.
- Positioned from `Screen.WorkingArea` so it sits above the taskbar, on the
  monitor under the cursor. This app has already been bitten by assuming a single
  96 DPI primary monitor.
- Clicking it opens the explanation window.

## The explanation record

One record, describing the most recent thing the app did **without asking**.

Stored: recipient, which rule matched, which account was chosen, when.

Shown when the notification is clicked, or when the app is launched with no
arguments.

Erased when:

- a manual send happens (the picker was shown, so nothing is unexplained)
- a newer automatic forward supersedes it
- the user has viewed it

Only the last one is kept, not a history. The normal case is that a rule
misfired and the user wants to fix it, and the intent can be inferred from what
just happened. Keeping a log would complicate the UI for a case the rules list
already covers: even if an explanation is erased unseen, the rule that caused it
is still sitting in the list to be found and removed.

## The retry payload

To offer "send this again from the right account", the original message has to be
kept. That means the full `mailto:` URI, verbatim: one string, and replay is then
trivial.

**Stored in its own file beside `config.json`, not inside it**, so it can be
deleted with a single file operation rather than a config rewrite. A delete that
cannot partially fail is much harder to get subtly wrong.

**Deletion is the protection, not encryption.** Triggers:

- on replay, once consumed
- on any manual send
- on supersede by a newer automatic forward
- once the explanation has been viewed
- a staleness sweep at startup, so a payload cannot outlive its usefulness if
  none of the above fires

Size is not a concern on disk. The natural ceiling is the roughly 32,767
character Windows command line, since the URI arrives as an argument.

## Rejected alternatives

Recorded so they do not get relitigated.

**Learned per-recipient memory.** Remembering which sender was used for each
recipient automatically, with no user action. Rejected: it grows without bound,
learns mistakes, and turns `config.json` into a log of everyone the user has
emailed. The remember control gets the same benefit with every entry
deliberately declared.

**Globs or regular expressions.** Rejected as unnecessary and risky. Nearly every
rule is domain based. Regular expressions invite quiet errors, notably unanchored
patterns and `.` matching any character, so `@a.com` also matches `@axcom`. A
misfiring rule means composing from the wrong account, which is the exact failure
this project spent its time eliminating.

**User-ordered rules with Move up/down.** Unnecessary once precedence is by
specificity.

**Global last-used account as a fallback.** Dropped. With automatic forwarding, a
rule firing would update last-used, so a work domain match would silently change
the default for the next unrelated personal message. Removing it removes that
coupling, and leaves account list order as the single, visible lever. Note this
reverses commit `28e624d`, which introduced `LastUsedAddress`; that field and its
handling would come out.

**Windows Credential Manager for the retry payload.** Rejected. `CredRead` returns
a generic credential to any process running as the same user with no prompt, so
it is not a stronger boundary against the realistic threat, only better hygiene.
Its 2560 byte blob limit also excludes exactly the long templated messages most
worth replaying. A size based split between credential store and disk was
considered and rejected too: sensitivity does not track byte count in either
direction, so size is an arbitrary axis to split on, and two storage paths means
two ways to leak or lose the payload.

**An unpredictable filename as a secret.** Only defends against something that
can read a known path but cannot list a directory, which is not a real threat
here.

**Dropping the body from the retry payload** on the grounds that it is still
visible in the open Gmail tab. Rejected in favour of the better user flow: one
button that resends everything to the correct account.

## Open questions

- **Which recipient is matched?** A mail link can carry several To addresses
  across different domains. First To is the predictable answer, and automatic
  forwarding lowers the stakes on getting it right, since a wrong pick is visible
  and correctable. Still needs deciding explicitly.
- **What happens when recipients span several domains** and the user asks to
  remember the domain.
- Whether the picker should show a reason line ("Work, rule
  `*@company.com`") when a rule preselects rather than auto forwards.
  With rules authored casually at send time, they are easy to forget, and this is
  what keeps the behaviour debuggable.

## Config additions

Sketch, not final:

```json
{
  "Accounts": [ ... ],
  "Rules": [
    { "Kind": "Domain",  "Match": "company.com", "EmailAddress": "you@company.com" },
    { "Kind": "Address", "Match": "bob@example.com",     "EmailAddress": "you@gmail.com" }
  ],
  "LastAutomaticForward": {
    "Recipient": "bob@company.com",
    "MatchedRule": "company.com",
    "SentFrom": "you@company.com",
    "When": "2026-08-10T14:02:11Z"
  }
}
```

Rules target an account by address, matching how accounts are already identified
everywhere else.
