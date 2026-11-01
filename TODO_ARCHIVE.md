# TODO_ARCHIVE.md — Fullførte seksjoner

Arkiv over fullførte faser og oppgaver. Gjenværende arbeid ligger i GitHub issues og milestones
(https://github.com/j0rg3n/saksapp/milestones). `[→ #N]` betyr at punktet ble flyttet til issue #N
da TODO.md ble avviklet 2026-11-01.

---

## Ad-hoc: Typografi i innkalling/referat-PDF (typography-report-2026-11-01.md)

Se SPEC.md «PDF typography». Brukerskrevet tekst og tredjepartsvedlegg er utenfor scope.

- [x] H1: `HeadingInline`-etiketter 10 pt Bold (`_h3Font`), ikke 12 pt
- [x] H2: vedleggsforside viser etikett (`Vedlegg 2.1`), ikke løpenummer — innkalling og referat
- [x] H3: vedleggsindeks med `s. <n>` høyrestilt i stedet for `10:`-prefiks; forkort lange navn
- [x] H4: fjern emoji/piktogrammer før tegning
- [x] M1/M7: historikklinjer `<dato> [badge] <tekst>` uten kolon; første linje målt mot bredden
      (fjern 38/28-tegnsklippingen i controlleren)
- [x] M2/M3: `AgendaTextSnapshot` vises bare under «Til møtet:», ikke i tillegg som eget avsnitt
- [x] M4: hengende innrykk for avsnitt som starter med listemerke
- [x] M6: keep-with-next for overskrifter, etiketter og linjer som slutter på `:`
- [x] M8: datoer `dd.MM.yyyy` (tidsfrist, historikk) via felles hjelper; historikkdato i
      `App:TimeZone`; fjern duplisert tidsfrist-logikk (innkalling + påminnelse)
- [x] §5: lik vedleggsforside for PDF og bilde; filnavn ombrytes
- [x] Tester for hvert punkt der det lar seg måle

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

- [→ #1] `auth/secret.env` har modus 0644 og `caddy.env` 0664. Deploy-skriptet bruker `rsync -av`, som
      bevarer rettighetene, så SMTP-credentials havner verdenslesbare på VM-en. Lav reell
      eksponering (bare root logger inn), men unødvendig. Fiks: `chmod 600 auth/*` og
      `--chmod=F600` på rsync

**Verifisert lukket**: build-konteksten er 1,2 GB → **13 MB**, og `auth/`, `dp_keys/`, `db/` og
`*.tar` er borte fra build-laget. Appen publiserer kun `127.0.0.1:8080` på VM-en, og Caddys
admin-port 2019 er ikke publisert — det er nettopp derfor det er trygt å stole på
`X-Forwarded-*` med tømt `KnownProxies`.

### Pålitelighet

- [→ #2] **`saksappweb` har `restart=no`, mens `caddy` har `unless-stopped`.** Dør app-containeren
      (OOM, krasj, feilet oppstart) blir den liggende, og Caddy svarer 502 til noen oppdager det.
      systemd-unit-en er `Type=oneshot` med `RemainAfterExit=yes` og starter den ikke på nytt.
      Anbefalt: `restart: unless-stopped` også på appen — én linje, samme policy som proxyen
- [→ #18] **`Backup:MinFreeBytes`-vakten kan stoppe backup i ubestemt tid med bare en logglinje per
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

- [→ #3] `docker-compose.override.yml` er committet og lastes automatisk lokalt. Ufarlig fordi
      deploy-skriptet bare overfører `docker-compose.yml`, men den som rsync-er hele repoet til en
      server vil stille bytte Caddy til `saksapp.test` og brekke TLS. Verdt en advarsel i filen
- [→ #4] `Caddyfile` og `Caddyfile.local` dupliserer site-blokken; greit for lesbarhet, men de kan
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
- [→ #16, #17] Phase 6B (Drive-opplasting) er den varige losningen: kort lokal oppbevaring, historikk offsite

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
- [x] A-record for produksjonsdomenet mot VM-ens IP (verifisert 2026-09-08)
- [x] `Caddyfile` med automatisk Let's Encrypt; Caddy publiserer 80 og 443
- [x] `docker-compose.yml`: legg til `caddy`-tjenesten; appen slutter å publisere offentlig port og
      binder bare `127.0.0.1:8080` for lokal debugging
- [x] `Program.cs`: `UseForwardedHeaders` (`X-Forwarded-For`, `X-Forwarded-Proto`) først i
      pipelinen, og `Cookie.SecurePolicy = Always` utenfor Development
- [x] `deployment/deploy-upcloud.sh`: overfør `Caddyfile` og `auth/secret.env`-avhengige variabler
- [x] Åpne port 443 i VM-ens brannmur (verifisert 2026-09-08)
- [x] Verifiser lokalt med `SAKSAPP_DOMAIN=localhost` (Caddy bruker intern CA, ingen ACME), og i
      produksjon at HTTP redirigerer til HTTPS og at sertifikatet er gyldig

---

## Pre-deploy-review 2026-09-02 (runde 2) — funn fra PDF-endringene

Review av innkalling-badges + sidetallsfiksen. Ingen funn blokkerer utrulling.

### Pålitelighet

- [→ #8] `FontResolverBootstrap.Initialize` svelger «already used»-unntaket uten logging (statisk
      klasse, ingen logger). Feiler resolveren reelt, dukker det opp senere som en uklar font-feil

### Code smells

- [→ #7] **`WriteAttachmentTocEntry` og `WriteAttachmentTocEntryLabel` er nå ubrukte** — den siste ble
      foreldreløs av sidetallsfiksen. Bør fjernes, men gjør det som en egen, forsiktig endring:
      et forsøk under denne runden kuttet for bredt og tok med `OutcomeBadge` og
      `DeferredTocPageNumber`; ble rullet tilbake
- [→ #7] **`ApplyPendingLinks` / `_pendingLinks` / `WriteTextWithAttachmentLinks` /
      `AddPdfAttachmentStart` er ubrukt.** `ApplyPendingLinks` har dessuten de *samme* to feilene
      som nettopp ble rettet i TOC-stien: `TargetPageNumber - 1` (peker én side for tidlig) og
      `new XPoint(link.Y, ...)` som bruker Y som X-koordinat. Kobles den noen gang på, er den feil
      fra dag én. Fjern, eller rett samtidig
- [→ #9] `DrawInlineBadge` parser fargen med `Convert.ToInt32(colorHex.Substring(...))` uten
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

- [→ #10] **Agenda-PDF materialiserer 12 MB vedlegg per generering** (7 MB med gammelt filter;
      17 vedlegg, største 3 MB) som `byte[]` i minnet, uten minnegrenser på containeren.
      Utvidelsen av kategorifilteret nær doblet dette, og det vokser med hvert vedlegg
- [→ #11] `previousMinutesCandidates` laster alle eldre `MeetingEventLink` (130 rader i dag) før
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
- [→ #19] Engangsopprydding av de 34 GB — først etter at Drive-opplasting er verifisert

---

## 🔴 HØY PRIORITET: Bugfix — Innkalling mangler hendelser siden forrige møte

**Symptom**: Innkallingen tar ikke med hendelser registrert siden forrige møte. Bekreftet mot
en produksjons-backup for møte #9 (2026-09-29): to hendelser (én `avvik`, én
`tiltak`) på to ulike saker mangler begge. Begge sakene sto sist på agendaen i møte 13 (2026-08-11), så vinduet er
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

- [→ #5] Legg til felt `AgendaPdfGeneratedAt` (nullable DateTime) på `Meeting` + migrering
- [→ #5] Sett `AgendaPdfGeneratedAt = DateTime.UtcNow` når agenda-PDF genereres
- [→ #5] I referat-visningen: for hvert agenda-punkt, hent CaseEvents på saken med
      `CreatedAt > meeting.AgendaPdfGeneratedAt AND CreatedAt <= meeting.MeetingDate`
- [→ #5] Vis disse under en seksjon «Utvikling siden innkallingen» per agenda-punkt
      (lesevisning, ikke redigerbar); render vedleggslenker som åpner i ny fane

---

## Ad-hoc: PDF — dedupliser vedlegg på tvers av saker

- [→ #6] I agenda-PDF og referat-PDF: samle alle vedlegg på tvers av agenda-punkter,
      dedupliser på `Attachment.Id`, og list hvert vedlegg kun én gang
      (f.eks. samlet vedleggsliste bakerst, eller én forekomst pr. agenda-punkt med
      en note «(se også sak X)»)
- [→ #6] Oppdater `PdfGeneratorService` (agenda og referat) tilsvarende

---


## Kjente feil (rapportert fra beta) ✅ FIKSET

- ~~**Vedlegg mangler i hendelsesredigering fra sakvisning**~~: Fikset — "Rediger"-knappen i sakens tidslinje peker nå til `CaseEvents/Edit` for alle hendelsestyper.
- ~~**Mange engelske termer i UI**~~: Fikset — systematisk gjennomgang av alle views; alle synlige tekster er nå oversatt til norsk bokmål.

---

## Fase 0: Test-oppsett ✅ KOMPLETT

- Testprosjekt opprettet med xUnit, Moq, EF Core SQLite
- 86 tester (76 bestått, 10 hoppet over grunn teknisk begrensninger)
- Coverage: ~9% line, ~44% method
- Se SPEC.md section "Testability Architecture" for detaljer

---

## Fase 0.1: Testability Refaktorering ✅ KOMPLETT

**Mål**: Ekstraher query-logikk og-abstrahere eksterne avhengigheter for å muliggjøre effektiv mocking og øke testbarhet.

### 0.1.1 Ekstraher Case Query Service ✅
- [x] Opprett `ICaseQueryService` interface
- [x] Implementer `CaseQueryService` med metoder: `GetFilteredCasesAsync`, `GetCaseDetailsAsync`
- [x] Oppdater `CasesController` til å bruke `ICaseQueryService`
- [x] Legg til tester for CaseQueryService

### 0.1.2 Ekstraher Meeting Query Service ✅
- [x] Opprett `IMeetingQueryService` interface
- [x] Implementer `MeetingQueryService` med metoder: `GetAllMeetingsAsync`, `GetMeetingWithAgendaAsync`, `GetMeetingWithMinutesAsync`
- [x] Oppdater `MeetingsController` til å bruke `IMeetingQueryService`
- [x] Legg til tester for MeetingQueryService

### 0.1.3 Ekstraher User Display Service ✅
- [x] Opprett `IUserDisplayService` interface
- [x] Implementer `UserDisplayService.GetDisplayNamesAsync(userIds)`
- [x] Oppdater controllere til å bruke `IUserDisplayService`
- [x] Legg til tester

### 0.1.4 Abstraher PDF Generator ✅
- [x] Opprett `ISimplePdfWriter` interface + `ISimplePdfWriterFactory`
- [x] `SimplePdfWriter` implementerer `ISimplePdfWriter`
- [x] `SimplePdfWriterFactory` registrert som singleton i DI
- [x] `MeetingsController` injiserer `ISimplePdfWriterFactory`, bruker `_pdfFactory.Create()`

### 0.1.5 Abstraher HTML Parser ✅
- [x] Opprett `IHtmlCaseImporter` interface med `ImportAsync(html, ct)` metode
- [x] `HtmlCaseImporter` implementerer `IHtmlCaseImporter`
- [x] `ImportController` bruker `IHtmlCaseImporter` via DI
- [x] 3 tidligere skippede `ImportControllerTests` er nå aktivert og bestått med Moq

### 0.1.6 Abstraher Backup Service ✅
- [x] Opprett `IDatabaseBackupExecutor` interface
- [x] Ekstraher backup-logikk fra `DatabaseBackupService` til `DatabaseBackupExecutor`
- [x] `DatabaseBackupService` er nå en tynn timer som injiserer `IDatabaseBackupExecutor`
- [x] Lagt til 2 tester: CreateBackupAsync oppretter fil, hopper over når DB mangler

### 0.1.7 Verifiser Coverage ✅
- [x] Kjør test suite med coverage
- [x] Lagt til CaseQueryServiceTests (8), MeetingQueryServiceTests (6 nye), controllertest utvidelser (16 nye)
- [x] Coverage: 19.25% line, 26.8% branch, 63.88% method — 157 tester

### 0.1.8 Ekstraher PDF Data-tjenester ✅
- [x] Opprett `IAgendaPdfDataService` / `AgendaPdfDataService`
- [x] Opprett `IMinutesPdfDataService` / `MinutesPdfDataService`
- [x] Legg til `CapturingPdfWriter` test-dobbel
- [x] Tester: AgendaPdfDataServiceTests (6), MinutesPdfDataServiceTests (6), controller PDF-tester (4 nye)
- [x] Oppdater `MeetingsController` og registrer i `Program.cs`

### 0.1.9 Ekstraher Minutes Save Service ✅
- [x] Opprett `IMinutesSaveService` / `MinutesSaveService`
- [x] Tester: MinutesSaveServiceTests (6)
- [x] Oppdater `MeetingsController.Minutes` (POST) til å delegere til tjenesten

**Coverage etter 0.1.8+0.1.9**: 19.25% line, 26.8% branch, 63.88% method — 157 tester (153 bestått, 4 hoppet over)

---

## Phase 1: Terminologi-oppdateringer ✅ KOMPLETT

### 1.1 Oppdater Enum-verdier
- [x] **Task 1.1.1**: Endre `MeetingCaseOutcome.Info` → `Orientering` i modellen

### 1.2 Oppdater View Labels
- [x] **Task 1.2.1**: Endre "Outcome" → "Utfall" i Minutes.cshtml
- [x] **Task 1.2.2**: Endre "Official notes" → "Referat" i Minutes.cshtml
- [x] **Task 1.2.3**: Endre "Decision" → "Vedtak" i Minutes.cshtml
- [x] **Task 1.2.4**: Endre "Follow-up" → "Oppfølging" i Minutes.cshtml
- [x] **Task 1.2.5**: Endre "Attendance" → "Oppmøte" i Minutes.cshtml
- [x] **Task 1.2.6**: Endre "Absence" → "Forfall" i Minutes.cshtml
- [x] **Task 1.2.7**: N/A — "Outcome" finnes ikke i EditAgendaItem.cshtml

---

## Phase 2: Database-migrering ✅ KOMPLETT

### 2.1 Opprett nye modeller
- [x] **Task 2.1.1**: Opprett `CaseEvent` modell
- [x] **Task 2.1.2**: Opprett `CaseEventCase` modell (+ `CaseEventAttachment`)
- [x] **Task 2.1.3**: Opprett `MeetingEventLink` modell
- [x] **Task 2.1.4**: Opprett EF Core migrering for nye tabeller (`AddCaseEventSystem`)
- [x] **Task 2.1.5**: Verifisert via bygg og tester

### 2.2 Migrer eksisterende data
- [x] **Task 2.2.1**: Migreringsscript for `CaseComment` → `CaseEvent`
- [x] **Task 2.2.2**: Testet: 115 kommentarer → 115 CaseEvents i prod-backup
- [x] **Task 2.2.3**: Migreringsscript for `MeetingMinutesCaseEntry` → `MeetingEventLink`
- [x] **Task 2.2.4**: Testet: 103 entries → 103 MeetingEventLinks med minutes-data
- [x] **Task 2.2.5**: `MeetingCase` → `MeetingEventLink` (samme script)
- [x] **Task 2.2.6**: Alle 103 MeetingCases dekket, alle kommentarer koblet til riktig sak

### 2.3 Oppdater modeller etter migrering
- [x] **Task 2.3.1**: `CaseEvent` arver fra `SoftDeletableEntity`
- [x] **Task 2.3.2**: `IsEventuelt` lagt til `MeetingEventLink`

---

## Phase 3: API/Controllers ✅ KOMPLETT

### 3.1 Oppdater CaseController
- [x] **Task 3.1.1**: `AddComment` oppretter `CaseEvent`+`CaseEventCase`
- [x] **Task 3.1.2**: `SoftDeleteComment`, `EditComment` bruker `CaseEvent`
- [x] **Task 3.1.3**: `GetCaseDetailsAsync` leser fra `CaseEvents`+`MeetingEventLinks`

### 3.2 Oppdater MeetingsController
- [x] **Task 3.2.1**: `Minutes`/`SaveMinutes` bruker `MeetingEventLink`
- [x] **Task 3.2.2**: `EditAgendaItem` lagrer til `MeetingEventLink`
- [x] **Task 3.2.3**: `AddCase` oppretter `CaseEvent`+`MeetingEventLink`+`CaseEventCase`
- [x] **Task 3.2.4**: Alle 162 tester passerer

### 3.3 Legg til CaseEvent-api
- [x] **Task 3.3.1**: Opprett `CaseEventsController` (Hendelseslogg)
- [x] **Task 3.3.2**: CRUD for CaseEvent: Index, Create, Edit, SoftDelete

---

## Phase 4: UI — Minutes Fokusert Visning ✅ KOMPLETT

### 4.1 Endre Minutes-visning
- [x] **Task 4.1.1**: Oppdater `MeetingMinutesVm` med `CurrentIndex`, `TotalCount`
- [x] **Task 4.1.2**: Endre Minutes.cshtml til fokusert visning (én sak om gangen)
- [x] **Task 4.1.3**: Legg til Previous/Next navigering i Minutes.cshtml
- [x] **Task 4.1.4**: Legg til GET/POST actions for Previous/Next

### 4.2 Eventuelt som sak
- [x] **Task 4.2.1**: Endre Eventuelt tekstfelt til "Legg til eventuelt-punkt" form
- [x] **Task 4.2.2**: `AddEventueltItem` POST oppretter CaseEvent + MeetingEventLink med IsEventuelt=true
- [x] **Task 4.2.3**: Lagre Eventuelt-status i `MeetingEventLink.IsEventuelt`

---

## Phase 4.5: Hendelsesmodell — Konsolidering ✅ KOMPLETT

**Mål**: Fjerne kunstig skille mellom "saksmerknad" og frittstående hendelser.

### 4.5.1 Kategorivalg i sakvisning
- [x] Erstatt "Add comment" form med "Legg til hendelse" form med kategorivalg
- [x] `CasesController.AddComment` → `AddEvent`, oppretter CaseEvent med valgt kategori
- [x] Oppdater view og action-navn konsistent

### 4.5.2 Slett fra sakvisning for alle hendelsestyper
- [x] Legg til slett-knapp for avvik/tiltak/general events i sakens tidslinje
- [x] Bruk `CaseEventsController.SoftDelete`

### 4.5.3 Multi-sak badges i tidslinje
- [x] Legg til `LinkedCases` på `CaseTimelineItemVm`
- [x] Populer fra `CaseEventCases` i `GetCaseDetailsAsync`
- [x] Vis som klikkbare badges i sakens tidslinje

### 4.5.4 Vedlegg-opplasting for alle hendelsestyper fra sakvisning
- [x] Legg til upload-skjema for avvik/tiltak/general i sakens tidslinje
- [x] Legg til `UploadBoardEventAttachment` og `RemoveBoardEventAttachment` actions
- [x] Vis vedlegg med slett-knapp for redigerbare hendelser

---

## Phase 5: PDF-endringer ✅ KOMPLETT

### 5.1 Fiks Heading-nivåer
- [x] **Task 5.1.1**: Standardiser H1/H2/H3 i Agenda PDF
- [x] **Task 5.1.2**: Standardiser H1/H2/H3 i Minutes PDF

### 5.2 Per-case Vedlegg-nummerering
- [x] **Task 5.2.1**: Endre logikk i `DownloadAgendaPdf` til per-case (2.1, 2.2)
- [x] **Task 5.2.2**: Endre logikk i `DownloadMinutesPdf` til per-case
- [x] **Task 5.2.3**: Testet i generert PDF

### 5.3 Utfall Badge
- [x] **Task 5.3.1**: Vis Utfall som badge (● farge) i Minutes PDF
- [x] **Task 5.3.2**: Fargekart: Blå=Fortsetter, Grønn=Avsluttet, Grå=Utsatt, Lilla=Orientering, Gul=Diskusjon

### 5.4 Diskusjon-utfall
- [x] Legg til `Discussion = 5` i `MeetingCaseOutcome`-enum
- [x] Legg til "Diskusjon" i utfallsvelger i Minutes-visningen

---

## Phase 6A: Brukerroller og godkjenning ✅ KOMPLETT

**Mål**: Innføre admin/vanlig-bruker-skille og krav om godkjenning for nye brukere.

### 6A.1 Datamodell
- [x] Legg til `IsApproved` og `IsAdmin` på `ApplicationUser`
- [x] EF Core-migrering for nye kolonner
- [x] Første bruker auto-admin via `AppUserManager.CreateAsync`

### 6A.2 Registrerings- og innloggingsflyt
- [x] Override login-flow: unapproved → "Din konto venter på godkjenning"-side
- [x] Implementer `RequireApprovedUserMiddleware`
- [x] Første bruker som registrerer seg får IsAdmin=true og IsApproved=true automatisk

### 6A.3 Brukeradministrasjon (kun admin)
- [x] Oppdater `UsersController` med `[AdminOnly]`-filter
- [x] Kolonne for godkjenningsstatus og admin-status i brukerliste
- [x] "Godkjenn"/"Avvis" knapper for ventende brukere
- [x] "Gjør til admin"/"Fjern admin" knapper (ikke på seg selv)
- [x] Tester: AppUserManagerTests, AdminOnlyFilterTests, RequireApprovedUserMiddlewareTests

### 6A.4 Pentest (automatiserte integrasjonstester)
- [x] `WebApplicationFactory`-baserte integrasjonstester for T6A-01 t.o.m. T6A-10
- [x] Dekker: middleware-redirect, CSRF-validering, AdminOnly-filter, masse-assignment-beskyttelse, selvdemotering
- [x] 208 tester passerer
