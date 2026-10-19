// Startbildschirm wie Revit «Home» (S1, entwurf/startbildschirm.md): die ganze Fläche unter dem Kopf, im Design von KICAD.
// Links die Leiste (Projekte: Öffnen …, Neu …, Beispielprojekt; Familien: Bibliothek …; Version und Stand), in der Mitte «Zuletzt
// geöffnet» als Kacheln mit 3D-Vorschaubild (angeheftete zuerst) und «Alle KICAD-Projekte auf W:», rechts die zuletzt bearbeiteten
// Familien. Das Suchfeld filtert alle Bereiche. Ohne offenes Projekt ist er der Start; mit offenem Projekt (Haus-Symbol, Strg+D,
// ?start=1) liegt er darüber, das Projekt bleibt im Hintergrund offen, Esc oder ein Klick auf das offene Projekt kehrt zurück.
// Keine Fachlogik: alles kommt vom Server (/api/zuletzt, /api/projekte/ablage, /api/familien/zuletzt, /api/start/info), Texte und
// HTML stehen in start-text.js (in Node getestet). Öffnen, Anlegen: dialoge.js.

import { zustand } from '../zustand.js';
import { holen, senden, abfrage } from '../api.js';
import { meldung, kontextmenue, zeigen as dialogZeigen, oeffnenAusfuehren, projektOeffnenDialog, projektAnlegenDialog } from './dialoge.js';
import { knoten, fehlerText } from './werkzeug.js';
import {
  esc, sym, kachelnSortieren, filtern, kachelnHtml, kachelMenue, ablageHtml, ablageStatusText, ablageTitel, familienHtml, bibliothekTabelleHtml,
  gleicherOrdner, FELDER_PROJEKT, FELDER_FAMILIE,
} from './start-text.js';

let wurzel = null;
let sichtbar = false;
let ladeNr = 0;
let suche = '';
const daten = { zuletzt: null, ablage: null, familien: null, info: null, ablageLaeuft: false };
let kacheln = [], projekte = [], familien = [];   // die angezeigten (gefilterten) Listen; data-i verweist darauf

const projektOffen = () => !!zustand.status?.offen;
const offenerOrt = () => (projektOffen() ? zustand.status.ort : null);

export const istSichtbar = () => sichtbar;

// ---------------------------------------------------------------------------------------------- Aufbau

export function einrichten() {
  if (wurzel) return;
  wurzel = document.createElement('div');
  wurzel.id = 'startbildschirm';
  wurzel.hidden = true;
  wurzel.setAttribute('role', 'region');
  wurzel.setAttribute('aria-label', 'Startbildschirm');
  wurzel.innerHTML = `
    <aside class="sb-leiste">
      <div class="sb-logo"><span class="sb-logo-zeichen">${sym('kicad', 26)}</span><div><b>KICAD</b><small>HLKS-CAD</small></div></div>
      <h2>Projekte</h2>
      <button type="button" class="sb-aktion" data-aktion="oeffnen">${sym('ordner', 18)}<span>Öffnen …</span></button>
      <button type="button" class="sb-aktion" data-aktion="neu">${sym('neu', 18)}<span>Neu …</span></button>
      <button type="button" class="sb-aktion" data-aktion="beispiel">${sym('beispiel', 18)}<span>Beispielprojekt</span></button>
      <h2>Familien</h2>
      <button type="button" class="sb-aktion" data-aktion="bibliothek">${sym('buch', 18)}<span>Bibliothek …</span></button>
      <div class="sb-stand"></div>
    </aside>
    <main class="sb-haupt">
      <header class="sb-kopf">
        <h1>Projekte</h1>
        <span class="sb-offen-hinweis" hidden></span><span class="sb-fueller"></span>
        <button type="button" class="dlg-knopf sb-zurueck" hidden>${sym('haus', 14)} Zurück zum Projekt</button>
        <label class="sb-suchfeld">${sym('suche', 14)}<input type="search" class="sb-suche" placeholder="Projekte und Familien suchen …" aria-label="Suchen" autocomplete="off" spellcheck="false"></label>
      </header>
      <div class="sb-inhalt">
        <div class="sb-links">
          <section class="sb-block" data-block="zuletzt"><h2>Zuletzt geöffnet</h2><div class="sb-kacheln"></div></section>
          <section class="sb-block" data-block="ablage"><h2><span class="sb-ablage-titel">Alle KICAD-Projekte</span>
            <button type="button" class="sb-klein sb-neu-suchen" title="Die Ablage neu durchsuchen">${sym('neuSuchen', 13)} Neu suchen</button></h2>
            <p class="sb-hinweiszeile leise"></p><div class="sb-ablage"></div></section>
        </div>
        <aside class="sb-rechts"><section class="sb-block" data-block="familien"><h2>Familien – zuletzt bearbeitet</h2><p class="sb-hinweiszeile leise sb-fam-hinweis"></p><div class="sb-familien"></div></section></aside>
      </div>
    </main>`;
  document.body.appendChild(wurzel);

  wurzel.querySelectorAll('.sb-aktion').forEach((b) => { b.onclick = () => seitenAktion(b.dataset.aktion); });
  wurzel.querySelector('.sb-zurueck').onclick = () => schliessen();
  wurzel.querySelector('.sb-neu-suchen').onclick = () => ablageLaden(true);
  const feld = wurzel.querySelector('.sb-suche');
  feld.oninput = () => { suche = feld.value; alleZeichnen(); };
  feld.onkeydown = (e) => { if (e.key === 'Escape' && feld.value) { feld.value = ''; suche = ''; alleZeichnen(); e.stopPropagation(); e.preventDefault(); } };

  // Klicks auf Kacheln, Zeilen und Familien (ein Zuhörer je Bereich)
  wurzel.querySelector('.sb-kacheln').addEventListener('click', (e) => {
    const menue = e.target.closest('.sb-menue');
    if (menue) { e.stopPropagation(); const r = menue.getBoundingClientRect(); kachelMenueZeigen(Number(menue.dataset.i), r.left, r.bottom + 2); return; }
    const k = e.target.closest('.sb-kachel');
    if (k) projektOeffnen(kacheln[Number(k.dataset.i)]?.ordner);
  });
  wurzel.querySelector('.sb-kacheln').addEventListener('contextmenu', (e) => {
    const k = e.target.closest('.sb-kachel');
    if (!k) return;
    e.preventDefault();
    kachelMenueZeigen(Number(k.dataset.i), e.clientX, e.clientY);
  });
  wurzel.querySelector('.sb-ablage').addEventListener('click', (e) => {
    const z = e.target.closest('.sb-zeile');
    if (z) projektOeffnen(projekte[Number(z.dataset.i)]?.ordner);
  });
  wurzel.querySelector('.sb-ablage').addEventListener('contextmenu', (e) => {
    const z = e.target.closest('.sb-zeile');
    if (!z) return;
    e.preventDefault();
    ablageMenueZeigen(Number(z.dataset.i), e.clientX, e.clientY);
  });
  wurzel.querySelector('.sb-familien').addEventListener('click', (e) => {
    const f = e.target.closest('.sb-familie');
    if (f) familieOeffnen(familien[Number(f.dataset.i)]);
  });
  // Enter/Leertaste auf Kacheln, Zeilen und Familien; Pfeiltasten wandern zwischen den Kacheln
  wurzel.addEventListener('keydown', (e) => {
    const ziel = e.target.closest?.('.sb-kachel, .sb-zeile, .sb-familie');
    if (ziel && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); ziel.click(); }
    else if (ziel && e.key === 'ContextMenu') { e.preventDefault(); const r = ziel.getBoundingClientRect(); ziel.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, clientX: r.left + 20, clientY: r.top + 20 })); }
    else if (ziel?.classList.contains('sb-kachel') && ['ArrowLeft', 'ArrowRight'].includes(e.key)) {
      const alle = [...wurzel.querySelectorAll('.sb-kachel')];
      alle[alle.indexOf(ziel) + (e.key === 'ArrowRight' ? 1 : -1)]?.focus();
      e.preventDefault();
    }
    // Tasten bleiben im Startbildschirm: die Kürzel des Projekts darunter (z. B. DE, Strg+Z) dürfen nicht auslösen
    if (e.key !== 'Escape' && !(e.ctrlKey && e.key.toLowerCase() === 'd')) e.stopPropagation();
  });

  // Tastatur: Strg+D schaltet um, Esc kehrt zum Projekt zurück. Sonst gehen Tasten, die ausserhalb des Startbildschirms landen
  // (Fokus auf dem Hintergrund), nicht an das Projekt darunter.
  document.addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key.toLowerCase() === 'd') {
      e.preventDefault();   // sonst setzt der Browser ein Lesezeichen
      if (dialogOffen()) return;
      e.stopPropagation();
      umschalten();
      return;
    }
    if (!sichtbar || dialogOffen()) return;
    if (e.key === 'Escape') {
      // Esc schliesst zuerst, was darüber liegt: ein Dialog oder Menü (deren Zuhörer laufen vor diesem und haben das Ereignis
      // schon behandelt, cancelBubble) oder den Text im Suchfeld; erst dann kehrt es zum Projekt zurück
      if (e.cancelBubble || document.querySelector('.kmenu')) return;
      if (e.target instanceof HTMLInputElement && e.target.classList.contains('sb-suche') && e.target.value) return;
      if (projektOffen()) { e.preventDefault(); e.stopPropagation(); schliessen(); }
      return;
    }
    if (!wurzel.contains(e.target) && !(e.target instanceof HTMLInputElement) && !(e.target instanceof HTMLTextAreaElement)) e.stopPropagation();
  }, true);
}

const dialogOffen = () => (document.getElementById('dialogschicht')?.childElementCount ?? 0) > 0;

// ---------------------------------------------------------------------------------------------- Anzeigen und Schliessen

/** Zeigt den Startbildschirm und lädt die Listen neu. ueberProjekt: nur Hinweis für die Kopfzeile, die Wirkung folgt dem Status. */
export async function zeigen() {
  einrichten();
  if (!sichtbar) {
    sichtbar = true;
    wurzel.hidden = false;
    document.body.classList.add('start-sichtbar');
  }
  kopfAktualisieren();
  setTimeout(() => wurzel.querySelector('.sb-suche')?.focus({ preventScroll: true }), 0);
  await alleLaden();
}

export function schliessen() {
  if (!wurzel || !sichtbar) return;
  sichtbar = false;
  wurzel.hidden = true;
  wurzel.classList.remove('beschaeftigt');
  document.body.classList.remove('start-sichtbar');
  ladeNr++;                     // laufende Antworten verfallen
  try { document.activeElement?.blur?.(); } catch { /* egal */ }
}

/** Haus-Symbol und Strg+D: zeigen oder (bei offenem Projekt) zurückkehren. */
export function umschalten() {
  if (sichtbar) { if (projektOffen()) schliessen(); return; }
  zeigen().catch((e) => console.error('Startbildschirm', e));
}

function kopfAktualisieren() {
  const offen = projektOffen();
  const hinweis = wurzel.querySelector('.sb-offen-hinweis');
  hinweis.hidden = !offen;
  hinweis.textContent = offen ? `Offen: ${zustand.status.projekt ?? ''}` : '';
  wurzel.querySelector('.sb-zurueck').hidden = !offen;
  wurzel.classList.toggle('ueber-projekt', offen);
}

// ---------------------------------------------------------------------------------------------- Laden

async function alleLaden() {
  const nr = ++ladeNr;
  zuletztLaden(nr);
  ablageLaden(false, nr);
  familienLaden(nr);
  infoLaden(nr);
}

async function zuletztLaden(nr = ladeNr) {
  try {
    const l = await holen('/api/zuletzt?max=40&pruefen=1');
    if (nr !== ladeNr) return;
    daten.zuletzt = Array.isArray(l) ? l : [];
  } catch (e) { console.error('Zuletzt geöffnet', e); if (nr === ladeNr) daten.zuletzt = daten.zuletzt ?? []; }
  if (nr === ladeNr) alleZeichnen();
}

async function ablageLaden(neu, nr = ladeNr) {
  daten.ablageLaeuft = true;
  zeichnenAblage();
  try {
    const a = await holen(`/api/projekte/ablage${neu ? '?neu=1' : ''}`);
    if (nr !== ladeNr && !neu) return;
    daten.ablage = a;
  } catch (e) {
    console.error('Projektablage', e);
    daten.ablage = { erreichbar: false, hinweis: `Die Suche ist fehlgeschlagen: ${fehlerText(e)}`, projekte: [] };
  }
  daten.ablageLaeuft = false;
  if (sichtbar) zeichnenAblage();
}

async function familienLaden(nr = ladeNr) {
  try {
    const f = await holen('/api/familien/zuletzt?n=12');
    if (nr !== ladeNr) return;
    daten.familien = f;
  } catch (e) {
    console.error('Familien', e);
    if (nr !== ladeNr) return;
    daten.familien = { erreichbar: false, hinweis: `Die Familienbibliothek ist nicht lesbar: ${fehlerText(e)}`, familien: [] };
  }
  zeichnenFamilien();
}

async function infoLaden(nr = ladeNr) {
  if (daten.info) { zeichnenInfo(); return; }
  try { daten.info = await holen('/api/start/info'); } catch (e) { console.error('Programmstand', e); }
  if (nr === ladeNr || sichtbar) zeichnenInfo();
}

// ---------------------------------------------------------------------------------------------- Zeichnen

function alleZeichnen() {
  if (!wurzel) return;
  zeichnenKacheln(); zeichnenAblage(); zeichnenFamilien(); zeichnenInfo();
}

function zeichnenKacheln() {
  if (daten.zuletzt === null) { wurzel.querySelector('.sb-kacheln').innerHTML = '<p class="sb-leer">Wird geladen …</p>'; return; }
  kacheln = filtern(kachelnSortieren(daten.zuletzt), suche, ['name', 'objekt', 'ordner']);
  const leer = suche.trim() ? 'Keine zuletzt geöffneten Projekte passen zur Suche.' : 'Noch keine Projekte geöffnet. Öffnen Sie ein Projekt oder legen Sie ein Beispielprojekt an.';
  wurzel.querySelector('.sb-kacheln').innerHTML = kachelnHtml(kacheln, { offen: offenerOrt() }, leer);
}

function zeichnenAblage() {
  const a = daten.ablage;
  wurzel.querySelector('.sb-ablage-titel').textContent = ablageTitel(a);
  const hinweis = wurzel.querySelector('[data-block="ablage"] .sb-hinweiszeile');
  hinweis.textContent = daten.ablageLaeuft ? 'Die Ablage wird durchsucht …' : ablageStatusText(a);
  hinweis.classList.toggle('warnung', !!a && !a.erreichbar);
  wurzel.querySelector('.sb-neu-suchen').disabled = daten.ablageLaeuft;
  const ziel = wurzel.querySelector('.sb-ablage');
  if (!a) { ziel.innerHTML = ''; projekte = []; return; }
  projekte = filtern(a.projekte ?? [], suche, FELDER_PROJEKT);
  const leer = !a.erreichbar ? '' : suche.trim() ? 'Kein Projekt passt zur Suche.' : 'Keine KICAD-Projekte gefunden.';
  ziel.innerHTML = a.erreichbar || projekte.length ? ablageHtml(projekte, { offen: offenerOrt(), leerText: leer }) : '';
}

function zeichnenFamilien() {
  const f = daten.familien;
  const ziel = wurzel.querySelector('.sb-familien');
  const hinweis = wurzel.querySelector('.sb-fam-hinweis');
  if (!f) { ziel.innerHTML = '<p class="sb-leer">Wird geladen …</p>'; hinweis.textContent = ''; return; }
  hinweis.textContent = f.hinweis ?? '';
  hinweis.classList.toggle('warnung', !!f.hinweis);
  familien = filtern(f.familien ?? [], suche, FELDER_FAMILIE);
  ziel.innerHTML = familienHtml(familien, { leerText: suche.trim() ? 'Keine Familie passt zur Suche.' : (f.hinweis ? '' : 'Keine Familien in der Bibliothek.') });
}

function zeichnenInfo() {
  const i = daten.info;
  wurzel.querySelector('.sb-stand').innerHTML = i
    ? `<b>Version ${esc(i.version)}</b><br>Stand ${esc(i.stand ?? '–')}<br><span title="Liste «zuletzt»: ${esc(i.zuletztOrt ?? '')}">${esc(i.akteur)} · ${esc(i.rechner)}</span>`
    : '';
}

// ---------------------------------------------------------------------------------------------- Aktionen

function busy(an, text = '') {
  wurzel.classList.toggle('beschaeftigt', an);
  const h = wurzel.querySelector('.sb-offen-hinweis');
  if (an) { h.hidden = false; h.textContent = text; } else kopfAktualisieren();
}

/** Öffnet das Projekt (oder kehrt zurück, wenn es schon offen ist). Fehler erscheinen als Meldung; der Startbildschirm bleibt. */
async function projektOeffnen(ordner) {
  if (!ordner) return;
  if (projektOffen() && gleicherOrdner(ordner, zustand.status.ort)) { schliessen(); return; }
  busy(true, 'Projekt wird geöffnet …');
  try {
    await oeffnenAusfuehren(ordner);   // projektGeoeffnet() schliesst den Startbildschirm
  } catch (e) {
    console.error(e);
    meldung(`Das Projekt konnte nicht geöffnet werden: ${e.message ?? e}`, 'fehler', { vorschlag: e.vorschlag });
    if (sichtbar) { busy(false); zuletztLaden(); }
  } finally { if (sichtbar) busy(false); }
}

async function seitenAktion(a) {
  try {
    if (a === 'oeffnen') await projektOeffnenDialog();
    else if (a === 'neu') await projektAnlegenDialog(false);
    else if (a === 'beispiel') await projektAnlegenDialog(true);
    else if (a === 'bibliothek') await bibliothekOeffnen();
  } catch (e) { console.error(e); meldung(fehlerText(e), 'fehler'); }
}

/** «Bibliothek …»: bei offenem Projekt der Dialog «Familien im Explorer …», sonst die Liste der Bibliothek nur lesend. */
async function bibliothekOeffnen() {
  if (projektOffen()) {
    const { ausfuehren } = await import('./aktionen.js');
    schliessen();
    await ausfuehren('familie.explorer');
    return;
  }
  const w = knoten('<div class="sb-bibliothek"><p class="leise">Wird geladen …</p></div>');
  const dlg = dialogZeigen({ titel: 'Familienbibliothek (nur lesen)', inhalt: w, breite: 900, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
  try {
    const f = await holen('/api/familien/zuletzt?n=100');
    w.innerHTML = `${f.hinweis ? `<p class="warnung">${esc(f.hinweis)}</p>` : ''}<p class="leise">${esc(f.bibliothek?.pfad ?? '')} · ${esc(f.anzahl ?? (f.familien ?? []).length)} Familien. Zum Bearbeiten zuerst ein Projekt öffnen.</p>${bibliothekTabelleHtml(f.familien ?? [])}`;
  } catch (e) { w.innerHTML = `<p class="fehlertext">${esc(fehlerText(e))}</p>`; }
  await dlg;
}

function kachelMenueZeigen(i, x, y) {
  const k = kacheln[i];
  if (!k) return;
  kontextmenue(x, y, kachelMenue(k, { offen: offenerOrt() }).map((e) => (e.trenner ? { trenner: true } : { titel: e.titel, aktion: () => kachelAktion(e.aktion, k) })));
}

function ablageMenueZeigen(i, x, y) {
  const p = projekte[i];
  if (!p) return;
  kontextmenue(x, y, [
    { titel: gleicherOrdner(p.ordner, offenerOrt()) ? 'Zum Projekt' : 'Öffnen', aktion: () => projektOeffnen(p.ordner) },
    { titel: 'Anheften', aktion: () => kachelAktion('anheften', { ordner: p.ordner, name: p.name, angeheftet: false }) },
    { trenner: true },
    { titel: 'Ordner im Explorer zeigen', aktion: () => kachelAktion('explorer', p) },
  ]);
}

async function kachelAktion(a, k) {
  try {
    if (a === 'oeffnen') await projektOeffnen(k.ordner);
    else if (a === 'anheften') { await senden('/api/zuletzt/anheften', { ordner: k.ordner, an: !k.angeheftet, name: k.name }); await zuletztLaden(); }
    else if (a === 'entfernen') { await senden('/api/zuletzt/entfernen', { ordner: k.ordner }); await zuletztLaden(); }
    else if (a === 'explorer') await senden('/api/ordner/zeigen', { pfad: k.ordner });
  } catch (e) { console.error(e); meldung(`${e.message ?? e}`, 'fehler', { vorschlag: e.vorschlag }); }
}

/** Familie aus «zuletzt bearbeitet»: das Projekt der letzten Änderung öffnen und darin die Familie im Editor zeigen. */
async function familieOeffnen(f) {
  if (!f) return;
  if (!f.projektOrdner) {
    meldung(`Zu «${f.name}» ist das Projekt der letzten Änderung nicht bekannt.`, 'warnung', { vorschlag: 'Projekt von Hand öffnen und die Familie über «Familien im Explorer …» laden.' });
    return;
  }
  busy(true, 'Projekt wird geöffnet …');
  try {
    if (projektOffen() && gleicherOrdner(f.projektOrdner, zustand.status.ort)) schliessen();
    else await oeffnenAusfuehren(f.projektOrdner);
  } catch (e) {
    console.error(e);
    meldung(`Das Projekt «${f.projekt ?? ''}» konnte nicht geöffnet werden: ${e.message ?? e}`, 'fehler', { vorschlag: e.vorschlag });
    if (sichtbar) busy(false);
    return;
  } finally { if (sichtbar) busy(false); }
  try {
    const kz = (await abfrage('familien')).familien?.find((x) => x.id === f.id)?.kennzeichen;
    if (!kz) { meldung(`Die Familie «${f.name}» gibt es im Projekt «${f.projekt ?? ''}» nicht (mehr).`, 'warnung', { vorschlag: 'Über «Familien im Explorer …» aus der Bibliothek laden.' }); return; }
    const fenster = await import('../fenster.js');
    await fenster.ansichtOeffnen(kz);
  } catch (e) { console.error(e); meldung(`Die Familie konnte nicht geöffnet werden: ${e.message ?? e}`, 'fehler', { vorschlag: e.vorschlag }); }
}
