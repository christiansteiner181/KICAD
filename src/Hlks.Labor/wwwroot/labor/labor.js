// Labor-Seite (XR-Labor L1): ein Reiterband mit Brille (L0: Teil «Quest App» aus KICAD mit der Marke des Labors, dazu die Teilnehmer im Kanal), Stellschrauben, Profile, Varianten,
// Prüfaufgaben, Auswertung, Fragebogen (alle sechs aus dem Dialog «XR-Labor» von KICAD, js/oberflaeche/xr-labor-dialog.js, als Seite eingebunden – nicht kopiert; Profile aus dem
// Labor, labor-profile.js) und Daten (labor-daten.js). Keine Logik hier: alles läuft über /api/… des Labor-Servers (LaborDienste.cs).
// Die Module aus KICAD (../js/…) liefert der Labor-Server aus src/Hlks.Web/wwwroot aus (LaborOberflaeche.cs) – dieselben Dateien wie in KICAD.

import { holen } from '../js/api.js';
import { knoten } from '../js/oberflaeche/werkzeug.js';
import { questAppTeil } from '../js/oberflaeche/quest-app-dialog.js';
import { xrLaborSeite } from '../js/oberflaeche/xr-labor-dialog.js';
import { markeName } from '../js/oberflaeche/xr-labor-text.js';
import { LABOR_MARKE } from './labor-marke.js';
import { laborProfilquelle } from './labor-profile.js';
import { datenBereich } from './labor-daten.js';
import { TEILNEHMER_TAKT_MS, questAusApp, standText, teilnehmerZeilen } from './labor-text.js';

let app = null;   // letzte Antwort von GET /api/quest/app (für den Zustand «Brille per adb»)

// ------------------------------------------------------------------------------------------ Reiter «Brille» (L0)

const brille = knoten(`<div class="labor-brille">
  <div class="leise qd-klein">Netzzugang, Installation der App «H+K XR» (dieselbe App wie bei KICAD) und Kopplung des Werkzeugs «XR-Labor» – getrennt von KICAD (eigene Ports 5340/5341, eigene Kopplungen). KICAD kann daneben laufen.</div>
  <div id="labor-quest-app"></div>
  <div class="labor-block" aria-labelledby="labor-teilnehmer-titel">
    <b id="labor-teilnehmer-titel">Verbundene Brillen im Kanal</b>
    <div class="labor-teilnehmer" id="labor-teilnehmer" aria-live="polite"></div>
  </div>
</div>`);

const teil = questAppTeil({ quest: () => questAusApp(app), beiGelesen: (a) => { app = a; }, marke: LABOR_MARKE });
brille.querySelector('#labor-quest-app').appendChild(teil.knoten);
teil.zustandNeu({ netzNeu: true });

let teilnehmerStand = '';
async function teilnehmer() {
  let zeilen;
  try { zeilen = teilnehmerZeilen(await holen('/api/xr/teilnehmer'), markeName); } catch { return; }
  const neu = JSON.stringify(zeilen);
  if (neu === teilnehmerStand) return;
  teilnehmerStand = neu;
  const ziel = document.getElementById('labor-teilnehmer');
  ziel.replaceChildren();
  if (zeilen.length === 0) {
    const leer = document.createElement('div');
    leer.className = 'leise';
    leer.textContent = 'Keine Brille verbunden. Die App «H+K XR» verbindet sich mit dem Werkzeug «XR-Labor», sobald der Netzzugang an und die Brille gekoppelt ist.';
    ziel.appendChild(leer);
    return;
  }
  for (const z of zeilen) {
    const d = document.createElement('div');
    d.className = 'labor-teilnehmer-zeile';
    d.dataset.id = z.id;
    d.textContent = z.text;   // Namen aus dem Netz nur als Text
    ziel.appendChild(d);
  }
}

// ------------------------------------------------------------------------------------------ Seite

async function stand() {
  try { document.getElementById('labor-stand').textContent = standText(await holen('/api/status')); }
  catch { document.getElementById('labor-stand').textContent = standText(null); }
}

const daten = datenBereich();
xrLaborSeite(document.getElementById('labor-xl'), {
  ort: 'labor',
  marken: true,
  profile: laborProfilquelle(),
  start: new URLSearchParams(location.search).get('bereich') || 'brille',
  zusatz: {
    vorne: [{ id: 'brille', titel: 'Brille', knoten: brille }],
    hinten: [{ id: 'daten', titel: 'Daten', knoten: daten.knoten, beiAnzeigen: daten.beiAnzeigen }],
  },
});

stand();
teilnehmer();
setInterval(teilnehmer, TEILNEHMER_TAKT_MS);
