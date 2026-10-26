// Reine Hilfen der Labor-Seite ohne DOM: Stand im Kopf, Zustand «Brille per adb» für den Teil «Quest App», Liste der Teilnehmer im Kanal (L0), Texte des Registers «Daten» (L1).
// Node-Test: labor.test.mjs. Quellen: GET /api/status (LaborStatus), GET /api/quest/app (QuestApp.cs), GET /api/xr/teilnehmer (XrKanal.cs), GET /api/labor/daten (LaborUebernahme.cs).
// Diese Datei lädt nichts aus KICAD (sie läuft auch in Node ohne den Ordner js/): was sie aus KICAD braucht, bekommt sie als Parameter.

/** Abfrageabstand (ms) für die Teilnehmer im Kanal. */
export const TEILNEHMER_TAKT_MS = 2000;

/** «XR-Labor L1 · Fassung 0.1.0 · Commit 3de9052» aus GET /api/status; ohne Antwort «Stand unbekannt». */
export function standText(s) {
  if (!s || s.app !== 'xr-labor') return 'Stand unbekannt';
  const commit = typeof s.commit === 'string' && /^[0-9a-f]{7,40}$/.test(s.commit) ? `Commit ${s.commit.slice(0, 7)}` : null;
  return ['XR-Labor L1', s.fassung ? `Fassung ${s.fassung}` : null, commit].filter(Boolean).join(' · ');
}

/**
 * Der Teil «Quest App» will wissen, ob adb da und eine Brille freigegeben ist (in KICAD aus GET /api/quest). Das Labor hat diesen Endpunkt nicht und leitet es aus
 * GET /api/quest/app ab: adbGefunden steht dort; «brille» gibt es nur, wenn eine Brille als «device» (freigegeben) verbunden ist.
 */
export function questAusApp(app) {
  return { adbGefunden: app?.adbGefunden === true, freigegeben: !!app?.brille, nichtFreigegeben: false };
}

/**
 * Teilnehmer aus GET /api/xr/teilnehmer als Zeilen für den Menschen: [{ id, text }]. Namen kommen aus dem Netz – die Seite gibt sie nur als Text aus.
 * markeName(id) liefert den Namen der Brillen-App zu «marke» («XR-Labor», «KICAD») oder null; ohne Funktion wird keine Marke gezeigt.
 */
export function teilnehmerZeilen(antwort, markeName = () => null) {
  return (antwort?.teilnehmer ?? []).filter((t) => t && typeof t.id === 'string').map((t) => ({
    id: t.id,
    text: [String(t.brillenName ?? t.name ?? t.brille ?? t.id), t.app ? `App ${String(t.app)}` : null, markeName(t.marke) ? `Marke ${markeName(t.marke)}` : null, t.geraet ? String(t.geraet) : null,
      `seit ${zeit(t.seit)}`, `${Number.isFinite(t.posen) ? t.posen : 0} Posen`].filter(Boolean).join(' · '),
  }));
}

function zeit(iso) {
  const d = new Date(iso);
  if (!iso || Number.isNaN(d.getTime())) return 'unbekannt';
  const z = (n) => String(n).padStart(2, '0');
  return `${z(d.getHours())}:${z(d.getMinutes())}`;
}

// ------------------------------------------------------------------------------------------ Register «Daten» (L1)

/** Datum und Uhrzeit für den Menschen: «26.10.2026 14:05»; ohne gültige Zeit «–». */
export function zeitpunktText(iso) {
  const d = new Date(iso);
  if (!iso || Number.isNaN(d.getTime())) return '–';
  const z = (n) => String(n).padStart(2, '0');
  return `${z(d.getDate())}.${z(d.getMonth() + 1)}.${d.getFullYear()} ${z(d.getHours())}:${z(d.getMinutes())}`;
}

/** Grösse für den Menschen: «512 Bytes», «12.3 KB», «4.2 MB». */
export function groesseText(bytes) {
  const b = Number(bytes);
  if (!Number.isFinite(b) || b < 0) return '–';
  if (b < 1024) return `${Math.round(b)} Bytes`;
  if (b < 1024 * 1024) return `${(b / 1024).toFixed(1)} KB`;
  return `${(b / (1024 * 1024)).toFixed(1)} MB`;
}

const dateien = (n) => `${n ?? 0} ${n === 1 ? 'Datei' : 'Dateien'}`;

/**
 * Die Ablage aus GET /api/labor/daten als Zeilen [Bezeichnung, Wert]: Ordner mit Quelle, Rechner, Schreiben, Messungen, Fragebogen, Profile.
 */
export function ablageZeilen(daten) {
  const a = daten?.ablage;
  if (!a) return [];
  const profile = a.profile ?? {};
  return [
    ['Ordner', `${a.verzeichnis ?? '–'} (${a.quelle ?? '–'})`],
    ['Rechner', a.rechner ?? '–'],
    ['Schreiben', a.erlaubt ? 'erlaubt' : 'gesperrt (Testlauf ausserhalb von %TEMP%)'],
    ['Messungen', `${dateien(a.messungen?.dateien)}, ${groesseText(a.messungen?.bytes)} · alle Rechner`],
    ['Fragebogen', `${dateien(a.fragebogen?.dateien)}, ${groesseText(a.fragebogen?.bytes)}`],
    ['Profile', `${profile.anzahl ?? 0} ${profile.anzahl === 1 ? 'Profil' : 'Profile'}${profile.aktiv ? ` · gilt für die App: ${profile.aktiv}` : ''}`],
  ];
}

/** Was es an bisherigen Daten auf diesem PC gibt (noch nicht oder schon übernommen): Zeilen [Bezeichnung, Wert]. */
export function altZeilen(daten) {
  const a = daten?.alt;
  if (!a) return [];
  const zeilen = [['Ordner (je PC)', a.xrOrdner ?? '–']];
  if (a.lesenErlaubt === false) zeilen.push(['Lesen', 'gesperrt (Testlauf ausserhalb von %TEMP%)']);
  zeilen.push(
    ['Messprotokolle', `${dateien(a.messprotokolle?.dateien)}, ${groesseText(a.messprotokolle?.bytes)}`],
    ['Fragebogen', `${dateien(a.fragebogen?.dateien)}, ${groesseText(a.fragebogen?.bytes)}`],
    ['Profile in den Benutzereinstellungen', `${a.profile?.anzahl ?? 0}${a.profile?.aktiv ? ` · für die App festgelegt: ${a.profile.aktiv}` : ''}`],
  );
  return zeilen;
}

const zahlen = (t, felder) => felder.map(([k, w]) => `${t?.[k] ?? 0} ${w}`).join(', ');

/** Der Merker der letzten Übernahme als Zeilen [Bezeichnung, Wert]; ohne Merker eine Zeile «Noch nicht übernommen». */
export function uebernahmeZeilen(merker) {
  if (!merker) return [['Stand', 'Noch nicht übernommen.']];
  const zeilen = [['Zuletzt', `${zeitpunktText(merker.zeit)} (Rechner ${merker.rechner ?? '–'})`]];
  if (merker.lesenErlaubt === false) zeilen.push(['Hinweis', 'Die Dateien wurden nicht gelesen (Testlauf ausserhalb von %TEMP%).']);
  const datei = [['gefunden', 'gefunden'], ['kopiert', 'kopiert'], ['aktualisiert', 'aktualisiert'], ['uebersprungen', 'doppelt übersprungen'], ['fehler', 'Fehler']];
  zeilen.push(['Messprotokolle', zahlen(merker.messungen, datei)], ['Fragebogen', zahlen(merker.fragebogen, datei)]);
  const p = merker.profile ?? {};
  zeilen.push(['Profile', `${zahlen(p, [['gefunden', 'gefunden'], ['kopiert', 'kopiert'], ['uebersprungen', 'gleich übersprungen'], ['abweichend', 'abweichend (nicht überschrieben)'], ['ungueltig', 'ungültig']])}${p.aktivUebernommen ? ` · für die App festgelegt: ${p.aktivUebernommen}` : ''}`]);
  return zeilen;
}

export const UEBERNAHME_ERKLAERUNG = 'Die bisherigen Messprotokolle, Fragebogen und Profile aus KICAD werden hierher kopiert – nie verschoben, nie gelöscht. Beim ersten Start des Labors geschieht das von selbst; '
  + 'mit «Erneut übernehmen» holt das Labor Neues nach. Gleiche Dateien werden übersprungen, ein im Labor geändertes Profil bleibt unberührt.';
