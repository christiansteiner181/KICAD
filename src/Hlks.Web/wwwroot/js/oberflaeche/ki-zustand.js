// Zustand der KI-Palette «Claude» (Modul 3c, Teil O) – rein, ohne DOM, in Node getestet (ki-palette.test.mjs).
// Der Verlauf kommt immer vom Server (GET /api/ki/gespraeche/{id}); hier wird nur zusammengesetzt, was der Ereignisstrom danach liefert:
//   frage → text* / werkzeug* / aenderung* → fertig | fehler | abgebrochen        (Ereignisse { art: 'ki', gespraech, frage, typ, … })
// Die Verarbeitung verträgt doppelte und verspätete Ereignisse (jede Wirkung ist wiederholbar: nach Id ersetzen, nie blind anhängen), Ereignisse
// vor dem «frage» (Palette mitten im Lauf geöffnet) und Ereignisse nach dem Ende (werden ignoriert). Der Text einer Antwort im Entstehen
// wird aus den Teilstücken zusammengesetzt (Zähler «ab» des Servers: was ein nachgeladener Stand schon enthält, wird nicht doppelt angehängt);
// die fertige Nachricht ersetzt ihn vollständig. Nachrichten gehören nie in den Browserspeicher.
//
//   neu() · gespraechSetzen(z, daten) · ereignisAnwenden(z, ev) · frageGesendet(z, antwort) · listeSetzen(z, daten) · zurueckgenommen(z, paket, rueckgaengig)
//   nachricht(z, rolle, frage) · claudeNachricht(z, frage) · laeuft(z)

export function neu() {
  return { gespraeche: [], aktuell: null, gespraech: null };
}

const kopie = (x) => JSON.parse(JSON.stringify(x));

export function listeSetzen(z, daten) {
  z.gespraeche = Array.isArray(daten?.gespraeche) ? daten.gespraeche.map((g) => ({ ...g })) : [];
  return z;
}

/** Das Gespräch, wie der Server es liefert (die Antwort im Entstehen ungekürzt: die «ab»-Zähler der Text-Ereignisse beziehen sich auf ihre Zeichen). */
export function gespraechSetzen(z, daten) {
  if (!daten) { z.aktuell = null; z.gespraech = null; return z; }
  const g = kopie(daten);
  g.nachrichten = Array.isArray(g.nachrichten) ? g.nachrichten : [];
  for (const n of g.nachrichten) {
    n.werkzeuge ??= [];
    n.aenderungen ??= [];
  }
  z.aktuell = g.id;
  z.gespraech = g;
  return z;
}

export const laeuft = (z) => !!z.gespraech && (z.gespraech.laeuft === true || z.gespraech.nachrichten.some((n) => n.rolle === 'claude' && n.status === 'laeuft'));
export const nachricht = (z, rolle, frage) => z.gespraech?.nachrichten.find((n) => n.rolle === rolle && n.frage === frage) ?? null;

/** Die Nachricht von Claude zu einer Frage; fehlt sie (Palette mitten im Lauf), entsteht sie als Platzhalter. */
export function claudeNachricht(z, frage, vorlage = {}) {
  let n = nachricht(z, 'claude', frage);
  if (!n && z.gespraech) {
    n = { rolle: 'claude', frage, status: 'laeuft', text: '', zeit: new Date().toISOString(), werkzeuge: [], aenderungen: [], ...vorlage };
    z.gespraech.nachrichten.push(n);
  }
  return n;
}

const ersetzen = (liste, schluessel, wert) => {
  const i = liste.findIndex((x) => x[schluessel] === wert[schluessel]);
  if (i >= 0) liste[i] = { ...liste[i], ...wert }; else liste.push(wert);
};

/**
 * Wendet ein Ereignis an (verändert z). Ergebnis, was die Darstellung tun soll:
 *   { ignoriert: true }                  nichts (anderes Gespräch, schon beendet, kein KI-Ereignis)
 *   { liste: true }                      Gesprächsliste neu holen
 *   { gespraech: true }                  alles neu zeichnen
 *   { nachricht: frage, rolle? }         nur die Nachricht(en) dieser Frage neu zeichnen
 */
export function ereignisAnwenden(z, ev) {
  if (!ev || ev.art !== 'ki') return { ignoriert: true };
  if (ev.typ === 'gespraeche') return { liste: true };
  const g = z.gespraech;
  if (!g || ev.gespraech !== g.id) return { ignoriert: true };

  if (ev.typ === 'zurueckgenommen') {
    return zurueckgenommen(z, ev.paket, ev.rueckgaengig) ? { gespraech: true } : { ignoriert: true };
  }
  const frage = ev.frage;
  if (!frage) return { ignoriert: true };

  switch (ev.typ) {
    case 'frage': {
      if (ev.nachricht && !nachricht(z, 'mensch', frage)) g.nachrichten.push({ werkzeuge: [], aenderungen: [], ...kopie(ev.nachricht), rolle: 'mensch', frage });
      const c = claudeNachricht(z, frage, { modell: ev.nachricht?.modell, internet: ev.nachricht?.internet, internetHinweis: ev.nachricht?.internetHinweis });
      // Die Antwort der Frage (POST) kann nach dem Ende der Antwort eintreffen (schnelle Antwort): ein beendeter Lauf bleibt beendet
      if (c.status === 'laeuft') { g.laeuft = true; g.frage = frage; }
      return { nachricht: frage };
    }
    case 'text': {
      const n = claudeNachricht(z, frage);
      if (n.status !== 'laeuft') return { ignoriert: true };   // verspätet: die fertige Nachricht gilt
      const teil = String(ev.teil ?? '');
      if (!teil) return { ignoriert: true };
      g.laeuft = true; g.frage = frage;
      const bisher = (n.text ?? '').length;
      if (typeof ev.ab === 'number') {
        // «ab» = Zeichen vor diesem Teil (Server): was ein nachgeladener Stand der Antwort (GET) schon enthält, nicht noch einmal anhängen
        if (ev.ab + teil.length <= bisher) return { ignoriert: true };
        n.text = (n.text ?? '') + (ev.ab < bisher ? teil.slice(bisher - ev.ab) : teil);
      } else n.text = (n.text ?? '') + teil;
      return { nachricht: frage };
    }
    case 'werkzeug': {
      const n = claudeNachricht(z, frage);
      if (n.status !== 'laeuft' || !ev.werkzeug?.id) return { ignoriert: true };
      g.laeuft = true; g.frage = frage;
      ersetzen(n.werkzeuge, 'id', { ...ev.werkzeug });
      return { nachricht: frage };
    }
    case 'aenderung': {
      const n = claudeNachricht(z, frage);
      if (!ev.aenderung?.paket) return { ignoriert: true };
      ersetzen(n.aenderungen, 'paket', { ...ev.aenderung });
      return { nachricht: frage };
    }
    case 'fertig':
    case 'fehler':
    case 'abgebrochen': {
      const n = claudeNachricht(z, frage);
      const fertig = ev.nachricht ? { werkzeuge: [], aenderungen: [], ...kopie(ev.nachricht), rolle: 'claude', frage } : null;
      const i = g.nachrichten.indexOf(n);
      if (fertig) g.nachrichten[i] = fertig;
      else { n.status = ev.typ === 'fertig' ? 'fertig' : ev.typ === 'abgebrochen' ? 'abgebrochen' : 'fehler'; if (ev.fehler) n.fehler = ev.fehler; if (ev.fehlerart) n.fehlerart = ev.fehlerart; }
      const nach = g.nachrichten[i];
      if (ev.typ === 'fehler') { nach.status = 'fehler'; if (ev.fehler && !nach.fehler) nach.fehler = ev.fehler; if (ev.fehlerart && !nach.fehlerart) nach.fehlerart = ev.fehlerart; }
      if (g.frage === frage) { g.laeuft = false; g.frage = null; }
      return { nachricht: frage, ende: true };
    }
    default:
      return { ignoriert: true };
  }
}

/** Antwort von POST …/frage ({ gespraech, frage, nachricht }) wie das Ereignis «frage» behandeln – egal, was zuerst ankommt. */
export function frageGesendet(z, antwort) {
  if (!antwort) return { ignoriert: true };
  return ereignisAnwenden(z, { art: 'ki', gespraech: antwort.gespraech, frage: antwort.frage, typ: 'frage', nachricht: antwort.nachricht });
}

/** Ein Paket gilt als zurückgenommen (Antwort des Servers oder Ereignis, auch aus einem anderen Fenster). */
export function zurueckgenommen(z, paket, rueckgaengig = null) {
  let geaendert = false;
  for (const n of z.gespraech?.nachrichten ?? []) {
    for (const a of n.aenderungen ?? []) {
      if (a.paket === paket && !a.zurueckgenommen) { a.zurueckgenommen = true; if (rueckgaengig) a.zurueckgenommenPaket = rueckgaengig; geaendert = true; }
    }
  }
  return geaendert;
}
