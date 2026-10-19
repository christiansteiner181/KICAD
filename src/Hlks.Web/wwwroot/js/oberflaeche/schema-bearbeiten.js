// Bearbeiten im Schema (Modul 3, Teil C2): die Abläufe mit Dialogen und Befehlen – Knoten verbinden, Verbindungen trennen, Elemente
// einfügen, Lage setzen und zurücksetzen, Schema neu anordnen. Die reine Logik (Parameter, Trennoptionen, Texte) steht in
// ansichten/schema-logik.js und schema-text.js; hier wird nur gefragt, gesendet und gemeldet. Keine Fachlogik: ob verbunden, getrennt
// oder angelegt werden darf, sagt der Kern (Ablehnungen kommen mit Vorschlag auf den Bildschirm).

import { zustand, element, auswaehlen } from '../zustand.js';
import { abfrage } from '../api.js';
import { sende, ergebnisMelden } from './befehle.js';
import { zeigen, meldung, kontextmenue } from './dialoge.js';
import { knoten as dom, aufElement } from './werkzeug.js';
import { hinweisSetzen } from './register.js';
import { familienLaden, familienListe } from '../familien/daten.js';
import * as L from '../ansichten/schema-logik.js';
import * as T from '../ansichten/schema-text.js';

const kzVonId = (id) => element(id)?.kennzeichen ?? null;

/** Zeigt die Ablehnung eines Befehls (Text und Vorschlag des Kerns) und gibt false zurück. */
function abgelehnt(r, titel = null) {
  meldung((titel ? `${titel}: ` : '') + (r.fehler || r.meldung || 'Der Befehl wurde abgelehnt.'), 'warnung', { vorschlag: r.vorschlag ?? undefined });
  return false;
}

/** Hinweise des Kerns zu einem angenommenen Befehl (ohne «Keine Änderung.»). */
function hinweiseZeigen(r) {
  const h = (r.hinweise ?? []).filter((x) => x && x !== 'Keine Änderung.');
  if (h.length) meldung(h.join(' '), 'info');
}

// ---------------------------------------------------------------------------------------------- Lage

/** schema_lage: Hand-Lage setzen. Gibt true zurück, wenn der Kern sie angenommen hat. */
export async function lageSetzen(ansicht, elementBezug, x, y) {
  const r = await sende('schema_lage', L.lageParameter(ansicht, elementBezug, x, y));
  if (!r.angenommen) return abgelehnt(r, 'Lage nicht gesetzt');
  return true;
}

/** schema_lage mit zuruecksetzen: «Automatische Lage». */
export async function lageZuruecksetzen(ansicht, elementBezug) {
  const r = await sende('schema_lage', L.lageZuruecksetzenParameter(ansicht, elementBezug));
  if (!r.angenommen) return abgelehnt(r, 'Automatische Lage');
  const h = (r.hinweise ?? []).filter(Boolean);
  if ((r.aenderungen ?? []).length === 0 && h.length) meldung(h.join(' '), 'info');
  else hinweisSetzen('Automatische Lage', 2500);
  return true;
}

/** Schema neu anordnen: Rückfrage («alle Hand-Lagen entfernen?»), dann schema_anordnen. daten = Antwort der Abfrage «schema». */
export async function neuAnordnen(ansicht, daten) {
  const hand = (daten?.knoten ?? []).filter((k) => k.manuell).length;
  const verwaist = (daten?.verwaisteLagen ?? []).length;
  if (hand + verwaist === 0) { meldung('Das Schema hat keine Hand-Lagen; es ist ganz automatisch angeordnet.', 'info'); return false; }
  const wahl = await zeigen({
    titel: 'Schema neu anordnen',
    inhalt: `<p class="dlg-text">${T.anordnenText(hand, verwaist).replace(/\n/g, '<br>')}</p>`,
    breite: 520,
    knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Nur Verwaiste aufräumen', wert: 'verwaist' }, { text: 'Alle Hand-Lagen entfernen', wert: 'alle', primaer: true, gefahr: true }],
  });
  if (wahl !== 'alle' && wahl !== 'verwaist') return false;
  const r = await sende('schema_anordnen', L.anordnenParameter(ansicht, wahl === 'alle'));
  if (!r.angenommen) return abgelehnt(r, 'Schema neu anordnen');
  const h = (r.hinweise ?? []).filter(Boolean);
  meldung(h.length ? h.join(' ') : 'Schema neu angeordnet.', 'ok');
  return true;
}

// ---------------------------------------------------------------------------------------------- Verbinden und Trennen

/** Auswahl der Anschlüsse; null = abgebrochen. Mit Paaren aus der Ablehnung des Kerns, sonst mit den freien Anschlüssen. */
async function anschlussWahl(a, b, fehlertext) {
  const paare = L.mehrfachKandidaten(fehlertext) ?? [];
  let frei = null;
  if (paare.length === 0) {
    const [la, lb] = await Promise.all([abfrage('element_lesen', { element: a.element }), abfrage('element_lesen', { element: b.element })]);
    frei = { a: T.freieAnschluesse(la), b: T.freieAnschluesse(lb) };
  }
  const w = dom(T.anschlussDialogHtml({ a: a.kennzeichen, b: b.kennzeichen, paare, frei }));
  const ok = await zeigen({ titel: 'Anschlüsse wählen', inhalt: w, breite: 460, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Verbinden', wert: true, primaer: true }] });
  if (ok !== true) return null;
  if (paare.length) return paare[Number(w.querySelector('input[name=sc-paar]:checked')?.value ?? 0)] ?? null;
  return [w.querySelector('.sc-a')?.value, w.querySelector('.sc-b')?.value];
}

/**
 * Verbindet zwei Knoten (Einträge der Abfrage «schema»: { element, kennzeichen }). Der Kern wählt die Anschlüsse; gibt es mehrere
 * passende Paare, fragt ein kleiner Dialog. Andere Ablehnungen (Medium, Fliessrichtung, Lage, Grösse) zeigt die Meldung mit dem
 * Vorschlag des Kerns. Gibt true zurück, wenn verbunden wurde.
 */
export async function verbinden(a, b) {
  let r = await sende('verbinden', L.verbindenParameter(a.element, b.element));
  if (!r.angenommen && L.mehrfachKandidaten(r.fehler) !== null) {
    const wahl = await anschlussWahl(a, b, r.fehler);
    if (!wahl) return false;
    r = await sende('verbinden', L.verbindenParameter(a.element, b.element, wahl[0], wahl[1]));
  }
  if (!r.angenommen) return abgelehnt(r, `${a.kennzeichen} und ${b.kennzeichen} nicht verbunden`);
  ergebnisMelden(r, `${a.kennzeichen} mit ${b.kennzeichen} verbunden.`);
  hinweiseZeigen(r);
  return true;
}

/** trennen über die Id einer Verbindung. */
export async function trennenVerbindung(verbindungId, text = '') {
  const r = await sende('trennen', L.trennenParameter(verbindungId));
  if (!r.angenommen) return abgelehnt(r, 'Nicht getrennt');
  ergebnisMelden(r, text ? `Getrennt: ${text}.` : 'Verbindung getrennt.');
  return true;
}

/**
 * Trennen auf einen Klick: t = Treffer (schema-logik.treffer) auf einer Linie oder einem Knoten, (sx, sy) = Bildschirmposition für das
 * Auswahlmenü. Eine Möglichkeit wird sofort getrennt, mehrere zeigt ein Menü.
 */
export async function trennenKlick(t, sx, sy) {
  let optionen = [];
  if (t.art === 'linie') optionen = L.trennOptionen(t.linie, zustand.verbindungen, kzVonId, t.nurGeometrie ? null : t.element);
  else if (t.art === 'knoten') {
    optionen = L.verbindungenVon(t.knoten.kennzeichen, zustand.verbindungen)
      .map((v) => ({ id: v.id, text: `${t.knoten.kennzeichen}.${v.anschluss} ↔ ${v.gegen}.${v.gegenAnschluss}` }));
  }
  if (optionen.length === 0) { meldung('Hier gibt es keine Verbindung zum Trennen (offenes Ende).', 'info'); return false; }
  if (optionen.length === 1) return trennenVerbindung(optionen[0].id, optionen[0].text);
  kontextmenue(sx, sy, optionen.map((o) => ({ titel: `Trennen: ${o.text}`, aktion: () => trennenVerbindung(o.id, o.text) })));
  return false;
}

// ---------------------------------------------------------------------------------------------- Element einfügen

let rollenFuer = null, rollenCache = null;

/** Rolle im Netz je Klasse (aus der vollen Klassendefinition; die Kurzform des Katalogs kennt sie nicht), einmal je Projekt. */
export function klassenRollen() {
  if (rollenCache && rollenFuer === zustand.geladen) return rollenCache;
  rollenFuer = zustand.geladen;
  rollenCache = (async () => {
    const m = new Map();
    const kandidaten = (zustand.katalog?.klassen ?? []).filter((k) => k.schicht === 'system' && !L.LINIENKLASSEN.includes(k.name) && (k.anschluesse ?? []).length > 0);
    await Promise.all(kandidaten.map(async (k) => {
      try { m.set(k.name, (await abfrage('katalog', { klasse: k.name })).rolle ?? null); } catch { m.set(k.name, null); }
    }));
    return m;
  })();
  return rollenCache;
}

/** Dialog «Element einfügen»: Klasse oder Familie (Typ) wählen. Gibt { art, klasse | familie+typ, knotenArt, kennzeichen, name } oder null. */
export async function einfuegenDialog() {
  const rollen = await klassenRollen();
  await familienLaden();
  const klassen = L.einfuegbareKlassen(zustand.katalog?.klassen ?? [], rollen);
  const info = new Map();
  for (const [gruppe, art] of [['endgeraete', 'endgeraet'], ['geraete', 'geraet'], ['armaturen', 'armatur'], ['formteile', 'formteil']]) for (const k of klassen[gruppe]) info.set(k.name, art);
  const familien = L.einfuegbareFamilien(familienListe(), info);
  const gruppen = T.einfuegenGruppen(klassen, familien);
  if (gruppen.length === 0) { meldung('Es gibt keine Klasse, die sich im Schema einfügen lässt.', 'info'); return null; }
  const w = dom(T.einfuegenDialogHtml(gruppen));
  const liste = w.querySelector('.sc-liste');
  let gewaehlt = null;
  liste.addEventListener('change', (e) => { if (e.target.name === 'sc-wahl') gewaehlt = e.target.value; });
  w.querySelector('.sc-suche').addEventListener('input', (e) => { liste.innerHTML = T.einfuegenListeHtml(T.einfuegenFiltern(gruppen, e.target.value), gewaehlt); });
  const ok = await zeigen({
    titel: 'Element einfügen', inhalt: w, breite: 540, klasse: 'dlg-hoch',
    knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Weiter – Zelle wählen', wert: true, primaer: true, pruefen: () => (gewaehlt ? true : 'Bitte eine Klasse oder eine Familie wählen.') }],
  });
  if (ok !== true || !gewaehlt) return null;
  const wahl = T.wahlAusWert(gewaehlt);
  if (!wahl) return null;
  const kennzeichen = w.querySelector('.sc-kz').value, name = w.querySelector('.sc-name').value;
  if (wahl.art === 'klasse') return { ...wahl, knotenArt: info.get(wahl.klasse), kennzeichen, name, titel: wahl.klasse };
  const fam = familien.find((f) => f.kennzeichen === wahl.familie);
  return { ...wahl, kategorie: fam?.kategorie, knotenArt: fam?.art, kennzeichen, name, titel: `${fam?.name ?? wahl.familie}: ${wahl.typ}`, familieObjekt: fam };
}

/**
 * Legt das gewählte Element ohne Geometrie an (geschoss aus der Zeile, system/strang aus der Spalte) und setzt danach seine Lage
 * an die Klickstelle (schema_lage). p = { x, y } in Papier-mm. Zwei Befehle, zwei Schritte im Rückgängig. Gibt das Kennzeichen zurück.
 */
export async function einfuegenAnlegen(ansicht, wahl, zelle, p) {
  const opt = { kennzeichen: wahl.kennzeichen ?? '', name: wahl.name ?? '' };
  const param = wahl.art === 'familie'
    ? L.familieAnlegenParameter({ kategorie: wahl.kategorie, kennzeichen: wahl.familie }, wahl.typ, zelle, wahl.knotenArt, opt)
    : L.klasseAnlegenParameter(wahl.klasse, zelle, wahl.knotenArt, opt);
  const r = await sende('element_anlegen', param);
  if (!r.angenommen) { abgelehnt(r, `${wahl.titel ?? 'Element'} nicht angelegt`); return null; }
  const kz = L.angelegtesElement(r, param.klasse);
  if (!kz) { meldung('Das Element ist angelegt, aber sein Kennzeichen ist unbekannt.', 'warnung'); return null; }
  const lage = await sende('schema_lage', L.lageParameter(ansicht, kz, p.x, p.y));
  if (!lage.angenommen) abgelehnt(lage, `${kz} steht an der automatischen Stelle – Lage nicht gesetzt`);
  meldung(`${kz} im Schema eingefügt (nicht platziert – im Grundriss platzieren).`, 'ok');
  hinweiseZeigen(r);
  const el = await aufElement(kz);
  if (el) auswaehlen(el.id);
  return kz;
}
