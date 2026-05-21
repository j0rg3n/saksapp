# TODO_ARCHIVE.md — Fullførte seksjoner

Arkiv over fullførte faser og oppgaver. Se TODO.md for gjenværende arbeid.

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
