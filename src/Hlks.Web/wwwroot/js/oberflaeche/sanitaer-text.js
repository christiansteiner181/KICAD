// Reine Funktionen für die Oberfläche der Sanitär-Belastungswerte (Modul 4 Teil A-O, entwurf/modul-4.md): Zahlenformat der Anzeige, Auswahl der Apparateart,
// Tabelle der Spezialinstallationen, Texte der Palette. Kein Browser, keine Fachlogik: LU, DU, Summen und alle Formeln liefert der Kern (element_lesen,
// sanitaer_zusammenstellung, sanitaer_katalog); hier wird nur dargestellt und die Eingabe in die Form des Kerns gebracht. In Node getestet (sanitaer-belastung.test.mjs).
//
// RUNDUNG (nur Anzeige – der Kern rechnet ungerundet, das «?» und die Tooltips der Liste zeigen den genauen Wert):
//   LU   ganze Zahl; hat der Wert eine Nachkommastelle (Katalog heute nur ganze LU, Summen daher ganz), eine Stelle. Wie im Excel (Zahlenformat «Standard»).
//   DU   eine Nachkommastelle, immer angezeigt («2,0», «38,8»): Katalogwerte haben höchstens eine Stelle (0.1 bis 2.5), die Summen damit auch; die feste
//        Stelle hält die Spalten bündig. Fliesskommafehler (38.800000000000004) verschwinden durch das Runden.
//   l/s  zwei Nachkommastellen, unter 1 l/s drei («1,14», «0,762»): Spitzenwerte der Kurven liegen meist zwischen 0.5 und 5 l/s; unter 1 l/s würde eine
//        zweite Stelle zu viel Information abschneiden (0,76 gegen 0,762). Volumenstrom (Total LU × 0,1) steht ebenfalls mit zwei Stellen («13,20»).
//   Spezialinstallationen (von Hand eingetragen, keine Rechenergebnisse): wie eingegeben, bis drei Stellen ohne Nachnullen («0,5», nicht «0,500»).
//   Die Palette zeigt Dezimalpunkt wie ihre übrigen Felder, Liste und CSV Dezimalkomma wie die anderen Listen (listenformat.js).

const leer = (x) => x === null || x === undefined || x === '';
const zahl = (x) => (typeof x === 'number' && Number.isFinite(x));

/** Zahl mit fester Stellenzahl und Dezimaltrenner ({ komma: true } = Komma, sonst Punkt). */
function fest(v, stellen, komma) {
  const f = 10 ** stellen;
  const t = (Math.round(v * f + (v >= 0 ? 1e-9 : -1e-9)) / f).toFixed(stellen);   // 1e-9: 38.800000000000004 und 0.15 runden wie geschrieben
  return komma ? t.replace('.', ',') : t;
}

/** LU: ganz, sonst eine Nachkommastelle; null/undefined → «». */
export function formatLu(v, { komma = false } = {}) {
  if (!zahl(v)) return '';
  const r = Math.round(v * 10) / 10;
  const t = Number.isInteger(r) ? String(r) : r.toFixed(1);
  return komma ? t.replace('.', ',') : t;
}

/** DU: eine Nachkommastelle. */
export function formatDu(v, { komma = false } = {}) {
  return zahl(v) ? fest(v, 1, komma) : '';
}

/** l/s: zwei Nachkommastellen, unter 1 l/s drei. */
export function formatLs(v, { komma = false } = {}) {
  if (!zahl(v)) return '';
  return fest(v, Math.abs(v) < 1 && v !== 0 ? 3 : 2, komma);
}

/** Von Hand eingetragener l/s-Wert (Spezialinstallationen): wie eingegeben, höchstens drei Nachkommastellen ohne überflüssige Nullen («0,5», «0,25», «2»). */
export function formatEingabe(v, { komma = false } = {}) {
  if (!zahl(v)) return '';
  const t = String(Math.round(v * 1000 + (v >= 0 ? 1e-9 : -1e-9)) / 1000);
  return komma ? t.replace('.', ',') : t;
}

/** Stückzahl je Geschoss: 0 bleibt leer (wie die leere Excel-Zelle). */
export function formatStueck(v) {
  return zahl(v) && v !== 0 ? String(v) : '';
}

/** Genauer Wert für Tooltips und «?»: so, wie ihn der Kern liefert (ohne Rundung), mit Dezimalkomma. */
export function genau(v, einheit = '') {
  return zahl(v) ? `${String(v).replace('.', ',')}${einheit ? ' ' + einheit : ''}` : '';
}

// ---------------------------------------------------------------------------------------------- Apparateart

/** Anzeigename der Art: Name aus auswahlNamen, sonst der Schlüssel. */
export const artName = (schluessel, namen) => (leer(schluessel) ? '' : (namen?.[schluessel] ?? String(schluessel)));

/**
 * Optionen der Auswahl «Apparateart»: erster Eintrag = «Vorgabe der Klasse (Name)» für null (nicht gesetzt), danach alle Arten in Katalogreihenfolge mit Namen.
 *   def           Eigenschaftsdefinition aus abfrage('katalog', { klasse }): { auswahl[], auswahlNamen{} }
 *   vorgabe       Schlüssel der Vorgabe der Klasse (sanitaer_katalog.vorgabeJeKlasse[klasse]) oder null/undefined = keine Vorgabe
 * → [[wert, text]] ('' = nicht gesetzt)
 */
export function artOptionen(def, vorgabe) {
  const namen = def?.auswahlNamen ?? {};
  const erste = leer(vorgabe) ? ['', 'Vorgabe der Klasse: keine – Art wählen'] : ['', `Vorgabe der Klasse (${artName(vorgabe, namen)})`];
  return [erste, ...(def?.auswahl ?? []).map((k) => [k, artName(k, namen)])];
}

/** Herkunft der wirksamen Art als kurzer Satz unter dem Feld (sanitaer = Block von element_lesen). */
export function artHinweis(s) {
  if (!s) return '';
  switch (s.woher) {
    case 'eigenschaft': return `gilt: ${s.name ?? s.art} (am Apparat gesetzt)`;
    case 'familie': return `gilt: ${s.name ?? s.art} (aus der Familie)`;
    case 'vorgabe': return `gilt: ${s.name ?? s.art} (Vorgabe der Klasse)`;
    case 'unbekannt': return `Art «${s.art ?? ''}» gibt es im Katalog nicht – bitte wählen (Befund P21)`;
    default: return 'keine Art – bitte wählen (Befund P21)';
  }
}

/** Zeilen «LU KW / LU WW / DU» unter der Apparateart: nur die Werte, die die Art hat (element_lesen.abgeleitet). → [{ schluessel, label, text, einheit }] */
export function apparatWerte(abgeleitet) {
  const a = abgeleitet ?? {};
  return [['luKw', 'LU KW', 'LU', formatLu], ['luWw', 'LU WW', 'LU', formatLu], ['du', 'DU', 'DU', formatDu]]
    .filter(([k]) => zahl(a[k])).map(([schluessel, label, einheit, f]) => ({ schluessel, label, einheit, text: f(a[schluessel]) }));
}

/** Zeilen der Belastungssummen einer Leitung (element_lesen.abgeleitet): Trinkwasser ΣLU KW, ΣLU WW, grösster Einzel-LU; Abwasser ΣDU. */
export function leitungWerte(abgeleitet) {
  const a = abgeleitet ?? {};
  return [['summeLuKw', 'ΣLU KW', 'LU', formatLu], ['summeLuWw', 'ΣLU WW', 'LU', formatLu], ['groessterLu', 'Grösster Einzel-LU', 'LU', formatLu], ['summeDu', 'ΣDU', 'DU', formatDu]]
    .filter(([k]) => zahl(a[k])).map(([schluessel, label, einheit, f]) => ({ schluessel, label, einheit, text: f(a[schluessel]) }));
}

/** Schlüssel von element_lesen.abgeleitet, die die Palette in der Sanitär-Darstellung zeigt (und nicht in der allgemeinen Gruppe «Abgeleitet»). */
export const SANITAER_ABGELEITET = ['luKw', 'luWw', 'du', 'summeLuKw', 'summeLuWw', 'groessterLu', 'summeDu'];

/** Zeile «Apparate» unter den Summen einer Leitung (element_lesen.sanitaer): Anzahl, Richtung, Hinweis bei Masche und Apparaten ohne Wert. */
export function leitungHinweise(s) {
  if (!s || typeof s !== 'object') return [];
  const t = [];
  if (zahl(s.apparate)) t.push(`${s.apparate} ${s.apparate === 1 ? 'Apparat' : 'Apparate'} (${s.richtung ?? ''})`.replace(' ()', ''));
  if (s.masche) t.push('Netz mit Masche – Summe nicht eindeutig (Befund P24)');
  if (Array.isArray(s.ohneWert) && s.ohneWert.length) t.push(`ohne Wert: ${s.ohneWert.join('; ')}`);
  return t;
}

// ---------------------------------------------------------------------------------------------- Abflusskennzahl K

/** Hinweis unter dem Feld K (Anlage und Projektinformation): keine Auswahlliste, keine Vorgabe – die Norm liegt nicht vor. */
export const K_HINWEIS = 'Wert aus SN 592000 nach Gebäudeart – Norm liegt nicht vor; Walkringen: 0.5';

// ---------------------------------------------------------------------------------------------- Spezialinstallationen (Tabelle)

export const SPEZIAL_FEHLT = 'Bezeichnung und Versorgung oder Entsorgung (l/s) eintragen';

/** Zahlenfeld der Tabelle → Text für das Eingabefeld (Dezimalpunkt wie die Palette). */
const feldText = (v) => (zahl(v) ? String(Math.round(v * 1000) / 1000) : '');

/** Wert der Eigenschaft spezialinstallationen (Liste von { bezeichnung, versorgung, entsorgung }) → Zeilen für die Tabelle; Unbrauchbares wird übersprungen. */
export function spezialZeilen(wert) {
  if (!Array.isArray(wert)) return [];
  return wert.filter((z) => z && typeof z === 'object' && !Array.isArray(z))
    .map((z) => ({ bezeichnung: leer(z.bezeichnung) ? '' : String(z.bezeichnung), versorgung: feldText(z.versorgung), entsorgung: feldText(z.entsorgung) }));
}

/** Zahl aus einem Eingabefeld: leer → null; «0,5» und «0.5» gehen; sonst Fehler mit verständlichem Text. */
export function spezialZahl(text, was) {
  const t = String(text ?? '').trim();
  if (t === '') return null;
  const n = Number(t.replace(',', '.'));
  if (!Number.isFinite(n)) throw Object.assign(new Error(`${was}: bitte eine Zahl in l/s eingeben.`), { eingabe: true });
  return n;
}

/**
 * Eingabezeilen der Tabelle → Wert für element_aendern.
 *   zeilen  [{ bezeichnung, versorgung, entsorgung }] als Text (so wie im Feld)
 * → { wert: [{ bezeichnung, versorgung?, entsorgung? }] | null, offen: Anzahl unvollständiger Zeilen }
 * Ganz leere Zeilen (frisch angefügt) zählen nicht; Zeilen mit Bezeichnung ohne Zahl oder Zahl ohne Bezeichnung sind «offen»: es wird nichts gesendet, bis alle
 * vollständig sind. Ob die Werte zulässig sind (≥ 0), entscheidet der Kern und lehnt sonst mit Vorschlag ab.
 */
export function spezialWert(zeilen) {
  const wert = [];
  let offen = 0;
  for (const z of zeilen ?? []) {
    const b = String(z.bezeichnung ?? '').trim();
    const v = spezialZahl(z.versorgung, 'Versorgung');
    const e = spezialZahl(z.entsorgung, 'Entsorgung');
    if (b === '' && v === null && e === null) continue;
    if (b === '' || (v === null && e === null)) { offen++; continue; }
    const o = { bezeichnung: b };
    if (v !== null) o.versorgung = v;
    if (e !== null) o.entsorgung = e;
    wert.push(o);
  }
  return { wert: wert.length ? wert : null, offen };
}
