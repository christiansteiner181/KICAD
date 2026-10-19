// Palette «Claude» (Modul 3c, Teil O, entwurf/modul-3c.md): Gespräch mit Claude über Christians Abo im Seitenfenster, Änderungen mit «Zurücknehmen», Normensuche.
// Keine Fachlogik: alles läuft über die Endpunkte /api/ki/* und /api/normen/* (Server, Teil K); geändert wird das Modell nur von Claude über die Befehle des Kerns, hier
// wird nur gefragt, abgebrochen und zurückgenommen. Der Verlauf kommt immer vom Server und wird live aus dem Ereignisstrom ({ art: 'ki', … }) nachgeführt; im Browser bleibt
// nur die Wahl des Gesprächs, des Modells und des Internet-Schalters je Projekt (ki-text.js › wahlLesen/wahlSchreiben).
//
//   Ort:        Palettenverwaltung (paletten.js, Id «claude»): Vorgabe rechts angedockt, nur auf Verlangen sichtbar (Ansicht › Paletten oder Kürzel KI, aktionen-ki.js)
//   Kopf:       Gesprächswahl (Liste, «Neues Gespräch», Entfernen), Modellwahl, Schalter «Internet» (mit Hinweis «ohne Projektzugriff»)
//   Verlauf:    Mensch rechts, Claude links; Text formatiert (ki-darstellung.js, nie HTML aus fremdem Text); Werkzeugaufrufe kompakt und aufklappbar;
//               Änderungen als Liste mit Elementen (Klick wählt im Modell) und «Zurücknehmen» je Paket (409: Ablehnung und spätere Pakete verständlich)
//   Eingabe:    mehrzeilig, Strg+Enter sendet, «Abbrechen» während des Laufs; Kontext-Chips (Ansicht, Geschoss, Auswahl, Bild der Ansicht) je Frage abwählbar
//   Internet:   mit Internet arbeitet Claude OHNE Projektzugriff und OHNE Projektkontext: Chips ausgegraut, nichts geht mit, Nachrichten sind markiert
//   Normen:     zweiter Reiter «Normen»: Suchfeld, Treffer mit Norm, Seite, Ziffer und Auszug (GET /api/normen/suche)
// Reine Teile (Formatierung, Kontext, Texte, Merken) stehen in ki-text.js, der Zustand des Verlaufs in ki-zustand.js – beide in Node getestet (ki-palette.test.mjs).

import { zustand, element, abonnieren, auswaehlen, merken } from '../zustand.js';
import { holen } from '../api.js';
import { anmelden, zeigen as paletteZeigen, verbergen as paletteVerbergen, anordnung, anordnungSetzen } from './paletten.js';
import { meldung, bestaetigen } from './dialoge.js';
import { symbol } from './symbole.js';
import { istZweitfenster } from './zweitfenster.js';
import { auswahlKennzeichen, beiAuswahlMenge } from '../werkzeuge/auswahl.js';
import * as T from './ki-text.js';
import * as Z from './ki-zustand.js';
import { el, textElement } from './ki-darstellung.js';
import { bildMoeglich, bildDerAnsicht } from './ki-bild.js';

export const ID = 'claude';
const BREITE_VORGABE = 380;   // Breite des rechten Bereichs beim ersten Einblenden (schmal wäre ein Gespräch unlesbar)
const REIHEN = ['SIA', 'SWKI', 'VKF', 'Vorlage', 'suissetec', 'Sprinkler', 'Merkblatt'];

const z = Z.neu();
let einst = null;                       // Antwort von /api/ki/einstellungen
const wahl = { modell: 'sonnet', internet: false };
let tab = 'gespraech';
let chipAb = { ansicht: true, geschoss: true, auswahl: true, bild: false };   // true = geht mit der nächsten Frage mit
let ort = null;                         // Ablageort des Projekts, für das die Palette geladen ist
let ui = null;                          // DOM-Verweise (nur solange die Palette eingeblendet ist)
let abmelden = [];
let ladeNr = 0;
let sendet = false;
let bricht = false;
let ladeFehler = null;
let meldungNr = 0;
let normen = { q: '', reihe: '', antwort: null, fehler: null, laedt: false };
const offeneDetails = new Set();        // Schlüssel aufgeklappter Werkzeuglisten (nur Darstellung)
const ablehnungen = new Map();          // paket → Ablehnungstext des Servers (nur Darstellung)
const wartend = new Set();              // Pakete, die gerade zurückgenommen werden

export const panelOffen = () => !!ui;
const projektOffen = () => !!zustand.status?.offen;

// ---------------------------------------------------------------------------------------------- Server

/** Anfrage mit Statuscode und JSON (auch bei Fehlern: { fehler, vorschlag, ablehnung, spaeter }). Wirft nur, wenn der Server nicht antwortet. */
async function anfrage(pfad, methode = 'GET', koerper) {
  const opt = { method: methode, cache: 'no-store' };
  if (koerper !== undefined) { opt.headers = { 'Content-Type': 'application/json' }; opt.body = JSON.stringify(koerper); }
  const r = await fetch(pfad, opt);
  let daten = null;
  try { daten = await r.json(); } catch { daten = null; }
  return { ok: r.ok, status: r.status, daten };
}

const fehlerVon = (r) => T.serverFehlerText(r?.daten, r?.status);

// ---------------------------------------------------------------------------------------------- Aufbau

function bauen(slot) {
  if (!slot || istZweitfenster()) return;   // im Zweitfenster sind Paletten ausgeblendet; dort wird nichts geladen
  const wurzel = el('div', 'ki-panel');
  wurzel.id = 'ki-panel';
  wurzel.setAttribute('role', 'region');
  wurzel.setAttribute('aria-label', 'Claude');

  // Reiter
  const reiter = el('div', 'ki-reiter');
  const tabG = el('button', 'ki-tab', 'Gespräch'); tabG.type = 'button'; tabG.dataset.tab = 'gespraech';
  const tabN = el('button', 'ki-tab', 'Normen'); tabN.type = 'button'; tabN.dataset.tab = 'normen';
  reiter.append(tabG, tabN);

  // Seite Gespräch
  const seiteG = el('div', 'ki-seite ki-seite-gespraech');
  const kopf = el('div', 'ki-kopf');
  const zeile1 = el('div', 'ki-kopfzeile1');
  const wahlG = el('select', 'ki-wahl-gespraech'); wahlG.title = 'Gespräch wählen'; wahlG.setAttribute('aria-label', 'Gespräch');
  const neuK = el('button', 'ki-knopf ki-neu', 'Neu'); neuK.type = 'button'; neuK.title = 'Neues Gespräch beginnen';
  const loeschK = el('button', 'ki-knopf ki-entfernen', '✕'); loeschK.type = 'button'; loeschK.title = 'Dieses Gespräch aus der Liste entfernen'; loeschK.setAttribute('aria-label', 'Gespräch entfernen');
  zeile1.append(wahlG, neuK, loeschK);
  const zeile2 = el('div', 'ki-kopfzeile2');
  const wahlM = el('select', 'ki-wahl-modell'); wahlM.title = 'Modell wählen'; wahlM.setAttribute('aria-label', 'Modell');
  const internet = el('button', 'ki-internet', 'Internet'); internet.type = 'button'; internet.setAttribute('aria-pressed', 'false');
  const internetNote = el('span', 'ki-internet-note', T.INTERNET_HINWEIS);
  zeile2.append(wahlM, internet, internetNote);
  kopf.append(zeile1, zeile2);

  const band = el('div', 'ki-band'); band.hidden = true;
  const verlauf = el('div', 'ki-verlauf'); verlauf.setAttribute('role', 'log'); verlauf.setAttribute('aria-live', 'polite'); verlauf.tabIndex = 0;
  const meldungK = el('div', 'ki-meldung'); meldungK.hidden = true;
  const chips = el('div', 'ki-chips'); chips.setAttribute('aria-label', 'Kontext zur Frage');
  const eingabeBox = el('div', 'ki-eingabe');
  const feld = el('textarea', 'ki-feld'); feld.rows = 3; feld.placeholder = 'Frage an Claude … (Strg+Enter sendet)'; feld.setAttribute('aria-label', 'Frage an Claude'); feld.maxLength = 20000;
  const leiste = el('div', 'ki-leiste');
  const zaehler = el('span', 'ki-zaehler');
  const senden = el('button', 'ki-knopf ki-primaer ki-senden', 'Senden'); senden.type = 'button'; senden.title = 'Frage senden (Strg+Enter)';
  const abbrechen = el('button', 'ki-knopf ki-abbrechen', 'Abbrechen'); abbrechen.type = 'button'; abbrechen.title = 'Die laufende Antwort abbrechen'; abbrechen.hidden = true;
  leiste.append(zaehler, abbrechen, senden);
  eingabeBox.append(feld, leiste);
  seiteG.append(kopf, band, verlauf, meldungK, chips, eingabeBox);

  // Seite Normen
  const seiteN = el('div', 'ki-seite ki-seite-normen'); seiteN.hidden = true;
  const form = el('form', 'ki-normen-form');
  const q = el('input', 'ki-normen-q'); q.type = 'search'; q.placeholder = 'Norm suchen, z. B. Mindestgefälle Prüfleitung'; q.setAttribute('aria-label', 'Normen durchsuchen'); q.autocomplete = 'off';
  const reihe = el('select', 'ki-normen-reihe'); reihe.setAttribute('aria-label', 'Normenreihe');
  const alle = el('option', null, 'alle Reihen'); alle.value = '';
  reihe.appendChild(alle);
  for (const r of REIHEN) { const o = el('option', null, r); o.value = r; reihe.appendChild(o); }
  const suchen = el('button', 'ki-knopf ki-primaer ki-normen-suchen', 'Suchen'); suchen.type = 'submit';
  form.append(q, reihe, suchen);
  const nInfo = el('div', 'ki-normen-info');
  const nListe = el('div', 'ki-normen-liste'); nListe.setAttribute('role', 'list');
  seiteN.append(form, nInfo, nListe);

  const leer = el('div', 'ki-leer'); leer.hidden = true;
  wurzel.append(reiter, seiteG, seiteN, leer);
  slot.appendChild(wurzel);

  ui = { wurzel, tabG, tabN, seiteG, seiteN, wahlG, neuK, loeschK, wahlM, internet, internetNote, band, verlauf, meldungK, chips, feld, zaehler, senden, abbrechen, q, reihe, nInfo, nListe, leer };
  verdrahten();
  abmelden = [
    abonnieren('ki', beiEreignis),
    abonnieren('status', () => projektPruefen()),
    abonnieren('modell', (d) => { if (d?.alles) projektPruefen(); chipsZeichnen(); elementeAktualisieren(); }),
    abonnieren('ansicht', () => chipsZeichnen()),
    abonnieren('auswahl', () => chipsZeichnen()),
    abonnieren('verbindung', (d) => { if (d?.ok && ort && z.aktuell && !sendet) gespraechNeuLaden(); }),
    beiAuswahlMenge(() => chipsZeichnen()),
  ];
  window.addEventListener('paletten-geaendert', beiPalettenAenderung);
  window.addEventListener('resize', meldungenAusweichen);
  document.addEventListener('pointerup', beiZeigerLos, true);
  projektPruefen(true);
  setTimeout(meldungenAusweichen, 0);   // erst nach dem Einhängen der Hülle in den Bereich
}

let ausweichTimer = null;
function beiZeigerLos() { clearTimeout(ausweichTimer); ausweichTimer = setTimeout(meldungenAusweichen, 120); }   // Palette verschoben oder Breite gezogen

function abbauen() {
  for (const a of abmelden) { try { a?.(); } catch { /* egal */ } }
  abmelden = [];
  window.removeEventListener('paletten-geaendert', beiPalettenAenderung);
  window.removeEventListener('resize', meldungenAusweichen);
  document.removeEventListener('pointerup', beiZeigerLos, true);
  clearTimeout(ausweichTimer);
  document.documentElement.style.removeProperty('--meldungen-rechts');
  ui = null;
  ladeNr++;   // laufende Ladevorgänge verwerfen
}

function beiPalettenAenderung() {
  // Die Palette wurde umgesetzt (andocken, lösen): ein neu eingehängtes Element verliert seine Rollposition – Stand vor dem Umsetzen wiederherstellen
  if (!ui?.verlauf?.isConnected) return;
  ui.verlauf.scrollTop = ui.endeMerken ? ui.verlauf.scrollHeight : (ui.scrollMerk ?? 0);
  meldungenAusweichen();
}

/**
 * Die Einblendungen der Oberfläche (#meldungen, unten rechts) würden bei einer rechts angedockten oder dort schwebenden Palette deren Knopf «Senden» verdecken –
 * gerade dann, wenn Claude etwas geändert hat («Änderung von KI: …»). Solange die Palette dort steht, rücken sie links neben sie (Variable --meldungen-rechts, css/ki-palette.css).
 */
function meldungenAusweichen() {
  const wurzel = document.documentElement;
  const g = ui?.wurzel?.closest('.pal-gruppe');
  const r = g?.isConnected ? g.getBoundingClientRect() : null;
  const stoert = r && r.width > 0 && r.right > window.innerWidth - 470 && r.bottom > window.innerHeight - 120 && r.left > 200;
  if (stoert) wurzel.style.setProperty('--meldungen-rechts', `${Math.round(window.innerWidth - r.left + 12)}px`);
  else wurzel.style.removeProperty('--meldungen-rechts');
}

function verdrahten() {
  ui.tabG.onclick = () => tabWaehlen('gespraech');
  ui.tabN.onclick = () => tabWaehlen('normen');
  ui.wahlG.onchange = () => { const v = ui.wahlG.value; if (v === '') neuesGespraech(); else gespraechWaehlen(v); };
  ui.neuK.onclick = () => neuesGespraech();
  ui.loeschK.onclick = () => gespraechEntfernen();
  ui.wahlM.onchange = () => { wahl.modell = ui.wahlM.value; wahlSpeichern(); };
  ui.internet.onclick = () => { wahl.internet = !wahl.internet; wahlSpeichern(); uiAktualisieren(); };
  ui.senden.onclick = () => fragen();
  ui.abbrechen.onclick = () => abbrechenKlick();
  ui.feld.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); fragen(); }
  });
  ui.feld.addEventListener('input', () => zaehlerZeichnen());
  ui.chips.addEventListener('click', (e) => {
    const c = e.target.closest('.ki-chip');
    if (!c || c.disabled) return;
    chipAb[c.dataset.id] = !chipAb[c.dataset.id];
    chipsZeichnen();
  });
  ui.verlauf.addEventListener('click', beiVerlaufKlick);
  ui.verlauf.addEventListener('scroll', () => {
    if (!ui.verlauf.clientHeight) return;   // ausgehängt: keine Rollposition
    ui.endeMerken = unten(); ui.scrollMerk = ui.verlauf.scrollTop;
  }, { passive: true });
  ui.q.closest('form').addEventListener('submit', (e) => { e.preventDefault(); normenSuchen(); });
  ui.nListe.addEventListener('click', (e) => {
    const k = e.target.closest('.ki-norm-uebernehmen');
    if (k) normUebernehmen(Number(k.dataset.i));
  });
}

// ---------------------------------------------------------------------------------------------- Projekt und Laden

function projektPruefen(erzwingen = false) {
  if (!ui) return;
  const neuOrt = projektOffen() ? (zustand.status.ort ?? '') : null;
  if (!erzwingen && neuOrt === ort) return;
  ort = neuOrt;
  const alt = z.gespraech;
  Z.gespraechSetzen(z, null);
  Z.listeSetzen(z, null);
  ablehnungen.clear(); offeneDetails.clear(); chipAb = { ansicht: true, geschoss: true, auswahl: true, bild: false };
  ladeFehler = null;
  if (alt) verlaufZeichnen();
  uiAktualisieren();
  if (ort !== null) starten();
}

/** Einstellungen, Liste und das gewählte Gespräch holen. */
async function starten() {
  const nr = ++ladeNr;
  const meinOrt = ort;
  try {
    const gemerkt = T.wahlLesen(merken.lesen(T.MERKEN_SCHLUESSEL, {}), meinOrt);
    einst = await holen('/api/ki/einstellungen');
    if (nr !== ladeNr) return;
    wahl.modell = T.modellWaehlen(einst, gemerkt.modell);
    wahl.internet = T.internetWaehlen(einst, gemerkt.internet);
    Z.listeSetzen(z, await holen('/api/ki/gespraeche'));
    if (nr !== ladeNr) return;
    const id = T.gespraechWaehlen(z.gespraeche, gemerkt.gespraech);
    if (id) {
      const g = await holen(`/api/ki/gespraeche/${encodeURIComponent(id)}`);
      if (nr !== ladeNr) return;
      Z.gespraechSetzen(z, g);
    } else Z.gespraechSetzen(z, null);
    ladeFehler = null;
  } catch (e) {
    if (nr !== ladeNr) return;
    ladeFehler = e?.vorschlag ? `${e.message} – ${e.vorschlag}` : (e?.message ?? String(e));
  }
  verlaufZeichnen(true);
  uiAktualisieren();
}

async function listeLaden() {
  if (!ort) return;
  const nr = ladeNr;
  try {
    const l = await holen('/api/ki/gespraeche');
    if (nr !== ladeNr || !ui) return;
    Z.listeSetzen(z, l);
    kopfZeichnen();
  } catch { /* die Liste ist Komfort */ }
}

/** Das gewählte Gespräch noch einmal holen (nach einer Unterbrechung des Ereignisstroms). */
async function gespraechNeuLaden() {
  const id = z.aktuell;
  if (!id || !ui) return;
  const nr = ++ladeNr;
  try {
    const g = await holen(`/api/ki/gespraeche/${encodeURIComponent(id)}`);
    if (nr !== ladeNr || !ui || z.aktuell !== id) return;
    Z.gespraechSetzen(z, g);
    verlaufZeichnen();
    uiAktualisieren();
  } catch { /* der nächste Ereignisstrom holt es nach */ }
}

async function gespraechWaehlen(id) {
  const nr = ++ladeNr;
  meldungLoeschen();
  try {
    const g = await holen(`/api/ki/gespraeche/${encodeURIComponent(id)}`);
    if (nr !== ladeNr || !ui) return;
    Z.gespraechSetzen(z, g);
    ladeFehler = null;
    ablehnungen.clear(); offeneDetails.clear();
  } catch (e) {
    if (nr !== ladeNr) return;
    meldungZeigen(e?.vorschlag ? `${e.message} – ${e.vorschlag}` : (e?.message ?? String(e)), 'fehler');
    await listeLaden();
    uiAktualisieren();
    return;
  }
  wahlSpeichern();
  verlaufZeichnen(true);
  uiAktualisieren();
}

/** «Neues Gespräch»: leere Seite; das Gespräch entsteht auf dem Server erst mit der ersten Frage (sonst bliebe bei jedem Klick ein leeres zurück). */
function neuesGespraech() {
  if (Z.laeuft(z) && z.gespraech) { meldungZeigen('Bitte zuerst die laufende Antwort abwarten oder abbrechen.', 'warnung'); kopfZeichnen(); return; }
  ladeNr++;
  Z.gespraechSetzen(z, null);
  ablehnungen.clear(); offeneDetails.clear();
  meldungLoeschen();
  verlaufZeichnen(true);
  uiAktualisieren();
  ui.feld.focus();
}

async function gespraechEntfernen() {
  const g = z.gespraech;
  if (!g) return;
  if (Z.laeuft(z)) { meldungZeigen('In diesem Gespräch läuft gerade eine Antwort. Zuerst abbrechen.', 'warnung'); return; }
  const ja = await bestaetigen(`Das Gespräch «${g.titel || 'Neues Gespräch'}» aus der Liste entfernen? Die Datei bleibt im Projekt unter ki\\geloescht erhalten.`, { ja: 'Entfernen', gefahr: true, titel: 'Gespräch entfernen' });
  if (!ja) return;
  try {
    const r = await anfrage(`/api/ki/gespraeche/${encodeURIComponent(g.id)}`, 'DELETE');
    if (!r.ok) { meldungZeigen(fehlerVon(r), 'fehler'); return; }
  } catch (e) { meldungZeigen(`Der Server antwortet nicht: ${e.message ?? e}`, 'fehler'); return; }
  await listeLaden();
  const id = T.gespraechWaehlen(z.gespraeche, null);
  if (id) await gespraechWaehlen(id); else { Z.gespraechSetzen(z, null); wahlSpeichern(); verlaufZeichnen(true); uiAktualisieren(); }
}

function wahlSpeichern() {
  if (!ort) return;
  merken.schreiben(T.MERKEN_SCHLUESSEL, T.wahlSchreiben(merken.lesen(T.MERKEN_SCHLUESSEL, {}), ort, { gespraech: z.aktuell, modell: wahl.modell, internet: wahl.internet }));
}

// ---------------------------------------------------------------------------------------------- Ereignisse

function beiEreignis(ev) {
  if (!ui || !ort) return;
  const r = Z.ereignisAnwenden(z, ev);
  if (r.liste) listeLaden();
  if (r.gespraech) verlaufZeichnen();
  else if (r.nachricht !== undefined) {
    nachrichtZeichnen('mensch', r.nachricht);
    nachrichtZeichnen('claude', r.nachricht);
  }
  if (r.liste || r.gespraech || r.nachricht !== undefined) { eingabeZeichnen(); }
}

// ---------------------------------------------------------------------------------------------- Frage senden

function kontextDaten() {
  const kz = zustand.aktiveAnsicht;
  const a = kz ? element(kz) : null;
  const ansicht = a ? { kennzeichen: a.kennzeichen, name: a.name || '', art: a.werte?.art ?? '' } : null;
  const gk = a?.geschoss ?? null;
  const g = gk ? element(gk) : null;
  const geschoss = gk ? { kennzeichen: g?.kennzeichen ?? gk, name: g?.name || g?.kennzeichen || gk } : null;
  return { ansicht, geschoss, auswahl: auswahlKennzeichen(), bildMoeglich: bildMoeglich() };
}

async function fragen() {
  if (!ui || sendet || !ort) return;
  const text = ui.feld.value;
  const problem = T.frageProbleme(text);
  if (problem) { meldungZeigen(problem, 'warnung'); return; }
  if (einst?.verfuegbar === false) { meldungZeigen(T.verfuegbarText(einst), 'fehler'); return; }
  if (Z.laeuft(z)) { meldungZeigen('Claude antwortet noch. Bitte abwarten oder abbrechen.', 'warnung'); return; }
  sendet = true;
  meldungLoeschen();
  eingabeZeichnen();
  try {
    const daten = kontextDaten();
    let bild = null;
    if (!wahl.internet && chipAb.bild && daten.bildMoeglich) {
      try { bild = await bildDerAnsicht(); } catch (e) { console.warn('Bild der Ansicht', e); }
      if (!bild) meldungZeigen('Das Bild der Ansicht konnte nicht erzeugt werden; die Frage geht ohne Bild.', 'warnung', true);
    }
    // Gespräch anlegen, wenn noch keines gewählt ist
    if (!z.gespraech) {
      const r = await anfrage('/api/ki/gespraeche', 'POST', {});
      if (!r.ok) { meldungZeigen(fehlerVon(r), 'fehler'); return; }
      Z.gespraechSetzen(z, r.daten);
      wahlSpeichern();
      verlaufZeichnen(true);
      listeLaden();
    }
    const id = z.gespraech.id;
    const koerper = T.frageKoerper({ text, kontext: T.kontextKoerper(daten, chipAb, wahl.internet, bild), modell: wahl.modell, internet: wahl.internet });
    const r = await anfrage(`/api/ki/gespraeche/${encodeURIComponent(id)}/frage`, 'POST', koerper);
    if (!r.ok) {
      meldungZeigen(fehlerVon(r), r.status === 409 ? 'warnung' : 'fehler');
      if (r.status === 404) { await listeLaden(); }
      return;
    }
    ui.feld.value = '';
    zaehlerZeichnen();
    chipAb = { ansicht: true, geschoss: true, auswahl: true, bild: false };   // abgewählt gilt nur für eine Frage
    const w = Z.frageGesendet(z, r.daten);
    if (w.nachricht !== undefined) { nachrichtZeichnen('mensch', w.nachricht); nachrichtZeichnen('claude', w.nachricht); }
    ende(true);
  } catch (e) {
    meldungZeigen(`Der Server antwortet nicht: ${e.message ?? e}`, 'fehler');
  } finally {
    sendet = false;
    if (ui) { uiAktualisieren(); }
  }
}

async function abbrechenKlick() {
  const g = z.gespraech;
  if (!g || bricht) return;
  bricht = true;
  eingabeZeichnen();
  try {
    const r = await anfrage(`/api/ki/gespraeche/${encodeURIComponent(g.id)}/abbrechen`, 'POST', {});
    if (!r.ok) meldungZeigen(fehlerVon(r), r.status === 409 ? 'warnung' : 'fehler');
    else if (ui && z.aktuell === g.id && r.daten?.id === g.id) { Z.gespraechSetzen(z, r.daten); verlaufZeichnen(); }
  } catch (e) { meldungZeigen(`Der Server antwortet nicht: ${e.message ?? e}`, 'fehler'); }
  finally { bricht = false; if (ui) uiAktualisieren(); }
}

// ---------------------------------------------------------------------------------------------- Zurücknehmen

async function zuruecknehmen(paket) {
  if (wartend.has(paket)) return;
  wartend.add(paket);
  ablehnungen.delete(paket);
  paketNeuZeichnen(paket);
  try {
    const r = await anfrage(`/api/ki/aenderungen/${encodeURIComponent(paket)}/zuruecknehmen`, 'POST', {});
    if (r.ok) {
      const rg = r.daten?.rueckgaengig;
      Z.zurueckgenommen(z, paket, typeof rg === 'string' ? rg : rg?.paketId ?? null);
    } else {
      ablehnungen.set(paket, T.ablehnungText(r.daten, r.status));
    }
  } catch (e) {
    ablehnungen.set(paket, { titel: 'Der Server antwortet nicht.', zeilen: [String(e.message ?? e)], spaeter: [] });
  } finally {
    wartend.delete(paket);
    paketNeuZeichnen(paket);
  }
}

function paketNeuZeichnen(paket) {
  if (!ui) return;
  for (const n of z.gespraech?.nachrichten ?? []) if ((n.aenderungen ?? []).some((a) => a.paket === paket)) nachrichtZeichnen(n.rolle, n.frage);
}

// ---------------------------------------------------------------------------------------------- Zeichnen: Kopf, Eingabe, Chips

function tabWaehlen(t) {
  tab = t;
  uiAktualisieren();
  if (t === 'normen') ui.q.focus();
}

/** Alles, was vom Zustand abhängt (nicht den Verlauf). */
function uiAktualisieren() {
  if (!ui) return;
  const offen = projektOffen();
  ui.leer.hidden = offen;
  ui.leer.textContent = 'Zuerst ein Projekt öffnen.';
  ui.tabG.classList.toggle('aktiv', tab === 'gespraech');
  ui.tabN.classList.toggle('aktiv', tab === 'normen');
  ui.tabG.setAttribute('aria-pressed', String(tab === 'gespraech'));
  ui.tabN.setAttribute('aria-pressed', String(tab === 'normen'));
  ui.seiteG.hidden = !offen || tab !== 'gespraech';
  ui.seiteN.hidden = !offen || tab !== 'normen';
  ui.tabG.hidden = ui.tabN.hidden = !offen;
  if (!offen) return;
  kopfZeichnen();
  bandZeichnen();
  chipsZeichnen();
  eingabeZeichnen();
  normenZeichnen();
}

function kopfZeichnen() {
  if (!ui) return;
  const sel = ui.wahlG;
  sel.replaceChildren();
  const neu = el('option', null, '＋ Neues Gespräch'); neu.value = '';
  sel.appendChild(neu);
  for (const g of z.gespraeche) { const o = el('option', null, T.gespraechLabel(g)); o.value = g.id; sel.appendChild(o); }
  sel.value = z.gespraech?.id ?? '';
  ui.loeschK.disabled = !z.gespraech;
  ui.neuK.disabled = !z.gespraech;   // schon eine leere Seite

  const modelle = einst?.modelle?.length ? einst.modelle : [{ id: 'sonnet', name: 'Sonnet' }, { id: 'opus', name: 'Opus' }, { id: 'haiku', name: 'Haiku' }];
  ui.wahlM.replaceChildren();
  for (const m of modelle) { const o = el('option', null, m.name || m.id); o.value = m.id; ui.wahlM.appendChild(o); }
  ui.wahlM.value = wahl.modell;
  ui.internet.setAttribute('aria-pressed', String(wahl.internet));
  ui.internet.classList.toggle('an', wahl.internet);
  ui.internet.title = wahl.internet ? `Internet ist an: ${T.INTERNET_TIPP}` : 'Internet für die nächste Frage zuschalten (dann ohne Projektzugriff)';
  ui.internetNote.hidden = !wahl.internet;
  ui.internetNote.textContent = einst?.internetHinweis?.replace(/^Internet:\s*/i, '') || T.INTERNET_HINWEIS;
}

function bandZeichnen() {
  const t = [];
  if (ladeFehler) t.push({ art: 'fehler', text: `Claude ist im Moment nicht erreichbar: ${ladeFehler}` });
  else if (einst?.verfuegbar === false) t.push({ art: 'fehler', text: T.verfuegbarText(einst) });
  if (wahl.internet) t.push({ art: 'internet', text: 'Internet ist an: Claude arbeitet ohne Projektzugriff und ohne Projektkontext. Projektfragen bitte ohne Internet stellen.' });
  ui.band.hidden = t.length === 0;
  ui.band.replaceChildren(...t.map((x) => el('div', `ki-band-zeile ${x.art}`, x.text)));
}

function chipsZeichnen() {
  if (!ui) return;
  const chips = T.kontextChips(kontextDaten(), chipAb, wahl.internet);
  ui.chips.classList.toggle('internet', wahl.internet);
  ui.chips.replaceChildren(...chips.map((c) => {
    const b = el('button', `ki-chip${c.an ? ' an' : ''}${c.gesperrt ? ' gesperrt' : ''}`);
    b.type = 'button';
    b.dataset.id = c.id;
    b.disabled = c.gesperrt;
    b.setAttribute('aria-pressed', String(c.an));
    b.title = c.tipp;
    const haken = el('span', 'ki-chip-haken', c.an ? '✓' : '');
    b.append(haken, el('span', 'ki-chip-text', c.text));
    return b;
  }));
}

function zaehlerZeichnen() {
  if (!ui) return;
  const n = ui.feld.value.trim().length;
  ui.zaehler.textContent = n > T.GRENZEN.textZeichen * 0.75 ? `${n} / ${T.GRENZEN.textZeichen}` : '';
  ui.zaehler.classList.toggle('zu-lang', n > T.GRENZEN.textZeichen);
}

function eingabeZeichnen() {
  if (!ui) return;
  const laeuft = Z.laeuft(z);
  ui.abbrechen.hidden = !laeuft;
  ui.abbrechen.disabled = bricht;
  ui.abbrechen.textContent = bricht ? 'Bricht ab …' : 'Abbrechen';
  ui.senden.hidden = laeuft;
  ui.senden.disabled = sendet || einst?.verfuegbar === false;
  ui.feld.placeholder = laeuft ? 'Claude antwortet … (die nächste Frage kann schon vorbereitet werden)' : wahl.internet ? 'Frage ohne Projektzugriff … (Strg+Enter sendet)' : 'Frage an Claude … (Strg+Enter sendet)';
  zaehlerZeichnen();
}

// ---------------------------------------------------------------------------------------------- Meldungen

function meldungZeigen(text, art = 'fehler', anhaengen = false) {
  if (!ui) { meldung(text, art === 'fehler' ? 'fehler' : 'warnung'); return; }
  const nr = ++meldungNr;
  ui.meldungK.hidden = false;
  ui.meldungK.className = `ki-meldung ${art}`;
  if (anhaengen && ui.meldungK.textContent) ui.meldungK.appendChild(el('div', null, text));
  else ui.meldungK.replaceChildren(el('div', null, text));
  const zu = el('button', 'ki-meldung-zu', '✕'); zu.type = 'button'; zu.title = 'Schliessen'; zu.setAttribute('aria-label', 'Meldung schliessen');
  zu.onclick = () => meldungLoeschen();
  ui.meldungK.appendChild(zu);
  if (art === 'warnung') setTimeout(() => { if (nr === meldungNr) meldungLoeschen(); }, 12000);
}

function meldungLoeschen() {
  meldungNr++;
  if (!ui) return;
  ui.meldungK.hidden = true;
  ui.meldungK.replaceChildren();
}

// ---------------------------------------------------------------------------------------------- Zeichnen: Verlauf

const unten = () => !ui || ui.verlauf.scrollHeight - ui.verlauf.scrollTop - ui.verlauf.clientHeight < 48;
function ende(erzwingen = false) {
  if (!ui) return;
  if (erzwingen || ui.endeMerken !== false) ui.verlauf.scrollTop = ui.verlauf.scrollHeight;
}

function verlaufZeichnen(anEnde = false) {
  if (!ui) return;
  const war = unten();
  const g = z.gespraech;
  if (!g) {
    const leer = el('div', 'ki-verlauf-leer');
    leer.append(
      el('div', 'ki-verlauf-leer-titel', 'Fragen Sie Claude zu Ihrem Projekt'),
      el('div', null, 'Claude sieht die aktive Ansicht, das Geschoss und Ihre Auswahl (unten als Chips, einzeln abwählbar) und ändert nur über die Befehle des Kerns. Jede Änderung lässt sich mit «Zurücknehmen» rückgängig machen.'),
      el('div', null, 'Normfragen beantwortet Claude nur mit Zitat aus dem Normenverzeichnis (Reiter «Normen»).'));
    ui.verlauf.replaceChildren(leer);
    ui.endeMerken = true;
    return;
  }
  ui.verlauf.replaceChildren(...g.nachrichten.map((n) => nachrichtElement(n)));
  if (anEnde || war) ende(true);
  ui.endeMerken = unten();
}

function nachrichtZeichnen(rolle, frage) {
  if (!ui || !z.gespraech) return;
  const n = Z.nachricht(z, rolle, frage);
  if (!n) return;
  const war = unten();
  const key = `${rolle}:${frage}`;
  const alt = [...ui.verlauf.children].find((c) => c.dataset?.key === key);
  const neu = nachrichtElement(n);
  if (alt) alt.replaceWith(neu);
  else {
    ui.verlauf.querySelector('.ki-verlauf-leer')?.remove();
    // an die richtige Stelle der Reihenfolge
    const i = z.gespraech.nachrichten.indexOf(n);
    const danach = z.gespraech.nachrichten.slice(i + 1).map((m) => `${m.rolle}:${m.frage}`);
    const vor = [...ui.verlauf.children].find((c) => danach.includes(c.dataset?.key));
    if (vor) ui.verlauf.insertBefore(neu, vor); else ui.verlauf.appendChild(neu);
  }
  if (war || rolle === 'mensch') ende(true);
}

function internetMarke(n) {
  const m = el('span', 'ki-internet-marke', `Internet · ${(n.internetHinweis || 'Internet: ohne Projektzugriff').replace(/^Internet:\s*/i, '')}`);
  m.title = T.INTERNET_TIPP;
  return m;
}

function nachrichtElement(n) {
  const mensch = n.rolle === 'mensch';
  const box = el('div', `ki-nachricht ${mensch ? 'ki-mensch' : 'ki-claude'}${n.internet ? ' mit-internet' : ''}`);
  box.dataset.key = `${n.rolle}:${n.frage}`;
  if (!mensch) box.dataset.status = n.status ?? '';
  const kopf = el('div', 'ki-nachricht-kopf');
  if (mensch) kopf.appendChild(el('span', 'ki-wer', 'Sie'));
  else {
    const s = el('span', 'ki-wer');
    s.innerHTML = symbol('claude', 14);   // eigenes Symbol aus symbole.js (kein fremder Text)
    s.appendChild(document.createTextNode(' Claude'));
    kopf.appendChild(s);
    if (n.modell) kopf.appendChild(el('span', 'ki-modell', T.modellName(n.modell, einst?.modelle)));
  }
  const zeit = T.zeitText(n.zeit);
  if (zeit) kopf.appendChild(el('span', 'ki-zeit', zeit));
  if (!mensch && n.status !== 'laeuft' && n.dauerSekunden) kopf.appendChild(el('span', 'ki-dauer', T.dauerText(n.dauerSekunden)));
  if (n.internet) kopf.appendChild(internetMarke(n));
  box.appendChild(kopf);

  if (mensch) {
    box.appendChild(el('div', 'ki-blase ki-blase-mensch', n.text ?? ''));
    const k = T.kontextText(n);
    if (k.length || n.kontextVerworfen) {
      const kz = el('div', 'ki-kontext');
      for (const t of k) kz.appendChild(el('span', 'ki-kontext-chip', t));
      if (n.kontextVerworfen) { const v = el('span', 'ki-kontext-chip verworfen', 'ohne Projektkontext'); v.title = T.INTERNET_TIPP; kz.appendChild(v); }
      box.appendChild(kz);
    }
    return box;
  }

  // Claude
  const blase = el('div', 'ki-blase ki-blase-claude');
  if ((n.text ?? '').trim()) blase.appendChild(textElement(n.text));
  else if (n.status === 'laeuft') blase.appendChild(el('div', 'ki-denkt', 'Claude arbeitet …'));
  if (n.status === 'laeuft' && (n.text ?? '').trim()) blase.appendChild(el('span', 'ki-cursor'));
  if (blase.childNodes.length) box.appendChild(blase);

  if (n.sitzungNeu) box.appendChild(el('div', 'ki-hinweis', 'Die frühere Claude-Sitzung war nicht verfügbar (anderer Rechner oder gelöscht). Claude hat die letzten Nachrichten dieses Gesprächs als Verlauf bekommen.'));

  if ((n.werkzeuge ?? []).length || (n.abrufe ?? []).length) box.appendChild(werkzeugeElement(n));
  if ((n.aenderungen ?? []).length) box.appendChild(aenderungenElement(n));

  const f = T.fehlerText(n, einst);
  if (f) box.appendChild(el('div', `ki-ende ${n.status}`, f));
  return box;
}

function werkzeugeElement(n) {
  const key = `w:${n.frage}`;
  const d = el('details', 'ki-werkzeuge');
  d.open = offeneDetails.has(key);
  d.addEventListener('toggle', () => { if (d.open) offeneDetails.add(key); else offeneDetails.delete(key); });
  d.appendChild(el('summary', null, T.werkzeugSumme(n.werkzeuge ?? []) + ((n.abrufe ?? []).length ? ` · ${n.abrufe.length} Abruf${n.abrufe.length === 1 ? '' : 'e'} im Internet` : '')));
  const l = el('ul', 'ki-werkzeug-liste');
  for (const w of n.werkzeuge ?? []) {
    const r = T.werkzeugZeile(w);
    const li = el('li', `ki-werkzeug ${r.zustand}`);
    li.appendChild(el('span', 'ki-werkzeug-zeichen', r.zeichen));
    li.appendChild(el('span', 'ki-werkzeug-name', r.name));
    if (r.kurz) li.appendChild(el('span', 'ki-werkzeug-kurz', r.kurz));
    if (r.meldung && r.zustand === 'fehler') li.appendChild(el('div', 'ki-werkzeug-meldung', r.meldung));
    l.appendChild(li);
  }
  for (const a of n.abrufe ?? []) {
    const li = el('li', `ki-werkzeug ${a.erfolg === false ? 'fehler' : 'ok'}`);
    li.appendChild(el('span', 'ki-werkzeug-zeichen', a.erfolg === false ? '✗' : '✓'));
    li.appendChild(el('span', 'ki-werkzeug-name', a.art === 'abruf' ? 'Abruf' : 'Websuche'));
    li.appendChild(el('span', 'ki-werkzeug-kurz', a.url ?? a.text ?? ''));
    l.appendChild(li);
  }
  d.appendChild(l);
  return d;
}

function aenderungenElement(n) {
  const box = el('div', 'ki-aenderungen');
  box.appendChild(el('div', 'ki-aenderungen-titel', (n.aenderungen.length === 1 ? 'Änderung' : `Änderungen (${n.aenderungen.length})`) + (n.status === 'laeuft' ? ' bisher' : '')));
  for (const a of n.aenderungen) {
    const zeile = el('div', `ki-aenderung${a.zurueckgenommen ? ' zurueckgenommen' : ''}`);
    zeile.dataset.paket = a.paket;
    const kopf = el('div', 'ki-aenderung-kopf');
    kopf.appendChild(el('span', 'ki-aenderung-text', a.beschreibung || a.befehl || a.paket));
    if (a.zurueckgenommen) {
      const m = el('span', 'ki-zurueck-marke', 'zurückgenommen');
      if (a.zurueckgenommenVon || a.zurueckgenommenZeit) m.title = [a.zurueckgenommenVon && `von ${a.zurueckgenommenVon}`, T.zeitText(a.zurueckgenommenZeit)].filter(Boolean).join(' · ');
      kopf.appendChild(m);
    } else {
      const k = el('button', 'ki-knopf ki-zuruecknehmen', wartend.has(a.paket) ? 'Nimmt zurück …' : 'Zurücknehmen');
      k.type = 'button';
      k.dataset.paket = a.paket;
      k.disabled = wartend.has(a.paket) || n.status === 'laeuft';
      k.title = n.status === 'laeuft' ? 'Erst nach dem Ende der Antwort' : 'Diese Änderung rückgängig machen (nur, wenn danach nichts Neues an denselben Elementen geändert wurde)';
      kopf.appendChild(k);
    }
    zeile.appendChild(kopf);
    const ids = a.ids ?? [];
    const elemente = el('div', 'ki-elemente');
    (a.elemente ?? []).forEach((kz, i) => {
      const id = ids[i] ?? kz;
      const da = !!element(id) || !!element(kz);
      const b = el('button', `ki-element${da ? '' : ' weg'}`, kz);
      b.type = 'button';
      b.dataset.id = id;
      b.dataset.kz = kz;
      b.title = da ? `${kz} im Modell wählen` : `${kz} gibt es im Modell nicht mehr`;
      b.disabled = !da;
      elemente.appendChild(b);
    });
    if (elemente.childNodes.length) zeile.appendChild(elemente);
    const abl = ablehnungen.get(a.paket);
    if (abl && !a.zurueckgenommen) zeile.appendChild(ablehnungElement(abl));
    box.appendChild(zeile);
  }
  return box;
}

function ablehnungElement(abl) {
  const b = el('div', 'ki-ablehnung');
  b.setAttribute('role', 'alert');
  b.appendChild(el('div', 'ki-ablehnung-titel', abl.titel));
  for (const t of abl.zeilen) b.appendChild(el('div', 'ki-ablehnung-zeile', t));
  if (abl.spaeter.length) {
    b.appendChild(el('div', 'ki-ablehnung-zeile', 'Spätere Änderungen an denselben Elementen:'));
    const l = el('ul', 'ki-ablehnung-liste');
    for (const t of abl.spaeter) l.appendChild(el('li', null, t));
    b.appendChild(l);
  }
  return b;
}

function beiVerlaufKlick(e) {
  const zu = e.target.closest('.ki-zuruecknehmen');
  if (zu && !zu.disabled) { zuruecknehmen(zu.dataset.paket); return; }
  const elb = e.target.closest('.ki-element');
  if (elb && !elb.disabled) {
    const id = element(elb.dataset.id)?.id ?? element(elb.dataset.kz)?.id;
    if (id) auswaehlen(id);
  }
}

/** Nach einer Modelländerung: Elemente, die es nicht mehr gibt (oder wieder gibt), in den Änderungen entsprechend zeigen. */
function elementeAktualisieren() {
  if (!ui) return;
  for (const b of ui.verlauf.querySelectorAll('.ki-element')) {
    const da = !!element(b.dataset.id) || !!element(b.dataset.kz);
    b.disabled = !da;
    b.classList.toggle('weg', !da);
    b.title = da ? `${b.dataset.kz} im Modell wählen` : `${b.dataset.kz} gibt es im Modell nicht mehr`;
  }
}

// ---------------------------------------------------------------------------------------------- Normen

async function normenSuchen() {
  const q = ui.q.value.trim();
  const reihe = ui.reihe.value;
  if (!q) { normen = { ...normen, q, fehler: 'Bitte einen Suchbegriff eingeben.', antwort: null }; normenZeichnen(); return; }
  normen = { q, reihe, antwort: null, fehler: null, laedt: true };
  normenZeichnen();
  try {
    const r = await anfrage(`/api/normen/suche?q=${encodeURIComponent(q)}${reihe ? `&reihe=${encodeURIComponent(reihe)}` : ''}`);
    if (!ui) return;
    if (!r.ok) normen = { q, reihe, antwort: null, fehler: fehlerVon(r), laedt: false };
    else normen = { q, reihe, antwort: r.daten, fehler: null, laedt: false };
  } catch (e) {
    normen = { q, reihe, antwort: null, fehler: `Der Server antwortet nicht: ${e.message ?? e}`, laedt: false };
  }
  normenZeichnen();
}

function normenZeichnen() {
  if (!ui) return;
  const n = normen;
  ui.nInfo.replaceChildren();
  ui.nListe.replaceChildren();
  if (n.laedt) { ui.nInfo.textContent = 'Sucht … (beim ersten Mal baut der Server das Normenverzeichnis auf, das kann etwas dauern)'; return; }
  if (n.fehler) { ui.nInfo.className = 'ki-normen-info fehler'; ui.nInfo.textContent = n.fehler; return; }
  ui.nInfo.className = 'ki-normen-info';
  if (!n.antwort) { ui.nInfo.textContent = 'Sucht im Normenverzeichnis (nur deutsche Fassungen, nur lesen). Treffer mit Norm, Seite und Auszug; Claude zitiert dieselben Stellen.'; return; }
  const a = n.antwort;
  const treffer = a.treffer ?? [];
  const teile = [treffer.length === 0 ? 'Keine Treffer.' : treffer.length === 1 ? '1 Treffer.' : `${treffer.length} Treffer.`];
  if (a.hinweis && treffer.length === 0) teile.push(a.hinweis);   // bei Treffern ist der Hinweis des Servers für Claude geschrieben (norm_lesen …)
  if (a.vollstaendig === false || a.ohneVolltext > 0) teile.push('Der Volltext ist noch nicht für alle Normen aufgebaut; die Suche kann Stellen übersehen.');
  ui.nInfo.textContent = teile.join(' ');
  treffer.forEach((t, i) => {
    const k = el('div', 'ki-norm');
    k.setAttribute('role', 'listitem');
    const kopf = el('div', 'ki-norm-kopf');
    kopf.appendChild(el('span', 'ki-norm-name', [t.norm, t.ausgabe && !String(t.norm ?? '').includes(t.ausgabe) ? t.ausgabe : ''].filter(Boolean).join(' ')));
    const ort2 = [t.seite ? `Seite ${t.seite}` : 'Titel', t.ziffer ? `Ziffer ${t.ziffer}` : ''].filter(Boolean).join(' · ');
    kopf.appendChild(el('span', 'ki-norm-ort', ort2));
    k.appendChild(kopf);
    if (t.titel) k.appendChild(el('div', 'ki-norm-titel', t.titel));
    if (t.auszug) k.appendChild(el('div', 'ki-norm-auszug', t.auszug));
    const b = el('button', 'ki-knopf ki-norm-uebernehmen', 'In die Frage übernehmen'); b.type = 'button'; b.dataset.i = String(i);
    b.title = 'Die Fundstelle in das Frage-Feld des Gesprächs übernehmen';
    k.appendChild(b);
    ui.nListe.appendChild(k);
  });
}

function normUebernehmen(i) {
  const t = normen.antwort?.treffer?.[i];
  if (!t || !ui) return;
  const stelle = [t.norm, t.seite ? `Seite ${t.seite}` : '', t.ziffer ? `Ziffer ${t.ziffer}` : ''].filter(Boolean).join(', ');
  const bisher = ui.feld.value.trimEnd();
  ui.feld.value = (bisher ? bisher + '\n' : '') + `Zu ${stelle}: `;
  tabWaehlen('gespraech');
  zaehlerZeichnen();
  ui.feld.focus();
  ui.feld.setSelectionRange(ui.feld.value.length, ui.feld.value.length);
}

// ---------------------------------------------------------------------------------------------- Einblenden

/** Blendet die Palette ein (beim ersten Mal rechts mit einer lesbaren Breite) und setzt den Cursor ins Eingabefeld. */
export function panelOeffnen({ fokus = true } = {}) {
  if (!projektOffen()) return;
  const erstes = anordnung().bereiche.rechts.groesse === null;
  paletteZeigen(ID);
  const a = anordnung();
  if (erstes && a.bereiche.rechts.gruppen.some((g) => g.paletten.includes(ID)) && a.bereiche.rechts.groesse === null) {
    a.bereiche.rechts.groesse = BREITE_VORGABE;
    anordnungSetzen(a);
  }
  if (fokus) setTimeout(() => ui?.feld?.focus(), 60);
}

export function panelSchliessen() { paletteVerbergen(ID); }

anmelden({
  id: ID, titel: 'Claude', ort: 'rechts', reihenfolge: 50, vorgabeSichtbar: false, fluechtig: false, lazy: true, scroll: false,
  elementId: 'palette-claude', symbol: 'claude', anteil: 1,
  bauen, abbauen, oeffnen: () => panelOeffnen(), aktiv: () => projektOffen(),
});
