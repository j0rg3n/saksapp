# PLAN.md - Implementeringsplan

> Se SPEC.md for detaljert spesifikasjon. Denne filen holder oversikt over fremdrift og gjenværende oppgaver.

---

## Fase 0: Test-oppsett ✅ KOMPLETT

- Testprosjekt opprettet med xUnit, Moq, EF Core SQLite
- 86 tester (76 bestått, 10 hoppet over grunn teknisk begrensninger)
- Coverage: ~9% line, ~44% method
- Se SPEC.md section "Testability Architecture" for detaljer

---

## Fase 0.1: Testability Refaktorering

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

### 0.1.7 Verifiser Coverage
- [ ] Kjør test suite med coverage
- [ ] Verifiser betydelig økning i dekningsgrad
- [ ] Iterer med flere tester ved behov

**Test**: Kjør `docker compose --profile test up test` og verifiser coverage >50% (mål: 75%)

---

## Gjenværende Faser

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

### Phase 1: Terminologi-oppdateringer (Enkle tekstoppdateringer)

#### 1.1 Oppdater Enum-verdier
- [ ] **Task 1.1.1**: Endre `MeetingCaseOutcome.Info` → `Orientering` i modellen

#### 1.2 Oppdater View Labels
- [ ] **Task 1.2.1**: Endre "Outcome" → "Utfall" i Minutes.cshtml
- [ ] **Task 1.2.2**: Endre "Official notes" → "Referat" i Minutes.cshtml
- [ ] **Task 1.2.3**: Endre "Decision" → "Vedtak" i Minutes.cshtml
- [ ] **Task 1.2.4**: Endre "Follow-up" → "Oppfølging" i Minutes.cshtml
- [ ] **Task 1.2.5**: Endre "Attendance" → "Oppmøte" i Minutes.cshtml
- [ ] **Task 1.2.6**: Endre "Absence" → "Forfall" i Minutes.cshtml
- [ ] **Task 1.2.7**: Endre "Outcome" → "Utfall" i EditAgendaItem.cshtml

**Test**: Bygg og verifiser at appen kjører.

---

### Phase 2: Database-migrering

#### 2.1 Opprett nye modeller
- [ ] **Task 2.1.1**: Opprett `CaseEvent` modell
- [ ] **Task 2.1.2**: Opprett `CaseEventCase` modell
- [ ] **Task 2.1.3**: Opprett `MeetingEventLink` modell
- [ ] **Task 2.1.4**: Opprett EF Core migrering for nye tabeller
- [ ] **Task 2.1.5**: Kjør migrering og verifiser tabeller opprettes

#### 2.2 Migrer eksisterende data
- [ ] **Task 2.2.1**: Skriv migreringsscript for `CaseComment` → `CaseEvent`
- [ ] **Task 2.2.2**: Test migrering av CaseComment (verify count)
- [ ] **Task 2.2.3**: Skriv migreringsscript for `MeetingMinutesCaseEntry` → `CaseEvent` + `MeetingEventLink`
- [ ] **Task 2.2.4**: Test migrering av MeetingMinutesCaseEntry (verify count)
- [ ] **Task 2.2.5**: Migrer `MeetingCase` → `MeetingEventLink`
- [ ] **Task 2.2.6**: Verifiser alle data er korrekt migrert

#### 2.3 Oppdater modeller etter migrering
- [ ] **Task 2.3.1**: Oppdater `CaseEvent` med felt for soft delete
- [ ] **Task 2.3.2**: Legg til `IsEventuelt` på `MeetingEventLink`

**Test**: Kjør app og verifiser eksisterende funksjonalitet virker.

---

### Phase 3: API/Controllers

#### 3.1 Oppdater CaseController
- [ ] **Task 3.1.1**: Endre `AddComment` til å opprette `CaseEvent` istedenfor `CaseComment`
- [ ] **Task 3.1.2**: Endre `DeleteComment` til soft delete `CaseEvent`
- [ ] **Task 3.1.3**: Test at kommentarer vises i sak-details

#### 3.2 Oppdater MeetingsController
- [ ] **Task 3.2.1**: Endre `Minutes` til å bruke `MeetingEventLink`
- [ ] **Task 3.2.2**: Endre `EditAgendaItem` til å lagre i `MeetingEventLink`
- [ ] **Task 3.2.3**: Endre `AddCase` til å opprette `MeetingEventLink`
- [ ] **Task 3.2.4**: Test at agenda-endringer lagres korrekt

#### 3.3 Legg til CaseEvent-api
- [ ] **Task 3.3.1**: Opprett `CaseEvents` controller for Board Log
- [ ] **Task 3.3.2**: Legg til CRUD for CaseEvent (uten møtekobling)

**Test**: Verifiser alle endpoints fungerer med nye modeller.

---

### Phase 4: UI - Minutes Fokusert Visning

#### 4.1 Endre Minutes-visning
- [ ] **Task 4.1.1**: Oppdater `MeetingMinutesVm` med `CurrentIndex`, `TotalCount`
- [ ] **Task 4.1.2**: Endre Minutes.cshtml til fokusert visning (én sak om gangen)
- [ ] **Task 4.1.3**: Legg til Previous/Next navigering i Minutes.cshtml
- [ ] **Task 4.1.4**: Legg til GET/POST actions for Previous/Next

#### 4.2 Eventuelt som sak
- [ ] **Task 4.2.1**: Endre Eventuelt tekstfelt til "Legg til eventuelt sak" knapp
- [ ] **Task 4.2.2**: Opprett modal/form for å opprette ny sak som Eventuelt
- [ ] **Task 4.2.3**: Lagre Eventuelt-status i `MeetingEventLink.IsEventuelt`

**Test**: Verifiser fokusert visning og Eventuelt-funksjonalitet.

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

**Til slutt Phase 6** (fremtidig):
- Når behov oppstår