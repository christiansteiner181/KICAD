# Erzeugt kataloge\schemasymbole.json neu aus Christians HKAP-Symbolbibliothek (Modul 5 A-S 2).
# Liest nur: data\schema_symbole.json (Vorgabe: die Testkopie unter %TEMP%\hkap_testkopie, NIE die Live-Daten von HKAP) und den Code
# AufgabenProtokolle.Web\Services\SchemaSymbole.cs. Zuordnung Klasse/art -> Symbol und Massstab stehen in
# werkzeuge\hkap_symbole_uebernehmen\zuordnung.json. Den Katalog nie von Hand bearbeiten.
# Aufruf:  powershell -File werkzeuge\hkap_symbole_uebernehmen.ps1 [-Daten <schema_symbole.json>] [-Build <Ordner>]
param(
    [string]$Daten = (Join-Path $env:TEMP 'hkap_testkopie\data\schema_symbole.json'),
    [string]$Build = (Join-Path $env:USERPROFILE 'HLKS_CAD_build\symbole_uebernehmen')
)
$ErrorActionPreference = 'Stop'
$wurzel = Split-Path -Parent $PSScriptRoot
$env:HLKS_BUILD = $Build
dotnet build (Join-Path $wurzel 'werkzeuge\hkap_symbole_uebernehmen') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen' }
dotnet (Join-Path $Build 'hkap_symbole_uebernehmen\bin\Release\net10.0\hkap_symbole_uebernehmen.dll') `
    --daten $Daten `
    --zuordnung (Join-Path $wurzel 'werkzeuge\hkap_symbole_uebernehmen\zuordnung.json') `
    --ausgabe (Join-Path $wurzel 'kataloge\schemasymbole.json')
if ($LASTEXITCODE -ne 0) { throw "Werkzeug beendet mit $LASTEXITCODE" }
