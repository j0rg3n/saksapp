---
name: no-real-data
description: Keep real people, events and case content out of SaksApp's public repo. Use whenever writing or editing tests, sample or seed data, code comments, SPEC.md, commit messages, GitHub issues/comments, or logs and fixtures — especially when reproducing a reported bug.
---

# No real data in the repo

The repository is **public**. Everything written to it or to its GitHub issues, milestones, comments and
commit messages is public too.

## Never include

- **Real people** — names of real persons (parties, case handlers, board members, users), initials that
  identify them, email addresses, phone numbers, usernames.
- **Real events** — actual incidents, disputes, meetings, dates tied to a real case, or descriptions that
  would let a reader recognise a real situation, even with the names removed.
- **Real places and organisations** — addresses, companies, housing units, production hostnames.
- **Real case text** — case titles, body text, comments, quotes, and attachment file names.

## Where this applies

Tests and fixtures, sample/seed data, code comments, XML doc comments, SPEC.md, LAYOUTS.md, commit
messages, GitHub issues and comments, logs pasted into issues, screenshots, and PDF/export samples.

## Use instead

Neutral placeholders: `Person A`, `Person B`, `Eksempelsak`, `Eksempel AS`, `vedlegg-a.pdf`,
`example.com`. Invent generic events ("a meeting was held", "case was closed") rather than paraphrasing
a real one.

## When reproducing a bug

Reduce the real input to the smallest structural case that triggers it (length, encoding, character set,
layout, count) and rebuild it with placeholder text. Do not copy-paste the failing input, and do not
describe the real case in the issue or commit message — describe the shape of the failure.

## Before committing

Skim the diff, including comments and test names, for anything that came from real case content. If
real content is already committed or posted, tell the user rather than quietly rewording it, since
git history and issues keep it.

## Working data stays outside the workspace

Databases (`app.db`, backups, `.sqlite*`), Data Protection keys, `secret.env`/`caddy.env`, TLS state, image
tarballs, logs and exported passwords belong in `~/saksapp-data/` and `~/.config/saksapp/`, never in the
repository directory — even if `.gitignore` would cover them. When a command needs scratch data, use a
temp directory outside the repo. If something like this turns up inside the workspace, move it out and
tell the user.
