# Quest App bauen und fuer KICAD ablegen (Modul XR, Quest App P7): KICAD installiert die App per adb aus dem Dialog "Quest verbinden"
# (Knopf "App installieren/aktualisieren") und sucht die APK dort, wo dieses Skript sie ablegt:
#   %USERPROFILE%\HLKS_CAD_werkzeuge\quest_app\ch.hkplanung.kicad.quest-Signed.apk   die gebaute APK (Testschluessel, nur fuer die eigene Brille)
#   %USERPROFILE%\HLKS_CAD_werkzeuge\quest_app\version.json                          Commit, Zeit, Groesse, SHA-256 (KICAD prueft die APK dagegen)
# Ein anderer Ordner ist als Einstellung moeglich (Benutzereinstellung quest.app_ablage) oder hier mit -Ablage.
# Das Skript baut nur und legt ab. Es ruft kein adb auf, laedt nichts herunter und aendert nichts an der Brille oder am System.
# Voraussetzung: .NET 10 mit der Workload "android", Android SDK und JDK 17 unter %USERPROFILE%\HLKS_CAD_werkzeuge (Quest App P0).
# Mit -Labor (XR-Labor, Entwurf entwurf\labor-app.md, Stufe L0; Christian 26.10.2026) baut das Skript stattdessen die Brillen-App des Labors,
# src\Hlks.Labor.Quest\Projects\Android\Hlks.Labor.Quest.Android.csproj (App-Kennung ch.hkplanung.xrlabor), und legt sie getrennt ab:
#   %USERPROFILE%\HLKS_CAD_werkzeuge\labor_app\ch.hkplanung.xrlabor-Signed.apk   die gebaute APK (Testschluessel)
#   %USERPROFILE%\HLKS_CAD_werkzeuge\labor_app\version.json                       gleiches Format wie bei der KICAD-App
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\quest_app_bereitstellen.ps1 [-Labor] [-Ablage <ordner>] [-OhneBau]
#   -Labor    die Labor-App statt der KICAD-App (ohne den Schalter alles wie bisher)
#   -OhneBau  legt die zuletzt gebaute APK aus dem Bauordner (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build) nur ab.
# Bauausgabe getrennt: vorher $env:HLKS_BUILD setzen, wenn mehrere Baulaeufe gleichzeitig arbeiten (Datei bewusst nur ASCII, PowerShell 5.1).
param(
  [string]$Ablage = '',
  [switch]$OhneBau,
  [switch]$Labor
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if ($Labor) {
  $appName = 'XR-Labor'
  $projektName = 'Hlks.Labor.Quest.Android'
  $projekt = "$repo\src\Hlks.Labor.Quest\Projects\Android\$projektName.csproj"
  $apkName = 'ch.hkplanung.xrlabor-Signed.apk'
  $standOrdner = @('src/Hlks.Labor.Quest', 'src/Hlks.Quest', 'src/Hlks.Xr')   # die Labor-App uebersetzt auch die gemeinsamen Quellen von Hlks.Quest
  if (-not $Ablage) { $Ablage = "$env:USERPROFILE\HLKS_CAD_werkzeuge\labor_app" }
} else {
  $appName = 'Quest App'
  $projektName = 'Hlks.Quest.Android'
  $projekt = "$repo\src\Hlks.Quest\Projects\Android\$projektName.csproj"
  $apkName = 'ch.hkplanung.kicad.quest-Signed.apk'
  $standOrdner = @('src/Hlks.Quest', 'src/Hlks.Xr')
  if (-not $Ablage) { $Ablage = "$env:USERPROFILE\HLKS_CAD_werkzeuge\quest_app" }
}
if (-not $env:HLKS_BUILD) { $env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build" }
$bau = $env:HLKS_BUILD
$apkQuelle = "$bau\$projektName\bin\Release\net10.0-android\publish\$apkName"

function Meldung([string]$t) { Write-Host $t }
function Fehler([string]$t) { Write-Host "FEHLER: $t" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $projekt)) { Fehler "Das Projekt fehlt: $projekt" }

if (-not $OhneBau) {
  if (-not (Test-Path $dotnet)) { Fehler "dotnet nicht gefunden: $dotnet" }
  Meldung "Baue die $appName (Release, net10.0-android), Bauordner $bau ..."
  & $dotnet publish $projekt -c Release -f net10.0-android -v q -nologo
  if ($LASTEXITCODE -ne 0) { Fehler "Der Bau ist fehlgeschlagen (Exit $LASTEXITCODE)." }
}
if (-not (Test-Path $apkQuelle)) { Fehler "Die APK fehlt: $apkQuelle (ohne -OhneBau wird sie gebaut)." }

# Commit des Standes; "-geaendert" haengt dran, wenn die Quellen der App nicht eingecheckt sind
$commit = 'unbekannt'
$git = Get-Command git.exe -ErrorAction SilentlyContinue | Select-Object -First 1
if ($git) {
  $c = (& $git.Source -C $repo rev-parse --short HEAD 2>$null)
  if ($LASTEXITCODE -eq 0 -and $c) {
    $commit = ([string]$c).Trim()
    $offen = (& $git.Source -C $repo status --porcelain -- @standOrdner 2>$null)
    if ($offen) { $commit = "$commit-geaendert" }
  }
}

# Versionsnummer aus dem Projekt
$text = Get-Content -Raw -Path $projekt
$versionName = $null; $versionCode = $null
$m = [regex]::Match($text, '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>')
if ($m.Success) { $versionName = $m.Groups[1].Value.Trim() }
$m = [regex]::Match($text, '<ApplicationVersion>(\d+)</ApplicationVersion>')
if ($m.Success) { $versionCode = [int]$m.Groups[1].Value }

New-Item -ItemType Directory -Force $Ablage | Out-Null
$ziel = Join-Path $Ablage $apkName
$tmp = "$ziel.neu"
Copy-Item -Path $apkQuelle -Destination $tmp -Force
$groesse = (Get-Item $tmp).Length
$sha = (Get-FileHash -Algorithm SHA256 -Path $tmp).Hash.ToLowerInvariant()
Move-Item -Path $tmp -Destination $ziel -Force

# version.json zuletzt und ohne Umlaute/BOM: KICAD liest sie und prueft die APK dagegen
$o = [ordered]@{
  datei = $apkName
  commit = $commit
  zeit = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')
  groesse = $groesse
  sha256 = $sha
}
if ($versionName) { $o.versionName = $versionName }
if ($versionCode) { $o.versionCode = $versionCode }
$json = $o | ConvertTo-Json -Compress
[IO.File]::WriteAllText((Join-Path $Ablage 'version.json'), $json, (New-Object Text.UTF8Encoding $false))

Meldung "Abgelegt in $Ablage"
Meldung ("  {0}  {1:N1} MB  Commit {2}  Fassung {3} ({4})" -f $apkName, ($groesse / 1MB), $commit, $versionName, $versionCode)
Meldung "  SHA-256 $sha"
if ($Labor) {
  Meldung "Weiter: Die Labor-App ist ein eigenes Paket (ch.hkplanung.xrlabor) neben der KICAD-App. Installation per adb: adb install -r `"$ziel`" (der Knopf im XR-Labor folgt in einer spaeteren Stufe)."
} else {
  Meldung 'Weiter: in KICAD Register Ansicht > Fenster > "Quest verbinden ..." > Quest App > "App installieren/aktualisieren".'
}
