# ddrive

dDrive ist als Windows-Utility für WebDAV-, FTP- und SFTP-Mounts geplant.

## Ziel

Das Tool soll unter Windows 10 und 11 als Tray-Anwendung laufen und mehrere
Remote-Verbindungen als Laufwerke einbinden können – ähnlich zu RaiDrive, aber
fokussiert auf WebDAV, FTP und SFTP.

## Empfohlener Technologie-Stack

Für dieses Projekt ist **C# mit .NET 8 (LTS)** die pragmatischste Wahl:

- **UI / Tray-App:** WPF
- **Virtuelles Dateisystem / Laufwerks-Mount:** Dokan.NET
- **SFTP:** SSH.NET
- **FTP/FTPS:** FluentFTP
- **WebDAV:** eigener Provider auf Basis von `HttpClient` statt dem nativen
  Windows-WebDAV-Redirector
- **Secrets:** Windows Credential Manager
- **Konfiguration:** JSON-Datei unter `%AppData%`, Passwörter ausschließlich im
  Credential Manager

## Warum diese Empfehlung?

### 1. Windows-freundlich

C#/.NET lässt sich sauber als EXE bauen, gut debuggen und einfach mit
Windows-Funktionen wie Tray-Icon, Credential Manager und Installer verbinden.

### 2. Stabilerer WebDAV-Weg als der Windows-Standard

Der native WebDAV-Support von Windows ist oft die instabilste Komponente. Der
sinnvollere Weg ist deshalb:

- **kein** Mount über den Windows-WebDAV-Redirector
- stattdessen ein **eigenes User-Mode-Dateisystem**
- und ein **eigener WebDAV-Client** im Prozess

Damit kontrolliert die Anwendung Timeouts, Retries, Caching, Locking und
Fehlerbehandlung selbst.

### 3. Gute Build- und Release-Pipeline

Die App kann als eigenständige EXE veröffentlicht werden. Der Runtime-Identifier
richtet sich nach der Zielplattform, typischerweise `win-x64` oder `win-arm64`.
Die Anwendung selbst kann als Single-File-EXE gebaut werden; **Dokan** bleibt
dabei aber eine separate Laufzeit- bzw. Installer-Abhängigkeit. Der folgende
Publish-Befehl beschreibt nur das Managed-App-Artefakt; für eine WPF-Tray-App
mit Dokan wird zusätzlich ein Installer bzw. Packaging-Schritt für die
notwendigen Voraussetzungen benötigt. Das Projekt selbst sollte dafür als
Windows-Desktop-App, also z. B. mit dem `Microsoft.NET.Sdk.WindowsDesktop`,
`net8.0-windows` und `UseWPF=true`, konfiguriert sein. Diese Einstellungen sind
eine **geplante Anforderung** für das spätere WPF-Projekt in diesem Repository,
nicht der aktuelle Stand. Beispielhafte `.csproj`-Konfiguration:

```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
```

Beispielhafter Publish-Befehl für ein späteres x64-WPF-App-Projekt:

```powershell
dotnet publish .\<pfad-zum-wpf-projekt>\dDrive.App.csproj -f net8.0-windows -c Release -r win-x64 --self-contained true /p:UseWPF=true /p:PublishSingleFile=true
```

## Empfohlene Architektur

### Komponenten

1. **Tray App**
   - Startet im Hintergrund
   - Zeigt Mount-Status
   - Bietet Verbinden/Trennen
   - Öffnet das Setup-Fenster

2. **Connection Manager**
   - Verwaltet Host, Port, Protokoll, Laufwerksbuchstaben und Benutzername
   - Speichert nur nicht-sensitive Metadaten lokal
   - Liest und schreibt Passwörter über den Windows Credential Manager

3. **Mount Service**
   - Bindet Verbindungen per Dokan.NET als Laufwerk ein
   - Verwaltet mehrere gleichzeitige Mounts
   - Hält Mount-Lebenszyklus und Reconnect-Logik

4. **Protocol Provider**
   - `IStorageBackend` als gemeinsame Schnittstelle
   - `WebDavBackend`
   - `FtpBackend`
   - `SftpBackend`

5. **Cache / IO-Schicht**
   - Directory-Listing-Cache
   - Read-Ahead für häufige Datei-Lesezugriffe
   - Optionaler Temp-Upload bei großen Dateien

## Datenmodell für Verbindungen

Je Verbindung sollten mindestens folgende Felder verwaltet werden:

- Anzeigename
- Protokoll (`webdav`, `ftp`, `sftp`)
- Host
- Port
- Basis-Pfad
- Laufwerksbuchstabe
- Benutzername
- Credential-Manager-Key
- Optionen wie Read-Only, Auto-Mount, Timeout, TLS prüfen

## MVP-Vorschlag

Die erste Version sollte bewusst klein bleiben:

### Phase 1

- Tray-App
- Verbindungen anlegen/bearbeiten/löschen
- Passwort im Credential Manager speichern
- SFTP als erstes stabiles Backend

### Phase 2

- WebDAV-Backend mit eigenem `HttpClient`-basierten Zugriff
- FTP/FTPS-Backend
- Mehrere parallele Mounts

### Phase 3

- Caching
- Reconnect
- Logging / Diagnoseansicht
- Installer und Auto-Start

## Alternativen

### Go

Gut für einzelne Binärdateien, aber für Windows-Tray-UI, Credential Manager und
virtuelle Dateisysteme meist umständlicher als .NET.

### Rust

Sehr performant, aber deutlich höherer Implementierungsaufwand für ein
Desktop-MVP.

### C++/Win32

Maximale Kontrolle, aber unnötig hoher Aufwand für dieses Projekt.

## Empfehlung

Wenn das Ziel ein realistisch umsetzbares, kompilierbares Windows-Tool ist,
sollte dDrive als **WPF-Tray-Anwendung in C#/.NET 8 mit Dokan.NET, SSH.NET,
FluentFTP und einem eigenen WebDAV-Provider** umgesetzt werden.
