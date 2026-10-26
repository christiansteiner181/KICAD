// Gesten am Arbeitsplatz (Modul G, entwurf/modul-gesten.md): reine Teile der Oberfläche, ohne DOM und ohne Kamera, in Node getestet.
// Hier stehen die Einstellung (Form, Vorgaben, Normalisierung), die Liste der Gesten für den Dialog, die vier Zustände der Statusleiste, die Zeilen
// der Stellschrauben (generisch aus STELLSCHRAUBEN_VORGABE der Gestenlogik), das Handskelett und die Datei einer Punktfolge.
// Die Erkennung selbst (Merkmale, Wisch-Regel, Glättung) liegt in gesten-logik.js und gesten.js (hlks-kern); die Oberfläche sieht nur ihre Ereignisse.
// Keine Normwerte: alle Grenzwerte der Gesten sind Bedienungs-Einstellungen, die Christian am Arbeitsplatz festlegt.

/** Schlüssel der Benutzereinstellung (Server: GestenEinstellung.cs). */
export const SCHLUESSEL = 'gesten';

// ---------------------------------------------------------------------------------------------- Einstellung

/** Aktionen, die eine Schritt-Geste auslösen darf: nur Ansicht und Navigation, nie etwas, das das Modell ändert (Leitlinie 3 der Spezifikation). */
export const BELEGBAR = ['ansicht.reiter-vor', 'ansicht.reiter-zurueck', 'ansicht.zoom-alles', 'ansicht.zoom-alle', 'ansicht.nebeneinander', 'ansicht.reiter'];
export const istBelegbar = (id) => typeof id === 'string' && BELEGBAR.includes(id);

/** Vorgabe der Belegung: G2 blättert die Ansichtsreiter (Wisch nach rechts = nächster, nach links = vorheriger), G6 «Zoom alles». */
export const BELEGUNG_VORGABE = Object.freeze({
  G2: Object.freeze({ links: 'ansicht.reiter-zurueck', rechts: 'ansicht.reiter-vor' }),
  G6: 'ansicht.zoom-alles',
});

export const HAENDE = ['links', 'rechts'];

/** Die Vorgabe der ganzen Einstellung: Gesten aus, linke Hand, Handskelett an, keine geänderte Stellschraube. */
export const EINSTELLUNG_VORGABE = Object.freeze({ an: false, hand: 'links', kameraId: null, skelett: true, stellschrauben: {}, belegung: BELEGUNG_VORGABE });

const aktionOderLeer = (x, vorgabe) => (x === '' || x === null ? '' : typeof x === 'string' && /^[A-Za-z][A-Za-z0-9_.\-]*$/.test(x) ? x : vorgabe);

/** Macht aus dem gelesenen Wert (oder null) eine vollständige, gültige Einstellung; Unbrauchbares wird zur Vorgabe. Verändert den Eingabewert nicht. */
export function einstellungNormal(roh) {
  const r = roh && typeof roh === 'object' && !Array.isArray(roh) ? roh : {};
  const stell = {};
  if (r.stellschrauben && typeof r.stellschrauben === 'object' && !Array.isArray(r.stellschrauben)) {
    for (const [k, v] of Object.entries(r.stellschrauben)) if (/^[a-z][a-z0-9_.]*$/.test(k) && typeof v === 'number' && Number.isFinite(v)) stell[k] = v;
  }
  const b = r.belegung && typeof r.belegung === 'object' ? r.belegung : {};
  const g2 = b.G2 && typeof b.G2 === 'object' ? b.G2 : {};
  return {
    an: r.an === true,
    hand: HAENDE.includes(r.hand) ? r.hand : EINSTELLUNG_VORGABE.hand,
    kameraId: typeof r.kameraId === 'string' && r.kameraId !== '' ? r.kameraId : null,
    skelett: r.skelett === false ? false : true,
    stellschrauben: stell,
    belegung: {
      G2: { links: aktionOderLeer(g2.links, BELEGUNG_VORGABE.G2.links), rechts: aktionOderLeer(g2.rechts, BELEGUNG_VORGABE.G2.rechts) },
      G6: aktionOderLeer(b.G6, BELEGUNG_VORGABE.G6),
    },
  };
}

/** Die belegte Aktion einer Schritt-Geste: G2 je Richtung (links/rechts), G6 ohne Richtung. Leer, unbekannt oder nicht belegbar ergibt null (keine Wirkung). */
export function belegungAktion(einst, nr, richtung = null) {
  const b = einstellungNormal(einst).belegung;
  const id = nr === 'G2' ? b.G2[richtung] : nr === 'G6' ? b.G6 : null;
  return id && istBelegbar(id) ? id : null;
}

/** Einstellung mit einer neuen Belegung (nr = 'G2' mit richtung, oder 'G6'). */
export function belegungSetzen(einst, nr, richtung, id) {
  const e = einstellungNormal(einst);
  if (nr === 'G2' && (richtung === 'links' || richtung === 'rechts')) e.belegung.G2[richtung] = id ?? '';
  else if (nr === 'G6') e.belegung.G6 = id ?? '';
  return e;
}

/** «Auf Vorgabe zurück»: alles ausser dem Grundschalter wird zur Vorgabe. */
export function aufVorgabe(einst) {
  return einstellungNormal({ an: einstellungNormal(einst).an });
}

// ---------------------------------------------------------------------------------------------- Liste der Gesten

/** Die Gesten der Stufe 1 (Spezifikation «Gestenwortschatz»); G1 entfällt (immer aktiv), die Nummer bleibt frei. */
export const GESTEN = Object.freeze([
  { nr: 'G2', name: 'Wisch links/rechts', wirkung: 'Ansichtsreiter wechseln; in Dialog, Liste und Hilfe mit Seiten: Seite zurück/vor', art: 'Schritt' },
  { nr: 'G3', name: 'Wisch hoch/runter', wirkung: 'im aktiven Fenster (Dialog, Liste) um eine Bildschirmseite rollen; in Plan, Schema und 3D ohne Wirkung', art: 'Schritt' },
  { nr: 'G4', name: 'Faust schliessen und bewegen', wirkung: '3D: Ansicht drehen um Auswahl bzw. Modellmitte; Plan und Schema: verschieben', art: 'fliessend' },
  { nr: 'G5', name: 'Faust zur Kamera hin/weg', wirkung: 'zoomen in der aktiven Ansicht', art: 'fliessend' },
  { nr: 'G6', name: 'Pistolengeste (Daumen hoch, Zeigefinger gestreckt), kurz', wirkung: 'Zoom alles; in 3D stellt sie die Ansicht zugleich wieder aufrecht', art: 'Schritt' },
  { nr: 'G7', name: 'Faust um die Blickachse drehen (nur 3D)', wirkung: 'Ansicht um die Blickachse rollen', art: 'fliessend' },
]);

const ohneWeich = (t) => String(t ?? '').replace(/[​­]/g, '');

/**
 * Zeilen des Dialogs: Nr, Geste, Wirkung, Art. Bei G2 und G6 steht dahinter, was nach der Belegung der Einstellung wirklich ausgelöst wird.
 *   titelVon(id) → Titel der Aktion (aus dem Aktionsregister) oder undefined
 */
export function gestenZeilen(einst, titelVon = () => undefined) {
  const e = einstellungNormal(einst);
  const t = (id) => (id ? ohneWeich(titelVon(id)) || id : 'keine Wirkung');
  return GESTEN.map((g) => {
    let wirkung = g.wirkung;
    if (g.nr === 'G2') wirkung += ` (belegt: nach rechts «${t(e.belegung.G2.rechts)}», nach links «${t(e.belegung.G2.links)}»)`;
    if (g.nr === 'G6') wirkung = `«${t(e.belegung.G6)}»${e.belegung.G6 === 'ansicht.zoom-alles' ? '; in 3D stellt sie die Ansicht zugleich wieder aufrecht' : ''}`;
    return { nr: g.nr, name: g.name, wirkung, art: g.art };
  });
}

// ---------------------------------------------------------------------------------------------- Statusleiste

/** Namen der fliessenden Gesten, wie gesten.js sie meldet. */
export const GESTE_NAMEN = Object.freeze({ drehen: 'drehen', rollen: 'rollen', zoomen: 'zoomen', verschieben: 'verschieben' });

const HINWEIS_BEDIENUNG = 'Klick schaltet die Gesten ein oder aus, Rechtsklick öffnet die Einstellungen.';

/**
 * Anzeige der Statusleiste: vier Zustände – aus, bereit (keine Hand), Hand erkannt, Geste aktiv (mit Namen) – dazu «startet», solange der Browser die Kamera öffnet.
 *   z = { an, laeuft, startet, zustand, geste, fehler, test }  (steuerung.zustand())
 * Ergebnis { stufe, text, titel }; stufe ist «aus» | «startet» | «bereit» | «hand» | «aktiv».
 */
export function statusAnzeige(z = {}) {
  const test = z.test ? ' (Testfeld: erkannte Gesten lösen nichts aus)' : '';
  if (z.startet) return { stufe: 'startet', text: 'Gesten starten …', titel: 'Die Kamera wird geöffnet. Beim ersten Einschalten fragt der Browser nach der Erlaubnis.' };
  if (!z.an || !z.laeuft || z.zustand === 'aus') {
    const grund = z.fehler ? ` Zuletzt: ${z.fehler}` : '';
    return { stufe: 'aus', text: 'Gesten aus', titel: `Die Gesten sind aus, die Kamera ist nicht in Betrieb.${grund} ${HINWEIS_BEDIENUNG}` };
  }
  if (z.zustand === 'aktiv') {
    const name = GESTE_NAMEN[z.geste] ?? (z.geste ? String(z.geste) : 'Geste');
    return { stufe: 'aktiv', text: `Geste: ${name}`, titel: `Geste aktiv: ${name}${test}. ${HINWEIS_BEDIENUNG}` };
  }
  if (z.zustand === 'hand') return { stufe: 'hand', text: 'Hand erkannt', titel: `Eine Hand ist im Bild${test}. ${HINWEIS_BEDIENUNG}` };
  return { stufe: 'bereit', text: 'Gesten bereit', titel: `Die Gesten sind bereit, im Bild ist keine Hand${test}. ${HINWEIS_BEDIENUNG}` };
}

/** Deutsche Meldung zu einem Fehler beim Starten (gesten.js wirft mit deutscher Meldung; Browserfehler der Kamera werden übersetzt). */
export function fehlerText(e) {
  const name = e?.name ?? '';
  const text = typeof e === 'string' ? e : String(e?.message ?? '').trim();
  if (name === 'NotAllowedError' || name === 'SecurityError') return 'Der Browser hat den Zugriff auf die Kamera nicht erlaubt. In der Adressleiste die Kamera erlauben und die Gesten erneut einschalten.';
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'Es wurde keine passende Kamera gefunden. Im Dialog «Gesten» eine andere Kamera wählen.';
  if (name === 'NotReadableError') return 'Die Kamera wird von einem anderen Programm benutzt.';
  return text || 'Die Gesten konnten nicht gestartet werden.';
}
export const NICHT_EINGEBAUT = 'Die Gestenerkennung ist in dieser Fassung von KICAD nicht vorhanden.';

// ---------------------------------------------------------------------------------------------- Stellschrauben

const zahl = (x) => (typeof x === 'number' && Number.isFinite(x) ? x : null);

/**
 * Zeilen der Empfindlichkeit, generisch aus STELLSCHRAUBEN_VORGABE (gesten-logik.js): jede Stellschraube mit Vorgabe, Bereich und Erklärung.
 * Gelesen werden die Felder vorgabe (oder wert), min und max (oder bereich [min, max]), schritt, einheit, titel, erklaerung (oder beschreibung, text);
 * eine Stellschraube ohne Zahl als Vorgabe wird übersprungen. werte = die gespeicherten Abweichungen der Einstellung.
 * Ergebnis [{ name, titel, vorgabe, wert, min, max, schritt, einheit, erklaerung, geaendert }].
 */
export function stellschraubenZeilen(vorgabe, werte = {}) {
  const ein = vorgabe && typeof vorgabe === 'object' ? vorgabe : {};
  const zeilen = [];
  for (const [name, roh] of Object.entries(ein)) {
    const o = roh && typeof roh === 'object' ? roh : { vorgabe: roh };
    const v = zahl(o.vorgabe) ?? zahl(o.wert) ?? zahl(o.standard);
    if (v === null) continue;
    const bereich = Array.isArray(o.bereich) ? o.bereich : [];
    const min = zahl(o.min) ?? zahl(bereich[0]);
    const max = zahl(o.max) ?? zahl(bereich[1]);
    const gesetzt = zahl(werte?.[name]);
    const wert = gesetzt === null ? v : begrenzen(gesetzt, min, max);
    zeilen.push({
      name, titel: String(o.titel ?? o.name ?? name), vorgabe: v, wert, min, max, schritt: zahl(o.schritt),
      einheit: String(o.einheit ?? ''), erklaerung: String(o.erklaerung ?? o.beschreibung ?? o.text ?? ''), geaendert: wert !== v,
    });
  }
  return zeilen;
}

/** Begrenzt auf den Bereich (fehlende Grenzen gelten nicht). */
export function begrenzen(w, min, max) {
  let x = w;
  if (min !== null && min !== undefined && x < min) x = min;
  if (max !== null && max !== undefined && x > max) x = max;
  return x;
}

/** Einstellung mit einer geänderten Stellschraube: der Wert wird auf den Bereich begrenzt; entspricht er der Vorgabe, entfällt der Eintrag. Ungültiges ändert nichts. */
export function stellschraubeSetzen(einst, zeile, neu) {
  const e = einstellungNormal(einst);
  const w = typeof neu === 'string' && neu.trim() !== '' ? Number(neu.replace(',', '.')) : neu;
  if (!zeile || !Number.isFinite(w)) return e;
  const x = begrenzen(w, zeile.min, zeile.max);
  if (x === zeile.vorgabe) delete e.stellschrauben[zeile.name]; else e.stellschrauben[zeile.name] = x;
  return e;
}

/** Wert mit Einheit für die Anzeige, mit Komma («0,045 m/s»). */
export const wertText = (w, einheit = '') => `${String(Math.round(w * 1e6) / 1e6).replace('.', ',')}${einheit ? ' ' + einheit : ''}`;

// ---------------------------------------------------------------------------------------------- Testfeld

/**
 * Eine Zeile des Testfelds zu einem Ereignis von gesten.js (zustand, schritt) – oder null, wenn es keine Zeile gibt (punkte laufen ständig).
 *   titelVon(id) → Titel der Aktion
 */
export function ereignisZeile(e, einst, titelVon = () => undefined) {
  if (!e || typeof e !== 'object') return null;
  if (e.art === 'zustand') {
    const a = statusAnzeige({ an: true, laeuft: true, zustand: e.zustand, geste: e.geste });
    return e.fehler ? `${a.text} – ${fehlerText(e.fehler)}` : a.text;
  }
  if (e.art === 'schritt') {
    const t = (id) => (id ? ohneWeich(titelVon(id)) || id : null);
    if (e.geste === 'G2') {
      const id = belegungAktion(einst, 'G2', e.richtung);
      return `G2 Wisch nach ${e.richtung ?? '?'}: würde ${t(id) ? `«${t(id)}» (bzw. die Seite im Dialog)` : 'nichts'} auslösen`;
    }
    if (e.geste === 'G3') return `G3 Wisch ${e.richtung ?? '?'}: würde eine Bildschirmseite rollen (nur in Dialog und Liste)`;
    if (e.geste === 'G6') { const id = belegungAktion(einst, 'G6'); return `G6 Pistolengeste: würde ${t(id) ? `«${t(id)}»` : 'nichts'} auslösen`; }
    return `${e.geste}: Schritt-Geste`;
  }
  return null;
}

// ---------------------------------------------------------------------------------------------- Handskelett

/** Verbindungen der 21 Punkte der MediaPipe-Hand (Handgelenk 0, je Finger vier Punkte; Daumen 1–4, Zeigefinger 5–8, Mittelfinger 9–12, Ringfinger 13–16, kleiner Finger 17–20). */
export const HAND_VERBINDUNGEN = Object.freeze([
  [0, 1], [1, 2], [2, 3], [3, 4],
  [0, 5], [5, 6], [6, 7], [7, 8],
  [5, 9], [9, 10], [10, 11], [11, 12],
  [9, 13], [13, 14], [14, 15], [15, 16],
  [13, 17], [17, 18], [18, 19], [19, 20],
  [0, 17],
]);

const punktGueltig = (p) => Array.isArray(p) && p.length >= 2 && Number.isFinite(p[0]) && Number.isFinite(p[1]);

/**
 * Linien des Handskeletts im Feld breite × hoehe (Pixel): Punkte in Bildkoordinaten 0…1 (schon gespiegelt, wie gesten.js sie liefert).
 * Ergebnis [[x1, y1, x2, y2], …] oder null, wenn keine Hand im Bild ist bzw. weniger als 21 gültige Punkte da sind.
 * Punkte ausserhalb 0…1 werden an den Rand gesetzt, damit nie etwas ausserhalb des Felds gezeichnet wird.
 */
export function skelettLinien(punkte, breite, hoehe) {
  if (!Array.isArray(punkte) || punkte.length < 21 || !punkte.slice(0, 21).every(punktGueltig)) return null;
  const k = (v) => Math.min(1, Math.max(0, v));
  const p = punkte.map((q) => [k(q[0]) * breite, k(q[1]) * hoehe]);
  return HAND_VERBINDUNGEN.map(([a, b]) => [p[a][0], p[a][1], p[b][0], p[b][1]]);
}

// ---------------------------------------------------------------------------------------------- Punktfolge aufnehmen

/**
 * Datei einer aufgenommenen Punktfolge (gesten.js › aufnahmeBeenden): nur Punkte, nie Bilder. Aus dem Ergebnis werden ausschliesslich version, hand und je Bild
 * t, punkte, welt und haendigkeit übernommen (alles andere, auch ein versehentlich mitgegebenes Bild, fällt weg). Nachtrag hlks-kern 26.10.2026, damit eine
 * Aufnahme im Test genau so abläuft wie live (gesten-logik.test.mjs): die Tiefe z der Bildpunkte (sonst wird die scheinbare Grösse der Faust flach gerechnet),
 * je Bild score und weitere Hände (Zahlen und Händigkeit) sowie – wenn vorhanden – bildbreite, bildhoehe und eingaben (Zeiten der Maus- und Tastendrücke, nötig für
 * die Sperre neben der Maus). Alles nur Zahlen.
 * Ergebnis { name, text, bilder } oder null, wenn nichts aufgenommen wurde.
 */
export function aufnahmeDatei(ergebnis, jetzt = new Date()) {
  const liste = Array.isArray(ergebnis?.bilder) ? ergebnis.bilder : [];
  const hand = (b) => ({
    punkte: Array.isArray(b.punkte) ? b.punkte.map((p) => (punktGueltig(p) ? (Number.isFinite(p[2]) ? [p[0], p[1], p[2]] : [p[0], p[1]]) : null)) : null,
    welt: Array.isArray(b.welt) ? b.welt.map((p) => (Array.isArray(p) ? p.filter((x) => Number.isFinite(x)) : null)) : null,
    haendigkeit: typeof b.haendigkeit === 'string' ? b.haendigkeit : null,
  });
  const bilder = liste.filter((b) => b && typeof b === 'object').map((b) => {
    const x = { t: zahl(b.t) ?? 0, ...hand(b) };
    if (zahl(b.score) !== null) x.score = b.score;
    const weitere = Array.isArray(b.weitere) ? b.weitere.filter((h) => h && typeof h === 'object').map((h) => ({ ...hand(h), ...(zahl(h.score) !== null ? { score: h.score } : {}) })) : [];
    if (weitere.length) x.weitere = weitere;
    return x;
  });
  if (bilder.length === 0) return null;
  const z = (n, l = 2) => String(n).padStart(l, '0');
  const stempel = `${jetzt.getFullYear()}${z(jetzt.getMonth() + 1)}${z(jetzt.getDate())}-${z(jetzt.getHours())}${z(jetzt.getMinutes())}${z(jetzt.getSeconds())}`;
  const daten = { version: zahl(ergebnis?.version) ?? 1, hand: typeof ergebnis?.hand === 'string' ? ergebnis.hand : null, bilder };
  if (zahl(ergebnis?.bildbreite) !== null && zahl(ergebnis?.bildhoehe) !== null) { daten.bildbreite = ergebnis.bildbreite; daten.bildhoehe = ergebnis.bildhoehe; }
  if (Array.isArray(ergebnis?.eingaben)) daten.eingaben = ergebnis.eingaben.filter((x) => zahl(x) !== null);
  return { name: `gestenfolge-${stempel}.json`, text: JSON.stringify(daten), bilder: bilder.length };
}

// ---------------------------------------------------------------------------------------------- Texte des Dialogs

export const DIALOG_TITEL = 'Gesten';
export const DATENSCHUTZ = 'Das Kamerabild bleibt im Browser: es wird weder gespeichert noch gesendet. Ausgewertet werden nur die Punkte der Hand; die Kamera-LED zeigt den Betrieb.';
export const HINWEIS_NEBEN_DER_MAUS = 'Die Gestenhand (Vorgabe links) gestikuliert, die andere bleibt an der Maus. Gesten ändern nie das Modell, nur Ansicht und Navigation.';
export const HINWEIS_STELLSCHRAUBEN = 'Die Empfindlichkeit gilt für alle Gesten. Neben jedem Wert stehen Bereich und Vorgabe; «Vorgabe» setzt eine einzelne Stellschraube zurück.';
export const HINWEIS_STELLSCHRAUBEN_FEHLT = 'Die Empfindlichkeit lässt sich einstellen, sobald die Gestenerkennung in dieser Fassung vorhanden ist.';
export const TESTFELD_LEER = 'Noch keine Geste erkannt. Das Testfeld zeigt erkannte Gesten, ohne etwas auszulösen, solange dieser Dialog offen ist.';
export const TESTFELD_AUS = 'Die Gesten sind aus. Mit «Gesten einschalten» oben beginnt die Erkennung; dann erscheinen die erkannten Gesten hier.';
export const AUFNAHME_HINWEIS = 'Zeichnet die Punkte der Hand auf (nie das Kamerabild) und bietet sie als JSON-Datei zum Speichern an, z. B. als Testfolge für die Gestenerkennung.';
