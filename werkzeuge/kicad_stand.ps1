# Stand des Builds gegen die Quellen (gemeinsam fuer start.ps1 und kicad_starten.ps1; mit ". kicad_stand.ps1" einbinden).
# Ein Build ist aktuell, wenn seine eigenen DLLs (Programm + Hlks.Kern.dll im selben Ausgabeordner) neuer sind als jede
# C#-Quelle bzw. jedes Projekt der beteiligten Projekte. Hlks.Kern.dll zaehlt mit, weil der Compiler das Programm bei einer
# Kern-Aenderung ohne neue oeffentliche Schnittstelle nicht neu schreibt (Referenzassembly), die kopierte Kern-DLL aber schon.
# Oberflaeche (wwwroot) und Kataloge liest der Server aus den Quellen - sie brauchen keinen Neubau.
# Datei bewusst nur ASCII (PowerShell 5.1).

function Kicad-NeuesteQuelle([string]$root, [string[]]$projekte) {
  $neu = [datetime]::MinValue
  foreach ($p in $projekte) {
    $f = Get-ChildItem "$root\src\$p" -Recurse -File -Include *.cs, *.csproj -ErrorAction SilentlyContinue |
      Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($f -and $f.LastWriteTimeUtc -gt $neu) { $neu = $f.LastWriteTimeUtc }
  }
  foreach ($f in @(Get-ChildItem "$root\*.props", "$root\src\*.props" -File -ErrorAction SilentlyContinue)) { if ($f.LastWriteTimeUtc -gt $neu) { $neu = $f.LastWriteTimeUtc } }
  return $neu
}

function Kicad-BuildStand([string]$bin, [string]$programm) {
  $p = Join-Path $bin "$programm.dll"
  if (-not (Test-Path $p)) { return $null }
  $stand = (Get-Item $p).LastWriteTimeUtc
  $k = Join-Path $bin 'Hlks.Kern.dll'
  if ((Test-Path $k) -and (Get-Item $k).LastWriteTimeUtc -gt $stand) { $stand = (Get-Item $k).LastWriteTimeUtc }
  return $stand
}

# Liefert je Programm (Hlks.Web, Hlks.Mcp): Ausgabeordner, Stand, neueste Quelle, veraltet
function Kicad-Stand([string]$root, [string]$basis) {
  $r = @{}
  foreach ($prog in @('Hlks.Web', 'Hlks.Mcp')) {
    $bin = "$basis\$prog\bin\Debug\net10.0"
    $stand = Kicad-BuildStand $bin $prog
    $quelle = Kicad-NeuesteQuelle $root @('Hlks.Kern', $prog)
    $r[$prog] = [pscustomobject]@{ Bin = $bin; Dll = "$bin\$prog.dll"; Stand = $stand; Quelle = $quelle; Veraltet = ($null -eq $stand -or $quelle -gt $stand) }
  }
  return $r
}
