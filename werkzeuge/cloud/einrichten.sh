#!/usr/bin/env bash
# Einrichtungsskript der Cloud-Umgebung für KICAD (HLKS_CAD), Christian 21.10.2026: «richte HLKS_CAD für Cloud-Sitzungen ein».
# Inhalt dieser Datei in claude.ai/code › Umgebung «KICAD» › Einrichtungsskript einfügen. Läuft bei jedem Sitzungsstart vor Claude
# (Ubuntu, Linux), installiert nur, was fehlt, und bricht nie ab: Fehler stehen als «[kicad] FEHLER» im Protokoll.
#
#   1. .NET 10 SDK (Microsoft dotnet-install.sh, Kanal 10.0)
#   2. Schriften für die PDF-Ausgabe: Arial und Calibri gibt es unter Linux nicht. Ersatz mit gleichen Laufweiten (metrisch
#      kompatibel, freie Lizenz): Liberation Sans für Arial, Carlito für Calibri. Sie liegen unter den Windows-Dateinamen in ~/.fonts,
#      wo der SchriftenAufloeser (src/Hlks.Kern/Plan/PdfZeichner.cs, Environment.SpecialFolder.Fonts) sucht.
#   3. Node 22 oder neuer prüfen (Node-Tests: node --test)
#   4. NuGet-Pakete vorab laden, wenn das Repository schon ausgecheckt ist
#
# Netzwerk: braucht dot.net, builds.dotnet.microsoft.com, api.nuget.org und die Ubuntu-Paketquellen. Meldet das Protokoll einen
# Fehler beim Herunterladen, in der Umgebung den Netzwerkzugriff erweitern (diese Domänen zulassen oder «Voll»).
set -uo pipefail
log() { echo "[kicad] $*"; }
SUDO=""
if [ "$(id -u)" -ne 0 ] && command -v sudo >/dev/null 2>&1; then SUDO="sudo"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# ---- 1. .NET 10 SDK
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  log ".NET 10 SDK vorhanden: $(dotnet --version)"
else
  log ".NET 10 SDK wird installiert"
  ziel=/usr/share/dotnet
  [ -n "$SUDO" ] || [ "$(id -u)" -eq 0 ] || ziel="$HOME/.dotnet"
  if curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
     && $SUDO bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$ziel" >/tmp/dotnet-install.log 2>&1; then
    $SUDO ln -sf "$ziel/dotnet" /usr/local/bin/dotnet 2>/dev/null || true
    printf 'export DOTNET_ROOT=%s\nexport DOTNET_CLI_TELEMETRY_OPTOUT=1\nexport DOTNET_NOLOGO=1\nexport PATH="$PATH:%s"\n' "$ziel" "$ziel" \
      | $SUDO tee /etc/profile.d/dotnet.sh >/dev/null 2>&1 || true
    export DOTNET_ROOT="$ziel" PATH="$PATH:$ziel"
    log ".NET installiert: $(dotnet --version 2>/dev/null)"
  else
    log "FEHLER: .NET-Installation fehlgeschlagen (Netzwerkzugriff dot.net / builds.dotnet.microsoft.com?); Details /tmp/dotnet-install.log"
  fi
fi

# ---- 2. Schriften (Arial -> Liberation Sans, Calibri -> Carlito)
if command -v apt-get >/dev/null 2>&1; then
  $SUDO apt-get update -qq >/dev/null 2>&1
  $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq fonts-liberation fonts-crosextra-carlito >/dev/null 2>&1 \
    || log "FEHLER: Schriftpakete nicht installiert (PDF-Tests schlagen dann fehl)"
fi
mkdir -p "$HOME/.fonts"
schrift() { # <Dateiname der freien Schrift> <Windows-Dateiname>
  local q; q=$(find /usr/share/fonts -name "$1" 2>/dev/null | head -n 1)
  if [ -n "$q" ]; then cp -f "$q" "$HOME/.fonts/$2"; else log "FEHLER: $1 nicht gefunden"; fi
}
schrift LiberationSans-Regular.ttf arial.ttf;  schrift LiberationSans-Bold.ttf arialbd.ttf
schrift LiberationSans-Italic.ttf ariali.ttf;  schrift LiberationSans-BoldItalic.ttf arialbi.ttf
schrift Carlito-Regular.ttf calibri.ttf;       schrift Carlito-Bold.ttf calibrib.ttf
schrift Carlito-Italic.ttf calibrii.ttf;       schrift Carlito-BoldItalic.ttf calibriz.ttf
log "Schriften in ~/.fonts: $(ls "$HOME/.fonts" 2>/dev/null | tr '\n' ' ')"

# ---- 3. Node
node_haupt=$(node -p 'process.versions.node.split(".")[0]' 2>/dev/null || echo 0)
if [ "$node_haupt" -lt 22 ]; then
  if [ -s "$HOME/.nvm/nvm.sh" ]; then
    . "$HOME/.nvm/nvm.sh" && nvm install 22 >/dev/null 2>&1 && nvm alias default 22 >/dev/null 2>&1 \
      && log "Node $(node --version) über nvm" || log "FEHLER: Node 22 über nvm nicht installiert"
  else
    log "FEHLER: Node $node_haupt ist zu alt (22 oder neuer nötig)"
  fi
else
  log "Node $(node --version)"
fi

# ---- 4. NuGet-Pakete vorab laden
repo="${CLAUDE_PROJECT_DIR:-$PWD}"
if [ -f "$repo/HLKS_CAD.slnx" ] && command -v dotnet >/dev/null 2>&1; then
  HLKS_BUILD=/tmp/hlks_build dotnet restore "$repo/HLKS_CAD.slnx" >/tmp/hlks-restore.log 2>&1 \
    && log "NuGet-Pakete geladen" || log "FEHLER: dotnet restore (Details /tmp/hlks-restore.log)"
fi
exit 0
