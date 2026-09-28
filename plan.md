# Umsetzungsplan dDrive

## Zielbild

dDrive soll eine native Windows-10/11-Tray-Anwendung werden, die mehrere
WebDAV-, FTP- und SFTP-Verbindungen als Laufwerke bereitstellt. Passwörter
werden nicht in Konfigurationsdateien gespeichert, sondern über den Windows
Credential Manager verwaltet.

## Technische Leitentscheidung

- **Sprache / Runtime:** C# mit .NET 8 LTS
- **Desktop-App:** WPF
- **Mount-Schicht:** Dokan.NET
- **SFTP:** SSH.NET
- **FTP/FTPS:** FluentFTP
- **WebDAV:** eigener Provider auf Basis von `HttpClient`
- **Secret Storage:** Windows Credential Manager
- **Konfiguration:** lokale JSON-Konfiguration in `%AppData%`

## Zielstruktur des Projekts

Empfohlene Projektaufteilung:

1. **dDrive.App**
   - Tray-App
   - Fenster für Einstellungen und Verbindungen
   - UI-Logik und Statusanzeige

2. **dDrive.Core**
   - Modelle für Verbindungen und Mount-Konfiguration
   - Interfaces für Backends und Mount-Verwaltung
   - Logging-, Fehler- und Statusmodelle

3. **dDrive.Storage**
   - Implementierungen für WebDAV, FTP und SFTP
   - Gemeinsame Backend-Schnittstelle
   - Mapping von Remote-Operationen auf Dateisystemoperationen

4. **dDrive.Mounting**
   - Dokan.NET-Integration
   - Laufwerks-Lifecycle
   - Reconnect- und Session-Steuerung

5. **dDrive.Tests**
   - Unit-Tests für Core-Logik
   - Tests für Konfigurations- und Mappinglogik

## Umsetzungsphasen

### Phase 1 – Projektgrundlage

- Solution und Projekte anlegen
- Grundlegende Ordner- und Namespace-Struktur festlegen
- Gemeinsame Modelle für Verbindungen definieren
- App-Konfiguration und Persistenzmodell festlegen
- Logging-Basis einführen

**Ergebnis:** kompilierbares Grundgerüst ohne produktive Mount-Funktion.

### Phase 2 – Desktop- und Tray-App

- WPF-Anwendung mit Tray-Icon aufsetzen
- Hauptfenster für Verbindungsverwaltung anlegen
- Dialoge für Neu/Ändern/Löschen von Verbindungen definieren
- Statusanzeige für aktiv, getrennt, Fehler und Reconnect vorsehen
- Auto-Start-Option vorbereiten

**Ergebnis:** nutzbare UI für die Verwaltung mehrerer Verbindungen.

### Phase 3 – Konfiguration und Credentials

- Nicht-sensitive Verbindungsdaten lokal speichern
- Passwörter ausschließlich im Credential Manager ablegen
- Eindeutige Credential-Keys pro Verbindung definieren
- Import-/Export-Strategie für Konfiguration ohne Secrets festlegen

**Ergebnis:** sichere Verwaltung von Accounts und Mount-Einstellungen.

### Phase 4 – Gemeinsame Backend-Schnittstelle

- Einheitliches Backend-Modell für Datei- und Verzeichnisoperationen definieren
- Fehlerklassen und Statuscodes vereinheitlichen
- Timeouts, Cancellation und Retry-Regeln festlegen
- Mapping für Lesen, Schreiben, Auflisten, Umbenennen und Löschen planen

**Ergebnis:** eine stabile Grundlage, damit alle Protokolle gleich behandelt
werden können.

### Phase 5 – SFTP zuerst umsetzen

- SFTP als erstes produktives Backend priorisieren
- Verbindung, Directory-Listing, Lesen und Schreiben stabil abbilden
- Umbenennen, Löschen und Dateiattribute sinnvoll unterstützen
- Parallele Zugriffe und Verbindungsabbrüche sauber behandeln

**Ergebnis:** erstes real nutzbares Laufwerk mit hoher Erfolgswahrscheinlichkeit.

### Phase 6 – Dokan-Mounting anbinden

- Dokan-Dateisystem an die gemeinsame Backend-Schnittstelle koppeln
- Mehrere gleichzeitige Mounts verwalten
- Laufwerksbuchstaben-Konflikte behandeln
- Mount/Unmount, Fehlerzustände und Wiederverbindung abbilden

**Ergebnis:** SFTP-Verbindungen können als Windows-Laufwerke genutzt werden.

### Phase 7 – WebDAV-Backend

- Eigenen WebDAV-Provider statt Windows-WebDAV-Redirector umsetzen
- Directory-Listing, Dateiabruf und Upload stabilisieren
- Umgang mit Locking, ETags und inkonsistenten Serverantworten festlegen
- Caching für häufige Metadatenzugriffe einplanen

**Ergebnis:** robusterer WebDAV-Support unter eigener Kontrolle.

### Phase 8 – FTP/FTPS-Backend

- FTP und FTPS über dieselbe Backend-Schnittstelle integrieren
- Unterschiede bei Pfaden, Encoding und Serverfähigkeiten behandeln
- Schreibzugriffe, Rename und Directory-Handling absichern

**Ergebnis:** drittes Protokoll unter derselben Architektur.

### Phase 9 – Stabilisierung

- Reconnect-Logik ergänzen
- Lokales Caching gezielt ausbauen
- Diagnose- und Fehleransicht in der UI ergänzen
- Verhalten bei Offline-Zuständen und Timeouts verbessern

**Ergebnis:** alltagstauglicher Betrieb für mehrere Verbindungen.

### Phase 10 – Packaging und Release

- Build für `win-x64` und optional `win-arm64` festlegen
- Installer mit Dokan-Voraussetzung definieren
- Signierung und Release-Struktur vorbereiten
- Basisdokumentation für Installation und Nutzung ergänzen

**Ergebnis:** lokal installierbare EXE/Installer-Auslieferung.

## Empfohlene Implementierungsreihenfolge

1. Solution mit den Kernprojekten anlegen
2. Core-Modelle und Konfigurationsmodell definieren
3. Tray-App mit einfachem Fenster und Verbindungsübersicht bauen
4. Credential-Manager-Anbindung ergänzen
5. Gemeinsame Storage-Schnittstelle definieren
6. SFTP-Backend umsetzen
7. Dokan-Mounting für SFTP anbinden
8. Danach WebDAV ergänzen
9. Danach FTP/FTPS ergänzen
10. Zum Schluss Stabilisierung, Installer und Release

## MVP-Definition

Die erste lokal brauchbare Version sollte nur Folgendes können:

- eine oder mehrere SFTP-Verbindungen verwalten
- Zugangsdaten sicher speichern
- per Tray verbinden und trennen
- ein Laufwerk pro Verbindung mounten
- grundlegendes Lesen, Schreiben, Erstellen und Löschen unterstützen

WebDAV und FTP/FTPS sollten erst danach folgen.

## Wichtige Risiken

- Dokan ist eine externe Abhängigkeit und muss in das Installer-Konzept passen
- WebDAV-Server verhalten sich oft unterschiedlich bei Locking und Metadaten
- Netzwerk-Dateisysteme brauchen saubere Timeout- und Retry-Strategien
- Schreibzugriffe, Rename und Datei-Locks sind die kritischsten Bereiche

## Empfehlung für den nächsten Schritt

Starte lokal mit einem **.NET-8-Solution-Setup plus WPF-Tray-App, Core-Modellen,
Credential-Manager-Anbindung und einem SFTP-Backend**. Das ist der schnellste
Weg zu einem realen, testbaren MVP.
