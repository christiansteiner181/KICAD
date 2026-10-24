// Palette «Problem-Marker» (Modul XR, XR-E, Quest App V1a): reine Hilfen ohne DOM – Zeilen der Liste, Filter, Sortierung, Frist, Eingaben prüfen, Parameter für
// element_aendern und bcf_export, Kamera für «In 3D zeigen» aus dem Blickpunkt der Brille. In Node getestet (problemmarker.test.mjs). Keine Fachlogik: geändert wird
// nur über den Befehl element_aendern des Kerns, exportiert über die Abfrage bcf_export (klasse ProblemMarker).
// Ein Marker im Zustand der Oberfläche: { id, kennzeichen, klasse: 'ProblemMarker', geschoss (Kennzeichen), geometrie: { art: 'punkt', punkte: [[x, y, z]] }, werte:
// { status, notiz, zustaendig, frist, beteiligte, blickpunkt: { kopf, richtung, modus, massstab }, benutzer, brille, zeit, quelle } } (Darstellung.cs).

export const KLASSE = 'ProblemMarker';
export const STATUS = ['offen', 'erledigt'];
export const FILTER = [['offen', 'Offen'], ['erledigt', 'Erledigt'], ['alle', 'Alle']];
export const NOTIZ_MAX = 500;
export const ZUSTAENDIG_MAX = 100;
/** Kamera höchstens so weit (m) vor der Stelle (am Sitzungstisch liegt der Kopf im Massstab weit über dem Modell), wie im BCF-Export (BcfExport.AbstandMax). */
export const ABSTAND_MAX_M = 30;
/** Ohne Blickpunkt: schräg von oben aus Südwest in diesem Abstand (m), wie im BCF-Export (BcfExport.AbstandMin, Blick). */
export const ABSTAND_OHNE_M = 3;

const text = (v) => (v === null || v === undefined ? '' : String(v));

/** Status eines Markers (ohne Angabe offen). */
export const status = (m) => (m?.werte?.status === 'erledigt' ? 'erledigt' : 'offen');

/** Erste Zeile der Notiz (gekürzt) bzw. «ohne Beschreibung». */
export function kurzNotiz(m, max = 60) {
  const t = text(m?.werte?.notiz).split('\n')[0].trim();
  if (!t) return 'ohne Beschreibung';
  return t.length > max ? t.slice(0, max - 1) + '…' : t;
}

/** Datum JJJJ-MM-TT aus einem ISO-Zeitpunkt (sonst ''). */
export const datum = (iso) => (/^\d{4}-\d{2}-\d{2}/.test(text(iso)) ? text(iso).slice(0, 10) : '');

/** Datum TT.MM.JJJJ für die Anzeige. */
export function datumText(iso) {
  const d = datum(iso);
  return d ? `${d.slice(8, 10)}.${d.slice(5, 7)}.${d.slice(0, 4)}` : '';
}

/** Ist die Frist eines offenen Markers vorbei (heute = JJJJ-MM-TT)? */
export const ueberfaellig = (m, heute) => status(m) === 'offen' && !!datum(m?.werte?.frist) && datum(m.werte.frist) < heute;

/** Natürlicher Vergleich der Kennzeichen (PM-2 vor PM-10). */
const kz = (a, b) => String(a ?? '').localeCompare(String(b ?? ''), 'de', { numeric: true });

/** Die Marker nach Filter (offen | erledigt | alle), offene zuerst, dann nach Kennzeichen. */
export function liste(marker, filter = 'offen') {
  return [...(marker ?? [])].filter((m) => m?.klasse === KLASSE && (filter === 'alle' || status(m) === filter))
    .sort((a, b) => (status(a) === status(b) ? kz(a.kennzeichen, b.kennzeichen) : status(a) === 'offen' ? -1 : 1));
}

/** Zahlen für den Kopf: offen, erledigt, überfällig. */
export function zahlen(marker, heute) {
  const l = (marker ?? []).filter((m) => m?.klasse === KLASSE);
  return { offen: l.filter((m) => status(m) === 'offen').length, erledigt: l.filter((m) => status(m) === 'erledigt').length, ueberfaellig: l.filter((m) => ueberfaellig(m, heute)).length };
}

/** Eine Zeile der Liste: Kennzeichen, Status, Notiz, Geschoss, gesetzt von/am, Zuständig, Frist (überfällig), aus der Brille. */
export function zeile(m, heute) {
  const w = m?.werte ?? {};
  return {
    id: m.id, kennzeichen: m.kennzeichen, status: status(m), notiz: kurzNotiz(m), geschoss: text(m.geschoss),
    von: [text(w.benutzer), datumText(w.zeit)].filter(Boolean).join(' · '), zustaendig: text(w.zustaendig), frist: datumText(w.frist),
    ueberfaellig: ueberfaellig(m, heute), brille: w.quelle === 'brille', blickpunkt: !!(w.blickpunkt && Array.isArray(w.blickpunkt.kopf)),
  };
}

/**
 * Eingaben des Formulars prüfen und die Parameter für element_aendern bauen (nur geänderte Werte; leer = entfernen = null). Rückgabe { fehler } oder { parameter }
 * bzw. { parameter: null } ohne Änderung.
 */
export function aenderung(m, eingabe) {
  const w = m?.werte ?? {};
  const st = eingabe.status;
  if (!STATUS.includes(st)) return { fehler: 'Status: offen oder erledigt.' };
  const zu = text(eingabe.zustaendig).trim();
  if (zu.length > ZUSTAENDIG_MAX) return { fehler: `Zuständig: höchstens ${ZUSTAENDIG_MAX} Zeichen.` };
  const fr = text(eingabe.frist).trim();
  if (fr && !/^\d{4}-\d{2}-\d{2}$/.test(fr)) return { fehler: 'Frist als Datum JJJJ-MM-TT (oder leer).' };
  if (fr && Number.isNaN(Date.parse(fr + 'T00:00:00Z'))) return { fehler: 'Frist: kein gültiges Datum.' };
  const no = text(eingabe.notiz).replace(/\r/g, '').trim();
  if (no.length > NOTIZ_MAX) return { fehler: `Notiz: höchstens ${NOTIZ_MAX} Zeichen.` };
  const eig = {};
  if (st !== status(m)) eig.status = st;
  for (const [n, neu] of [['zustaendig', zu], ['frist', fr], ['notiz', no]]) {
    const alt = text(w[n]).trim();
    if (neu !== alt) eig[n] = neu === '' ? null : neu;
  }
  return { parameter: Object.keys(eig).length ? { element: m.id, eigenschaften: eig } : null };
}

/** Parameter für bcf_export: Datei, klasse ProblemMarker, Status nach dem Filter (alle: ohne). */
export function bcfParameter(datei, filter) {
  const p = { datei: text(datei).trim().replace(/^"|"$/g, ''), klasse: KLASSE };
  if (filter === 'offen' || filter === 'erledigt') p.status = filter;
  return p;
}

/** Dateiname für den Export: Problem-Marker_<Projekt>_<JJJJ-MM-TT>.bcf (ohne verbotene Zeichen). */
export function bcfDateiname(projekt, heute) {
  const n = text(projekt).replace(/[\\/:*?"<>|]+/g, '_').trim() || 'Projekt';
  return `Problem-Marker_${n}_${heute}.bcf`;
}

const laenge = (v) => Math.hypot(v[0], v[1], v[2]);
const zahl3 = (v) => Array.isArray(v) && v.length >= 3 && v.slice(0, 3).every((x) => typeof x === 'number' && Number.isFinite(x));

/**
 * Kamera der 3D-Ansicht für einen Marker (Szene der 3D-Ansicht: x = x, y = Höhe, z = −y, Meter): am Kopf des Blickpunkts mit Blick auf die Stelle, höchstens
 * ABSTAND_MAX_M davor; ohne Blickpunkt schräg von oben aus Südwest im Abstand ABSTAND_OHNE_M. Rückgabe { position: [x, y, z], ziel: [x, y, z] } bzw. null ohne Lage.
 */
export function kamera(m) {
  const ort = m?.geometrie?.punkte?.[0];
  if (!zahl3(ort)) return null;
  const zu3d = (p) => [p[0] / 1000, p[2] / 1000, -p[1] / 1000];
  const ziel = zu3d(ort);
  const kopf = m.werte?.blickpunkt?.kopf;
  if (zahl3(kopf)) {
    const d = [ort[0] - kopf[0], ort[1] - kopf[1], ort[2] - kopf[2]];
    const l = laenge(d);
    if (l >= 100) {
      const a = Math.min(l, ABSTAND_MAX_M * 1000);
      return { position: zu3d([ort[0] - (d[0] / l) * a, ort[1] - (d[1] / l) * a, ort[2] - (d[2] / l) * a]), ziel };
    }
    const r = m.werte.blickpunkt.richtung;
    if (zahl3(r) && laenge(r) > 1e-9) return { position: zu3d(kopf), ziel: zu3d([kopf[0] + r[0] * 1000, kopf[1] + r[1] * 1000, kopf[2] + r[2] * 1000]) };
  }
  const s = 1 / Math.sqrt(3);
  const a = ABSTAND_OHNE_M * 1000;
  return { position: zu3d([ort[0] - s * a, ort[1] - s * a, ort[2] + s * a]), ziel };
}
