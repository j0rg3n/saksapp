# TODO.md - Implementeringsplan

> Se SPEC.md for detaljert spesifikasjon. Fullførte faser er arkivert i TODO_ARCHIVE.md.

---

## Ad-hoc: Hjem-side med Markdown-innhold

- [x] Legg til `Markdig` NuGet-pakke
- [x] Opprett `SaksAppWeb/Content/home.md` med forklaring av appen
- [x] `HomeController.Index()` leser filen og konverterer til HTML med Markdig
- [x] `Views/Home/Index.cshtml` rendrer HTML-innholdet

---

## Ad-hoc: Bugfix — Agenda mangler hendelser med blå Generelt-badge

- [ ] Undersøk hvorfor CaseEvents med Category="general" (blå badge) utelates fra agenda
      mens Category="comment" (grå badge) inkluderes; sjekk filtrering i agenda-query
      og PDF-generator (`MeetingController`, `PdfGeneratorService`, agenda-query i
      `IMeetingQueryService`)
- [ ] Fiks filtreringen slik at begge kategorier inkluderes konsekvent

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

---

## Phase 6B: Google Drive Backup

**Mål**: Automatisk opplasting av SQLite-sikkerhetskopier til brukernes Google Drive, med mulighet for admin-gjenoppretting.

**Avhengigheter**: Krever Phase 6A (admin-rolle for restore-tilgang). ✅ 6A er komplett.

### 6B.1 Google OAuth — Drive-tilkobling
- [ ] Registrer OAuth 2.0-app i Google Cloud Console (scope: `drive.file`)
- [ ] Legg til `UserDriveToken`-entitet: `UserId`, `AccessToken` (kryptert), `RefreshToken` (kryptert), `TokenExpiry`, `LinkedAt`
- [ ] EF Core-migrering
- [ ] Innstillingsside (`/settings`): "Koble til Google Drive"-knapp → OAuth-flyt → lagre tokens kryptert med ASP.NET Data Protection
- [ ] Vis koblingsstatus og "Koble fra"-knapp
- [ ] Krypter tokens med `IDataProtector` (purpose: `"DriveTokens"`)

### 6B.2 Backup-tjeneste
- [ ] Legg til `DriveBackupLog`-entitet: `AttemptedAt`, `BackupDate`, `UserId`, `Success`, `ErrorMessage`
- [ ] EF Core-migrering
- [ ] Implementer `IGoogleDriveUploader` interface + `GoogleDriveUploader`
- [ ] Legg til `DriveBackupService` (BackgroundService): kjør daglig, bruk SQLite Online Backup API
- [ ] Konfigurasjon: `GoogleDrive:BackupIntervalDays` (default: 1), `GoogleDrive:ClientId`, `GoogleDrive:ClientSecret`
- [ ] Logg resultat til `DriveBackupLog`

### 6B.3 Gjenoppretting (kun admin)
- [ ] Admin-side `/admin/restore`: vis liste over kjente backups fra `DriveBackupLog`
- [ ] "Gjenopprett"-knapp: krev passordbekreftelse, last ned valgt backup-fil fra Drive
- [ ] Skriv ned til staging-fil (`/app/db/app.db.pending`), skriv flaggfil `/app/db/restore.pending`
- [ ] Startup-logikk i `Program.cs`: swap filer og kjør migreringer ved `restore.pending`
- [ ] Invalider alle sesjoner etter gjenoppretting
- [ ] Audit-logging av backup- og restore-hendelser

---

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
