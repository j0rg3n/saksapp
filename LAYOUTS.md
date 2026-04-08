# LAYOUTS.md - Optimaliserte Layouts

> Denne filen dokumenterer de endelige, optimaliserte layoutene for SaksApp.
> Alle terminologi er på norsk (Borettslag-kontekst).

---

## 1. Saker Index (Cases Index)

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [ ] Vis lukkede saker                                    SAKER    [+ Ny sak] │
├──────┬────────────────────────┬────────┬────────┬──────────┬────────────────┤
│  #   │ Tittel                 │ Tema   │ Pri    │ Status   │ Ansvarlig     │
├──────┼────────────────────────┼────────┼────────┼──────────┼────────────────┤
│  47  │ Oppsett vaskeri        │ drift  │   2    │ Åpen     │ Hansen        │
│  46  │ Budsjett 2026          │ økonomi│   3    │ Åpen     │ Johansen      │
│  45  │ Alt                   │        │   1    │ Lukket   │ --            │
└──────┴────────────────────────┴────────┴────────┴──────────┴────────────────┘
```

### Struktur
- **Header**: Checkbox "Vis lukkede saker" (venstre), Tittel "Saker" (sentrert), "Ny sak"-knapp (høyre)
- **Tabellkolonner**: #, Tittel, Tema, Pri (prioritet), Status, Ansvarlig, Tidsfrist, Handlinger
- **Rad**: Klikk på rad for å åpne sak

---

## 2. Sak Details

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ ← Tilbake                                              [Rediger]  [Slett]    │
├─────────────────────────────────────────────────────────────────────────────┤
│ #47 — Oppsett vaskeri                                                         │
│                                                                             │
│ PRIORITET: 2  |  STATUS: Åpen  |  ANSVARLIG: Hansen (B)                     │
│ TIDSFRIST: 15.04.2026  |  TEMA: drift                                       │
│                                                                           ─── │
│ BESKRIVELSE                                                                │
│ Det er behov for å etablere et nytt vaskeri i bygget. Vi har fått tilbud   │
│ fra tre leverandører som må evalueres.                                     │
│                                                                           ─── │
│ SAKSHENDELSER (kronologisk)                                                │
│ [+ Ny saksmerknad 📎]                                                      │
│                                                                           ─── │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 12.03.2026 18:00 — Møte 2026/3 — Referat                                📎 │
│ │ Saken ble behandlet. Styret besluttet å utsette avgjørelsen.            │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 12.03.2026 18:00 — Møte 2026/3 — Vedtak                                 │ │
│ │ Saken utsettes til neste møte                                           │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 12.03.2026 18:00 — Møte 2026/3 — Oppfølging                             📎 │
│ │ Hansen innhenter ytterligere tilbud                                      │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 10.04.2026 14:30 — Saksmerknad                                          📎 │
│ │ Mottatt tilbud fra leverandørene, sendt til styret for vurdering       │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 05.04.2026 09:15 — Saksmerknad                                          │ │
│ │ Møte med leverandører planlagt                                         │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Headerlinje**: Navigasjon, Rediger, Slett
- **Sakstittel**: # og tittel
- **Metadata**: Prioritet, Status, Ansvarlig, Tidsfrist, Tema
- **Beskrivelse**: Full tekst
- **Sakshendelser** (kronologisk):
  - "Ny saksmerknad" + opplastingsikon på samme linje (topp)
  - Interleaved entries: Saksmerknader + Møtehistorikk (Vedtak, Referat, Oppfølging)
  - Hver entry har: Dato/tid, Kilde (Møte eller Saksmerknad), Type, Innhold, Vedlegg-ikon
  - Icons: 📎 for vedlegg eksisterende, kan ha eget icon for å legge til vedlegg

---

## 3. Møter Index

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ MØTER                                                                     [+ Nytt møte] │
├────────────────────┬─────────────────┬──────────────────────────────────────┤
│ Dato               │ Møtenr          │ Sted                                 │
├────────────────────┼─────────────────┼──────────────────────────────────────┤
│ 15.04.2026         │ 2026/4          │ Borettslagets hus                   │
│ 12.03.2026         │ 2026/3          │ Teams                                │
│ 15.02.2026         │ 2026/2          │ Borettslagets hus                   │
└────────────────────┴─────────────────┴──────────────────────────────────────┘
```

### Struktur
- **Header**: "Møter", "Nytt møte"-knapp
- **Tabell**: Dato, Møtenr, Sted

---

## 4. Møte Details (Agenda)

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ ← Tilbake       [Rediger]  [Referat]  [Last ned innkalling PDF]            │
├─────────────────────────────────────────────────────────────────────────────┤
│ Møte 2026/4 — 15.04.2026                                                     │
│ Borettslagets hus                                                           │
│                                                                           ─── │
│ LEGG TIL PÅ SAKLISTE                                                        │
│ [Velg sak ▼]                                    [Legg til]                  │
│                                                                           ─── │
│ SAKLISTE                                                                     │
│ ┌────┬──────────────────────────────────────┬──────────────────────────────┐ │
│ │ 1. │ #47 Oppsett vaskeri (Hansen)         │ [↑][↓][✎][✕]               │ │
│ │    │ Tidsfrist: 15.04.2026                │                              │ │
│ │    │ Tema: drift                          │                              │ │
│ └────┴──────────────────────────────────────┴──────────────────────────────┘ │
│ ┌────┬──────────────────────────────────────┬──────────────────────────────┐ │
│ │ 2. │ #46 Budsjett 2026 (Johansen)         │ [↑][↓][✎][✕]               │ │
│ │    │ Tidsfrist: --                        │                              │ │
│ │    │ Tema: økonomi                        │                              │ │
│ └────┴──────────────────────────────────────┴──────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: Tilbake, Rediger, Referat, Last ned PDF
- **Møteinfo**: Dato, nummer, sted
- **Legg til sak**: Dropdown med åpne saker, "Legg til"-knapp
- **Sakliste**: Tabell med kolonner: Rekkefølge, Saksinfo (nr, tittel, ansvarlig, tidsfrist, tema), Handlinger
- **Handlinger**: Pil opp/ned (rekkefølge), Rediger, Fjern

---

## 5. Møte Minutes (Fokusert visning)

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ ← Tilbake                     [Til agenda]  [Last ned referat PDF]         │
├─────────────────────────────────────────────────────────────────────────────┤
│ Referat — Møte 2026/4 — 15.04.2026                                          │
│                                                                           ─── │
│ MØTEINFO                                                                      │
│ ┌─────────────────────────────────┬───────────────────────────────────────┐ │
│ │ Oppmøte: [________________]     │ Forfall: [________________]          │ │
│ └─────────────────────────────────┴───────────────────────────────────────┘ │
│ Godkjenning av forrige referat: [________________________________________]  │
│ Neste møte: [____-__-__]                                                     │
│ [Legg til eventuelt sak 📎]                                                  │
│                                                                           ─── │
│ SAKSPROSESSOR                                                                │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ [< Forrige]  SAK 2 AV 5  [Neste >]                                     │ │
│ │                                                                      │ │
│ │ #46 — Budsjett 2026 (Johansen)    [● Fortsetter]                     │ │
│ │ ────────────────────────────────────────────────────────────────      │ │
│ │ Referat: [______________________________________________________]     │ │
│ │                                                                       │ │
│ │ Vedtak: [_____________________________________________________]      │ │
│ │                                                                       │ │
│ │ Oppfølging: [___________________________________________________]     │ │
│ │                                                                       │ │
│ │ Vedlegg: ingen                                                       │ │
│ │ [📎 Last opp vedlegg]                                                │ │
│ │                                                                      │ │
│ │ [Lagre]                                                             │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: Navigasjon, Last ned PDF
- **Møteinfo**: Oppmøte, Forfall, Godkjenning, Neste møte
- **Eventuelt**: "Legg til eventuelt sak" knapp med vedlegg-ikon
- **Saksprosessor**:
  - Navigering: Forrige/Neste-knapper + saksnummer
  - Saksinfo: Casenumber, tittel, ansvarlig, Utfall-badge
  - Utfall: Farget badge (● Blå=Fortsetter, ● Grønn=Avsluttet, ● Grå=Utsatt, ● Lilla=Orientering)
  - Referat: Tekstfelt
  - Vedtak: Tekstfelt
  - Oppfølging: Tekstfelt
  - Vedlegg: Liste + opplastingsikon
  - Lagre-knapp

---

## 6. Opprett/Rediger Møte

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Nytt møte  |  Rediger møte                                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│ Møtedato:        [____-__-__]                                                │
│ Møtenr (år):     [____] (autofylles fra år)                                 │
│ Møtenr (sekvens):[__]                                                       │
│ Sted:            [________________________]                                  │
│                                                                           ─── │
│ [Avbryt]                                     [Opprett] / [Lagre]           │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 7. Rediger Saks Punkt

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Rediger saks punkt — #47 Oppsett vaskeri                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│ Dagsorden-tekst:                                                            │
│ [________________________________________________________________________] │
│                                                                           __ │
│ Tidsfrist:                                                                  │
│ Dato:    [____-__-__]  |  Tekst: [________________________]                 │
│                                                                           __ │
│ [Avbryt]                                     [Lagre]                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 8. Board Log (Styrelogg) — Fremtidig

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ STYRELOGG                                    [+ Ny hendelse]               │
├─────────────────────────────────────────────────────────────────────────────┤
│ FILTRE                                                                        │
│ Kategori: [Alle ▼]  Dato: [fra] — [til]  Sak: [velg]                       │
│                                                                           ─── │
│ KRONOLOGI                                                                     │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 12.04.2026 14:30 — HMS                                                   │ │
│ │ Avvik: Varmtvannstank lekkasje                                          │ │
│ │ Beskrivelse: Oppdaget lekkasje i kjeller. Produsent varslet.            │ │
│ │ Relatert sak: #47 (Åpen)                                                │ │
│ │ Tiltak: Installere ny tank                                              │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 10.04.2026 09:00 — Drift                                                 │ │
│ │ Generell: Møte med vaskerileverandør                                    │ │
│ │ Saksmerknad: Mottatt tilbud fra tre leverandører                        │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 01.04.2026 11:00 — HMS                                                   │ │
│ │ Avvik: Kontroll av brannslukningsapparat                                │ │
│ │ Beskrivelse: Årlig kontroll gjennomført                                 │ │
│ │ Relatert sak: -- (ingen sak knyttet)                                    │ │
│ │ Tiltak: --                                                              │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: Tittel, "Ny hendelse"-knapp
- **Filter**: Kategori, dato range, sak
- **Kronologi**: Liste med dato/tid, kategori, tittel, beskrivelse
- **Relatert sak**: Kan være tom (ingen sak knyttet)
- **Automatisk lukking**: Hvis avvik er knyttet til sak og saken lukkes, lukkes avvik automatisk

---

## 9. HMS Avvik og Tiltak Logg — Fremtidig

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ HMS AVDELINGEN                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│ SAMMENDRAG                                                                  │
│ ╔══════════════════════════════════════════════════════════════════════╗ │
│ ║  Avvik: 12  |  Åpne: 3  |  Lukket: 9  |  Tiltak: 8                   ║ │
│ ╚══════════════════════════════════════════════════════════════════════╝ │
│                                                                           ─── │
│ AVDELINGSLISTE                                                              │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ ● 12.04.2026 — Varmtvannstank lekkasje                                 📎│ │
│ │ Tiltak: Installere ny tank                                            │ │
│ │ Sak: #47 Oppsett vaskeri (Åpen)                                       │ │
│ │                                                                       │ │
│ │ Status: Åpen (automatisk lukkes når sak #47 lukkes)                  │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ ✓ 01.04.2026 — Brannslukningsapparat kontrollert                        │ │
│ │ Tiltak: --                                                            │ │
│ │ Sak: -- (ingen sak knyttet)                                           │ │
│ │                                                                       │ │
│ │ Status: Lukket                                                        │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: "HMS AVDELINGEN"
- **Sammendrag**: Tellere (avvik, åpne/lukkede, tiltak)
- **Avviksliste**: Kort per avvik med:
  - Status-indikator (● Åpen / ✓ Lukket)
  - Dato, tittel, vedlegg-ikon
  - Tiltak
  - Relatert sak (kan være tom)
  - Automatisk lukking-melding hvis koblet til sak
- **Regler**:
  - Avvik kan eksistere uten sak (CaseId = null)
  - Tiltak kan eksistere uten avvik (spørsmålstegn i loggen)
  - Når sak knyttet til avvik lukkes, lukkes avvik automatisk

---

## 10. Innkalling PDF

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     INNKALLING STYREMØTE 2026/4                            │
│                     15. april 2026                                         │
│                     Versjon: 1                                             │
│                                                                           │
│ Sted: Borettslagets hus                                                    │
│                                                                           ─── │
│                                                                           │
│ DAGSLISTE                                                                   │
│                                                                           │
│ 1. Godkjenne forrige referat                                               │
│                                                                           ─── │
│ 2. Oppsett vaskeri (Hansen; #47)                                          │
│    ═══════════════════════════════════════════════════════════════════   │
│    Beskrivelse (som i sak):                                               │
│    Det er behov for å etablere et nytt vaskeri i bygget...               │
│                                                                           ─── │
│    Dagsorden:                                                              │
│    Styret skal ta stilling til hvilken leverandør som skal velges...      │
│                                                                           ─── │
│    Tidsfrist: 15.04.2026                                                   │
│                                                                           ─── │
│    Forrige møte (12.03.2026):                                             │
│    Vedtak: Utsatt                                                          │
│    Oppfølging: Innhente tilbud fra flere leverandører                     │
│                                                                           ─── │
│    Vedlegg: Vedlegg 1                                                     │
│                                                                           ─── │
│                                                                           ─── │
│ 3. Budsjett 2026 (Johansen; #46)                                          │
│    ═══════════════════════════════════════════════════════════════════   │
│    ...                                                                    │
│                                                                           ─── │
│ ─────────────────────────────────────────────────────────────────────────  │
│ Vedlegg                                                                   │
│ 1. Tilbud_vaskeri_abc.pdf ........................................... 12 │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Sideoppbygging
- **Sidehode**: Møtenummer, dato, versjon
- **Sted**: Under overskrift
- **Dagsorden**:
  - Fast punkt 1: "Godkjenne forrige referat"
  - Per sak (nummerert):
    - Nummer + tittel + ansvarlig + saksnummer (H2)
    - Beskrivelse (kursiv)
    - Dagsorden (normal, hvis ulik beskrivelse)
    - Tidsfrist
    - **Forrige møte** (hvis finnes): Inkluderer alle case events (referat, vedtak, oppfølging) fra forrige møte
    - **Sakshendelser**: Alle saksmerknader etter forrige møte og frem til dette møtet
    - Vedlegg-referanser (per-case: 2.1, 2.2)
- **Vedlegg**: Tabell med casenr.navn, filnavn, sidenummer

---

## 11. Referat PDF

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           REFERAT                                           │
│                     Møte 2026/4 — 15. april 2026                           │
│                     Versjon: 1                                             │
│                                                                           │
│ Sted: Borettslagets hus                                                    │
│ Oppmøte: Styremedlemmer Hansen, Johansen, Olsen                           │
│ Forfall: --                                                                │
│ Neste møte: 15.05.2026                                                     │
│                                                                           ─── │
│ SAKER                                                                      │
│                                                                           ─── │
│ 1. Godkjenning av forrige referat                                          │
│   Godkjent uten merknader                                                  │
│                                                                           ─── │
│ 2. Oppsett vaskeri (Hansen; #47)   ● Fortsetter                           │
│   Referat: Diskutert ulike leverandører. Styret ønsker ytterligere...     │
│   Vedtak: Saken utsettes til neste møte for endelig avgjørelse            │
│   Oppfølging: Hansen innhenter ytterligere tilbud                        │
│   Vedlegg: 2.1, 2.2                                                        │
│                                                                           ─── │
│ 3. Budsjett 2026 (Johansen; #46)   ● Avsluttet                            │
│   ...                                                                     │
│                                                                           ─── │
│ Eventuelt                                                                  │
│ [Her vises eventuelt-saker som ikke er på innkalling]                    │
│                                                                           ─── │
│ ─────────────────────────────────────────────────────────────────────────  │
│ Vedlegg                                                                   │
│ 2.1 Tilbud_abc.pdf .................................................... 15 │
│ 2.2 Tilbud_def.pdf .................................................... 18 │
│ 3. Budsjett_2026.xlsx .................................................. 20 │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Sideoppbygging
- **Sidehode**: "REFERAT", møtenummer, dato, versjon
- **Møteinfo**: Sted, oppmøte, forfall, neste møte
- **Saker**:
  - Fast punkt 1: "Godkjenning av forrige referat" (hvis tekst finnes)
  - Per sak (H2):
    - Tittel + ansvarlig + saksnummer + Utfall-badge
    - Utfall: Farget badge (● Blå=Fortsetter, ● Grønn=Avsluttet, ● Grå=Utsatt, ● Lilla=Orientering)
    - Referat (hvis ikke tom)
    - Vedtak (hvis ikke tom)
    - Oppfølging (hvis ikke tom)
    - Vedlegg (per case: 2.1, 2.2)
- **Eventuelt**: Eventuelt-saker (ikke på innkalling) vises her
- **Vedlegg**: Tabell med casenr.navn, filnavn, sidetall

---

## 12. Påminnelse PDF (Assignee Reminder)

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│                PÅMINNELSE — MØTE 2026/4 — 15.04.2026                       │
│                                                                           │
│ Du er ansvarlig for følgende saker:                                        │
│                                                                           ─── │
│ 1. Budsjett 2026 (#46)                                                     │
│    Tittel: Budsjett 2026                                                   │
│    Beskrivelse: Styret skal behandle budsjett for kommende år...          │
│    Tidsfrist: --                                                           │
│    Neste møte: Møte 2026/4 (15.04.2026)                                   │
│                                                                           ─── │
│ Vennligst forbered deg til møtet.                                          │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- Kortfattet, en side per ansvarlig
- Kun relevante saker for den enkelte

---

## Endringslogg fra nåværende til optimalisert

| Område | Endring |
|--------|---------|
| Møte Minutes visning | Fokus på én sak om gangen med navigering |
| Vedlegg numbering | Per case (f.eks. 2.1, 2.2) istedenfor global |
| Styrelogg | Nytt layout med filter og kategorier |
| HMS | Dedikert layout med sammendrag |
| Alle overskrifter | Konsistent bruk av H1/H2/H3 |