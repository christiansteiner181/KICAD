// Reine Hilfen der Labor-Seite (L0) ohne DOM: Stand im Kopf, Zustand «Brille per adb» für den Teil «Quest App», Liste der Teilnehmer im Kanal.
// Node-Test: labor.test.mjs. Quellen: GET /api/status (LaborStatus), GET /api/quest/app (QuestApp.cs), GET /api/xr/teilnehmer (XrKanal.cs).

/** Abfrageabstand (ms) für die Teilnehmer im Kanal. */
export const TEILNEHMER_TAKT_MS = 2000;

/** «Fassung 0.1.0 · Commit 3de9052» aus GET /api/status; ohne Antwort «Stand unbekannt». */
export function standText(s) {
  if (!s || s.app !== 'xr-labor') return 'Stand unbekannt';
  const commit = typeof s.commit === 'string' && /^[0-9a-f]{7,40}$/.test(s.commit) ? `Commit ${s.commit.slice(0, 7)}` : null;
  return ['XR-Labor L0', s.fassung ? `Fassung ${s.fassung}` : null, commit].filter(Boolean).join(' · ');
}

/**
 * Der Teil «Quest App» will wissen, ob adb da und eine Brille freigegeben ist (in KICAD aus GET /api/quest). Das Labor hat diesen Endpunkt nicht und leitet es aus
 * GET /api/quest/app ab: adbGefunden steht dort; «brille» gibt es nur, wenn eine Brille als «device» (freigegeben) verbunden ist.
 */
export function questAusApp(app) {
  return { adbGefunden: app?.adbGefunden === true, freigegeben: !!app?.brille, nichtFreigegeben: false };
}

/** Teilnehmer aus GET /api/xr/teilnehmer als Zeilen für den Menschen: [{ id, text }]. Namen kommen aus dem Netz – die Seite gibt sie nur als Text aus. */
export function teilnehmerZeilen(antwort) {
  return (antwort?.teilnehmer ?? []).filter((t) => t && typeof t.id === 'string').map((t) => ({
    id: t.id,
    text: [String(t.brillenName ?? t.name ?? t.brille ?? t.id), t.app ? `App ${String(t.app)}` : null, t.geraet ? String(t.geraet) : null,
      `seit ${zeit(t.seit)}`, `${Number.isFinite(t.posen) ? t.posen : 0} Posen`].filter(Boolean).join(' · '),
  }));
}

function zeit(iso) {
  const d = new Date(iso);
  if (!iso || Number.isNaN(d.getTime())) return 'unbekannt';
  const z = (n) => String(n).padStart(2, '0');
  return `${z(d.getHours())}:${z(d.getMinutes())}`;
}
