// VR-Startseite /vr (Modul XR, 21.10.2026): «ich möchte in VR lediglich ein Icon anklicken um im Modell zu landen» (Christian Steiner).
// Die Seite lädt das aktuell offene Projekt des Servers, baut die 3D-Szene mit der vorhandenen 3D-Ansicht auf (ansichten/ansicht3d.js, ohne ihre
// Bedienelemente; die Seite liegt als ein einziger grosser Knopf «Eintreten» darüber) und startet bei einem Tipp bzw. Pinch irgendwo die VR-Sitzung
// (ansichten/vr3d.js, starten(standort)). WebXR verlangt eine Benutzeraktion: requestSession steht in vr3d.starten vor jedem await, der Standort ist
// vorher gewählt, der Klick-Handler ruft starten synchron auf.
// Startpunkt, Merken des Standpunkts und Anzeige: vr-start-logik.js (Node-getestet). Hier steht nur das Zusammenstecken mit Seite und Server.
//
// Prüfhilfen: #vr-eintreten[data-status = laedt | bereit | startet | aktiv | kein-projekt | fehler], [data-startquelle = parameter | gemerkt | eg | gesamt];
// Test-Naht: import('/js/vr-start.js').then((m) => m.naht) → { instanz(), standort(), quelle(), gemerkt() }.

import { holen, abfrage } from './api.js';
import { zustand, modellLaden, nachladen, abonnieren, derKlasse } from './zustand.js';
import { benutzerLaden, benutzerWert, benutzerSetzen } from './oberflaeche/benutzer.js';
import ansicht3d from './ansichten/ansicht3d.js';
import { SCHLUESSEL, raumParameter, standpunktMerken, standortAusGemerkt, egRaeume, startpunktWaehlen, startAnzeige } from './vr-start-logik.js';

const el = (id) => document.getElementById(id);
const knopf = el('vr-eintreten');
const AUGENHOEHE_SCHLUESSEL = 'ansicht3d.augenhoehe';
const SZENE_WARTEN_MS = 6000;
const PRUEF_TAKT_MS = 100;
/** Ansicht, falls das Projekt keine 3D-Ansicht hat: nur zum Aufbauen der Szene, nie im Modell gespeichert (VR schaltet den Schnittbereich ohnehin aus). */
const ERSATZ_ANSICHT = Object.freeze({ id: 'vr-start', kennzeichen: 'VR', klasse: 'Ansicht', name: 'VR', werte: { art: '3d' } });

let instanz = null;
let punkt = { quelle: '', standort: null };   // gewählter Startpunkt { quelle, standort }
let gemerkt = null;                           // zuletzt gemerkter Wert (Benutzereinstellung)
let neuLaden = false;                         // der Server hat das Projekt gewechselt, während VR lief
let projektName = '';

// ---------------------------------------------------------------------------------------------------- Anzeige
function setzeStatus(status, hinweis = '') {
  knopf.dataset.status = status;
  el('vr-hinweis').textContent = hinweis;
  el('vr-gross').textContent = status === 'startet' ? 'Startet …' : 'Eintreten';
  knopf.disabled = status === 'laedt' || status === 'kein-projekt';
  if (status === 'bereit') knopf.setAttribute('aria-label', `Eintreten: ${projektName}`); else knopf.removeAttribute('aria-label');
}
function anzeigen() {
  const a = startAnzeige(punkt);
  el('vr-projekt').textContent = projektName;
  el('vr-geschoss').textContent = a.geschoss ? `Geschoss: ${a.geschoss}` : '';
  el('vr-raum').textContent = a.raum ? (punkt.standort ? `Raum: ${a.raum}` : a.raum) : '';
  el('vr-quelle').textContent = a.quelle ? `Startpunkt: ${a.quelle}` : '';
  knopf.dataset.startquelle = punkt.quelle;
}
function ohneProjekt(text) {
  projektName = 'Kein Projekt offen';
  el('vr-projekt').textContent = projektName;
  el('vr-geschoss').textContent = ''; el('vr-raum').textContent = ''; el('vr-quelle').textContent = '';
  setzeStatus('kein-projekt', text);
}

// ---------------------------------------------------------------------------------------------------- Server
/** Standort eines Raums über die Kern-Abfrage raum_begehen; null, wenn der Kern ihn ablehnt (unbekannter Raum, kein Umriss). */
async function raumStandort(bezug) {
  const par = { raum: bezug };
  const roh = benutzerWert(AUGENHOEHE_SCHLUESSEL, null);
  const augen = typeof roh === 'number' ? roh : NaN;
  if (Number.isFinite(augen)) par.augenhoehe = augen;   // wie «Raum begehen» am PC (der Server prüft den Bereich beim Speichern)
  try { return await abfrage('raum_begehen', par); } catch { return null; }
}

function dreiDAnsicht() {
  const liste = derKlasse('Ansicht').filter((a) => a.werte?.art === '3d' && !a.werte?.vorlage)
    .sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }));
  return liste.find((a) => /gesamt/i.test(a.name ?? '')) ?? liste.find((a) => a.kennzeichen === 'AN-3D') ?? liste[0] ?? ERSATZ_ANSICHT;
}

/** Wartet, bis die Körper des Modells in der Szene stehen (alle kommen mit einer Antwort), höchstens SZENE_WARTEN_MS (leeres Projekt). */
async function szeneAbwarten() {
  const t0 = Date.now();
  while (Date.now() - t0 < SZENE_WARTEN_MS) {
    if ((instanz.vr?.api?.modell?.children?.length ?? 0) > 0) break;
    await new Promise((r) => setTimeout(r, PRUEF_TAKT_MS));
  }
}

/** Ereignisstrom des Servers: Änderungen am Modell kommen in die Szene, ein Projektwechsel lädt die Seite neu (nicht mitten in der Sitzung). */
function stromOeffnen() {
  const q = new EventSource('/api/ereignisse');
  let unterbrochen = false;
  let schlange = Promise.resolve();
  const projektGewechselt = () => { if (instanz?.vr?.aktiv || instanz?.vr?.startet) neuLaden = true; else location.reload(); };
  q.addEventListener('hallo', () => { if (unterbrochen) { unterbrochen = false; projektGewechselt(); } });
  q.onerror = () => { unterbrochen = true; };
  q.onmessage = (e) => {
    let d;
    try { d = JSON.parse(e.data); } catch { return; }
    schlange = schlange.then(async () => {
      if (d.art === 'aenderung') {
        if (d.stand) zustand.stand = d.stand;
        await nachladen(d.ids ?? [], { quelle: d.quelle, akteur: d.akteur, akteurArt: d.akteurArt, paket: d.paket });
      } else if (d.art === 'projekt') projektGewechselt();
    }).catch((err) => console.error('Ereignis', d, err));
  };
}

// ---------------------------------------------------------------------------------------------------- Sitzung
/** Ende der Sitzung (Quest-Menü, Tablet «VR beenden»): Standpunkt merken, zurück auf die Startseite. */
function beiSitzungsEnde(ende) {
  const wert = standpunktMerken(ende, punkt.standort, zustand.status?.ort ?? '', new Date().toISOString());
  if (wert) {
    gemerkt = wert;
    const standort = standortAusGemerkt(wert, zustand.status?.ort ?? '');
    if (standort) punkt = { quelle: 'gemerkt', standort };
    benutzerSetzen(SCHLUESSEL, wert).catch((e) => console.warn('VR: Standpunkt nicht gemerkt', e?.message ?? e));
  }
  if (neuLaden) { location.reload(); return; }
  anzeigen();
  setzeStatus('bereit');
}

function eintreten() {
  if (knopf.dataset.status !== 'bereit' || !instanz?.vr || instanz.vr.aktiv || instanz.vr.startet) return;
  setzeStatus('startet');
  const lauf = instanz.vr.starten(punkt.standort);   // synchron bis requestSession: der Klick ist die Benutzeraktion
  Promise.resolve(lauf).then(() => {
    if (instanz.vr.aktiv) setzeStatus('aktiv');
    else if (knopf.dataset.status === 'startet') setzeStatus('bereit', 'VR konnte nicht gestartet werden. Brille und Browser prüfen, dann erneut tippen.');
  }).catch((e) => { console.warn('VR-Start', e); setzeStatus('bereit', 'VR konnte nicht gestartet werden.'); });
}
knopf.addEventListener('click', eintreten);

// ---------------------------------------------------------------------------------------------------- Start
async function start() {
  const status = await holen('/api/status');
  zustand.status = status;
  if (!status.offen) {
    ohneProjekt('Am PC in KICAD ein Projekt öffnen, dann diese Seite neu laden.');
    // der Server meldet einen Projektwechsel über den Ereignisstrom; dann lädt die Seite neu
    stromOeffnen();
    return;
  }
  projektName = status.projekt ?? '';
  zustand.katalog = await abfrage('katalog');
  zustand.geladen = status.ort ?? null;
  await modellLaden();
  await benutzerLaden();

  instanz = ansicht3d.erstellen(el('vr-szene'), dreiDAnsicht());
  if (!instanz?.vr) { ohneProjekt('Die 3D-Ansicht konnte nicht gestartet werden (WebGL).'); return; }
  abonnieren('modell', (d) => instanz.aktualisieren(d));
  instanz.vr.api.beiEnde = beiSitzungsEnde;
  stromOeffnen();

  gemerkt = benutzerWert(SCHLUESSEL, null);
  punkt = await startpunktWaehlen({
    parameter: raumParameter(location.search),
    gemerkt: standortAusGemerkt(gemerkt, status.ort ?? ''),
    raeumeEg: egRaeume(derKlasse('Geschoss'), derKlasse('Raum')),
    raumStandort,
  });
  await szeneAbwarten();
  anzeigen();
  setzeStatus('bereit');
  const unterstuetzt = await instanz.vr.pruefen();   // nur ein Hinweis: der Tipp versucht es trotzdem
  if (!unterstuetzt && knopf.dataset.status === 'bereit') el('vr-hinweis').textContent = 'Keine VR-Brille erkannt: diese Seite im Browser der Meta Quest öffnen.';
}

window.addEventListener('error', (e) => console.error(e.error ?? e.message));
start().catch((e) => {
  console.error('VR-Startseite', e);
  ohneProjekt(`Das Projekt konnte nicht geladen werden: ${e?.message ?? e}`);
  knopf.dataset.status = 'fehler';
});

/** Kontrolle für Browser-Tests. */
export const naht = { instanz: () => instanz, standort: () => punkt.standort, quelle: () => punkt.quelle, gemerkt: () => gemerkt };
