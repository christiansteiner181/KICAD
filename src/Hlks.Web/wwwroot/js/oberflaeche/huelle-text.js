// Reine Funktionen für Register «Architektur» (Decke, Boden, Dach) und Register «Hülle» (Modul 1c, Auftrag C1a): Texte, HTML und Parameter der
// Dialoge Decke erzeugen, Aufbau-Editor, Material, Fensterkennwerte, Übergang (Rsi/Rse), Hülle prüfen, Wärmebrücken und der Listen Raumgrenzen /
// Hüllbauteile. Kein Browser, keine Fachlogik: gezeigt wird, was der Kern liefert (Abfragen aufbau_rechnen, materialien, huelle_pruefen, raumgrenzen,
// huellbauteile; Trockenläufe der Befehle); gerechnet wird nichts. Zahlen werden nur gelesen und zum Anzeigen gerundet.
// Grundsatz «keine Kennwerte ohne Quelle»: jede Prüfung hier verlangt zu jedem Kennwert eine Quelle; die Oberfläche setzt nie einen Wert selbst.
// In Node getestet (huelle.test.mjs).

import { esc } from './einfuegen-text.js';

export { esc };

const leer = (x) => x === null || x === undefined || (typeof x === 'string' && x.trim() === '');
const istObjekt = (x) => x !== null && typeof x === 'object' && !Array.isArray(x);
const text = (x) => (leer(x) ? '' : String(x).trim());

export const QUELLENSATZ = 'Kennwerte nur mit Quelle (Norm mit Ausgabe, Datenblatt)';
export const UEBERGANG_SATZ = 'Ohne Rsi/Rse rechnet KICAD nur R, keinen U-Wert.';

// ---------------------------------------------------------------------------------------------- Zahlen

/** Zahl aus einem Eingabefeld («0,13» oder «0.13»); null bei leer oder ungültig. */
export function zahlLesen(t) {
  const s = text(t).replace(/\s/g, '').replace(',', '.');
  if (s === '' || !/^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/.test(s)) return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

/** Zahl zum Anzeigen: höchstens `stellen` Nachkommastellen, ohne Nullen am Ende; leer als «–». */
export function zahlAnzeige(n, stellen = 3) {
  if (n === null || n === undefined || n === '' || !Number.isFinite(Number(n))) return '–';
  const f = 10 ** stellen;
  return String(Math.round(Number(n) * f) / f);
}

/** Zahl für ein Eingabefeld (leer bleibt leer). */
export const zahlFeld = (n) => (n === null || n === undefined || n === '' || !Number.isFinite(Number(n)) ? '' : String(Math.round(Number(n) * 1e6) / 1e6));

// ---------------------------------------------------------------------------------------------- Texte des Kerns für Menschen

const KNOEPFE = {
  material_aendern: 'Material', material_anlegen: 'Material', material_uebernehmen: 'Material', aufbau_aendern: 'Aufbau', aufbau_anlegen: 'Aufbau',
  aufbau_zuweisen: 'Zuweisen', uebergang_setzen: 'Übergang', fenster_kennwerte: 'Kennwerte', decke_erzeugen: 'Decke', element_loeschen: 'Löschen',
  waermebruecken_vorschlagen: 'Wärmebrücken vorschlagen', huelle_pruefen: 'Hülle prüfen', element_aendern: 'Eigenschaften',
};

/**
 * Ein Text des Kerns für Menschen: Rat an die MCP-Werkzeuge in Klammern («(material_aendern mit werte.lambda und Quelle)») wird zu
 * «Dialog «Material …»», nackte Befehlsnamen heissen wie die Knöpfe. Der Inhalt des Satzes bleibt unverändert.
 */
export const hinweisFuerMenschen = (t) => String(t ?? '')
  .replace(/\s*\(((?:[a-z]+_)+[a-z]+)[^)]*\)/g, (m, name) => (KNOEPFE[name] ? ` (Dialog «${KNOEPFE[name]} …»)` : m))
  .replace(/\b((?:[a-z]+_)+[a-z]+)\b/g, (m, name) => (KNOEPFE[name] ? `«${KNOEPFE[name]} …»` : m))
  .trim();

/** Sätze der Liste «es fehlt: …» aus einem Hinweis des Kerns («AUF-1: kein U-Wert, es fehlt: Schicht 2: λ von … fehlt (…). Rsi/Rse … fehlen (…).»). */
export function fehltAusHinweisen(hinweise) {
  for (const h of hinweise ?? []) {
    const m = /(?:kein U-Wert|kein Uw), es fehlt:\s*([\s\S]+)$/.exec(String(h));
    if (m) return m[1].trim().split(/(?<=\.)\s+(?=Schicht\s+\d+:|Schichten\s|Rsi|Rse\b|Dicke)/).map((s) => s.trim()).filter(Boolean).map(hinweisFuerMenschen);
  }
  return [];
}

/**
 * R und U aus dem Trockenlauf von aufbau_anlegen / aufbau_aendern: der Kern nennt sie im Detail der Änderung
 * («rWert: – → 3.075 m²·K/W; uWert: – → 0.308 W/(m²·K)»). → { r?: number|null, u?: number|null } – nur, was der Lauf nennt; null = fällt weg («–»).
 */
export function rechnungAusLauf(r) {
  const aus = {};
  for (const a of r?.aenderungen ?? []) {
    if (a.klasse !== 'Aufbau' || typeof a.detail !== 'string') continue;
    for (const teil of a.detail.split(';')) {
      const m = /^\s*(rWert|uWert):\s*[\s\S]*?→\s*([\s\S]*)$/.exec(teil);
      if (!m) continue;
      const n = /^\s*(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)/.exec(m[2]);
      aus[m[1] === 'rWert' ? 'r' : 'u'] = n ? Number(n[1]) : null;
    }
  }
  return aus;
}

/** Fehlertext eines abgelehnten Befehls mit Vorschlag. */
export const ablehnung = (r) => [r?.fehler || r?.meldung || 'Abgelehnt.', r?.vorschlag ? `Vorschlag: ${r.vorschlag}` : ''].filter(Boolean).join(' – ');

/** Kasten mit Fehler oder Hinweisen eines (Trocken-)Laufs. */
export function laufHtml(r, { titel = '' } = {}) {
  if (!r) return '';
  if (!r.angenommen) return `<div class="fehlerkasten" role="alert"><b>${esc(hinweisFuerMenschen(r.fehler || r.meldung || 'Abgelehnt.'))}</b>${r.vorschlag ? `<div>Vorschlag: ${esc(hinweisFuerMenschen(r.vorschlag))}</div>` : ''}</div>`;
  const h = (r.hinweise ?? []).filter(Boolean);
  return `${titel ? `<h4>${esc(titel)}</h4>` : ''}${h.length ? `<ul class="hinweisliste">${h.map((t) => `<li>${esc(hinweisFuerMenschen(t))}</li>`).join('')}</ul>` : ''}`;
}

// ---------------------------------------------------------------------------------------------- Decke, Boden, Dach (decke_erzeugen)

export const DECKE_ARTEN = {
  decke: { titel: 'Decke', symbol: 'decke', id: 'huelle.decke', erklaerung: 'Decke über dem Geschoss (Oberkante = Kote des Geschosses darüber). Sie gehört zum Geschoss darüber.' },
  boden: { titel: 'Boden', symbol: 'boden', id: 'huelle.boden', erklaerung: 'Boden bzw. Bodenplatte des Geschosses (Oberkante = Kote des Geschosses).' },
  dach: { titel: 'Dach', symbol: 'dach', id: 'huelle.dach', erklaerung: 'Flaches Dach über dem Geschoss (Oberkante = Kote + Geschosshöhe).' },
};

/** Geschosse für die Auswahl: von oben nach unten (Kote), mit Name. */
export function geschossOptionen(geschosse) {
  return [...(geschosse ?? [])].sort((a, b) => (b.werte?.kote ?? 0) - (a.werte?.kote ?? 0))
    .map((g) => ({ wert: g.kennzeichen, text: g.name ? `${g.kennzeichen} – ${g.name}` : g.kennzeichen }));
}

/** Vorgabe des Geschosses: das der aktiven Ansicht, sonst das unterste mit Wänden, sonst das erste. */
export function geschossVorgabe(geschosse, aktiv = null) {
  const kz = (geschosse ?? []).map((g) => g.kennzeichen);
  if (aktiv && kz.includes(aktiv)) return aktiv;
  const sortiert = [...(geschosse ?? [])].sort((a, b) => (a.werte?.kote ?? 0) - (b.werte?.kote ?? 0));
  return sortiert.find((g) => g.werte?.kote === 0)?.kennzeichen ?? sortiert[0]?.kennzeichen ?? '';
}

/** Prüft das Formular von «Decke erzeugen»; die Dicke ist Pflicht (der Kern setzt keine Vorgabe). → true | Text */
export function deckePruefen({ geschoss, art, dicke }) {
  if (leer(geschoss)) return 'Bitte ein Geschoss wählen.';
  if (!DECKE_ARTEN[art]) return 'Bitte Decke, Boden oder Dach wählen.';
  const d = zahlLesen(dicke);
  if (d === null) return 'Bitte die Dicke in mm angeben (aus dem Bauteilaufbau bzw. den Plänen des Architekten – es gibt keine Vorgabe).';
  if (d <= 0) return 'Die Dicke muss grösser als 0 sein.';
  return true;
}

/** Parameter von decke_erzeugen; der Aufbau nur, wenn gewählt. */
export function deckeParameter({ geschoss, art, dicke, aufbau = '' }) {
  const p = { geschoss: text(geschoss), art, dicke: zahlLesen(dicke) };
  if (!leer(aufbau)) p.aufbau = text(aufbau);
  return p;
}

/** Aufbauten, die zur Art der Decke passen: decke → decke, boden → boden, dach → dach (der Kern übernimmt sonst mit Hinweis). */
export function aufbautenFuerDecke(aufbauten, art) {
  return [...(aufbauten ?? [])].filter((a) => a.werte?.art === art).sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }));
}

const FLAECHE_ZEILE = /Fläche\s+[\d.']+\s*m²/;

/** Ergebnis von decke_erzeugen (Trockenlauf oder echt): Kopf mit Fläche und Höhe der neuen Decken, dazu alle Hinweise des Kerns. */
export function deckeErgebnisHtml(r) {
  if (!r) return '';
  if (!r.angenommen) return laufHtml(r);
  const hinweise = (r.hinweise ?? []).filter(Boolean);
  const neu = hinweise.filter((h) => FLAECHE_ZEILE.test(h));
  const rest = hinweise.filter((h) => !FLAECHE_ZEILE.test(h));
  const kopf = r.trockenlauf ? 'Vorschau (Trockenlauf – nichts wurde geändert)' : 'Erzeugt';
  return `<h4>${esc(kopf)}</h4>`
    + (neu.length ? `<ul class="hinweisliste huelle-neu">${neu.map((t) => `<li><b>${esc(t)}</b></li>`).join('')}</ul>` : '<p class="leise">Keine neue Fläche.</p>')
    + (rest.length ? `<h4>Hinweise des Kerns</h4><ul class="hinweisliste">${rest.map((t) => `<li>${esc(hinweisFuerMenschen(t))}</li>`).join('')}</ul>` : '');
}

// ---------------------------------------------------------------------------------------------- Material

/** Kennwerte eines Materials in der Reihenfolge des Kerns (MaterialBibliothek.Kennwerte); Einheiten wie im Katalog (klassen.json). */
export const KENNWERTE = [
  { name: 'lambda', label: 'λ Wärmeleitfähigkeit', kurz: 'λ', einheit: 'W/(m·K)' },
  { name: 'rohdichte', label: 'Rohdichte', kurz: 'ρ', einheit: 'kg/m³' },
  { name: 'waermekapazitaet', label: 'Wärmekapazität', kurz: 'c', einheit: 'J/(kg·K)' },
  { name: 'emission', label: 'Emission (0 … 1)', kurz: 'ε', einheit: '' },
  { name: 'absorption', label: 'Absorption (0 … 1)', kurz: 'α', einheit: '' },
  { name: 'mu', label: 'μ Diffusionswiderstand', kurz: 'μ', einheit: '' },
];

/** Leeres Formular. */
export const materialLeer = () => ({ kennzeichen: null, name: '', kategorie: '', werte: Object.fromEntries(KENNWERTE.map((k) => [k.name, { wert: '', quelle: '' }])) });

/** Formular aus einem Eintrag der Abfrage materialien (projekt[] oder bibliothek.eintraege[]). */
export function materialFormular(m) {
  const f = materialLeer();
  if (!m) return f;
  f.kennzeichen = m.kennzeichen ?? null;
  f.name = m.name ?? '';
  f.kategorie = m.kategorie ?? '';
  for (const k of KENNWERTE) {
    const w = m.werte?.[k.name];
    if (w) f.werte[k.name] = { wert: zahlFeld(istObjekt(w) ? w.wert : w), quelle: text(w.quelle) };
  }
  return f;
}

/** Ausgangswerte (Stand beim Öffnen) zum Vergleichen: was hat sich geändert? */
const gleich = (a, b) => text(a) === text(b);
const wertGleich = (a, b) => (zahlLesen(a) === null && zahlLesen(b) === null ? gleich(a, b) : zahlLesen(a) === zahlLesen(b));

/**
 * Prüft das Material-Formular. → { fehler: [Text], warnungen: [Text] }.
 * Fehler: Name fehlt, Wert keine Zahl oder ausserhalb 0 … 1 (Emission, Absorption), Wert ohne Quelle (Pflicht je Kennwert), Quelle ohne Wert.
 * Warnung: Wert geändert, Quelle aber dieselbe wie vorher (gilt sie auch für den neuen Wert?).
 */
export function materialPruefen(f, bisher = null) {
  const fehler = [], warnungen = [];
  if (leer(f.name)) fehler.push('Bitte einen Namen angeben.');
  for (const k of KENNWERTE) {
    const w = f.werte?.[k.name] ?? { wert: '', quelle: '' };
    const hatWert = !leer(w.wert), hatQuelle = !leer(w.quelle);
    if (!hatWert && hatQuelle) { fehler.push(`${k.kurz}: Quelle angegeben, aber kein Wert.`); continue; }
    if (!hatWert) continue;
    const n = zahlLesen(w.wert);
    if (n === null) { fehler.push(`${k.kurz}: «${w.wert}» ist keine Zahl.`); continue; }
    if (n <= 0 && !['emission', 'absorption'].includes(k.name)) fehler.push(`${k.kurz}: muss grösser als 0 sein.`);
    if (['emission', 'absorption'].includes(k.name) && (n < 0 || n > 1)) fehler.push(`${k.kurz}: muss zwischen 0 und 1 liegen.`);
    if (!hatQuelle) fehler.push(`${k.kurz}: Quelle fehlt. ${QUELLENSATZ}.`);
    else if (bisher?.werte?.[k.name] && !wertGleich(w.wert, bisher.werte[k.name].wert) && gleich(w.quelle, bisher.werte[k.name].quelle))
      warnungen.push(`${k.kurz}: Wert geändert, Quelle unverändert – gilt sie auch für den neuen Wert?`);
  }
  return { fehler, warnungen };
}

/** Parameter von material_anlegen: Kennwerte nur mit Wert und Quelle. */
export function materialAnlegenParameter(f) {
  const p = { name: text(f.name) };
  if (!leer(f.kategorie)) p.kategorie = text(f.kategorie);
  const werte = {};
  for (const k of KENNWERTE) { const w = f.werte[k.name]; if (!leer(w.wert)) werte[k.name] = { wert: zahlLesen(w.wert), quelle: text(w.quelle) }; }
  if (Object.keys(werte).length) p.werte = werte;
  return p;
}

/** Parameter von material_aendern: nur, was sich gegenüber `bisher` geändert hat; ein geleertes Feld entfernt den Kennwert (null). → Parameter | null (nichts zu tun) */
export function materialAendernParameter(f, bisher) {
  const p = { material: bisher.kennzeichen };
  if (text(f.name) !== text(bisher.name)) p.name = text(f.name);
  if (text(f.kategorie) !== text(bisher.kategorie)) p.kategorie = text(f.kategorie);
  const werte = {};
  for (const k of KENNWERTE) {
    const w = f.werte[k.name], alt = bisher.werte?.[k.name] ?? { wert: '', quelle: '' };
    if (leer(w.wert)) { if (!leer(alt.wert)) werte[k.name] = null; continue; }
    if (!wertGleich(w.wert, alt.wert) || !gleich(w.quelle, alt.quelle)) werte[k.name] = { wert: zahlLesen(w.wert), quelle: text(w.quelle) };
  }
  if (Object.keys(werte).length) p.werte = werte;
  return Object.keys(p).length > 1 ? p : null;
}

/** Wert mit Einheit für Listen. */
const kennwertText = (k, w) => (w ? `${zahlAnzeige(w.wert ?? w, 4)}${k.einheit ? ' ' + k.einheit : ''}` : '');

/** HTML-Liste der Projektmaterialien (Abfrage materialien): Klick wählt eine Zeile; λ fehlt oder ohne Quelle in Rot. */
export function materialListeHtml(daten, gewaehlt = null) {
  const liste = daten?.projekt ?? [];
  if (liste.length === 0) return '<p class="leise">Noch kein Material im Projekt. Rechts eines anlegen oder aus der Bürobibliothek übernehmen.</p>';
  return `<table class="tab klickbar huelle-liste"><thead><tr><th>Material</th><th>Kategorie</th><th class="zahl">λ</th><th>Verwendet in</th></tr></thead><tbody>${liste.map((m) => {
    const fehlt = (m.fehlt ?? []).length > 0;
    const lam = m.werte?.lambda;
    return `<tr data-kz="${esc(m.kennzeichen)}"${m.kennzeichen === gewaehlt ? ' class="gewaehlt"' : ''}><td><b>${esc(m.kennzeichen)}</b> ${esc(m.name ?? '')}</td><td>${esc(m.kategorie ?? '')}</td>`
      + `<td class="zahl${fehlt ? ' rot' : ''}" title="${esc(fehlt ? (m.fehlt ?? []).join(', ') + ' fehlt' : text(lam?.quelle))}">${fehlt ? esc('λ fehlt' + ((m.fehlt ?? []).some((x) => /Quelle/.test(x)) ? ' (Quelle)' : '')) : esc(zahlAnzeige(lam?.wert, 4))}</td>`
      + `<td class="leise">${esc((m.verwendetIn ?? []).join(', '))}</td></tr>`;
  }).join('')}</tbody></table>`;
}

/** HTML der Bürobibliothek: Einträge mit «übernehmen»; leer: der Hinweis des Kerns. */
export function bibliothekHtml(bib) {
  if (!bib) return '';
  if (bib.fehler) return `<div class="fehlerkasten"><b>${esc(bib.fehler)}</b>${bib.vorschlag ? `<div>Vorschlag: ${esc(bib.vorschlag)}</div>` : ''}</div>`;
  const e = bib.eintraege ?? [];
  if (e.length === 0) return `<p class="leise">${esc(bib.hinweis ?? 'Die Bürobibliothek ist leer.')}</p>`;
  return `<table class="tab huelle-bib"><thead><tr><th>Material</th><th>Kategorie</th><th>Kennwerte (mit Quelle)</th><th></th></tr></thead><tbody>${e.map((x) =>
    `<tr><td>${esc(x.name)}</td><td>${esc(x.kategorie ?? '')}</td><td>${esc(KENNWERTE.filter((k) => x.werte?.[k.name]).map((k) => `${k.kurz} ${zahlAnzeige(x.werte[k.name].wert, 4)}`).join(' · '))}</td>`
    + `<td><button type="button" class="dlg-knopf klein b-uebernehmen" data-name="${esc(x.name)}">übernehmen</button></td></tr>`).join('')}</tbody></table>`;
}

// ---------------------------------------------------------------------------------------------- Aufbau

export const AUFBAU_ARTEN = [['wand', 'Wand'], ['decke', 'Decke'], ['dach', 'Dach'], ['boden', 'Boden'], ['tuer', 'Tür']];
export const WAERMESTROM = [['', 'aus der Art (Wand und Tür horizontal, Decke und Dach aufwärts, Boden abwärts)'], ['horizontal', 'horizontal'], ['aufwaerts', 'aufwärts'], ['abwaerts', 'abwärts']];
/** Klassen, die einen Aufbau bekommen (die Art-Prüfung macht der Kern). */
export const AUFBAU_KLASSEN = ['Wand', 'Decke', 'Dach', 'Tuer'];

/** Leeres Formular des Aufbau-Editors. */
export const aufbauLeer = () => ({ kennzeichen: null, name: '', art: 'wand', waermestrom: '', schichten: [{ material: '', dicke: '' }], uWert: '', uQuelle: '', quelle: '' });

/**
 * Formular aus einem Aufbau-Element der Darstellung und (falls vorhanden) dem Ergebnis von aufbau_rechnen.
 * materialKz(id) → Kennzeichen eines Materials aus seiner Id. Ein ausdrücklicher U-Wert (aufbau_rechnen: uAusdruecklich) kommt mit Quelle ins Formular.
 */
export function aufbauFormular(a, rechnung = null, materialKz = (id) => id) {
  const f = aufbauLeer();
  if (!a) return f;
  f.kennzeichen = a.kennzeichen;
  f.name = a.name ?? '';
  f.art = a.werte?.art ?? 'wand';
  f.waermestrom = a.werte?.waermestrom ?? '';
  const s = Array.isArray(a.werte?.schichten) ? a.werte.schichten : [];
  f.schichten = s.length ? s.map((x) => ({ material: materialKz(x.material) ?? '', dicke: zahlFeld(x.dicke) })) : [{ material: '', dicke: '' }];
  if (rechnung?.uAusdruecklich) { f.uWert = zahlFeld(rechnung.u); f.uQuelle = text(rechnung.uQuelle); }
  return f;
}

/** Sind die Schichten zweier Formulare gleich (Material und Dicke in dieser Reihenfolge)? */
export function schichtenGleich(a, b) {
  const x = (a ?? []).filter((s) => !leer(s.material) || !leer(s.dicke)), y = (b ?? []).filter((s) => !leer(s.material) || !leer(s.dicke));
  return x.length === y.length && x.every((s, i) => text(s.material) === text(y[i].material) && zahlLesen(s.dicke) === zahlLesen(y[i].dicke));
}

/** Hat sich das Formular gegenüber dem gespeicherten Stand geändert? */
export function aufbauGeaendert(f, bisher) {
  if (!bisher) return true;
  return text(f.name) !== text(bisher.name) || f.art !== bisher.art || text(f.waermestrom) !== text(bisher.waermestrom)
    || !schichtenGleich(f.schichten, bisher.schichten) || !wertGleich(f.uWert, bisher.uWert) || (!leer(f.uWert) && !gleich(f.uQuelle, bisher.uQuelle));
}

/** Prüft das Formular. → { fehler: [Text] } – ohne Fehler lässt sich speichern bzw. rechnen. */
export function aufbauPruefen(f) {
  const fehler = [];
  if (leer(f.name)) fehler.push('Bitte einen Namen angeben.');
  const s = (f.schichten ?? []).filter((x) => !leer(x.material) || !leer(x.dicke));
  if (s.length === 0) fehler.push('Mindestens eine Schicht mit Material und Dicke angeben.');
  s.forEach((x, i) => {
    if (leer(x.material)) fehler.push(`Schicht ${i + 1}: Material wählen.`);
    const d = zahlLesen(x.dicke);
    if (d === null) fehler.push(`Schicht ${i + 1}: Dicke in mm angeben.`);
    else if (d <= 0) fehler.push(`Schicht ${i + 1}: Dicke muss grösser als 0 sein.`);
  });
  if (!leer(f.uWert)) {
    const u = zahlLesen(f.uWert);
    if (u === null || u <= 0) fehler.push('Ausdrücklicher U-Wert: eine Zahl grösser als 0 angeben.');
    if (leer(f.uQuelle)) fehler.push(`Ausdrücklicher U-Wert: Quelle fehlt. ${QUELLENSATZ}.`);
  } else if (!leer(f.uQuelle)) fehler.push('Quelle zum U-Wert angegeben, aber kein U-Wert.');
  return { fehler };
}

const schichtParameter = (f) => (f.schichten ?? []).filter((x) => !leer(x.material) || !leer(x.dicke)).map((x) => ({ material: text(x.material), dicke: zahlLesen(x.dicke) }));

/** Parameter von aufbau_anlegen. */
export function aufbauAnlegenParameter(f) {
  const p = { name: text(f.name), art: f.art, schichten: schichtParameter(f) };
  if (!leer(f.waermestrom)) p.waermestrom = f.waermestrom;
  if (!leer(f.uWert)) p.uWert = { wert: zahlLesen(f.uWert), quelle: text(f.uQuelle) };
  if (!leer(f.quelle)) p.quelle = text(f.quelle);
  return p;
}

/** Parameter von aufbau_aendern: nur, was sich geändert hat (die Schichten immer als Ganzes). → Parameter | null (nichts zu tun) */
export function aufbauAendernParameter(f, bisher) {
  const p = { aufbau: bisher.kennzeichen };
  if (text(f.name) !== text(bisher.name)) p.name = text(f.name);
  if (f.art !== bisher.art) p.art = f.art;
  if (text(f.waermestrom) !== text(bisher.waermestrom)) p.waermestrom = leer(f.waermestrom) ? 'auto' : f.waermestrom;
  if (!schichtenGleich(f.schichten, bisher.schichten)) { p.schichten = schichtParameter(f); if (!leer(f.quelle)) p.quelle = text(f.quelle); }
  if (!leer(f.uWert) && (!wertGleich(f.uWert, bisher.uWert) || !gleich(f.uQuelle, bisher.uQuelle))) p.uWert = { wert: zahlLesen(f.uWert), quelle: text(f.uQuelle) };
  return Object.keys(p).length > 1 ? p : null;
}

/** Muss ein vorhandener ausdrücklicher U-Wert entfernt werden (Feld geleert)? Der Kern kennt dafür element_aendern { uWert: null }. */
export const uWertEntfernen = (f, bisher) => !!bisher && !leer(bisher.uWert) && leer(f.uWert);

/**
 * Schichtzeilen mit λ und R aus dem Kern. materialien = Abfrage materialien (projekt[]), gespeichert = Ergebnis von aufbau_rechnen
 * (nur, wenn die Zeile unverändert ist: gleiches Material und gleiche Dicke an gleicher Stelle, sonst bleibt R leer – der Kern rechnet es nach dem Speichern).
 * → [{ nr, material, lambda, lambdaQuelle, lambdaFehlt, r }] ; lambdaFehlt = Text des Kerns («λ fehlt», «Quelle von λ fehlt») oder ''.
 */
export function schichtInfo(f, materialien, gespeichert = null) {
  const proKz = new Map((materialien?.projekt ?? []).map((m) => [m.kennzeichen, m]));
  const zeilen = (f.schichten ?? []).filter((x) => !leer(x.material) || !leer(x.dicke));
  return zeilen.map((s, i) => {
    const m = proKz.get(text(s.material));
    const l = m?.werte?.lambda;
    const fehlt = m ? (m.fehlt ?? []) : [];
    const alt = gespeichert?.schichten?.[i];
    const gleichGeblieben = alt && alt.material === text(s.material) && Number(alt.dicke_mm) === zahlLesen(s.dicke);
    return {
      nr: i + 1, material: text(s.material), name: m?.name ?? '',
      lambda: fehlt.length ? null : (l?.wert ?? null), lambdaQuelle: l?.quelle ?? null,
      lambdaFehlt: !m ? '' : fehlt.includes('lambda') ? 'λ fehlt' : fehlt.some((x) => /Quelle/.test(x)) ? 'Quelle von λ fehlt' : '',
      r: gleichGeblieben ? (alt.r ?? null) : null,
    };
  });
}

/**
 * Summen R und U zum Anzeigen. lauf = Trockenlauf (aufbau_anlegen / aufbau_aendern), gespeichert = aufbau_rechnen des gespeicherten Stands.
 * Was der Trockenlauf nennt (Detail «rWert: … → …»), gilt; sonst der gespeicherte Wert; ohne beides fehlt der Wert.
 * → { r, u, fehlt: [Sätze], quelle: Text|null, ausdruecklich: bool, rsi, rse }
 */
export function summen(lauf, gespeichert) {
  const l = rechnungAusLauf(lauf);
  const hatLauf = lauf && lauf.angenommen;
  const pick = (k) => (k in l ? l[k] : gespeichert ? (k === 'r' ? gespeichert.r : gespeichert.u) ?? null : null);
  const fehltLauf = hatLauf ? fehltAusHinweisen(lauf.hinweise) : [];
  const fehlt = fehltLauf.length ? fehltLauf : (!hatLauf || !('u' in l)) && gespeichert ? (gespeichert.fehlt ?? []).map(hinweisFuerMenschen) : [];
  return {
    r: pick('r'), u: pick('u'), fehlt,
    quelle: gespeichert?.uQuelle ?? null, ausdruecklich: !!gespeichert?.uAusdruecklich,
    rsi: gespeichert?.rsi ?? null, rse: gespeichert?.rse ?? null,
  };
}

/** Kopfzeile der Rechnung: R und U gross, fehlende Werte rot mit dem Text des Kerns. */
export function summenHtml(s) {
  const wert = (n, einheit, name) => (n === null || n === undefined ? `<span class="huelle-wert rot">fehlt</span>` : `<span class="huelle-wert"><b>${esc(zahlAnzeige(n))}</b> <span class="leise">${esc(einheit)}</span></span>`);
  return `<div class="huelle-summen"><span class="leise">R</span> ${wert(s.r, 'm²·K/W', 'R')} <span class="leise">U</span> ${wert(s.u, 'W/(m²·K)', 'U')}${s.ausdruecklich ? ' <span class="leise">(ausdrücklich, geht vor)</span>' : ''}</div>`
    + (s.fehlt.length ? `<ul class="huelle-fehlt rot">${s.fehlt.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : '');
}

/** Eine Zeile der Schichtentabelle (Eingabefelder). materialOptionen = [{ wert, text }]. */
export function schichtZeileHtml(i, s, info, materialOptionen, anzahl) {
  const opt = [{ wert: '', text: '– Material wählen –' }, ...materialOptionen];
  if (!leer(s.material) && !materialOptionen.some((o) => o.wert === s.material)) opt.push({ wert: s.material, text: `${s.material} (nicht gefunden)` });
  return `<tr data-i="${i}"><td class="zahl leise">${i + 1}</td>`
    + `<td><select class="s-material" aria-label="Material Schicht ${i + 1}">${opt.map((o) => `<option value="${esc(o.wert)}"${o.wert === s.material ? ' selected' : ''}>${esc(o.text)}</option>`).join('')}</select></td>`
    + `<td><input type="text" class="s-dicke zahl" inputmode="decimal" value="${esc(s.dicke)}" aria-label="Dicke Schicht ${i + 1} in mm" data-kein-enter="1"></td>`
    + `<td class="zahl s-lambda">${schichtLambdaHtml(info)}</td><td class="zahl s-r">${info?.r === null || info?.r === undefined ? '<span class="leise">·</span>' : esc(zahlAnzeige(info.r))}</td>`
    + `<td class="s-knoepfe"><button type="button" class="chip s-hoch" title="nach innen"${i === 0 ? ' disabled' : ''}>↑</button><button type="button" class="chip s-runter" title="nach aussen"${i === anzahl - 1 ? ' disabled' : ''}>↓</button>`
    + `<button type="button" class="chip s-davor" title="Schicht davor einfügen">＋</button><button type="button" class="chip s-weg" title="Schicht löschen">✕</button></td></tr>`;
}

/** Inhalt der λ-Zelle. */
export function schichtLambdaHtml(info) {
  if (!info) return '';
  if (info.lambdaFehlt) return `<span class="rot" title="${esc(info.lambdaFehlt)}">${esc(info.lambdaFehlt)}</span>`;
  if (info.lambda === null || info.lambda === undefined) return '<span class="leise">·</span>';
  return `<span title="${esc(info.lambdaQuelle ?? '')}">${esc(zahlAnzeige(info.lambda, 4))}</span>`;
}

/** Verschiebt/entfernt/fügt Schichten ein (Reihenfolge innen → aussen); gibt eine neue Liste zurück. */
export function schichtenAendern(liste, aktion, i) {
  const l = liste.map((s) => ({ ...s }));
  if (aktion === 'weg') { l.splice(i, 1); if (l.length === 0) l.push({ material: '', dicke: '' }); }
  else if (aktion === 'davor') l.splice(i, 0, { material: '', dicke: '' });
  else if (aktion === 'hoch' && i > 0) [l[i - 1], l[i]] = [l[i], l[i - 1]];
  else if (aktion === 'runter' && i < l.length - 1) [l[i + 1], l[i]] = [l[i], l[i + 1]];
  else if (aktion === 'ende') l.push({ material: '', dicke: '' });
  return l;
}

/** Materialoptionen für die Auswahl. */
export function materialOptionen(materialien) {
  return (materialien?.projekt ?? []).map((m) => ({ wert: m.kennzeichen, text: m.name ? `${m.kennzeichen} – ${m.name}` : m.kennzeichen }));
}

// ---------------------------------------------------------------------------------------------- Zuweisen (aufbau_zuweisen)

/** Bauteile, die einen Aufbau bekommen können, als Zeilen für den Dialog; ausgewählte zuerst nicht – Sortierung nach Geschoss, Klasse, Kennzeichen. */
export function zuweisenZeilen(elemente, auswahlKz = [], suche = '') {
  const s = text(suche).toLowerCase();
  const gewaehlt = new Set(auswahlKz.map((x) => String(x).toLowerCase()));
  return (elemente ?? []).filter((e) => AUFBAU_KLASSEN.includes(e.klasse))
    .map((e) => ({ kennzeichen: e.kennzeichen, klasse: e.klasse, geschoss: e.geschoss ?? '', aufbau: e.bezuege?.aufbau ?? '', gewaehlt: gewaehlt.has(String(e.kennzeichen).toLowerCase()) }))
    .filter((z) => s === '' || `${z.kennzeichen} ${z.klasse} ${z.geschoss} ${z.aufbau}`.toLowerCase().includes(s))
    .sort((a, b) => String(a.geschoss).localeCompare(String(b.geschoss), 'de') || a.klasse.localeCompare(b.klasse) || a.kennzeichen.localeCompare(b.kennzeichen, 'de', { numeric: true }));
}

export function zuweisenTabelleHtml(zeilen) {
  if (zeilen.length === 0) return '<p class="leise">Keine Wände, Decken, Dächer oder Türen im Modell (oder keine Treffer).</p>';
  return `<table class="tab klickbar huelle-zuweisen"><thead><tr><th></th><th>Bauteil</th><th>Klasse</th><th>Geschoss</th><th>Aufbau jetzt</th></tr></thead><tbody>${zeilen.map((z) =>
    `<tr data-kz="${esc(z.kennzeichen)}"><td><input type="checkbox" class="z-wahl" value="${esc(z.kennzeichen)}"${z.gewaehlt ? ' checked' : ''} aria-label="${esc(z.kennzeichen)}"></td><td>${esc(z.kennzeichen)}</td><td>${esc(z.klasse)}</td><td>${esc(z.geschoss)}</td><td class="leise">${esc(z.aufbau || '–')}</td></tr>`).join('')}</tbody></table>`;
}

export function zuweisenPruefen(kennzeichen, aufbau) {
  if (leer(aufbau)) return 'Bitte einen Aufbau wählen.';
  if (!kennzeichen?.length) return 'Bitte mindestens ein Bauteil wählen (Häkchen). Die Art des Aufbaus prüft der Kern.';
  return true;
}

export const zuweisenParameter = (kennzeichen, aufbau) => ({ elemente: [...kennzeichen], aufbau: text(aufbau) });

// ---------------------------------------------------------------------------------------------- Fenster und Türen (fenster_kennwerte)

/** Felder des Dialogs: Name im Kern, Beschriftung, Einheit; `fuer` = Fenster | Tuer. */
export const FENSTER_FELDER = [
  { name: 'ug', label: 'Ug Verglasung', einheit: 'W/(m²·K)', fuer: 'Fenster' },
  { name: 'uf', label: 'Uf Rahmen', einheit: 'W/(m²·K)', fuer: 'Fenster' },
  { name: 'psiG', label: 'ψg Glasrand', einheit: 'W/(m·K)', fuer: 'Fenster' },
  { name: 'g', label: 'g Gesamtenergiedurchlass (0 … 1)', einheit: '', fuer: 'Fenster' },
  { name: 'rahmenanteil', label: 'Rahmenanteil (0 … 1)', einheit: '', fuer: 'Fenster' },
  { name: 'fc', label: 'fc Sonnenschutz (0 … 1)', einheit: '', fuer: 'Fenster' },
  { name: 'uw', label: 'Uw ausdrücklich', einheit: 'W/(m²·K)', fuer: 'Fenster', hilfe: 'Ausdrücklicher Wert (Datenblatt, IFC) geht vor der Rechnung aus Ug, Uf, ψg und Rahmenanteil.' },
  { name: 'ud', label: 'Ud Tür', einheit: 'W/(m²·K)', fuer: 'Tuer', hilfe: 'Ohne Ud gilt der U-Wert des Aufbaus.' },
];
export const SONNENSCHUTZ = [['', '(unverändert)'], ['keiner', 'keiner'], ['aussen', 'aussen'], ['innen', 'innen'], ['zwischen', 'zwischen']];
export const UW_TIPP = 'Uw gerechnet aus Ug, Uf, ψg, Rahmenanteil und Mass – Annahme des Kerns: Verglasung dem Fenster ähnlich (Prüfliste). Ein ausdrückliches Uw geht vor.';

/** Welche Klassen enthält die Auswahl? → { Fenster: [e], Tuer: [e], andere: [e] } */
export function fensterAuswahl(elemente) {
  const aus = { Fenster: [], Tuer: [], andere: [] };
  for (const e of elemente ?? []) (aus[e.klasse] ?? aus.andere).push(e);
  return aus;
}

/** Prüft das Formular {werte: {name: text}, sonnenschutz, quelle}. Jeder Zahlenwert braucht die Quelle (Pflicht). → { fehler } */
export function fensterPruefen(f, klasse = 'Fenster') {
  const fehler = [];
  const felder = FENSTER_FELDER.filter((x) => x.fuer === klasse);
  let zahlen = 0;
  for (const x of felder) {
    const t = f.werte?.[x.name];
    if (leer(t)) continue;
    zahlen++;
    const n = zahlLesen(t);
    if (n === null) { fehler.push(`${x.label}: «${t}» ist keine Zahl.`); continue; }
    if (['g', 'rahmenanteil', 'fc'].includes(x.name) ? n < 0 || n > 1 : n < 0) fehler.push(`${x.label}: ${['g', 'rahmenanteil', 'fc'].includes(x.name) ? 'muss zwischen 0 und 1 liegen' : 'darf nicht negativ sein'}.`);
  }
  if (zahlen > 0 && leer(f.quelle)) fehler.push(`Quelle fehlt. ${QUELLENSATZ}.`);
  if (zahlen === 0 && leer(f.sonnenschutz)) fehler.push('Mindestens einen Kennwert oder den Sonnenschutz angeben.');
  return { fehler };
}

/** Parameter von fenster_kennwerte: nur ausgefüllte Felder; Quelle nur mit Zahlenwerten. */
export function fensterParameter(kennzeichen, f, klasse = 'Fenster') {
  const p = { elemente: [...kennzeichen] };
  let zahlen = 0;
  for (const x of FENSTER_FELDER.filter((y) => y.fuer === klasse)) {
    const n = zahlLesen(f.werte?.[x.name]);
    if (n !== null) { p[x.name] = n; zahlen++; }
  }
  if (klasse === 'Fenster' && !leer(f.sonnenschutz)) p.sonnenschutz = f.sonnenschutz;
  if (zahlen > 0) p.quelle = text(f.quelle);
  return p;
}

/** Änderungen eines (Trocken-)Laufs mit Detail als Tabelle («F-1 – uw: – → 1.3 W/(m²·K)»); Zeilen ohne Detail entfallen. */
export function aenderungenDetailHtml(aenderungen, max = 40) {
  const z = (aenderungen ?? []).filter((a) => !leer(a.detail));
  if (z.length === 0) return '';
  return `<table class="tab kompakt huelle-details"><tbody>${z.slice(0, max).map((a) => `<tr><td>${esc(a.kennzeichen)}</td><td>${esc(a.detail)}</td></tr>`).join('')}</tbody></table>${z.length > max ? `<p class="leise">… und ${z.length - max} weitere</p>` : ''}`;
}

/** Uw (und Ud) der Auswahl aus der Darstellung: «F-1: Uw 1.3 W/(m²·K)». */
export function uwZeilen(elemente) {
  return (elemente ?? []).map((e) => {
    const n = e.klasse === 'Tuer' ? e.werte?.ud : e.werte?.uw;
    const name = e.klasse === 'Tuer' ? 'Ud' : 'Uw';
    return { kennzeichen: e.kennzeichen, text: n === null || n === undefined ? `${name} fehlt` : `${name} ${zahlAnzeige(n)} W/(m²·K)`, fehlt: n === null || n === undefined };
  });
}

// ---------------------------------------------------------------------------------------------- Übergang (uebergang_setzen)

export const UEBERGANG_GRUPPEN = [
  { name: 'horizontal', titel: 'Wärmestrom horizontal', fuer: 'Wände, Türen' },
  { name: 'aufwaerts', titel: 'Wärmestrom aufwärts', fuer: 'Decken, Dächer' },
  { name: 'abwaerts', titel: 'Wärmestrom abwärts', fuer: 'Böden' },
  { name: 'erdreich', titel: 'Gegen Erdreich', fuer: 'erdberührte Bauteile' },
];

/** Formular aus der Projektinformation (werte.uebergang). */
export function uebergangFormular(info) {
  const u = istObjekt(info?.werte?.uebergang) ? info.werte.uebergang : {};
  return Object.fromEntries(UEBERGANG_GRUPPEN.map((g) => {
    const x = istObjekt(u[g.name]) ? u[g.name] : {};
    return [g.name, { rsi: zahlFeld(x.rsi), rse: zahlFeld(x.rse), quelle: text(x.quelle) }];
  }));
}

/** Prüft das Formular: je Gruppe beide Werte oder keiner, mit Quelle (Pflicht je Gruppe). → { fehler, geaendert: [Gruppen] } */
export function uebergangPruefen(f, bisher = null) {
  const fehler = [], geaendert = [];
  for (const g of UEBERGANG_GRUPPEN) {
    const x = f[g.name] ?? { rsi: '', rse: '', quelle: '' }, alt = bisher?.[g.name] ?? { rsi: '', rse: '', quelle: '' };
    const leerG = leer(x.rsi) && leer(x.rse) && leer(x.quelle);
    if (leerG) continue;
    if (leer(x.rsi) || leer(x.rse)) { fehler.push(`${g.titel}: Rsi und Rse angeben.`); continue; }
    const rsi = zahlLesen(x.rsi), rse = zahlLesen(x.rse);
    if (rsi === null || rse === null) { fehler.push(`${g.titel}: Rsi und Rse müssen Zahlen sein.`); continue; }
    if (rsi < 0 || rse < 0) { fehler.push(`${g.titel}: Rsi und Rse dürfen nicht negativ sein.`); continue; }
    if (leer(x.quelle)) { fehler.push(`${g.titel}: Quelle fehlt. Norm mit Ausgabe und Tabelle angeben.`); continue; }
    if (zahlLesen(alt.rsi) !== rsi || zahlLesen(alt.rse) !== rse || !gleich(alt.quelle, x.quelle)) geaendert.push(g.name);
  }
  if (fehler.length === 0 && geaendert.length === 0) fehler.push('Nichts geändert.');
  return { fehler, geaendert };
}

/** Aufrufe von uebergang_setzen: der Kern kennt eine Quelle je Aufruf, darum ein Aufruf je Quelle. → [{ quelle, gruppen: [..], parameter }] */
export function uebergangAufrufe(f, geaendert) {
  const nachQuelle = new Map();
  for (const g of geaendert) {
    const q = text(f[g].quelle);
    if (!nachQuelle.has(q)) nachQuelle.set(q, []);
    nachQuelle.get(q).push(g);
  }
  return [...nachQuelle].map(([quelle, gruppen]) => {
    const parameter = { quelle };
    for (const g of gruppen) parameter[g] = { rsi: zahlLesen(f[g].rsi), rse: zahlLesen(f[g].rse) };
    return { quelle, gruppen, parameter };
  });
}

// ---------------------------------------------------------------------------------------------- Hülle prüfen (huelle_pruefen)

/**
 * Einträge des Prüfergebnisses als Abschnitte mit Zeilen: { titel, anzahl, gekuerzt, zeilen: [{ kz, text, art }] } – nur Abschnitte mit Inhalt.
 * kz = Element, zu dem die Oberfläche springt (Bauteil, Aufbau, Material, Fenster); ohne kz keine Sprungzeile (Übergang).
 */
export function pruefAbschnitte(d) {
  if (!istObjekt(d)) return [];
  const aus = [];
  const add = (titel, o, zeile, art) => {
    const liste = Array.isArray(o?.liste) ? o.liste : [];
    if (liste.length === 0 && !(o?.anzahl > 0)) return;
    aus.push({ titel, anzahl: o.anzahl ?? liste.length, gekuerzt: !!o.gekuerzt, art, zeilen: liste.map(zeile) });
  };
  const o = d.bauteileOhneAufbau;
  if (o?.anzahl > 0) aus.push({
    titel: 'Bauteile ohne Aufbau', anzahl: o.anzahl, gekuerzt: !!o.gekuerzt, art: 'bauteil',
    zeilen: (o.liste ?? []).map((kz) => ({ kz, text: '' })),
    zusatz: istObjekt(o.nachKlasse) ? Object.entries(o.nachKlasse).map(([k, n]) => `${n} ${k}`).join(', ') : '',
  });
  add('Aufbauten ohne U-Wert', d.aufbautenOhneU, (x) => ({ kz: x.aufbau, text: [x.name, (x.fehlt ?? []).map(hinweisFuerMenschen).join(' ')].filter(Boolean).join(' – '), rot: true }), 'aufbau');
  add('Rsi/Rse fehlen', d.uebergangFehlt, (x) => ({ kz: null, text: `${x.gruppe}: ${x.grund}` }), 'uebergang');
  add('Fenster ohne Kennwerte', d.fensterOhneKennwerte, (x) => ({ kz: x.fenster, text: (x.fehlt ?? []).map(hinweisFuerMenschen).join(' '), rot: true }), 'fenster');
  add('Türen ohne Kennwerte', d.tuerenOhneKennwerte, (x) => ({ kz: x.tuer, text: (x.fehlt ?? []).map(hinweisFuerMenschen).join(' '), rot: true }), 'tuer');
  add('Materialien ohne λ', d.materialienOhneLambda, (x) => ({ kz: x.material, text: `${x.name ?? ''} – ${x.fehlt} fehlt`, rot: true }), 'material');
  add('Aufbau der falschen Art', d.aufbauUnpassendeArt, (x) => ({ kz: x.bauteil, text: `${x.klasse}: Aufbau ${x.aufbau} ist «${x.art}», erwartet ${(x.erwartet ?? []).join(' oder ')}`, rot: true }), 'bauteil');
  return aus;
}

export function pruefHtml(d) {
  if (!istObjekt(d)) return '<p class="leise">Keine Antwort.</p>';
  const abschnitte = pruefAbschnitte(d);
  const kopf = `<p class="${abschnitte.length === 0 ? 'huelle-ok' : 'huelle-summe'}"><b>${esc(d.zusammenfassung ?? '')}</b></p>`;
  return kopf + abschnitte.map((a) => `<h4>${esc(a.titel)} <span class="leise">(${a.anzahl}${a.gekuerzt ? ', gekürzt' : ''})</span></h4>${a.zusatz ? `<p class="leise">${esc(a.zusatz)}</p>` : ''}`
    + (a.art === 'bauteil' && a.zeilen.every((z) => !z.text)
      ? `<div class="chips">${a.zeilen.map((z) => `<button type="button" class="chip p-sprung" data-kz="${esc(z.kz)}" title="Zum Element springen">${esc(z.kz)}</button>`).join('')}</div>`
      : `<table class="tab klickbar huelle-pruef"><tbody>${a.zeilen.map((z) =>
        `<tr${z.kz ? ` class="p-sprung" data-kz="${esc(z.kz)}" title="Zum Element springen"` : ''}><td>${esc(z.kz ?? '')}</td><td class="${z.rot ? 'rot' : ''}">${esc(z.text)}</td></tr>`).join('')}</tbody></table>`)).join('');
}

// ---------------------------------------------------------------------------------------------- Wärmebrücken (waermebruecken_vorschlagen)

export const WB_PSI_SATZ = 'ψ fehlt: Der Kern hat keinen Wärmebrückenkatalog als Quelle – ψ je Wärmebrücke mit Quelle nachtragen (Katalog mit Ausgabe oder Berechnung).';

export const waermebrueckenParameter = (geschoss = '') => (leer(geschoss) ? {} : { geschoss: text(geschoss) });

/** Zahl der angelegten/geänderten Wärmebrücken eines Laufs aus den Änderungen. */
export function waermebrueckenZaehlen(aenderungen) {
  const z = { neu: 0, geaendert: 0, geloescht: 0 };
  for (const a of aenderungen ?? []) if (a.klasse === 'Waermebruecke') { if (a.art === 'angelegt') z.neu++; else if (a.art === 'geaendert') z.geaendert++; else if (a.art === 'geloescht') z.geloescht++; }
  return z;
}

// ---------------------------------------------------------------------------------------------- Listen (raumgrenzen, huellbauteile)

export const LISTEN = {
  raumgrenzen: { titel: 'Raumgrenzen', kz: 'AN-LI-RAUMGRENZEN', abfrage: 'raumgrenzen' },
  huellbauteile: { titel: 'Hüllbauteile', kz: 'AN-LI-HUELLBAUTEILE', abfrage: 'huellbauteile' },
};

/**
 * Zeigt die Listenansicht (listenformat.aufbereiten) alle Zeilen dieser Ableitung? Sonst zeigt die Oberfläche die Abfrage in einem Dialog.
 * `aufbereiten` kommt vom Aufrufer (listenformat.js), damit dieses Modul ohne Import bleibt.
 */
export function ansichtZeigtZeilen(name, daten, aufbereiten) {
  try {
    if (!Array.isArray(daten?.zeilen)) return false;
    const n = daten.zeilen.length;
    const t = aufbereiten(name, daten)?.tabellen?.[0];
    return !!t && t.zeilen.length === n && (n === 0 || t.spalten.some((s) => s.schluessel === (name === 'raumgrenzen' ? 'art' : 'bauteil')));
  } catch { return false; }
}

const SPALTEN = {
  raumgrenzen: [['raum', 'Raum'], ['nachbar', 'Nachbar'], ['angrenzung', 'Angrenzung'], ['art', 'Art'], ['bauteil', 'Bauteil'], ['aufbau', 'Aufbau'],
    ['flaeche_m2', 'Fläche m²', 'zahl'], ['azimut', 'Azimut °', 'zahl'], ['neigung', 'Neigung °', 'zahl'], ['u', 'U W/(m²·K)', 'zahl'], ['ua_WK', 'U·A W/K', 'zahl']],
  huellbauteile: [['bauteil', 'Bauteil'], ['klasse', 'Klasse'], ['geschoss', 'Geschoss'], ['aufbau', 'Aufbau'], ['u', 'U W/(m²·K)', 'zahl'],
    ['flaecheInnen_m2', 'Fläche innen m²', 'zahl'], ['flaecheAussen_m2', 'Fläche aussen m²', 'zahl'], ['azimut', 'Azimut °', 'zahl'], ['angrenzung', 'Angrenzung'], ['hinweis', 'Hinweis']],
};

/** Tabelle der Abfrage für den Dialog (Ersatz, wenn die Listenansicht die Zeilen nicht zeigt). Klick auf eine Zeile: data-kz = Bauteil bzw. Raum. */
export function ableitungTabelleHtml(name, d) {
  if (!istObjekt(d)) return '<p class="leise">Keine Antwort.</p>';
  const spalten = SPALTEN[name] ?? [];
  const zeilen = Array.isArray(d.zeilen) ? d.zeilen : [];
  const f = (v) => (v === null || v === undefined ? '' : typeof v === 'number' ? zahlAnzeige(v, 3) : String(v));
  const kopf = `<p class="huelle-summe"><b>${esc(String(d.anzahl ?? zeilen.length))} ${name === 'raumgrenzen' ? 'Raumgrenzen' : 'Hüllbauteile'}</b>${d.anzahlRaeume !== undefined ? ` in ${esc(String(d.anzahlRaeume))} Räumen` : ''}</p>`;
  const hinweise = (d.hinweise ?? []).filter(Boolean);
  const tab = zeilen.length === 0 ? '<p class="leise">Keine Einträge.</p>'
    : `<table class="tab klickbar huelle-ableitung"><thead><tr>${spalten.map(([, t, a]) => `<th${a ? ' class="zahl"' : ''}>${esc(t)}</th>`).join('')}</tr></thead><tbody>${zeilen.map((z) =>
      `<tr data-kz="${esc(z.bauteil ?? z.raum ?? '')}">${spalten.map(([k, , a]) => `<td${a ? ' class="zahl"' : ''}>${esc(f(z[k]))}</td>`).join('')}</tr>`).join('')}</tbody></table>`;
  return kopf + (hinweise.length ? `<ul class="hinweisliste">${hinweise.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : '') + tab;
}

// ---------------------------------------------------------------------------------------------- Projektbrowser

/** Knoten «Hülle»: Zahlen der drei Äste aus den Elementen der Darstellung. */
export function huelleZahlen(elemente) {
  const z = { Material: 0, Aufbau: 0, Waermebruecke: 0 };
  for (const e of elemente ?? []) if (e.klasse in z) z[e.klasse]++;
  return z;
}

// ---------------------------------------------------------------------------------------------- Eigenschaftenpalette: Hinweise und «ohne Quelle»

/**
 * Kennwerte, die nur mit Quelle gelten (Spiegel der Nachführung im Kern, HuelleNachfuehren.cs: MaterialKennwerte, FensterKennwerte, ud, uWert) – je Klasse.
 * Ohne Quelle gelten sie in der Rechnung als fehlend; die Palette kennzeichnet sie. Gerechnete Werte (Herkunft «berechnung») brauchen keine Quelle.
 */
export const QUELLE_PFLICHT = {
  Material: ['lambda', 'rohdichte', 'waermekapazitaet', 'emission', 'absorption', 'mu'],
  Aufbau: ['uWert'],
  Fenster: ['ug', 'uf', 'psiG', 'g', 'rahmenanteil', 'fc', 'uw'],
  Tuer: ['ud'],
};

const HERKUNFT = /^([^,]+), ([^,]*), (\d{4}-\d\d-\d\d \d\d:\d\d)(?:, ([\s\S]+))?$/;

/**
 * Fehlt dem Wert die Quelle? herkunft = Text aus element_lesen («mensch, T, 2026-10-18 12:07[, Quelle]»). Nur Kennwerte laut QUELLE_PFLICHT; gerechnete Werte nie.
 * Passt der Text nicht zum Muster (Herkunft unbekannt), gibt es keine Marke.
 */
export function ohneQuelle(klasse, name, herkunft) {
  if (!(QUELLE_PFLICHT[klasse] ?? []).includes(name)) return false;
  const m = HERKUNFT.exec(String(herkunft ?? '').trim());
  if (!m || m[1] === 'berechnung') return false;
  return leer(m[4]);
}

export const OHNE_QUELLE_TIPP = 'Kein Quellenbeleg: dieser Wert gilt in der Rechnung nicht. Die Quelle nennen: Register Hülle (Material …, Kennwerte …, Aufbau …).';

/** Marke «ohne Quelle» am Feld (HTML) oder ''. */
export const ohneQuelleMarke = (klasse, name, herkunft) => (ohneQuelle(klasse, name, herkunft) ? `<span class="ep-ohnequelle" title="${esc(OHNE_QUELLE_TIPP)}">ohne Quelle</span>` : '');

/** Hinweise eines angenommenen Befehls für die Palette: ohne leere und ohne «Keine Änderung.», ohne Doppelte, mit den Namen der Knöpfe statt der Befehle. */
export function paletteHinweise(hinweise) {
  const aus = [];
  for (const h of hinweise ?? []) {
    const t = hinweisFuerMenschen(h);
    if (t && !/^Keine Änderung\.?$/i.test(t) && !aus.includes(t)) aus.push(t);
  }
  return aus;
}

/** Kasten mit den Hinweisen des Kerns oben in der Palette (bestehender Hinweisstil: wie «Reserviert von …»). */
export function paletteHinweiseHtml(texte) {
  if (!texte?.length) return '';
  return `<div class="ep-hinweise" role="status"><button type="button" class="ep-hinweise-zu" title="Schliessen" aria-label="Hinweise schliessen">✕</button>`
    + `<b>${texte.length === 1 ? 'Hinweis' : 'Hinweise'} des Kerns</b><ul>${texte.map((t) => `<li>${esc(t)}</li>`).join('')}</ul></div>`;
}
