# LAYOUTS.md - Optimaliserte Layouts

> Denne filen dokumenterer de endelige, optimaliserte layoutene for SaksApp.
> Alle terminologi er på norsk (Borettslag-kontekst).

---

## 1. Saker Index (Cases Index)

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ SAKER                                                                    [+ Ny sak] │
├──────┬────────────────────────┬────────┬────────┬──────────┬────────────────┤
│  #   │ Tittel                 │ Tema   │ Pri    │ Status   │ Ansvarlig     │
├──────┼────────────────────────┼────────┼────────┼──────────┼────────────────┤
│  47  │ Oppsett vaskeri        │ drift  │   2    │ Åpen     │ Hansen        │
│  46  │ Budsjett 2026          │ økonomi│   3    │ Åpen     │ Johansen      │
│  45  │ Alt                   │        │   1    │ Lukket   │ --            │
├──────┴────────────────────────┴────────┴────────┴──────────┴────────────────┤
│ [ ] Vis lukkede saker                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: Tittel "Saker", "Ny sak"-knapp (høyre)
- **Filter**: Checkbox "Vis lukkede saker" (venstre, samme linje som header)
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
│ SAKSHENDELSER (nyeste først)                                               │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 10.04.2026 14:30 — Saksmerknad                                          │ │
│ │ Mottatt tilbud fra leverandørene, sendt til styret for vurdering      │ │
│ │ [Vedlegg: tilbud_abc.pdf]                                              │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 05.04.2026 09:15 — Saksmerknad                                          │ │
│ │ Møte med leverandører planlagt                                         │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│                                                                           ─── │
│ MØTEHISTORIKK                                                              │
│ • Møte 2026/3 (12.03.2026) — Vedtak: Utsatt                               │
│ • Møte 2026/2 (15.02.2026) — Vedtak: Fortsetter                           │
│                                                                           ─── │
│ HANDLINGER                                                                  │
│ [Ny saksmerknad]  [Last opp vedlegg]                                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Headerlinje**: Navigasjon, Rediger, Slett
- **Sakstittel**: # og tittel
- **Metadata**: Prioritet, Status, Ansvarlig, Tidsfrist, Tema
- **Beskrivelse**: Full tekst
- **Sakshendelser**: Kronologisk liste med dato, type, innhold, vedlegg
- **Møtehistorikk**: Lista over møter der saken har vært behandlet
- **Handlinger**: Knapper for nye hendelser/vedlegg

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
│ ← Tilbake                     [Referat]  [Last ned innkalling PDF]          │
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
- **Header**: Tilbake, Referat, Last ned PDF
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
│ Eventuelt: [___________________________________________________________]    │
│                                                                           ─── │
│ SAKSPROSESSOR                                                                │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ [< Forrige]  SAK 2 AV 5  [Neste >]                                     │ │
│ │                                                                      │ │
│ │ #46 — Budsjett 2026 (Johansen)                                        │ │
│ │ ────────────────────────────────────────────────────────────────      │ │
│ │ UTALL: [Fortsetter ▼]                                                 │ │
│ │                                                                       │ │
│ │ Referat: [______________________________________________________]     │ │
│ │                                                                       │ │
│ │ Vedtak: [_____________________________________________________]      │ │
│ │                                                                       │ │
│ │ Oppfølging: [___________________________________________________]    │ │
│ │                                                                       │ │
│ │ Vedlegg: ingen                                                       │ │
│ │ [Last opp vedlegg]                                                   │ │
│ │                                                                      │ │
│ │ [Lagre]                                                             │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: Navigasjon, Last ned PDF
- **Møteinfo**: Oppmøte, Forfall, Godkjenning, Neste møte, Eventuelt
- **Saksprosessor**:
  - Navigering: Forrige/Neste-knapper + saksnummer
  - Saksinfo: Casenumber, tittel, ansvarlig
  - Utfall: Dropdown (Fortsetter, Avsluttet, Utsatt, Orientering)
  - Referat: Tekstfelt
  - Vedtak: Tekstfelt
  - Oppfølging: Tekstfelt
  - Vedlegg: Liste + last opp
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
│ Dagsorden-tekst (snitt):                                                    │
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
│ │ 12.04.2026 14:30 — AVDELING: HMS                                        │ │
│ │ Avvik: Varmtvannstank lekkasje                                         │ │
│ │ Beskrivelse: Oppdaget lekkasje i kjeller. Produsent varslet.           │ │
│ │ Relatert sak: #47 Oppsett vaskeri                                      │ │
│ │ Tiltak: Installere ny tank                                             │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ 10.04.2026 09:00 — AVDELING: Drift                                     │ │
│ │ Generell: Møte med vaskerileverandør                                   │ │
│ │ Saksmerknad: Mottatt tilbud fra tre leverandører                       │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: Tittel, "Ny hendelse"-knapp
- **Filter**: Kategori, dato range, sak
- **Kronologi**: Liste med dato/tid, kategori, tittel, beskrivelse, relaterte saker, tiltak

---

## 9. HMS Avvik og Tiltak Logg — Fremtidig

### Visualisering
```
┌─────────────────────────────────────────────────────────────────────────────┐
│ HMS AVDELINGEN                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│ SAMMENDRAG                                                                  │
│ ╔══════════════════════════════════════════════════════════════════════╗ │
│ ║  Antall avvik: 12   |  Åpne: 3  |  Lukket: 9  |  Tiltak: 8          ║ │
│ ╚══════════════════════════════════════════════════════════════════════╝ │
│                                                                           ─── │
│ AVDELINGSLISTE                                                              │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ #12 — 12.04.2026 — Varmtvannstank lekkasje                            │ │
│ │ Status: Åpen  |  Tiltak: Installere ny tank                           │ │
│ │ Relatert sak: #47                                                     │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ #11 — 01.04.2026 — Brannslukningsapparat kontrollert                  │ │
│ │ Status: Lukket  |  Tiltak: --                                         │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Struktur
- **Header**: "HMS AVDELINGEN"
- **Sammendrag**: Tellere (antall avvik, åpne/lukkede, tiltak)
- **Avviksliste**: Kort per avvik med status, tiltak, relatert sak

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
  - Per sak:
    - Nummer + tittel + ansvarlig + saksnummer (H2)
    - Beskrivelse (kursiv)
    - Dagsorden (normal, hvis ulik beskrivelse)
    - Tidsfrist
    - Forrige møte (hvis finnes): dato, vedtak, oppfølging, vedlegg-referanser
- **Vedlegg**: Tabell med nummer, filnavn, sidenummer

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
│ 2. Oppsett vaskeri (Hansen; #47)                                          │
│   Utfall: Fortsetter                                                       │
│   Referat: Diskutert ulike leverandører. Styret ønsker ytterligere...     │
│   Vedtak: Saken utsettes til neste møte for endelig avgjørelse            │
│   Oppfølging: Hansen innhenter ytterligere tilbud                        │
│   Vedlegg: Vedlegg 2.1, 2.2                                               │
│                                                                           ─── │
│ 3. Budsjett 2026 (Johansen; #46)                                          │
│   ...                                                                     │
│                                                                           ─── │
│ Eventuelt                                                                  │
│ Ikke behandlet                                                             │
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
    - Tittel + ansvarlig + saksnummer
    - Utfall (label + verdi)
    - Referat (hvis ikke tom)
    - Vedtak (hvis ikke tom)
    - Oppfølging (hvis ikke tom)
    - Vedlegg (per case: 2.1, 2.2)
- **Eventuelt**: Eventuelt-tekst
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