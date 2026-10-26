// Abbruchprotokoll der Gesten (Modul G, Abnahme Christian 26.10.2026: «speichere weshalb die gesten nach, 1-2 sek. abbrechen»).
// Hört auf die Ereignisse { art: 'abbruch' } der Gestensteuerung und schickt sie an den Server (POST /api/gesten/abbruch, GestenProtokoll.cs), der sie lokal in
// %LOCALAPPDATA%\HLKS_CAD\gesten\abbrueche.jsonl ablegt. Bewusst ausserhalb von js/gesten/: dort spricht nichts mit dem Server (gesten-datenschutz.test.mjs).
// Weiter geht nur { geste, dauerMs, grund, werte { Name: Zahl } } – nie Punkte oder Bilder.

/** Nur die erlaubten Felder eines Abbruchs (rein, testbar). */
export function abbruchEintrag(e) {
  if (e?.art !== 'abbruch') return null;
  const werte = {};
  for (const [k, v] of Object.entries(e.werte ?? {}).slice(0, 12)) if (v === null || Number.isFinite(v)) werte[k] = v;
  return { geste: typeof e.geste === 'string' ? e.geste.slice(0, 40) : null, dauerMs: Number.isFinite(e.dauerMs) ? e.dauerMs : null, grund: typeof e.grund === 'string' ? e.grund.slice(0, 40) : null, werte };
}

/** Abonniert die Steuerung; Rückgabe: abmelden. Fehler beim Senden bleiben still (das Protokoll ist ein Hilfsmittel, keine Funktion). */
export function gestenProtokollEinrichten(steuerung, senden = (k) => fetch('/api/gesten/abbruch', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(k) })) {
  return steuerung.abonnieren((e) => {
    const k = abbruchEintrag(e);
    if (k) Promise.resolve().then(() => senden(k)).catch(() => { /* still */ });
  });
}
