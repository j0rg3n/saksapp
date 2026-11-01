# TODO.md - Implementeringsplan

> Se SPEC.md for detaljert spesifikasjon. Fullførte faser er arkivert i TODO_ARCHIVE.md.

---

## Ad-hoc: Uleselig vedlegg skal ikke stoppe innkalling/referat-PDF

Se SPEC.md «Add Attachment» → «An unreadable attachment must not break …».

- [x] `SimplePdfWriter`: fang feil ved innlesing av PDF-vedlegg; fjern sider som allerede er
      kopiert, behold forsiden og skriv fast norsk merknad (ingen `ex.Message` i dokumentet),
      ombrutt med `WriteWrapped`, plassert under filnavnet (riktig fonthøyde)
- [x] Samme håndtering for bildevedlegg, via felles hjelpemetode; fjern ubrukt `imageStream`
- [x] Logg feilen som warning (`ILogger<SimplePdfWriter>` via `SimplePdfWriterFactory`)
- [x] Test: søppelbytes som PDF, passordbeskyttet PDF og søppelbytes som bilde gir lesbar PDF,
      riktige sidetall for etterfølgende vedlegg, og en logget warning

---

## Ad-hoc: Innkalling — badge for Avvik og Tiltak i historikken

- [x] Ta med `Category` i `AgendaCommentData` og fyll den i `AgendaPdfDataService`
- [x] I «Historikk siden forrige møte»: tegn badge foran hendelser med `avvik` (#DC3545) og
      `tiltak` (#FFC107) via eksisterende `ISimplePdfWriter.OutcomeBadge`. Vanlige hendelser
      (`general`, legacy `comment`) får **ingen** badge
- [x] Test: avvik og tiltak gir badge, general og comment gir ingen

---

## Ad-hoc: Bugfix — Sidetall for vedlegg i innkallingen er alle like

**Symptom**: Alle vedlegg i innkallingens vedleggsliste viser samme sidetall — sidetallet for den
første siden som inneholder vedlegg.

**Rotårsak**: `MeetingsController.cs:541` kaller
`pdf.WriteAttachmentTocEntryLabel(pdf.GetCurrentPageNumber() + 1, ...)` inne i løkken som skriver
innholdsfortegnelsen, *før* vedleggene er lagt til. `GetCurrentPageNumber()` endrer seg ikke mens
TOC-linjene skrives på samme side, så hver oppføring får samme verdi. Samme feil i referat-PDF-en
på `MeetingsController.cs:769`.

- [x] Legg til vedleggene først for å finne faktisk startside per vedlegg, og skriv TOC-en etterpå
      — eller beregn sideantall per vedlegg på forhånd. `ISimplePdfWriter` har allerede
      `GetAttachmentPageNumbers()` og `AddPdfAttachmentStart()` som ser ut til å være ment for
      nettopp dette; sjekk om de kan brukes
- [x] Rett begge steder (innkalling og referat)
- [x] Test: innkalling med minst tre flersidige PDF-vedlegg skal gi stigende, korrekte sidetall

---

## Pre-deploy-review 2026-09-08 — funn (TLS + disksikring)

Review av `c19e6d6..6677755`. TLS-endringen er allerede ute; den fikk aldri sin egen review-runde,
så den er dekket her. Ingen funn blokkerer utrulling.

### Sikkerhet

- [ ] `auth/secret.env` har modus 0644 og `caddy.env` 0664. Deploy-skriptet bruker `rsync -av`, som
      bevarer rettighetene, så SMTP-credentials havner verdenslesbare på VM-en. Lav reell
      eksponering (bare root logger inn), men unødvendig. Fiks: `chmod 600 auth/*` og
      `--chmod=F600` på rsync

**Verifisert lukket**: build-konteksten er 1,2 GB → **13 MB**, og `auth/`, `dp_keys/`, `db/` og
`*.tar` er borte fra build-laget. Appen publiserer kun `127.0.0.1:8080` på VM-en, og Caddys
admin-port 2019 er ikke publisert — det er nettopp derfor det er trygt å stole på
`X-Forwarded-*` med tømt `KnownProxies`.

### Pålitelighet

- [ ] **`saksappweb` har `restart=no`, mens `caddy` har `unless-stopped`.** Dør app-containeren
      (OOM, krasj, feilet oppstart) blir den liggende, og Caddy svarer 502 til noen oppdager det.
      systemd-unit-en er `Type=oneshot` med `RemainAfterExit=yes` og starter den ikke på nytt.
      Anbefalt: `restart: unless-stopped` også på appen — én linje, samme policy som proxyen
- [ ] **`Backup:MinFreeBytes`-vakten kan stoppe backup i ubestemt tid med bare en logglinje per
      intervall.** Den bytter en høylytt feil (full disk) mot en stille (ingen backup). Ingenting
      viser «backup har ikke kjørt på N dager». `DriveBackupLog` i Phase 6B er riktig sted å
      eksponere det; inntil da er det kun logg

**Verifisert**: sertifikatet er gyldig til 2026-12-05, og `./caddy_data` med ACME-tilstand ligger
på VM-en, så fornyelse har det den trenger.

### Stabilitet

- Oppryddingsgrensene faller fra 48 → 24 filer og 2 GB → 768 MB ved utrulling. Uten praktisk
  konsekvens nå: prod har 7 filer / 299 MB fordi `download-backups.sh` trimmer remote til 5, så
  utrullingen kaster ingen historikk

### Code smells

- [ ] `docker-compose.override.yml` er committet og lastes automatisk lokalt. Ufarlig fordi
      deploy-skriptet bare overfører `docker-compose.yml`, men den som rsync-er hele repoet til en
      server vil stille bytte Caddy til `saksapp.test` og brekke TLS. Verdt en advarsel i filen
- [ ] `Caddyfile` og `Caddyfile.local` dupliserer site-blokken; greit for lesbarhet, men de kan
      komme i utakt

### Vurdert uten funn

- **Responsivitet**: ingen regresjon. Caddy legger til ett lokalt hopp, `encode zstd gzip` er på,
  HTTP/3 tilgjengelig på 443/udp
- **Ytelse**: `DriveInfo` er ett statvfs-kall per backup- og oppryddingsrunde
- **Konfigurasjon**: verifisert at `Backup__*`-verdiene i compose faktisk parses
  (`805306368`, ikke strengen med YAML-kommentaren)

---

## Hendelse 2026-09-08: full disk tok ned appen

**Symptom**: «An error occurred while processing your request» på alle sider som skriver.
Loggen viste `SQLite Error 8: 'attempt to write a readonly database'` — som ser ut som et
rettighetsproblem, men var full disk (96 % brukt, 401 MB ledig av 9,8 GB). Rettighetene var
korrekte (`1654:1654`, samme som container-brukeren).

**Løst akutt** ved å frigjøre 2,4 GB: `docker image prune -f` (2,16 GB dangling images),
slette `/root/saksappweb.tar` (121 MB) og `apt-get clean` (~400 MB). Disk 96 % → 70 %.

**Rotårsaker**

1. **Deploy-skriptet ryddet ikke.** Hver utrulling la igjen tarballen *og* gjorde forrige image
   dangling — ~1 GB per deploy. Dette, ikke backupene, var største forbruker.
2. **Backup-budsjettet var feil dimensjonert.** `MaxTotalBytes` default 2 GB på en 9,8 GB disk;
   48 filer x ~40 MB = 1,9 GB, altsa 20 % av disken. Oppryddingen gjorde noyaktig det den var
   konfigurert til - konfigurasjonen var gal.
3. **Databasen vokser** ~8 MB/uke fra vedlegg: 19 MB (mai) -> 37 MB (1. sep) -> 45 MB (8. sep).
   Et fast budsjett blir feil over tid.

- [x] `deployment/deploy-upcloud.sh`: slett tarballen etter `docker load`, og kjor
      `docker image prune -f` etter vellykket start
- [x] `Backup:MinFreeBytes` (default 1 GB): hopp over snapshot og logg advarsel hvis skrivingen
      ville etterlate mindre ledig plass. Backup-systemet skal aldri kunne fylle disken det
      beskytter
- [x] Senk `Backup:MaxTotalBytes`-default og sett en eksplisitt verdi for produksjon som passer
      9,8 GB
- [x] Logg ledig diskplass ved hver oppryddingsrunde, slik at neste hendelse ikke starter med et
      misvisende «readonly database»
- [ ] Phase 6B (Drive-opplasting) er den varige losningen: kort lokal oppbevaring, historikk offsite

---

## 🔴 HØY PRIORITET: TLS — appen kjører på ufortrolig HTTP

**Symptom**: Produksjon serveres over `http://<ip>` (port 80, `docker-compose.yml`). Passord ved
innlogging og selve auth-cookien går ukryptert over nettet. `Program.cs:72` kaller `UseHsts()`, men
det er en no-op: nettlesere ignorerer HSTS-headeren når den kommer over HTTP, og det finnes ingen
`UseHttpsRedirection`. Enhver på nettverket mellom et styremedlem og VM-en kan lese
innloggingsdetaljer og kapre sesjonen.

**Blokkerer også Phase 6B**: Google krever HTTPS for OAuth-redirect-URI-er og godtar ikke bare
IP-adresser som redirect-vert.

- [x] `SAKSAPP_DOMAIN` lagt i `auth/caddy.env` (egen fil — proxyen skal ikke ha SMTP-credentials)
- [ ] **Gjenstår hos deg**: pek en A-record for `bergstien12.fabeljet.com` mot VM-ens IP
- [x] `Caddyfile` med automatisk Let's Encrypt; Caddy publiserer 80 og 443
- [x] `docker-compose.yml`: legg til `caddy`-tjenesten; appen slutter å publisere offentlig port og
      binder bare `127.0.0.1:8080` for lokal debugging
- [x] `Program.cs`: `UseForwardedHeaders` (`X-Forwarded-For`, `X-Forwarded-Proto`) først i
      pipelinen, og `Cookie.SecurePolicy = Always` utenfor Development
- [x] `deployment/deploy-upcloud.sh`: overfør `Caddyfile` og `auth/secret.env`-avhengige variabler
- [ ] Åpne port 443 i VM-ens brannmur
- [x] Verifiser lokalt med `SAKSAPP_DOMAIN=localhost` (Caddy bruker intern CA, ingen ACME), og i
      produksjon at HTTP redirigerer til HTTPS og at sertifikatet er gyldig

---

## Pre-deploy-review 2026-09-02 (runde 2) — funn fra PDF-endringene

Review av innkalling-badges + sidetallsfiksen. Ingen funn blokkerer utrulling.

### Pålitelighet

- [ ] `FontResolverBootstrap.Initialize` svelger «already used»-unntaket uten logging (statisk
      klasse, ingen logger). Feiler resolveren reelt, dukker det opp senere som en uklar font-feil

### Code smells

- [ ] **`WriteAttachmentTocEntry` og `WriteAttachmentTocEntryLabel` er nå ubrukte** — den siste ble
      foreldreløs av sidetallsfiksen. Bør fjernes, men gjør det som en egen, forsiktig endring:
      et forsøk under denne runden kuttet for bredt og tok med `OutcomeBadge` og
      `DeferredTocPageNumber`; ble rullet tilbake
- [ ] **`ApplyPendingLinks` / `_pendingLinks` / `WriteTextWithAttachmentLinks` /
      `AddPdfAttachmentStart` er ubrukt.** `ApplyPendingLinks` har dessuten de *samme* to feilene
      som nettopp ble rettet i TOC-stien: `TargetPageNumber - 1` (peker én side for tidlig) og
      `new XPoint(link.Y, ...)` som bruker Y som X-koordinat. Kobles den noen gang på, er den feil
      fra dag én. Fjern, eller rett samtidig
- [ ] `DrawInlineBadge` parser fargen med `Convert.ToInt32(colorHex.Substring(...))` uten
      validering. Kalles kun med hardkodede literaler i dag; kaster ved ugyldig input

### Vurdert uten funn

- **Sikkerhet**: ingen ny angrepsflate. Brukertekst og filnavn tegnes via `DrawString`; ingen
  injeksjonsvei i PDF-tekst
- **Responsivitet**: uendret kostnad; `ApplyAttachmentTocPageNumbers` er O(oppføringer)
- **Ytelse**: forbedring — `labeledAttachments.IndexOf(...)` i referat-PDF-en var O(n²) og ga feil
  sekvensnummer for duplikater; erstattet med en teller
- **Stabilitet**: TOC-renna er dimensjonert for «0000: », dvs. sidetall opp til 9999

---

## 🔴 Pre-deploy-review 2026-09-02 — funn

Review av 67884be..c4ba898 før utrulling. Se CLAUDE.md «Pre-deployment review».

### Sikkerhet

- [x] **HØY: Hemmeligheter og Data Protection-nøkler havner i Docker build-laget.**
      `.dockerignore` ekskluderer `**/.env`, som *ikke* matcher `auth/secret.env`, og hverken
      `db/` eller `dp_keys/`. Build-steget inneholder SMTP-credentials, VM-host, tre ukrypterte
      DP-nøkler, live-databasen og 830 MB backups (build-image: 4,43 GB, kontekst 1,2 GB).
      Runtime-imaget er rent — multi-stage, og `docker save saksappweb:latest` tar kun siste steg
      — så det utrullede artefaktet er ikke berørt. Men laget er lesbart lokalt, og DP-nøklene
      beskytter antiforgery-tokens i dag og `"DriveTokens"` etter Phase 6B.
      `.gitignore` dekker alt dette allerede; `.dockerignore` har aldri blitt oppdatert i takt.
      Fiks: legg til `auth/`, `**/db/`, `**/dp_keys/`, `*.tar` i `.dockerignore`

### Pålitelighet

- [x] **Opprydding kjører aldri når snapshot-skrivingen feiler.** `ApplyRetention()` kalles etter
      try/finally, så en exception fra `WriteSnapshotAsync` hopper over pruning. Full disk er
      nettopp tilfellet der skrivingen feiler *og* der pruning trengs mest — tjenesten kan ikke
      redde sin egen disk. Fiks: kjør opprydding også før snapshot
- [x] **Delvis `.gz` blir liggende ved avbrudd.** Faller cancellation-token under `CopyToAsync`,
      etterlates en avkortet `.gz` under sitt endelige navn, umulig å skille fra en gyldig backup;
      oppryddingen beholder den og sletter eldre, gyldige. Fiks: gzip til `.gz.tmp` og
      `File.Move` på plass

### Stabilitet

- [x] **Backup-filnavn bruker `DateTimeOffset.Now`, resten av koden `UtcNow`.** Harmløst i dag
      (container-TZ er UTC), men oppryddingen sorterer nå på tidsstempelet i filnavnet. Settes TZ,
      gir vintertidsomstilling ikke-monotone navn og feil filer slettes. Fiks: bruk `UtcNow`

### Korrekthet (innført i denne milepælen)

- [x] **`MeetingTime` tolkes som UTC, men fylles ut som lokal klokkeslett.** `MeetingBoundary`
      bygger `DateTimeOffset` med `TimeSpan.Zero`, mens `<input type="time">` gir norsk lokaltid og
      `CreatedAt` er `UtcNow`. Møte 18:00 lokalt er 16:00 UTC, så vinduet strekker seg ~2 t
      (1 t om vinteren) forbi reell møtestart og tar med hendelser registrert under møtet.
      Feiler i trygg retning — ingenting droppes — men semantikken er feil

### Ytelse

- [ ] **Agenda-PDF materialiserer 12 MB vedlegg per generering** (7 MB med gammelt filter;
      17 vedlegg, største 3 MB) som `byte[]` i minnet, uten minnegrenser på containeren.
      Utvidelsen av kategorifilteret nær doblet dette, og det vokser med hvert vedlegg
- [ ] `previousMinutesCandidates` laster alle eldre `MeetingEventLink` (130 rader i dag) før
      filtrering. Vokser med møter × agendapunkter. Eksisterende, ikke haster

### Vurdert uten funn

- **Responsivitet**: ingen regresjon. PDF-generering er synkron på request-stien (eksisterende);
  møteskjemaet gikk fra 3 til 4 `col-md-3`-kolonner og stables riktig på mobil
- **Code smells**: rent. Ingen etterlatte navn, kategoriliteraler er samlet, duplisert testklasse
  fjernet. Merk at `.dockerignore`/`.gitignore`-driften i sikkerhetsfunnet er samme type drift som
  forårsaket innkallings-feilen

---

## 🔴 HØY PRIORITET: Backup fyller disken — tjenesteavbrudd

**Symptom**: Cloud-VM-en går tom for diskplass og tjenesten faller ut.

**Rotårsak**: `DatabaseBackupService` tar timesvise snapshots (`DatabaseBackupService.cs:120`)
uten komprimering. `TryApplyRetention(200)` er hardkodet (`:67`) og glob-mønsteret `app-*.sqlite`
(`:74`) treffer aldri `.bz2`-filer, så de blir liggende for alltid. 200 × 37 MB = 7,4 GB selv når
oppryddingen virker. Nedlastingsarkivet `../SaksApp_backups` er nå **34 GB / 2324 filer**
(2248 ukomprimerte `.sqlite` = 33 GB, 60 `.bz2` = 999 MB).

- [x] Gjør oppryddingen konfigurerbar: `Backup:MaxFiles` (48), `Backup:MaxTotalBytes` (2 GB),
      `Backup:Compress` (true)
- [x] Gzip hvert snapshot: skriv `app-<ts>.sqlite.tmp`, stream gjennom `GZipStream` til
      `app-<ts>.sqlite.gz`, slett temp-fila i `finally`
      (målt: kun ~12 % spart — databasen er dominert av vedlegg som allerede er komprimert,
      så det er antalls- og størrelsesgrensene som gjør jobben, ikke komprimeringen)
- [x] Skriv om `TryApplyRetention` (`:70-88`): matche `.sqlite`, `.sqlite.gz` og `.sqlite.bz2`;
      sortere på tidsstempelet i filnavnet, **ikke** `CreationTimeUtc` (upålitelig på Linux);
      slette eldste ut over `MaxFiles`, deretter til totalen er under `MaxTotalBytes`; alltid
      beholde nyeste
- [x] Logg én oppsummeringslinje per runde: filer beholdt, filer slettet, bytes på disk
- [x] `deployment/download-backups.sh`: bygg ekskluderingsliste fra lokale filnavn (strippet for
      `.gz`/`.bz2`) og send til `rsync --exclude-from` — uten dette laster
      `--ignore-existing` ned alt på nytt så snart lokale filer er komprimert
- [x] `download-backups.sh`: gjør `LOCAL_PATH` absolutt ut fra skriptets egen plassering, dropp
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

- [x] Legg kategorisettet i én delt konstant (ved siden av
      `CasesController.ValidCaseEventCategories`): skrivbart sett (`general`, `avvik`, `tiltak`)
      og lesbart sett (de tre pluss legacy `comment`)
- [x] `IAgendaPdfDataService.cs:136`: filtrer på det lesbare settet
- [x] Døp om `allCommentEvents` og `commentAttachments*` til kategorinøytrale navn
- [x] Pek `ICaseQueryService.cs:156` på samme konstant
- [x] Rett regresjonstesten `AgendaPdfDataServiceTests.cs:132-161` — fixturen bruker
      `Category = "comment"` og passerer derfor mens produksjonskoden er ødelagt. Parametriser
      over `general`, `avvik`, `tiltak`, `comment`; `meeting` skal ikke med

---

## Ad-hoc: Møtetidspunkt — `MeetingTime`

12:00 UTC-ankeret i vindusberegningen (`IAgendaPdfDataService.cs:176-179, 215-218`) er vilkårlig
fordi `Meeting.MeetingDate` er en `DateOnly`. Møter planlegges alltid med både dato og klokkeslett
— modellen mangler feltet.

- [x] Legg til `MeetingTime` (`TimeOnly?`) på `Meeting` + migrering. `MeetingDate` forblir
      `DateOnly` (86 bruksteder)
- [x] Klokkeslettfelt i `Views/Meetings/Create.cshtml` og `Edit.cshtml`; vis dato + tid i
      Index/Details/Minutes og i PDF-hodene
- [x] Vinduets slutt = `MeetingDate` + `MeetingTime` når satt, ellers slutten av døgnet — aldri
      midt på dagen. Samme behandling for vindusstart mot forrige møte
- [x] Tester: hendelse før møtetidspunktet er med; etter er ute; uten tid satt er en hendelse
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
