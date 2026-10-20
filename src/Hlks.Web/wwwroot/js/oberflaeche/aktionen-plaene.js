// Aktionen der Planblätter im Register «Ansicht», Gruppe «Planblätter»: Planblatt anlegen (Befehl planblatt_anlegen),
// Ansicht auf ein Blatt platzieren (ansicht_platzieren), PDF in neuem Fenster öffnen (/api/plan/{kz}.pdf), dazu «Planblatt
// öffnen» für das Kontextmenü im Projektbrowser. Alles läuft über Befehle des Kerns; hier stehen nur Dialoge und Aufrufe.
// Eingetragen wird nur über registrieren() (register.js); geladen wird diese Datei vom Projektbrowser.

import { zustand, element, derKlasse } from '../zustand.js';
import * as fenster from '../fenster.js';
import { registrieren } from './register.js';
import { zeigen, meldung } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { esc, knoten, vergleichen, aufElement, anzeigeName } from './werkzeug.js';
import { abfrage } from '../api.js';
import { plansaetzeDaten, plansaetzeLaden } from './serienpdf-daten.js';
import { ART_OPTIONEN, vorschlagText, vorschlagParameter } from './planblatt-hk.js';

const FORMATE = ['A4', 'A3', 'A2', 'A1', 'A0'];
const FORMAT_VORGABE = 'A3';                      // Vorgabe des Kerns (Blattformate.Vorgabe)
const PLANARTEN = new Set(['grundriss', 'schnitt', 'ansicht', 'schema', 'liste']);   // Schema seit Modul 3 (C2): ohne Massstab, Papier-mm; Liste seit Modul 7 Teil A: Tabelle im PDF

const bloetter = () => derKlasse('Planblatt').sort((a, b) => vergleichen(a.werte?.nummer ?? a.kennzeichen, b.werte?.nummer ?? b.kennzeichen));
const blattText = (b) => [b.kennzeichen, b.werte?.nummer, b.werte?.titel].filter(Boolean).join(' · ');

/** Ansichten, die auf ein Blatt dürfen: Grundriss, Schnitt, Ansicht, Schema, Liste (keine Vorlagen), die noch auf keinem Blatt stehen. */
function platzierbar() {
  const belegt = new Set(derKlasse('Ansichtsfenster').map((f) => String(f.bezuege?.ansicht ?? '').toLowerCase()));
  return derKlasse('Ansicht')
    .filter((a) => PLANARTEN.has(a.werte?.art) && a.werte?.vorlage !== true && !belegt.has(a.kennzeichen.toLowerCase()))
    .sort((a, b) => vergleichen(anzeigeName(a), anzeigeName(b)));
}

/** Planblatt aus Parameter, aktiver Ansicht oder – gibt es nur eines – dem einzigen Blatt. */
function blattVon(p) {
  const b = element(p?.planblatt ?? zustand.aktiveAnsicht);
  if (b?.klasse === 'Planblatt') return b;
  const alle = bloetter();
  return alle.length === 1 ? alle[0] : null;
}

// ---------------------------------------------------------------------------------------------- Planblatt anlegen

async function planblattAnlegen() {
  // Modul 7 Teil A: Gewerk, Geschoss und Art bestimmen den Vorschlag des Kerns (Abfrage plannummer_vorschlag) für Plannummer und Titel; beides bleibt überschreibbar
  if (!plansaetzeDaten()) await plansaetzeLaden();
  const gewerke = plansaetzeDaten()?.gewerke ?? [];
  const geschosse = derKlasse('Geschoss').sort((a, b) => (a.werte?.kote ?? 0) - (b.werte?.kote ?? 0));
  const w = knoten(`<div class="ansichtform plan-anlegen">
    <div class="fz-zeile">
      <label class="fz"><span>Gewerk</span><select class="f-gewerk"><option value="">– (frei)</option>${gewerke.map((g) => `<option value="${esc(g.gewerk)}">${esc(g.name)}</option>`).join('')}</select></label>
      <label class="fz"><span>Geschoss</span><select class="f-geschoss"><option value="">–</option>${geschosse.map((g) => `<option value="${esc(g.kennzeichen)}">${esc(g.name ? `${g.kennzeichen} – ${g.name}` : g.kennzeichen)}</option>`).join('')}</select></label>
      <label class="fz"><span>Art</span><select class="f-art">${ART_OPTIONEN.map(([v, t]) => `<option value="${esc(v)}">${esc(t)}</option>`).join('')}</select></label>
    </div>
    <label class="fz"><span>Plannummer</span><input type="text" class="f-nummer" placeholder="z. B. 2317-101 – oder Gewerk und Geschoss wählen (Vorschlag)"></label>
    <p class="dlg-text leise f-vorschlag" aria-live="polite"></p>
    <label class="fz"><span>Titel (Planinhalt)</span><input type="text" class="f-titel" placeholder="z. B. Grundriss Erdgeschoss Sanitär"></label>
    <label class="fz"><span>Format</span><select class="f-format">${FORMATE.map((f) => `<option ${f === FORMAT_VORGABE ? 'selected' : ''}>${f}</option>`).join('')}</select></label>
    <label class="haken"><input type="checkbox" class="f-hoch"> Hochformat</label>
  </div>`);
  const q = (s) => w.querySelector(s);
  let nummerVonHand = false, titelVonHand = false, lauf = 0;
  q('.f-nummer').addEventListener('input', () => { nummerVonHand = q('.f-nummer').value.trim() !== ''; });
  q('.f-titel').addEventListener('input', () => { titelVonHand = q('.f-titel').value.trim() !== ''; });
  async function vorschlagen() {
    const mein = ++lauf;
    const par = vorschlagParameter({ gewerk: q('.f-gewerk').value, geschoss: q('.f-geschoss').value, art: q('.f-art').value });
    if (!par.gewerk) { q('.f-vorschlag').textContent = ''; return; }
    try {
      const v = await abfrage('plannummer_vorschlag', par);
      if (mein !== lauf) return;
      if (v.nummer && !nummerVonHand) q('.f-nummer').value = v.nummer;
      if (v.titel && !titelVonHand) q('.f-titel').value = v.titel;
      q('.f-vorschlag').textContent = v.nummer ? `Vorschlag des Kerns: ${vorschlagText(v)}` : `Kein Vorschlag: ${vorschlagText(v)}`;
    } catch (e) { if (mein === lauf) q('.f-vorschlag').textContent = `Kein Vorschlag: ${e.message ?? e}`; }
  }
  for (const s of ['.f-gewerk', '.f-geschoss', '.f-art']) q(s).addEventListener('change', vorschlagen);
  const ok = await zeigen({
    titel: 'Planblatt anlegen', inhalt: w, breite: 560,
    beiOeffnen: () => q('.f-gewerk').focus(),
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Anlegen', wert: true, primaer: true,
        pruefen: () => !q('.f-nummer').value.trim() ? 'Die Plannummer darf nicht leer sein (Gewerk und Geschoss wählen gibt einen Vorschlag).' : !q('.f-titel').value.trim() ? 'Der Titel darf nicht leer sein.' : true }],
  });
  if (ok !== true) return;
  const par = {
    nummer: q('.f-nummer').value.trim(), titel: q('.f-titel').value.trim(), format: q('.f-format').value, hochformat: q('.f-hoch').checked,
  };
  if (q('.f-gewerk').value) par.gewerk = q('.f-gewerk').value;
  const r = await sende('planblatt_anlegen', par);
  if (!ergebnisMelden(r, 'Planblatt angelegt.')) return;
  const neu = (r.aenderungen ?? []).find((a) => a.klasse === 'Planblatt' && a.art === 'angelegt');
  if (neu?.kennzeichen && await aufElement(neu.kennzeichen)) await fenster.ansichtOeffnen(neu.kennzeichen);
}

registrieren({
  id: 'plan.anlegen', titel: 'Planblatt', symbol: 'planblatt', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  ausfuehren: planblattAnlegen,
});

// ---------------------------------------------------------------------------------------------- Ansicht platzieren

async function ansichtPlatzieren(p) {
  const blaetter = bloetter(), ansichten = platzierbar();
  if (blaetter.length === 0) { meldung('Es gibt noch kein Planblatt.', 'info', { vorschlag: 'Zuerst ein Planblatt anlegen.' }); return; }
  if (ansichten.length === 0) { meldung('Alle Grundrisse, Schnitte, Ansichten, Schemas und Listen stehen schon auf einem Planblatt.', 'info', { vorschlag: 'Eine Ansicht duplizieren oder neu anlegen.' }); return; }
  const vorAnsicht = ansichten.find((a) => a.kennzeichen === (p?.ansicht ?? zustand.aktiveAnsicht)) ?? ansichten[0];
  const vorBlatt = blattVon(p) ?? blaetter[0];
  const w = knoten(`<div class="ansichtform">
    <label class="fz"><span>Ansicht</span><select class="f-ansicht">${ansichten.map((a) => `<option value="${esc(a.kennzeichen)}" ${a === vorAnsicht ? 'selected' : ''}>${esc(anzeigeName(a))} (${esc(a.kennzeichen)})</option>`).join('')}</select></label>
    <label class="fz"><span>Planblatt</span><select class="f-blatt">${blaetter.map((b) => `<option value="${esc(b.kennzeichen)}" ${b === vorBlatt ? 'selected' : ''}>${esc(blattText(b))}</option>`).join('')}</select></label>
    <p class="dlg-text leise">Die Lage auf dem Blatt wählt der Kern (Mitte des freien Bereichs); Massstab und Ausschnitt kommen von der Ansicht (ein Schema und eine Liste haben keinen Massstab; eine Liste erscheint als Tabelle).</p>
  </div>`);
  const ok = await zeigen({ titel: 'Ansicht platzieren', inhalt: w, breite: 520, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Platzieren', wert: true, primaer: true }] });
  if (ok !== true) return;
  const blatt = w.querySelector('.f-blatt').value, ansicht = w.querySelector('.f-ansicht').value;
  const r = await sende('ansicht_platzieren', { planblatt: blatt, ansicht });
  if (!ergebnisMelden(r, `Ansicht ${ansicht} auf ${blatt} platziert.`)) return;
  await fenster.ansichtOeffnen(blatt);
}

registrieren({
  id: 'plan.platzieren', titel: 'Ansicht platzieren', symbol: 'grundriss', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  aktiv: () => bloetter().length > 0 && platzierbar().length > 0,
  hinweis: () => (bloetter().length === 0 ? 'Zuerst ein Planblatt anlegen.' : 'Alle Ansichten stehen schon auf einem Planblatt.'),
  ausfuehren: ansichtPlatzieren,
});

// ---------------------------------------------------------------------------------------------- PDF und Öffnen

async function blattWaehlen(p, titel) {
  const direkt = blattVon(p);
  if (direkt) return direkt;
  const liste = bloetter();
  const w = knoten(`<div class="ansichtform"><label class="fz"><span>Planblatt</span><select class="f-blatt">${liste.map((b) => `<option value="${esc(b.kennzeichen)}">${esc(blattText(b))}</option>`).join('')}</select></label></div>`);
  const ok = await zeigen({ titel, inhalt: w, breite: 460, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'OK', wert: true, primaer: true }] });
  return ok === true ? element(w.querySelector('.f-blatt').value) : null;
}

registrieren({
  id: 'plan.pdf', titel: 'PDF öffnen', symbol: 'oeffnen', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  aktiv: () => bloetter().length > 0, hinweis: 'Zuerst ein Planblatt anlegen.',
  async ausfuehren(p) {
    const b = await blattWaehlen(p, 'PDF öffnen');
    if (!b) return;
    // Der Kern erzeugt das PDF beim Abruf; der Browser zeigt es im eingebauten Betrachter (Drucken von dort)
    window.open(`/api/plan/${encodeURIComponent(b.kennzeichen)}.pdf`, '_blank', 'noopener');
  },
});

// ---------------------------------------------------------------------------------------------- Revision hinzufügen (Modul 7 Teil A)

/** Revision hinzufügen: Dialog mit Planblatt (vorbelegt), Beschreibung (Pflicht) und Datum (leer = heute); den Index (nächster Buchstabe) und das Datum setzt der Kern. */
async function revisionHinzufuegen(p) {
  const alle = bloetter();
  if (alle.length === 0) { meldung('Es gibt noch kein Planblatt.', 'info', { vorschlag: 'Zuerst ein Planblatt anlegen.' }); return; }
  const vor = blattVon(p) ?? alle[0];
  const w = knoten(`<div class="ansichtform">
    <label class="fz"><span>Planblatt</span><select class="f-blatt">${alle.map((b) => `<option value="${esc(b.kennzeichen)}" ${b === vor ? 'selected' : ''}>${esc(blattText(b))}</option>`).join('')}</select></label>
    <label class="fz"><span>Beschreibung</span><input type="text" class="f-beschreibung" placeholder="z. B. Beschriftungen ergänzt"></label>
    <label class="fz"><span>Datum</span><input type="text" class="f-datum" placeholder="heute (TT.MM.JJJJ)"></label>
    <p class="dlg-text leise">Der Plankopf bekommt eine Zeile in «Planänderungen»; der Index ist der nächste Buchstabe (A, B, C …).</p>
  </div>`);
  const q = (s) => w.querySelector(s);
  const ok = await zeigen({
    titel: 'Revision hinzufügen', inhalt: w, breite: 520,
    beiOeffnen: () => q('.f-beschreibung').focus(),
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Hinzufügen', wert: true, primaer: true, pruefen: () => !q('.f-beschreibung').value.trim() ? 'Die Beschreibung darf nicht leer sein.' : true }],
  });
  if (ok !== true) return;
  const par = { planblatt: q('.f-blatt').value, beschreibung: q('.f-beschreibung').value.trim() };
  if (q('.f-datum').value.trim()) par.datum = q('.f-datum').value.trim();
  const r = await sende('revision_hinzufuegen', par);
  ergebnisMelden(r, (r.hinweise ?? [])[0] ?? 'Revision hinzugefügt.');
}

registrieren({
  id: 'plan.revision', titel: 'Revision …', symbol: 'planblatt', kuerzel: null, register: 'ansicht', gruppe: 'Planblätter',
  aktiv: () => bloetter().length > 0, hinweis: 'Zuerst ein Planblatt anlegen.',
  ausfuehren: revisionHinzufuegen,
});

// Kontextmenü des Projektbrowsers (ohne Register)
registrieren({
  id: 'plan.oeffnen', titel: 'Öffnen', symbol: 'planblatt', kuerzel: null,
  aktiv: (p) => !!blattVon(p), hinweis: 'Zuerst ein Planblatt wählen.',
  ausfuehren: async (p) => { await fenster.ansichtOeffnen(blattVon(p).kennzeichen); },
});
