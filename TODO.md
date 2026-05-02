# TODO.md - Implementeringsplan

> Se SPEC.md for detaljert spesifikasjon. Denne filen holder oversikt over fremdrift og gjenværende oppgaver.

---

## Kjente feil (rapportert fra beta)

- **Vedlegg mangler i hendelsesredigering fra sakvisning**: Når man trykker "Rediger" på en hendelse i sakens tidslinje, åpnes `Cases/EditComment` som ikke har vedlegg-håndtering. `CaseEvents/Edit` har det, men nås ikke fra denne lenken. Fiks: enten redirect til `CaseEvents/Edit` fra sakvisningen, eller legg til vedlegg-seksjon i `Cases/EditComment`.
- **Mange engelske termer i UI**: Gjenstår en rekke engelske labels, knapper og overskrifter i grensesnittet. Systematisk gjennomgang nødvendig — se terminologitabellen i seksjon 1 nedenfor.

---

## Fase 0: Test-oppsett ✅ KOMPLETT

- Testprosjekt opprettet med xUnit, Moq, EF Core SQLite
- 86 tester (76 bestått, 10 hoppet over grunn teknisk begrensninger)
- Coverage: ~9% line, ~44% method
- Se SPEC.md section "Testability Architecture" for detaljer

---

## Fase 0.1: Testability Refaktorering ✅ KOMPLETT

**Mål**: Ekstraher query-logikk og-abstrahere eksterne avhengigheter for å muliggjøre effektiv mocking og øke testbarhet.

### 0.1.1 Ekstraher Case Query Service ✅ KOMPLETT
- [x] Opprett `ICaseQueryService` interface
- [x] Implementer `CaseQueryService` med metoder:
  - `GetFilteredCasesAsync(status, assigneeUserId, showClosed)`
  - `GetCaseDetailsAsync(id)`
- [x] Oppdater `CasesController` til å bruke `ICaseQueryService`
- [x] Legg til tester for CaseQueryService

### 0.1.2 Ekstraher Meeting Query Service ✅ KOMPLETT
- [x] Opprett `IMeetingQueryService` interface
- [x] Implementer `MeetingQueryService` med metoder:
  - `GetAllMeetingsAsync()`
  - `GetMeetingWithAgendaAsync(id)`
  - `GetMeetingWithMinutesAsync(id)`
- [x] Oppdater `MeetingsController` til å bruke `IMeetingQueryService`
- [x] Legg til tester for MeetingQueryService

### 0.1.3 Ekstraher User Display Service ✅ KOMPLETT
- [x] Opprett `IUserDisplayService` interface
- [x] Implementer `UserDisplayService.GetDisplayNamesAsync(userIds)`
- [x] Oppdater controllere til å bruke `IUserDisplayService`
- [x] Legg til tester

### 0.1.4 Abstraher PDF Generator ✅ KOMPLETT
- [x] Opprett `ISimplePdfWriter` interface + `ISimplePdfWriterFactory`
- [x] `SimplePdfWriter` implementerer `ISimplePdfWriter`
- [x] `SimplePdfWriterFactory` registrert som singleton i DI
- [x] `MeetingsController` injiserer `ISimplePdfWriterFactory`, bruker `_pdfFactory.Create()`

### 0.1.5 Abstraher HTML Parser ✅ KOMPLETT
- [x] Opprett `IHtmlCaseImporter` interface med `ImportAsync(html, ct)` metode
- [x] `HtmlCaseImporter` implementerer `IHtmlCaseImporter`
- [x] `ImportController` bruker `IHtmlCaseImporter` via DI
- [x] 3 tidligere skippede `ImportControllerTests` er nå aktivert og bestått med Moq

### 0.1.6 Abstraher Backup Service ✅ KOMPLETT
- [x] Opprett `IDatabaseBackupExecutor` interface
- [x] Ekstraher backup-logikk fra `DatabaseBackupService` til `DatabaseBackupExecutor`
- [x] `DatabaseBackupService` er nå en tynn timer som injiserer `IDatabaseBackupExecutor`
- [x] Lagt til 2 tester: CreateBackupAsync oppretter fil, hopper over når DB mangler

### 0.1.7 Verifiser Coverage (IN PROGRESS)
- [x] Kjør test suite med coverage
- [x] Lagt til CaseQueryServiceTests (8 tester), MeetingQueryServiceTests (6 nye), controllertest utvidelser (16 nye)
- [x] Coverage: 19.25% line, 26.8% branch, 63.88% method — 157 tester, 153 bestått (etter 0.1.8+0.1.9)
- [x] 75%-mål forlatt som urealistisk — nytt mål: +10pp (se seksjon 6 nederst)

### 0.1.8 Ekstraher PDF Data-tjenester ✅ KOMPLETT
- [x] Opprett `IAgendaPdfDataService` / `AgendaPdfDataService` — ekstraher data-henting fra `DownloadAgendaPdf`
- [x] Opprett `IMinutesPdfDataService` / `MinutesPdfDataService` — ekstraher data-henting fra `DownloadMinutesPdf`
- [x] Legg til `CapturingPdfWriter` test-dobbel (implementerer `ISimplePdfWriter`, logger kall uten PDF-infrastruktur)
- [x] Tester: `AgendaPdfDataServiceTests` (6 tester), `MinutesPdfDataServiceTests` (6 tester), controller PDF-tester (4 nye)
- [x] Oppdater `MeetingsController` til å bruke de nye tjenestene
- [x] Registrer i `Program.cs`

### 0.1.9 Ekstraher Minutes Save Service ✅ KOMPLETT
- [x] Opprett `IMinutesSaveService` / `MinutesSaveService` — ekstraher POST-logikk fra `Minutes`-action
- [x] Tester: `MinutesSaveServiceTests` (6 tester)
- [x] Oppdater `MeetingsController.Minutes` (POST) til å delegere til tjenesten

**Coverage etter 0.1.8+0.1.9**: 19.25% line, 26.8% branch, 63.88% method — 157 tester (153 bestått, 4 hoppet over)

**Test**: Kjør `docker compose --profile test run --rm test` og verifiser coverage

---

## 1. Gjenværende Faser

Tabellen under viser nåværende engelske termer i kode og UI, og deres anbefalte norske (Borettslag) termer.

| Nåværende (Engelsk) | Nåværende (i kode/UI) | Anbefalt Norsk (Borettslag) | Merknad |
|---------------------|----------------------|---------------------------|---------|
| Case | Case / Sak | Sak | |
| Meeting | Meeting / Møte | Møte | |
| Agenda | Agenda / Innkalling | Innkalling / Dagsorden | "Innkalling" for innkalling PDF |
| Minutes | Minutes / Referat | Referat | |
| Board | Board / Styret | Styret | |
| Case Event / Comment | CaseComment / Saksmerknad | Saksmerknad | |
| Outcome | Outcome / Resultat | Utfall | |
| Follow-up | Follow-up / Oppfølging | Oppfølging | |
| Decision | Decision / Vedtak | Vedtak | |
| Deviation | Deviation / Avvik | Avvik | |
| Measure | Measure / Tiltak | Tiltak | |
| Attachment | Attachment / Vedlegg | Vedlegg | |
| Priority | Priority / Prioritet | Prioritet | |
| Status | Status | Status | |
| Theme | Theme / Tema | Tema | |
| Assignee | Assignee / Ansvarlig | Ansvarlig | |
| Tidsfrist | Tidsfrist | Tidsfrist | |
| Attendance | Attendance / Oppmøte | Oppmøte | |
| Absence | Absence / Forfall | Forfall | |
| Eventuelt | Eventuelt | Eventuelt | |
| Continue (outcome) | Fortsetter | Fortsetter | |
| Closed (outcome) | Avsluttet | Avsluttet | |
| Deferred (outcome) | Utsatt | Utsatt | |
| Info (outcome) | Informasjon | Orientering | |

---

## 2. Database-endringer

### 2.1 Unifisering av modeller

**Mål**: Erstatte `CaseComment` og `MeetingMinutesCaseEntry` med `CaseEvent`, `CaseEventCase`, og `MeetingEventLink`.

#### Nye/entiteter

```csharp
// CaseEvent - erstatter CaseComment
public class CaseEvent
{
    public int Id { get; set; }
    public int? BoardCaseId { get; set; }           // Kan være null for frittstående hendelser
    public DateTimeOffset CreatedAt { get; set; }
    public string Content { get; set; } = "";
    public string Category { get; set; } = "";      // "saksmerknad", "generell", "avvik", "tiltak"
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

// CaseEventCase - many-to-many mellom CaseEvent og BoardCase
public class CaseEventCase
{
    public int Id { get; set; }
    public int CaseEventId { get; set; }
    public int BoardCaseId { get; set; }
}

// MeetingEventLink - erstatter MeetingCase og delvis MeetingMinutesCaseEntry
public class MeetingEventLink
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public int CaseEventId { get; set; }
    public int AgendaOrder { get; set; }
    
    // Disse feltene erstatter MeetingMinutesCaseEntry feltene:
    public string? OfficialNotes { get; set; }      // "Referat"
    public string? DecisionText { get; set; }       // "Vedtak"  
    public string? FollowUpText { get; set; }        // "Oppfølging"
    public MeetingCaseOutcome? Outcome { get; set; } // "Utfall"
    public bool IsEventuelt { get; set; }           // Eventuelt-status
}
```

---

## 3. Oppgaver (Task Tree)

### Phase 1: Terminologi-oppdateringer (Enkle tekstoppdateringer) ✅ KOMPLETT

#### 1.1 Oppdater Enum-verdier
- [x] **Task 1.1.1**: Endre `MeetingCaseOutcome.Info` → `Orientering` i modellen

#### 1.2 Oppdater View Labels
- [x] **Task 1.2.1**: Endre "Outcome" → "Utfall" i Minutes.cshtml
- [x] **Task 1.2.2**: Endre "Official notes" → "Referat" i Minutes.cshtml
- [x] **Task 1.2.3**: Endre "Decision" → "Vedtak" i Minutes.cshtml
- [x] **Task 1.2.4**: Endre "Follow-up" → "Oppfølging" i Minutes.cshtml
- [x] **Task 1.2.5**: Endre "Attendance" → "Oppmøte" i Minutes.cshtml
- [x] **Task 1.2.6**: Endre "Absence" → "Forfall" i Minutes.cshtml
- [x] **Task 1.2.7**: N/A — "Outcome" finnes ikke i EditAgendaItem.cshtml

**Test**: Bygg og verifiser at appen kjører. ✅ 157 tester passerer.

---

### Phase 2: Database-migrering ✅ KOMPLETT

#### 2.1 Opprett nye modeller
- [x] **Task 2.1.1**: Opprett `CaseEvent` modell
- [x] **Task 2.1.2**: Opprett `CaseEventCase` modell (+ `CaseEventAttachment`)
- [x] **Task 2.1.3**: Opprett `MeetingEventLink` modell
- [x] **Task 2.1.4**: Opprett EF Core migrering for nye tabeller (`AddCaseEventSystem`)
- [x] **Task 2.1.5**: Verifisert via bygg og tester

#### 2.2 Migrer eksisterende data
- [x] **Task 2.2.1**: Migreringsscript for `CaseComment` → `CaseEvent` (i `MigrateLegacyDataToCaseEvents`)
- [x] **Task 2.2.2**: Testet: 115 kommentarer → 115 CaseEvents i prod-backup
- [x] **Task 2.2.3**: Migreringsscript for `MeetingMinutesCaseEntry` → `MeetingEventLink`
- [x] **Task 2.2.4**: Testet: 103 entries → 103 MeetingEventLinks med minutes-data
- [x] **Task 2.2.5**: `MeetingCase` → `MeetingEventLink` (samme script)
- [x] **Task 2.2.6**: Alle 103 MeetingCases dekket, alle kommentarer koblet til riktig sak

#### 2.3 Oppdater modeller etter migrering
- [x] **Task 2.3.1**: `CaseEvent` arver fra `SoftDeletableEntity`
- [x] **Task 2.3.2**: `IsEventuelt` lagt til `MeetingEventLink`

**Test**: 162 tester passerer. ✅

---

### Phase 3: API/Controllers ✅ KOMPLETT (3.1+3.2; 3.3 utsatt)

#### 3.1 Oppdater CaseController
- [x] **Task 3.1.1**: `AddComment` oppretter `CaseEvent`+`CaseEventCase`
- [x] **Task 3.1.2**: `SoftDeleteComment`, `EditComment` bruker `CaseEvent`
- [x] **Task 3.1.3**: `GetCaseDetailsAsync` leser fra `CaseEvents`+`MeetingEventLinks`

#### 3.2 Oppdater MeetingsController
- [x] **Task 3.2.1**: `Minutes`/`SaveMinutes` bruker `MeetingEventLink`
- [x] **Task 3.2.2**: `EditAgendaItem` lagrer til `MeetingEventLink`
- [x] **Task 3.2.3**: `AddCase` oppretter `CaseEvent`+`MeetingEventLink`+`CaseEventCase`
- [x] **Task 3.2.4**: Alle 162 tester passerer

#### 3.3 Legg til CaseEvent-api ✅ KOMPLETT
- [x] **Task 3.3.1**: Opprett `CaseEventsController` (Hendelseslogg)
- [x] **Task 3.3.2**: CRUD for CaseEvent: Index, Create, Edit, SoftDelete

**Test**: 162 tester passerer. ✅

---

### Phase 4: UI - Minutes Fokusert Visning ✅ KOMPLETT

#### 4.1 Endre Minutes-visning ✅
- [x] **Task 4.1.1**: Oppdater `MeetingMinutesVm` med `CurrentIndex`, `TotalCount`
- [x] **Task 4.1.2**: Endre Minutes.cshtml til fokusert visning (én sak om gangen)
- [x] **Task 4.1.3**: Legg til Previous/Next navigering i Minutes.cshtml
- [x] **Task 4.1.4**: Legg til GET/POST actions for Previous/Next

#### 4.2 Eventuelt som sak ✅
- [x] **Task 4.2.1**: Endre Eventuelt tekstfelt til "Legg til eventuelt-punkt" form
- [x] **Task 4.2.2**: `AddEventueltItem` POST oppretter CaseEvent + MeetingEventLink med IsEventuelt=true
- [x] **Task 4.2.3**: Lagre Eventuelt-status i `MeetingEventLink.IsEventuelt`

---

### Phase 4.5: Hendelsesmodell — Konsolidering

**Mål**: Fjerne kunstig skille mellom "saksmerknad" (comment) og frittstående hendelser. Alle sakhendelser bruker samme kategorier (general/avvik/tiltak) og har samme funksjonalitet uavhengig av om de ble opprettet fra saken eller fra Hendelseslogg.

#### 4.5.1 Kategorivalg i sakvisning
- [ ] Erstatt "Add comment" form med "Legg til hendelse" form med kategorivalg (Generelt/Avvik/Tiltak, default: Generelt)
- [ ] `CasesController.AddComment` → `AddEvent`, oppretter CaseEvent med valgt kategori (ikke hardkodet "comment")
- [ ] Oppdater view og action-navn konsistent

#### 4.5.2 Slett fra sakvisning for alle hendelsestyper
- [ ] Legg til slett-knapp for avvik/tiltak/general events i sakens tidslinje
- [ ] Bruk `CaseEventsController.SoftDelete` (redirect tilbake til saken etter sletting)

#### 4.5.3 Multi-sak badges i tidslinje
- [ ] Legg til `LinkedCases` (liste av `LinkedCaseSummary`) på `CaseTimelineItemVm`
- [ ] Populer fra `CaseEventCases` i `GetCaseDetailsAsync` — filtrer ut gjeldende sak, vis de andre
- [ ] Vis som klikkbare badges i sakens tidslinje

#### 4.5.4 Vedlegg-opplasting for alle hendelsestyper fra sakvisning
- [ ] Legg til upload-skjema for avvik/tiltak/general i sakens tidslinje (samme mønster som for comment)
- [ ] Legg til `UploadBoardEventAttachment` og `RemoveBoardEventAttachment` actions i CasesController
- [ ] Vis vedlegg med slett-knapp for redigerbare hendelser

**Test**: Bygg og kjør manuelle tester. Legg til tester for nye actions.

---

### Phase 5: PDF-endringer

#### 5.1 Fiks Heading-nivåer
- [ ] **Task 5.1.1**: Gå gjennom Agenda PDF og standardiser H1/H2/H3
- [ ] **Task 5.1.2**: Gå gjennom Minutes PDF og standardiser H1/H2/H3

#### 5.2 Per-case Vedlegg-nummerering
- [ ] **Task 5.2.1**: Endre logikk i `DownloadAgendaPdf` til per-case (2.1, 2.2)
- [ ] **Task 5.2.2**: Endre logikk i `DownloadMinutesPdf` til per-case
- [ ] **Task 5.2.3**: Test at vedlegg-nummerering er korrekt i generert PDF

#### 5.3 Utfall Badge
- [ ] **Task 5.3.1**: Endre Minutes PDF til å vise Utfall som badge (● farge)
- [ ] **Task 5.3.2**: Definer fargekart: Blå=Fortsetter, Grønn=Avsluttet, Grå=Utsatt, Lilla=Orientering

**Test**: Generer begge PDF-er og verifiser layout.

---

### Phase 6: Nye Features (Fremtidig)

#### 6.1 Board Log (Styrelogg)
- [ ] **Task 6.1.1**: Opprett Board Log controller og view
- [ ] **Task 6.1.2**: Implementer filter (kategori, dato, sak)
- [ ] **Task 6.1.3**: Vis kronologisk liste av CaseEvents uten MeetingEventLink

#### 6.2 HMS Avvik og Tiltak
- [ ] **Task 6.2.1**: Opprett HMS controller og view
- [ ] **Task 6.2.2**: Vis sammendrag (tellere)
- [ ] **Task 6.2.3**: Implementer auto-lukk av Avvik når relatert sak lukkes
- [ ] **Task 6.2.4**: Tillat Avvik uten sak (null CaseId)

---

### Phase 6A: Brukerroller og godkjenning

**Mål**: Innføre admin/vanlig-bruker-skille og krav om godkjenning for nye brukere.

#### 6A.1 Datamodell
- [ ] Legg til `IsApproved` (bool, default false) og `IsAdmin` (bool, default false) på `ApplicationUser`
- [ ] EF Core-migrering for nye kolonner
- [ ] Ved oppstart: hvis ingen brukere finnes enda, sett første bruker til IsApproved=true, IsAdmin=true automatisk etter registrering (via seed-logikk i Program.cs)

#### 6A.2 Registrerings- og innloggingsflyt
- [ ] Override login-flow: hvis `!IsApproved`, redirect til "Din konto venter på godkjenning"-side i stedet for å gi tilgang
- [ ] Implementer `RequireApprovedUser`-attributt eller middleware som sjekker godkjenningsstatus på alle [Authorize]-sider
- [ ] Første bruker som registrerer seg får IsAdmin=true og IsApproved=true automatisk

#### 6A.3 Brukeradministrasjon (kun admin)
- [ ] Oppdater `UsersController` med `[AdminOnly]`-filter (eller policy)
- [ ] Legg til kolonne for godkjenningsstatus og admin-status i brukerliste
- [ ] Legg til "Godkjenn"/"Avvis" knapper for ventende brukere
- [ ] Legg til "Gjør til admin"/"Fjern admin" knapper (ikke på seg selv)
- [ ] Tester for admin-only tilgangskontroll

---

### Phase 6B: Google Drive Backup

**Mål**: Automatisk opplasting av SQLite-sikkerhetskopier til brukernes Google Drive, med mulighet for admin-gjenoppretting.

#### 6B.1 Google OAuth — Drive-tilkobling
- [ ] Registrer OAuth 2.0-app i Google Cloud Console (scope: `drive.file`)
- [ ] Legg til `UserDriveToken`-entitet: `UserId`, `AccessToken` (kryptert), `RefreshToken` (kryptert), `TokenExpiry`, `LinkedAt`
- [ ] EF Core-migrering
- [ ] Innstillingsside (`/settings`): "Koble til Google Drive"-knapp → OAuth-flyt → lagre tokens kryptert med ASP.NET Data Protection
- [ ] Vis koblingsstatus og "Koble fra"-knapp
- [ ] Krypter tokens med `IDataProtector` (purpose: `"DriveTokens"`)

#### 6B.2 Backup-tjeneste
- [ ] Legg til `DriveBackupLog`-entitet: `AttemptedAt`, `BackupDate`, `UserId`, `Success`, `ErrorMessage`
- [ ] EF Core-migrering
- [ ] Implementer `IGoogleDriveUploader` interface + `GoogleDriveUploader` — tar en SQLite-snapshot og laster opp til Drive med filnavn `saksapp-backup-YYYY-MM-DD.db`
- [ ] Legg til `DriveBackupService` (BackgroundService): kjør daglig, sjekk om backup er forfallen (N dager siden siste), bruk SQLite Online Backup API til å lage snapshot, kall uploader for alle brukere med koblet Drive
- [ ] Konfigurasjon: `GoogleDrive:BackupIntervalDays` (default: 1), `GoogleDrive:ClientId`, `GoogleDrive:ClientSecret`
- [ ] Logg resultat til `DriveBackupLog`

#### 6B.3 Gjenoppretting (kun admin)
- [ ] Admin-side `/admin/restore`: vis liste over kjente backups fra `DriveBackupLog`
- [ ] "Gjenopprett"-knapp: krev passordbekreftelse, last ned valgt backup-fil fra Drive
- [ ] Skriv ned til staging-fil (`/app/db/app.db.pending`), skriv flaggfil `/app/db/restore.pending`
- [ ] Startup-logikk i `Program.cs`: hvis `restore.pending` finnes, swap `app.db` → `app.db.bak`, flytt `app.db.pending` → `app.db`, slett flagg, kjør migreringer
- [ ] Invalider alle sesjoner etter gjenoppretting (roter Data Protection-nøkler eller sett en sesjon-ugyldig-etter-timestamp)
- [ ] Audit-logging av backup- og restore-hendelser

**Avhengigheter**: Krever Phase 6A (admin-rolle for restore-tilgang).

---

### Phase 7: WhatsApp Bot

**Mål**: Automatisk inntak av WhatsApp-meldinger som CaseEvents, med vedlegg og sak-kobling via hashtags.

#### 7.1 Ingest API (SaksAppWeb)
- [ ] **Task 7.1.1**: Legg til `POST /api/whatsapp/ingest` endpoint (autentisert med delt hemmelighet)
- [ ] **Task 7.1.2**: Definer `WhatsAppIngestPayload` DTO: `GroupId`, `SenderId`, `SenderName`, `Text`, `MediaContentType`, `MediaBytes`, `Timestamp`
- [ ] **Task 7.1.3**: Implementer `IWhatsAppIngestService` / `WhatsAppIngestService`:
  - Finn eller opprett grupperingsbuffer (nøkkel: GroupId + SenderId + åpent vindu)
  - Legg til tekst (newline-separert) og/eller vedlegg
- [ ] **Task 7.1.4**: Implementer `WhatsAppBufferFlushService` (BackgroundService): flush buffere som har passert grupperingsvinduet
- [ ] **Task 7.1.5**: Legg til konfigurasjon: `WhatsApp:SharedSecret`, `WhatsApp:GroupingWindowMinutes` (default 5)
- [ ] **Task 7.1.6**: Flush til `CaseEvent` + `Attachment` ved timeout — `Source = "whatsapp"`, `SourceGroupId`, `SourceSenderId`

#### 7.2 Sak-kobling via hashtag
- [ ] **Task 7.2.1**: Parse `#<tall>` fra meldingens tekst ved ingest
- [ ] **Task 7.2.2**: Opprett `CaseEventCase`-kobling for hvert funnet saksnummer (log ukjente saker som advarsel)

#### 7.3 WhatsApp-sidecar
- [ ] **Task 7.3.1**: Velg tilnærming: whatsmeow (Go) eller Baileys (Node.js) — anbefalt: whatsmeow for minimal avhengighet
- [ ] **Task 7.3.2**: Implementer sidecar som kobler til WhatsApp, lytter på meldinger i konfigurerte grupper
- [ ] **Task 7.3.3**: Sidecar poster til `/api/whatsapp/ingest` med shared secret
- [ ] **Task 7.3.4**: Legg til sidecar i `docker-compose.yml`
- [ ] **Task 7.3.5**: QR-kode-autentisering ved første oppstart (lagre session til volum)

#### 7.4 UI — WhatsApp-merking i tidslinje
- [ ] **Task 7.4.1**: Vis WhatsApp-ikon ved CaseEvents med `Source = "whatsapp"` i sakens tidslinje
- [ ] **Task 7.4.2**: Vis avsendernavn (fra `SourceSenderId` eller konfigurerbar navne-map)

**Avhengigheter**: Krever at CaseEvent-modellen er implementert (Phase 2+3).

**Test**: Sett opp testgruppe i WhatsApp, send meldinger, verifiser at CaseEvents opprettes med riktig innhold og koblinger.

---

## 4. Avhengigheter

```
Phase 0 (Test-oppsett)
  └─ Ingen avhengigheter, starter først

Phase 1 (Terminologi)
  └─ Kan startes når som helst, ingen avhengigheter

Phase 2 (Database)
  └─ Avhenger av: Phase 0 (tester på plass)

Phase 3 (API/Controllers)
  └─ Avhenger av: Phase 2
    ├─ Task 3.1.x avhenger av: Task 2.2.1, 2.2.2
    ├─ Task 3.2.x avhenger av: Task 2.2.3, 2.2.4
    └─ Task 3.3.x avhenger av: Task 2.1.5

Phase 4 (UI - Minutes)
  └─ Avhenger av: Phase 3
    ├─ Task 4.1.x avhenger av: Task 3.2.1
    └─ Task 4.2.x avhenger av: Task 3.3.1

Phase 5 (PDF)
  └─ Avhenger av: Phase 3
    ├─ Task 5.1.x avhenger av: Task 3.2.1
    └─ Task 5.2.x avhenger av: Task 3.2.1

Phase 6 (Nye Features)
  └─ Kan startes etter: Phase 3

Phase 7 (WhatsApp Bot)
  └─ Avhenger av: Phase 2+3 (CaseEvent-modellen må være på plass)
```

---

## 5. Anbefalt Rekkefølge

**Start med Phase 1** (Terminologi) - Enkle endringer, rask feedback:
1. Task 1.1.1 → Task 1.2.7 (alle under Phase 1)

**Deretter Phase 2** (Database):
1. Task 2.1.1 → 2.1.5 (opprett tabeller)
2. Task 2.2.1 → 2.2.6 (migrer data)
3. Task 2.3.1 → 2.3.2 (oppdater modeller)

**Deretter Phase 3** (API):
1. Task 3.1.x (CaseController)
2. Task 3.2.x (MeetingsController)
3. Task 3.3.x (CaseEvents API)

**Deretter Phase 4** (UI):
1. Task 4.1.x (fokusert visning)
2. Task 4.2.x (eventuelt som sak)

**Deretter Phase 5** (PDF):
1. Task 5.1.x (heading-nivåer)
2. Task 5.2.x (vedlegg-nummerering)
3. Task 5.3.x (utfall badge)

**Deretter Phase 6** (fremtidig features):
- Når behov oppstår

**Phase 7** (WhatsApp Bot) — etter Phase 3:
- Task 7.1.x → 7.2.x (ingest API + hashtag-kobling)
- Task 7.3.x (sidecar)
- Task 7.4.x (UI)

---

## 6. Testdekningsgrad — løpende mål

**Nåværende**: 19.25% line / 26.8% branch / 63.88% method (157 tester, per Phase 0.1.9)

**Mål**: Øk line coverage med minst 10 prosentpoeng (til ≥ 30%) som del av løpende utvikling.

- Nye tjenester og kontrollere skal ha tester fra dag én
- Ved større refaktoreringer: legg til tester for den berørte koden
- 75%-målet er forlatt som urealistisk gitt testinfrastrukturens begrensninger