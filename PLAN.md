# PLAN.md - Implementeringsplan

> Denne filen dokumenterer stegene som trengs for å bringe dagens implementasjon i tråd med SPEC.md og LAYOUTS.md.

---

## 1. Terminologimapping

Tabellen under viser nåværende engelske termer i kode og UI, og deres anbefalte norske (Borettslag) termer.

| Nåværende (Engelsk) | Nåværende (i kode/UI) | Anbefalt Norsk (Borettslag) | Merknad |
|---------------------|----------------------|---------------------------|---------|
| Case | Case / Sak | Sak | |
| Meeting | Meeting / Møte | Møte | |
| Agenda | Agenda / Innkalling | Innkalling / Dagsorden | "Innkalling" for innkalling PDF, "Dagsorden" kan brukes i UI |
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
}
```

#### Migration-steg

1. Opprett nye tabeller (`CaseEvent`, `CaseEventCase`, `MeetingEventLink`)
2. Migrer `CaseComment` → `CaseEvent` (Category = "saksmerknad")
3. Migrer `MeetingMinutesCaseEntry` → opptil 3 `CaseEvent` per entry (en for hvert ikke-tomt felt), deretter opprett `MeetingEventLink`
4. Flytt data fra `MeetingCase` → `MeetingEventLink` (AgendaOrder)
5. Slett gamle tabeller (`CaseComment`, `MeetingMinutesCaseEntry`, `MeetingCase`)

---

## 3. UI-endringer

### 3.1 Møte Minutes — Fokusert visning

| Endringspunkt | Detaljer |
|---------------|----------|
| **Navigasjon** | Legg til "Forrige" / "Neste" knapper for å bla mellom saker |
| **En sak om gangen** | Vis kun én sak om gangen, ikke alle i liste |
| **Utfall-dropliste** | Oppdater label til "Utfall" (ikke "Outcome") |

**Steg:**
1. Endre `MeetingMinutesVm` til å inkludere `CurrentIndex` og `TotalCount`
2. Oppdater `Minutes.cshtml` til fokusert visning med navigasjon
3. Legg til GET/POST actions for Previous/Next navigering

### 3.2 Vedlegg-nummerering

| Endringspunkt | Detaljer |
|---------------|----------|
| **Per-case nummerering** | Endre fra global (Vedlegg 1, 2, 3) til per-case (2.1, 2.2) |
| **PDF-oppdatering** | Oppdater `DownloadAgendaPdf` og `DownloadMinutesPdf` |

**Steg:**
1. Endre logikk i begge PDF-genereringsmetoder
2. Oppdater LAYOUTS.md dokumentasjon

### 3.3 Heading-nivåer

| Endringspunkt | Detaljer |
|---------------|----------|
| **Agenda PDF** | Fikse inkonsistente heading-nivåer i "Forrige møte"-seksjonen |
| **Konsistens** | Sørg for H1 → H2 → H3 er konsistent gjennom alle PDF-er |

### 3.4 Board Log (Styrelogg) — Fremtidig

| Endringspunkt | Detaljer |
|---------------|----------|
| **Ny side** | Opprett ny side for Styrelogg |
| **Filter** | Kategori, dato, sak |
| **Kronologi** | Liste over alle CaseEvents uten MeetingEventLink |

### 3.5 HMS Avvik og Tiltak — Fremtidig

| Endringspunkt | Detaljer |
|---------------|----------|
| **Ny side** | Opprett dedikert HMS-side |
| **Sammendrag** | Vis tellere (antall avvik, åpne/lukkede, tiltak) |
| **Avviksliste** | Filtrert liste for avvik og tiltak |

---

## 4. Terminologi-oppdateringer i kode

### 4.1 Enum-verdier

| Enum | Gammel verdi | Ny verdi |
|------|--------------|----------|
| MeetingCaseOutcome | Info | Orientering |

### 4.2 View-labels

Oppdater alle labels i Views til norsk:
- "Outcome" → "Utfall"
- "Official notes" → "Referat"
- "Decision" → "Vedtak"
- "Follow-up" → "Oppfølging"
- "Attendance" → "Oppmøte"
- "Absence" → "Forfall"

---

## 5. Implementeringsrekkefølge

1. **Phase 1: Database**
   - Opprett nye tabeller
   - Kjør migrering av eksisterende data
   - Test integritet

2. **Phase 2: API/Controllers**
   - Oppdater eksisterende endpoints til å bruke nye modeller
   - Legg til nye endpoints for CaseEvent

3. **Phase 3: UI - Minutes visning**
   - Implementer fokusert visning med navigasjon
   - Oppdater terminologi

4. **Phase 4: PDF-er**
   - Fiks heading-nivåer
   - Implementer per-case vedlegg-nummerering

5. **Phase 5: Nye features**
   - Board Log (Styrelogg)
   - HMS Avvik og Tiltak

---

## 6. TODO-liste

- [ ] Opprett CaseEvent, CaseEventCase, MeetingEventLink tabeller
- [ ] Migrer CaseComment → CaseEvent
- [ ] Migrer MeetingMinutesCaseEntry → CaseEvent + MeetingEventLink
- [ ] Migrer MeetingCase → MeetingEventLink
- [ ] Slett gamle tabeller
- [ ] Oppdater endpoints til nye modeller
- [ ] Implementer fokusert Minutes-visning
- [ ] Oppdater terminologi i UI
- [ ] Fiks heading-nivåer i PDF-er
- [ ] Implementer per-case vedlegg-nummerering
- [ ] Board Log-side (fremtidig)
- [ ] HMS-side (fremtidig)

---

## 7. Avhengigheter

- **Fase 1** må fullføres før **Fase 2**
- **Fase 3** avhenger av **Fase 2**
- **Fase 4** kan parallelliseres med **Fase 3**
- **Fase 5** er uavhengig og kan implementeres etter behov