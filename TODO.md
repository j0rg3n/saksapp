# TODO.md - Implementeringsplan

> Se SPEC.md for detaljert spesifikasjon. Fullførte faser er arkivert i TODO_ARCHIVE.md.

---

## 🔴 HØY PRIORITET: Backup fyller disken — tjenesteavbrudd

**Symptom**: Cloud-VM-en går tom for diskplass og tjenesten faller ut.

**Rotårsak**: `DatabaseBackupService` tar timesvise snapshots (`DatabaseBackupService.cs:120`)
uten komprimering. `TryApplyRetention(200)` er hardkodet (`:67`) og glob-mønsteret `app-*.sqlite`
(`:74`) treffer aldri `.bz2`-filer, så de blir liggende for alltid. 200 × 37 MB = 7,4 GB selv når
oppryddingen virker. Nedlastingsarkivet `../SaksApp_backups` er nå **34 GB / 2324 filer**
(2248 ukomprimerte `.sqlite` = 33 GB, 60 `.bz2` = 999 MB).

- [ ] Gjør oppryddingen konfigurerbar: `Backup:MaxFiles` (48), `Backup:MaxTotalBytes` (2 GB),
      `Backup:Compress` (true)
- [ ] Gzip hvert snapshot: skriv `app-<ts>.sqlite.tmp`, stream gjennom `GZipStream` til
      `app-<ts>.sqlite.gz`, slett temp-fila i `finally`
- [ ] Skriv om `TryApplyRetention` (`:70-88`): matche `.sqlite`, `.sqlite.gz` og `.sqlite.bz2`;
      sortere på tidsstempelet i filnavnet, **ikke** `CreationTimeUtc` (upålitelig på Linux);
      slette eldste ut over `MaxFiles`, deretter til totalen er under `MaxTotalBytes`; alltid
      beholde nyeste
- [ ] Logg én oppsummeringslinje per runde: filer beholdt, filer slettet, bytes på disk
- [ ] `deployment/download-backups.sh`: bygg ekskluderingsliste fra lokale filnavn (strippet for
      `.gz`/`.bz2`) og send til `rsync --exclude-from` — uten dette laster
      `--ignore-existing` ned alt på nytt så snart lokale filer er komprimert
- [ ] `download-backups.sh`: gjør `LOCAL_PATH` absolutt ut fra skriptets egen plassering, dropp
      duplikate snapshots (hash mot forrige beholdte), gzip nye filer, og prun lokalt arkiv med
      `KEEP_LOCAL` (400) og `MAX_LOCAL_BYTES` (10 GB). Skriv ut før/etter-oppsummering.
      Legg til `set -uo pipefail`
- [ ] Engangsopprydding av de 34 GB — først etter at Drive-opplasting er verifisert

---

## 🔴 HØY PRIORITET: Bugfix — Innkalling mangler hendelser siden forrige møte

**Symptom**: Innkallingen tar ikke med hendelser registrert siden forrige møte. Bekreftet mot
`app-20260901-191631.sqlite` for møte #9 (`Id 14`, 2026-09-29): hendelse **277** (`avvik`, sak 68,
Isak om sirene/kabler etter service) og hendelse **284** (`tiltak`, sak 19, befaring med Ulf
Teigen) mangler begge. Begge sakene sto sist på agendaen i møte 13 (2026-08-11), så vinduet er
`(2026-08-11, 2026-09-29]` og begge hendelsene ligger midt i det.

**Rotårsak**: `SaksAppWeb/Services/IAgendaPdfDataService.cs:136` filtrerer på
`x.CaseEvent.Category == "comment"` — kun legacy-kategorien. Commit `888c095` (Phase 4.5,
2026-05-02) endret skrivesiden til `general`/`avvik`/`tiltak` (`CasesController.cs:235`) uten å
oppdatere denne leseren. **Alle hendelser opprettet etter 2026-05-02 faller ut av innkallingen.**
Vinduslogikken er ikke skyld i dette.

- [ ] Legg kategorisettet i én delt konstant (ved siden av
      `CasesController.ValidCaseEventCategories`): skrivbart sett (`general`, `avvik`, `tiltak`)
      og lesbart sett (de tre pluss legacy `comment`)
- [ ] `IAgendaPdfDataService.cs:136`: filtrer på det lesbare settet
- [ ] Døp om `allCommentEvents` og `commentAttachments*` til kategorinøytrale navn
- [ ] Pek `ICaseQueryService.cs:156` på samme konstant
- [ ] Rett regresjonstesten `AgendaPdfDataServiceTests.cs:132-161` — fixturen bruker
      `Category = "comment"` og passerer derfor mens produksjonskoden er ødelagt. Parametriser
      over `general`, `avvik`, `tiltak`, `comment`; `meeting` skal ikke med

---

## Ad-hoc: Møtetidspunkt — `MeetingTime`

12:00 UTC-ankeret i vindusberegningen (`IAgendaPdfDataService.cs:176-179, 215-218`) er vilkårlig
fordi `Meeting.MeetingDate` er en `DateOnly`. Møter planlegges alltid med både dato og klokkeslett
— modellen mangler feltet.

- [ ] Legg til `MeetingTime` (`TimeOnly?`) på `Meeting` + migrering. `MeetingDate` forblir
      `DateOnly` (86 bruksteder)
- [ ] Klokkeslettfelt i `Views/Meetings/Create.cshtml` og `Edit.cshtml`; vis dato + tid i
      Index/Details/Minutes og i PDF-hodene
- [ ] Vinduets slutt = `MeetingDate` + `MeetingTime` når satt, ellers slutten av døgnet — aldri
      midt på dagen. Samme behandling for vindusstart mot forrige møte
- [ ] Tester: hendelse før møtetidspunktet er med; etter er ute; uten tid satt er en hendelse
      sent på møtedagen fortsatt med

---

## Ad-hoc: Hjem-side med Markdown-innhold

- [x] Legg til `Markdig` NuGet-pakke
- [x] Opprett `SaksAppWeb/Content/home.md` med forklaring av appen
- [x] `HomeController.Index()` leser filen og konverterer til HTML med Markdig
- [x] `Views/Home/Index.cshtml` rendrer HTML-innholdet

---

## Ad-hoc: Referat-modus — «Utvikling siden innkallingen»

- [ ] Legg til felt `AgendaPdfGeneratedAt` (nullable DateTime) på `Meeting` + migrering
- [ ] Sett `AgendaPdfGeneratedAt = DateTime.UtcNow` når agenda-PDF genereres
- [ ] I referat-visningen: for hvert agenda-punkt, hent CaseEvents på saken med
      `CreatedAt > meeting.AgendaPdfGeneratedAt AND CreatedAt <= meeting.MeetingDate`
- [ ] Vis disse under en seksjon «Utvikling siden innkallingen» per agenda-punkt
      (lesevisning, ikke redigerbar); render vedleggslenker som åpner i ny fane

---

## Ad-hoc: PDF — dedupliser vedlegg på tvers av saker

- [ ] I agenda-PDF og referat-PDF: samle alle vedlegg på tvers av agenda-punkter,
      dedupliser på `Attachment.Id`, og list hvert vedlegg kun én gang
      (f.eks. samlet vedleggsliste bakerst, eller én forekomst pr. agenda-punkt med
      en note «(se også sak X)»)
- [ ] Oppdater `PdfGeneratorService` (agenda og referat) tilsvarende

---

## Phase 6: Nye Features

### 6.1 Board Log (Styrelogg)
- [ ] **Task 6.1.1**: Opprett Board Log controller og view
- [ ] **Task 6.1.2**: Implementer filter (kategori, dato, sak)
- [ ] **Task 6.1.3**: Vis kronologisk liste av CaseEvents uten MeetingEventLink

### 6.2 HMS Avvik og Tiltak
- [ ] **Task 6.2.1**: Opprett HMS controller og view
- [ ] **Task 6.2.2**: Vis sammendrag (tellere)
- [ ] **Task 6.2.3**: Implementer auto-lukk av Avvik når relatert sak lukkes
- [ ] **Task 6.2.4**: Tillat Avvik uten sak (null CaseId)

### 6.3 Avvik–Tiltak-kobling og status

Lavere prioritet enn de to høyprioriterte sakene øverst.

- [ ] `CaseEventRelation`-entitet (mange-til-mange mellom avvik- og tiltak-hendelser) + migrering,
      med soft delete og query filter som resten av modellen
- [ ] Felt på `CaseEvent`: `IsClosed`, `ClosedAt`, `ClosedByUserId` — gjelder kun `avvik`
- [ ] UI for å koble et tiltak til ett eller flere avvik (og omvendt) fra HMS-visningen og
      sakstidslinjen; vis koblede motparter som lenker
- [ ] «Lukk avvik»-handling. Kobling av et tiltak lukker **ikke** avviket automatisk
- [ ] Statusfilter i HMS-visningen, default åpne avvik

### 6.4 Excel-eksport av avvik og tiltak

- [ ] Velg bibliotek (ClosedXML — MIT, ingen Excel-avhengighet på serveren)
- [ ] «Eksporter til Excel» i HMS-visningen; eksporter det filtrerte settet
- [ ] Kolonner: Type, Dato, Innhold, Saksnumre, Status, Lukket dato, Koblede tiltak/avvik,
      Registrert av
- [ ] Test: eksport med og uten filter, avvik uten sak, avvik med flere koblede tiltak

---

## Phase 6B: Google Drive Backup (slanket)

**Mål**: Offsite-kopier av backupene, slik at lokal oppbevaring kan holdes kort.

**Merk**: Opprinnelig Phase 6B var overspesifisert (per-bruker Drive-kobling, admin-restore-UI med
DB-bytte ved oppstart). SPEC.md er skrevet om til: én admin kobler én Drive, restore er en
dokumentert manuell prosedyre.

### 6B.1 Drive-kobling
- [ ] Registrer OAuth 2.0-app i Google Cloud Console (scope: `drive.file`) — manuelt forarbeid
- [ ] `DriveToken`-entitet: `UserId`, `AccessToken` (kryptert), `RefreshToken` (kryptert),
      `TokenExpiry`, `LinkedAt` + migrering
- [ ] `/settings`-side bak `AdminOnlyFilter`: koble til / koble fra, vis koblingsstatus og siste
      opplastingsresultat
- [ ] Krypter tokens med `IDataProtector` (purpose: `"DriveTokens"`)

### 6B.2 Opplasting
- [ ] `DriveBackupLog`-entitet: `AttemptedAt`, `BackupDate`, `Success`, `ErrorMessage` + migrering
- [ ] `IGoogleDriveUploader` / `GoogleDriveUploader`: last opp, list app-opprettede filer, slett
      etter id
- [ ] `DriveBackupService` (BackgroundService): sjekk hver `GoogleDrive:BackupIntervalDays`
      (default 1), last opp **nyeste eksisterende lokale backup** som
      `saksapp-backup-YYYY-MM-DD.db.gz`, no-op når ingen Drive er koblet
- [ ] Prun Drive til `GoogleDrive:KeepCount` (default 30)
- [ ] Konfigurasjon: `GoogleDrive:ClientId`, `GoogleDrive:ClientSecret` (via `auth/secret.env`),
      `BackupIntervalDays`, `KeepCount`
- [ ] Logg hvert forsøk til `DriveBackupLog` — både suksess og feil

## Phase 7: WhatsApp Bot

**Mål**: Automatisk inntak av WhatsApp-meldinger som CaseEvents, med vedlegg og sak-kobling via hashtags.

**Avhengigheter**: Krever Phase 2+3 (CaseEvent-modellen). ✅ Komplett.

### 7.1 Ingest API (SaksAppWeb)
- [ ] **Task 7.1.1**: Legg til `POST /api/whatsapp/ingest` endpoint (autentisert med delt hemmelighet)
- [ ] **Task 7.1.2**: Definer `WhatsAppIngestPayload` DTO
- [ ] **Task 7.1.3**: Implementer `IWhatsAppIngestService` / `WhatsAppIngestService` med grupperingsbuffer
- [ ] **Task 7.1.4**: Implementer `WhatsAppBufferFlushService` (BackgroundService)
- [ ] **Task 7.1.5**: Legg til konfigurasjon: `WhatsApp:SharedSecret`, `WhatsApp:GroupingWindowMinutes` (default 5)
- [ ] **Task 7.1.6**: Flush til `CaseEvent` + `Attachment` ved timeout

### 7.2 Sak-kobling via hashtag
- [ ] **Task 7.2.1**: Parse `#<tall>` fra meldingens tekst ved ingest
- [ ] **Task 7.2.2**: Opprett `CaseEventCase`-kobling for hvert funnet saksnummer

### 7.3 WhatsApp-sidecar
- [ ] **Task 7.3.1**: Velg tilnærming: whatsmeow (Go) eller Baileys (Node.js)
- [ ] **Task 7.3.2**: Implementer sidecar som kobler til WhatsApp, lytter på meldinger i konfigurerte grupper
- [ ] **Task 7.3.3**: Sidecar poster til `/api/whatsapp/ingest` med shared secret
- [ ] **Task 7.3.4**: Legg til sidecar i `docker-compose.yml`
- [ ] **Task 7.3.5**: QR-kode-autentisering ved første oppstart

### 7.4 UI — WhatsApp-merking i tidslinje
- [ ] **Task 7.4.1**: Vis WhatsApp-ikon ved CaseEvents med `Source = "whatsapp"`
- [ ] **Task 7.4.2**: Vis avsendernavn (fra `SourceSenderId` eller konfigurerbar navne-map)
