// Palette «Koordination» (Modul 3b, Auftrag C1): Kollisionsliste und Aussparungsliste als Fussleiste unter der Ansichtsfläche (angedockt, nicht modal – die Ansichten
// darüber bleiben bedienbar und passen sich der Höhe an). Keine Fachlogik: die Zeilen liefern die Abfragen kollisionen und aussparungsliste des Kerns, geändert wird nur über die Befehle
// kollision_status und aussparung_status. Ein Klick auf eine Zeile springt in den Grundriss (Element wählen und zoomen: dieselbe Aktion wie «Im Grundriss zeigen»
// aus Modul 3), «In 3D zeigen» öffnet die 3D-Ansicht und wählt das Element. Die Form einer Aussparung (rund = Kernbohrung von Hand, rechteckig = Regel) wählt der Schalter in der Zeile
// bzw. «Als Kernbohrung» / «Als Rechteck» für die markierten Zeilen: erst die Vorschau des Kerns (Trockenlauf) in der Palette, dann «Ausführen» (Befehl aussparung_form). Reine Teile (Tabellen, Parameter, Prüfungen): koordination-text.js.
// Nachtrag C1c: Die Kollisionsliste hat einen Paar-Filter und blendet Kollisionen der Paare aus, die der letzte Prüflauf nicht geprüft hat (Schalter «auch nicht geprüfte Paare» mit der Zahl der ausgeblendeten);
// beides wirkt im Browser auf die geladene Antwort (T.kollisionSicht), die Abfrage selbst bleibt unverändert.

import { zustand, element, derKlasse, abonnieren, auswaehlen, merken } from '../zustand.js';
import { abfrage } from '../api.js';
import * as fenster from '../fenster.js';
import { ausfuehren } from './register.js';
import { meldung } from './dialoge.js';
import { sende } from './befehle.js';
import { knoten, entprellen, esc } from './werkzeug.js';
import * as T from './koordination-text.js';
import { anmelden, zeigen as paletteZeigen, verbergen as paletteVerbergen } from './paletten.js';

const ART_KLASSEN = new Set(['Kollision', 'Aussparung']);
const st = {
  tab: 'kollisionen',
  k: { filter: T.KOLLISION_FILTER_VORGABE(), auswahl: new Set(), aktiv: null, daten: null, fehler: null, letztePaare: [] },
  a: { geschoss: '', status: [...T.AUSSPARUNG_STATUS], auswahl: new Set(), aktiv: null, daten: null, fehler: null, form: null },
};
let panel = null;
let ladeNr = 0;
let abo = null;

const q = (s) => panel?.querySelector(s);
export const panelOffen = () => !!panel;
const geschosse = () => derKlasse('Geschoss').sort((a, b) => (a.werte?.kote ?? 0) - (b.werte?.kote ?? 0));

/**
 * Die Kollisionsliste, wie die Palette sie zeigt (Antwort des Kerns, Paar-Filter und ohne Kollisionen der Paare, die der letzte Prüflauf nicht geprüft hat; rein: T.kollisionSicht).
 * Zwischengespeichert, solange Antwort, Paar-Filter, Schalter und die zuletzt im Dialog gewählten Paare gleich sind.
 */
let sichtStand = null;
function kSicht() {
  const k = st.k;
  if (!k.daten) return null;
  const key = `${k.filter.paar}|${k.filter.auchNichtGeprueft}|${k.letztePaare.join()}`;
  if (sichtStand?.daten !== k.daten || sichtStand.key !== key) sichtStand = { daten: k.daten, key, sicht: T.kollisionSicht(k.daten, k.filter, { zuletzt: k.letztePaare }) };
  return sichtStand.sicht;
}

/** Die im Dialog «Kollisionen prüfen» zuletzt gewählten Paare: Rückfall für die Liste, falls der Kern die Paare des letzten Laufs nicht nennt. */
export function letztePaareMerken(paare) { st.k.letztePaare = [...(paare ?? [])]; if (panel && st.tab === 'kollisionen') listeZeichnen(); }

/** Die im Dialog «Kollisionen prüfen» zuletzt gewählten Paare (der Projektbrowser zeigt dieselbe Sicht wie die Liste, G1). */
export const zuletztGewaehltePaare = () => [...st.k.letztePaare];

/** Kennzeichen der in der Kollisionsliste angehakten Kollisionen (für den BCF-Export). */
export const gewaehlteKollisionen = () => (panel && st.k.daten ? T.ausgewaehlte(st.k.auswahl, kSicht().zeilen ?? [], null, 'kollision') : []);

// ---------------------------------------------------------------------------------------------- Aufbau

/** Baut den Inhalt der Palette in die Hülle (slot), die die Palettenverwaltung stellt (W1: Höhe, Lage und ✕ gehören ihr). */
function bauen(slot) {
  if (!slot) return false;
  panel = knoten(`<div id="koord-panel" class="koord-panel" role="region" aria-label="Koordination">
    <div class="kp-kopf">
      <button type="button" class="kp-reiter" data-tab="kollisionen">Kollisionen</button>
      <button type="button" class="kp-reiter" data-tab="aussparungen">Aussparungen</button>
      <span class="kp-info"></span>
      <button type="button" class="kp-knopf kp-neu" title="Neu laden">Neu laden</button>
    </div>
    <div class="kp-filter"></div>
    <div class="kp-liste"></div>
    <div class="kp-meldung" hidden></div>
    <div class="kp-detail"></div>
    <div class="kp-aktionen"></div>
  </div>`);
  slot.appendChild(panel);
  verdrahten();
  abo = abonnieren('modell', (ev) => {
    if (!panel) return;
    if (!zustand.status?.offen) { panelSchliessen(); return; }
    const relevant = ev?.alles || (ev?.geloescht ?? []).length > 0 || (ev?.ids ?? []).some((id) => ART_KLASSEN.has(element(id)?.klasse));
    if (relevant) planLaden();
  });
  return true;
}

/** Blendet die Palette ein (Vorgabe: unten angedockt; die Verwaltung merkt, wohin der Benutzer sie gelegt hat) und lädt die Liste. */
export function panelOeffnen(tab = st.tab) {
  if (!zustand.status?.offen) return;
  paletteZeigen('koordination');
  if (!panel) return;
  st.tab = tab;
  rahmenZeichnen();
  laden();
}

/** Blendet die Palette aus (✕ am Palettenkopf, Projekt geschlossen); den Aufräumschritt macht abbauen() über die Verwaltung. */
export function panelSchliessen() { paletteVerbergen('koordination'); }

function abbauen() {
  abo?.(); abo = null;
  panel?.remove();
  panel = null;
}

// Als Palette der Palettenverwaltung (W1): Vorgabe unten angedockt, nur auf Verlangen sichtbar, nach dem Neuladen nicht wiederhergestellt (braucht ein offenes Projekt)
anmelden({
  id: 'koordination', titel: 'Koordination', ort: 'unten', reihenfolge: 30, vorgabeSichtbar: false, fluechtig: true, lazy: true, scroll: false,
  elementId: 'palette-koordination', symbol: 'koordination', anteil: 1,
  bauen, abbauen, oeffnen: () => panelOeffnen(), aktiv: () => !!zustand.status?.offen,
});

const planLaden = entprellen(() => { if (panel) laden(); }, 400);
export const panelNeuLaden = () => { if (panel) laden(); };

function panelMeldung(html, art = 'info') {
  const m = q('.kp-meldung');
  if (!m) return;
  m.hidden = !html;
  m.className = `kp-meldung ${art}`;
  m.innerHTML = html ? `${html}<button type="button" class="kp-meldung-zu" title="Schliessen">✕</button>` : '';
}

// ---------------------------------------------------------------------------------------------- Zeichnen

function rahmenZeichnen() {
  panel.querySelectorAll('.kp-reiter').forEach((b) => b.classList.toggle('aktiv', b.dataset.tab === st.tab));
  panelMeldung('');
  st.a.form = null;   // eine offene Vorschau der Form gilt nur für die Meldung, die jetzt verschwindet
  q('.kp-filter').innerHTML = st.tab === 'kollisionen' ? kollisionFilterHtml() : aussparungFilterHtml();
  q('.kp-aktionen').innerHTML = st.tab === 'kollisionen' ? kollisionAktionenHtml() : aussparungAktionenHtml();
  listeZeichnen();
}

function kollisionFilterHtml() {
  const f = st.k.filter;
  const opt = (liste, w, alle = '(alle)') => `<option value="">${alle}</option>` + liste.map((x) => `<option value="${x}"${x === w ? ' selected' : ''}>${esc(x)}</option>`).join('');
  return `<span class="kp-gruppe">Status ${T.KOLLISION_STATUS.map((s) => `<label class="haken"><input type="checkbox" class="kf-status" value="${s}"${f.status.includes(s) ? ' checked' : ''}> ${esc(T.statusText(s))}</label>`).join(' ')}</span>`
    + `<label>Art <select class="kf-art">${opt(T.KOLLISION_ARTEN, f.art)}</select></label>`
    + `<label>Typ <select class="kf-typ">${opt(T.KOLLISION_TYPEN, f.typ)}</select></label>`
    + `<label>Geschoss <select class="kf-geschoss">${T.geschossOptionenHtml(geschosse(), f.geschoss)}</select></label>`
    + `<label>Element <input type="search" class="kf-element" value="${esc(f.element)}" placeholder="Kennzeichen" data-kein-enter="1"></label>`
    + `<label>Paar <select class="kf-paar">${T.paarOptionenHtml(kSicht(), f.paar)}</select></label>`
    + `<label class="haken kf-ungeprueft-feld"><input type="checkbox" class="kf-ungeprueft"${f.auchNichtGeprueft ? ' checked' : ''}> <span class="kf-ungeprueft-text">${esc(T.ungeprueftText(kSicht(), f.auchNichtGeprueft))}</span></label>`;
}

function aussparungFilterHtml() {
  const f = st.a;
  return `<span class="kp-gruppe">Status ${T.AUSSPARUNG_STATUS.map((s) => `<label class="haken"><input type="checkbox" class="af-status" value="${s}"${f.status.includes(s) ? ' checked' : ''}> ${esc(T.statusText(s))}</label>`).join(' ')}</span>`
    + `<label>Geschoss <select class="af-geschoss">${T.geschossOptionenHtml(geschosse(), f.geschoss)}</select></label>`;
}

const statusOptionen = (liste) => liste.map((s) => `<option value="${s}">${esc(T.statusText(s))}</option>`).join('');

function kollisionAktionenHtml() {
  return `<button type="button" class="kp-knopf" data-kp="grundriss">Im Grundriss zeigen</button><button type="button" class="kp-knopf" data-kp="3d">In 3D zeigen</button>`
    + `<span class="kp-trenner"></span><label>Status <select class="ka-status">${statusOptionen(T.KOLLISION_STATUS)}</select></label>`
    + `<label>Kommentar <input type="text" class="ka-kommentar" placeholder="leer = unverändert" data-kein-enter="1"></label>`
    + `<label class="haken"><input type="checkbox" class="ka-entfernen"> Kommentar entfernen</label><button type="button" class="kp-knopf kp-primaer" data-kp="kstatus">Status setzen</button>`
    + `<span class="kp-trenner"></span><button type="button" class="kp-knopf" data-kp="aktion:koordination.pruefen">Kollisionen prüfen …</button><button type="button" class="kp-knopf" data-kp="aktion:koordination.bcf-export">BCF exportieren …</button><button type="button" class="kp-knopf" data-kp="aktion:koordination.bcf-import">BCF importieren …</button>`;
}

function aussparungAktionenHtml() {
  return `<button type="button" class="kp-knopf" data-kp="grundriss">Im Grundriss zeigen</button><button type="button" class="kp-knopf" data-kp="3d">In 3D zeigen</button>`
    + `<span class="kp-trenner"></span><label>Status <select class="aa-status">${statusOptionen(T.AUSSPARUNG_STATUS)}</select></label><button type="button" class="kp-knopf kp-primaer" data-kp="astatus">Status setzen</button>`
    + `<button type="button" class="kp-knopf" data-kp="afreigeben" title="Gewählte Aussparungen freigeben">Freigeben</button>`
    + `<span class="kp-trenner"></span><button type="button" class="kp-knopf" data-kp="aform:rund" title="Gewählte Aussparungen als Kernbohrung (rund, von Hand gewählt) – zuerst eine Vorschau des Kerns">${T.FORM_KNOPF.rund}</button>`
    + `<button type="button" class="kp-knopf" data-kp="aform:rechteckig" title="Gewählte Aussparungen zurück zum Rechteck (Regel) – zuerst eine Vorschau des Kerns">${T.FORM_KNOPF.rechteckig}</button>`
    + `<span class="kp-trenner"></span><button type="button" class="kp-knopf" data-kp="aktion:koordination.aus-vorschlagen">Vorschlagen …</button><button type="button" class="kp-knopf" data-kp="aktion:koordination.aus-nummerieren">Nummerieren …</button>`
    + `<button type="button" class="kp-knopf" data-kp="aktion:koordination.aus-abgleich">Abgleich …</button><button type="button" class="kp-knopf" data-kp="aktion:koordination.aus-plan">Aussparungsplan …</button><button type="button" class="kp-knopf" data-kp="aktion:koordination.aus-ifc">IFC für Architekt …</button>`;
}

function zeilenAnzeige() {
  if (st.tab === 'kollisionen') return kSicht()?.zeilen ?? [];
  return T.aussparungFilter(st.a.daten?.zeilen ?? [], { status: st.a.status });
}
const sicht = () => (st.tab === 'kollisionen' ? st.k : st.a);
const schluessel = () => (st.tab === 'kollisionen' ? 'kollision' : 'aussparung');

function listeZeichnen() {
  if (!panel) return;
  const s = sicht();
  const liste = q('.kp-liste');
  const oben = liste.scrollTop;
  if (s.fehler) liste.innerHTML = `<div class="fehlerkasten" role="alert"><b>${esc(s.fehler.text)}</b>${s.fehler.vorschlag ? `<div>Vorschlag: ${esc(s.fehler.vorschlag)}</div>` : ''}</div>`;
  else if (!s.daten) liste.innerHTML = '<div class="lauf"><span class="spinner" aria-hidden="true"></span><span class="lauf-text">Wird geladen …</span></div>';
  else if (st.tab === 'kollisionen') liste.innerHTML = T.kollisionTabelleHtml(kSicht(), { auswahl: s.auswahl, aktiv: s.aktiv });
  else {
    const z = zeilenAnzeige();
    liste.innerHTML = T.aussparungTabelleHtml(z, { auswahl: s.auswahl, aktiv: s.aktiv, leerHinweis: (s.daten.zeilen ?? []).length || s.geschoss ? 'Keine Aussparung passt zum Filter.' : 'Noch keine Aussparungen – «Vorschlagen …» legt sie an.' });
  }
  liste.scrollTop = oben;
  q('.kp-info').textContent = st.tab === 'kollisionen' ? T.kollisionKopf(kSicht()) : T.aussparungKopf(s.daten, zeilenAnzeige().length);
  if (st.tab === 'kollisionen') paarFilterZeichnen();
  detailZeichnen();
}

/** Paar-Filter und Schalter «auch nicht geprüfte Paare» zeigen Zahlen aus der geladenen Antwort; sie ändern sich mit jedem Laden. */
function paarFilterZeichnen() {
  const sel = q('.kf-paar'), txt = q('.kf-ungeprueft-text');
  const sicht = kSicht();
  if (sel) sel.innerHTML = T.paarOptionenHtml(sicht, st.k.filter.paar);
  if (txt) { txt.textContent = T.ungeprueftText(sicht, st.k.filter.auchNichtGeprueft); txt.parentElement.title = T.ungeprueftTitel(sicht); }
}

function aktiveZeile() {
  const s = sicht();
  return zeilenAnzeige().find((z) => z[schluessel()] === s.aktiv) ?? null;
}

function detailZeichnen() {
  const z = aktiveZeile();
  q('.kp-detail').innerHTML = st.tab === 'kollisionen' ? T.kollisionDetailHtml(z) : T.aussparungDetailHtml(z);
}

// ---------------------------------------------------------------------------------------------- Laden

async function laden() {
  const mein = ++ladeNr;
  const s = sicht();
  const tab = st.tab;
  s.fehler = null;
  try {
    let daten;
    if (tab === 'kollisionen') {
      const p = T.kollisionFilterParameter(s.filter);
      daten = p === null ? { zeilen: [], anzahl: 0, hinweis: 'Kein Status gewählt.' } : await abfrage('kollisionen', p);
    } else daten = await abfrage('aussparungsliste', s.geschoss ? { geschoss: s.geschoss } : {});
    if (mein !== ladeNr || !panel) return;
    s.daten = daten;
  } catch (e) {
    if (mein !== ladeNr || !panel) return;
    console.error(tab, e);
    s.fehler = { text: e.message ?? String(e), vorschlag: e.vorschlag ?? '' };
  }
  if (tab === st.tab) listeZeichnen();
}

// ---------------------------------------------------------------------------------------------- Springen

/** Zeigt ein Element im Grundriss seines Geschosses (wählen und zoomen) – dieselbe Aktion wie «Im Grundriss zeigen» im Schema. */
export async function imGrundrissZeigen(ref) {
  const el = element(ref);
  if (!el) { meldung(`${ref} gibt es nicht (mehr).`, 'warnung'); return false; }
  if (!el.geometrie || !el.geschoss) {
    meldung(`${el.kennzeichen} hat keine Lage in einem Geschoss.`, 'info', { vorschlag: '«In 3D zeigen» wählt das Element in der 3D-Ansicht.' });
    return false;
  }
  await ausfuehren('schema.im-grundriss-zeigen', { element: el.id });
  return true;
}

/** Öffnet eine 3D-Ansicht (eine offene zuerst) und wählt das Element. */
export async function in3dZeigen(ref) {
  const el = element(ref);
  if (!el) { meldung(`${ref} gibt es nicht (mehr).`, 'warnung'); return false; }
  const offen = fenster.alle().map((o) => element(o.id)?.kennzeichen);
  const liste = derKlasse('Ansicht').filter((a) => a.werte?.art === '3d' && a.werte?.vorlage !== true)
    .sort((a, b) => (offen.includes(b.kennzeichen) ? 1 : 0) - (offen.includes(a.kennzeichen) ? 1 : 0));
  if (liste.length === 0) { meldung('Es gibt keine 3D-Ansicht.', 'info', { vorschlag: 'Register Ansicht › 3D-Ansicht.' }); return false; }
  await fenster.ansichtOeffnen(liste[0].kennzeichen);
  auswaehlen(el.id);
  return true;
}

// ---------------------------------------------------------------------------------------------- Bedienung

function verdrahten() {
  panel.addEventListener('click', async (e) => {
    const t = e.target;
    if (t.closest('.kp-neu')) { laden(); return; }
    if (t.closest('.kp-meldung-zu')) { panelMeldung(''); return; }
    const reiter = t.closest('.kp-reiter');
    if (reiter) { if (reiter.dataset.tab !== st.tab) { st.tab = reiter.dataset.tab; rahmenZeichnen(); if (!sicht().daten) laden(); } return; }
    const formwahl = t.closest('.kp-formwahl');
    if (formwahl) { await formVorschau(formwahl.dataset.form, [formwahl.dataset.aus]); return; }
    const chip = t.closest('.kp-beteiligt');
    if (chip) { await imGrundrissZeigen(chip.dataset.ref); return; }
    const knopf = t.closest('[data-kp]');
    if (knopf) { await aktion(knopf.dataset.kp); return; }
    if (t.closest('.kp-wahl, .kp-alle')) return;
    const zeile = t.closest('.kp-zeile');
    if (zeile) {
      const s = sicht();
      s.aktiv = zeile.dataset.kz;
      liste().querySelectorAll('.kp-zeile').forEach((r) => r.classList.toggle('aktiv', r === zeile));
      detailZeichnen();
      await imGrundrissZeigen(s.aktiv);
    }
  });
  panel.addEventListener('change', (e) => {
    const t = e.target;
    const s = sicht();
    if (t.matches('.kp-wahl')) {
      const kz = t.closest('tr').dataset.kz;
      if (t.checked) s.auswahl.add(kz); else s.auswahl.delete(kz);
      return;
    }
    if (t.matches('.kp-alle')) {
      for (const z of zeilenAnzeige().slice(0, 500)) { if (t.checked) s.auswahl.add(z[schluessel()]); else s.auswahl.delete(z[schluessel()]); }
      listeZeichnen();
      return;
    }
    if (t.matches('.kf-status')) { st.k.filter.status = [...panel.querySelectorAll('.kf-status:checked')].map((c) => c.value); laden(); return; }
    if (t.matches('.kf-art')) { st.k.filter.art = t.value; laden(); return; }
    if (t.matches('.kf-typ')) { st.k.filter.typ = t.value; laden(); return; }
    if (t.matches('.kf-geschoss')) { st.k.filter.geschoss = t.value; laden(); return; }
    if (t.matches('.kf-element')) { st.k.filter.element = t.value.trim(); laden(); return; }
    if (t.matches('.kf-paar')) { st.k.filter.paar = t.value; listeZeichnen(); return; }   // Paar und Schalter wirken im Browser auf die geladene Antwort (T.kollisionSicht)
    if (t.matches('.kf-ungeprueft')) { st.k.filter.auchNichtGeprueft = t.checked; listeZeichnen(); return; }
    if (t.matches('.af-status')) { st.a.status = [...panel.querySelectorAll('.af-status:checked')].map((c) => c.value); listeZeichnen(); return; }
    if (t.matches('.af-geschoss')) { st.a.geschoss = t.value; laden(); }
  });
}
const liste = () => q('.kp-liste');

async function aktion(was) {
  if (was.startsWith('aktion:')) { await ausfuehren(was.slice(7)); return; }
  if (was === 'form-ja') { await formAusfuehren(); return; }
  if (was === 'form-nein') { st.a.form = null; panelMeldung(''); return; }
  const s = sicht();
  const z = zeilenAnzeige();
  const wahl = T.ausgewaehlte(s.auswahl, z, s.aktiv, schluessel());
  if (was === 'grundriss' || was === '3d') {
    const ref = s.aktiv ?? wahl[0];
    if (!ref) { meldung('Zuerst eine Zeile wählen.', 'info'); return; }
    await (was === 'grundriss' ? imGrundrissZeigen(ref) : in3dZeigen(ref));
    return;
  }
  if (was === 'kstatus') await kollisionStatus(wahl);
  else if (was === 'astatus') await aussparungStatus(wahl, q('.aa-status')?.value);
  else if (was === 'afreigeben') await aussparungStatus(wahl, 'freigegeben');
  else if (was.startsWith('aform:')) await formVorschau(was.slice(6), wahl);
}

const ergebnisHtml = (r) => `<b>${esc(r.angenommen ? (r.hinweise?.[0] ?? 'Erledigt.') : T.ablehnung(r))}</b>${r.angenommen && (r.hinweise ?? []).length > 1 ? T.hinweiseListeHtml(r.hinweise.slice(1), { max: 8 }) : ''}`;

async function kollisionStatus(wahl) {
  const f = { kollisionen: wahl, status: q('.ka-status')?.value, kommentar: q('.ka-kommentar')?.value, kommentarEntfernen: q('.ka-entfernen')?.checked };
  const pr = T.kollisionStatusPruefen(f);
  if (pr !== true) { panelMeldung(esc(pr), 'fehler'); return; }
  const r = await sende('kollision_status', T.kollisionStatusParameter(f));
  panelMeldung(ergebnisHtml(r), r.angenommen ? 'ok' : 'fehler');
  if (r.angenommen) { q('.ka-kommentar').value = ''; q('.ka-entfernen').checked = false; await laden(); }
}

/**
 * Form wählen (aussparung_form): erst die Vorschau des Kerns (Trockenlauf) in der Palette, dann «Ausführen». Gesendet werden nur Aussparungen, die der Kern in der Liste als
 * wählbar meldet (`formWaehlbar`); die übrigen stehen mit seinem Grund (`formSperre`) in der Meldung. Eine Ablehnung des Kerns zeigt immer Fehler und Vorschlag.
 */
async function formVorschau(form, gewaehlt) {
  const pr = T.formPruefen({ aussparungen: gewaehlt, form });
  if (pr !== true) { panelMeldung(esc(pr), 'fehler'); return; }
  const ziele = T.formZiele(st.a.daten?.zeilen ?? [], gewaehlt);
  st.a.form = null;
  if (ziele.senden.length === 0) { panelMeldung(T.formKeineHtml(ziele, form), 'fehler'); return; }
  const r = await sende('aussparung_form', T.formParameter({ aussparungen: ziele.senden, form }), { trockenlauf: true });
  st.a.form = r.angenommen ? { form, aussparungen: ziele.senden } : null;
  panelMeldung(T.formMeldungHtml(r, { trocken: true, form, ziele }), r.angenommen ? 'info' : 'fehler');
}

async function formAusfuehren() {
  const f = st.a.form;
  if (!f) return;
  st.a.form = null;
  const r = await sende('aussparung_form', T.formParameter(f));
  panelMeldung(T.formMeldungHtml(r, { form: f.form }), r.angenommen ? 'ok' : 'fehler');
  if (r.angenommen) await laden();
}

async function aussparungStatus(wahl, status) {
  const f = { aussparungen: wahl, status };
  const pr = T.aussparungStatusPruefen(f);
  if (pr !== true) { panelMeldung(esc(pr), 'fehler'); return; }
  const r = await sende('aussparung_status', T.aussparungStatusParameter(f));
  panelMeldung(ergebnisHtml(r), r.angenommen ? 'ok' : 'fehler');
  if (r.angenommen) await laden();
}
