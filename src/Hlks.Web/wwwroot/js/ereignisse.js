// Ereignisstrom vom Server (Server-Sent Events): jede Änderung – eigene, fremde (anderer Arbeitsplatz) und die der KI –
// kommt hier an und wird an alle Ansichten weitergegeben. Der Browser verbindet sich bei Abbruch selbst neu.

import { zustand, melden, nachladen, modellLaden, projektLeeren } from './zustand.js';
import { holen } from './api.js';

let quelle = null;
let warteschlange = Promise.resolve();
let unterbrochen = false;

export function verbinden() {
  quelle?.close();
  quelle = new EventSource('/api/ereignisse');
  quelle.addEventListener('hallo', () => {
    melden('verbindung', { ok: true });
    // Nach einer Unterbrechung fehlen die Ereignisse dazwischen: Projektstand neu holen und alles neu laden
    if (unterbrochen) {
      unterbrochen = false;
      warteschlange = warteschlange.then(async () => projektGewechselt(await holen('/api/status')))
        .catch(err => console.error('Neu laden nach Unterbrechung', err));
    }
  });
  quelle.onerror = () => { unterbrochen = true; melden('verbindung', { ok: false }); };
  quelle.onmessage = (e) => {
    let d;
    try { d = JSON.parse(e.data); } catch { return; }
    // nacheinander abarbeiten, damit sich Nachladevorgänge nicht überholen
    warteschlange = warteschlange.then(() => behandeln(d)).catch(err => console.error('Ereignis', d, err));
  };
}

async function behandeln(d) {
  switch (d.art) {
    case 'aenderung':
      if (d.stand) zustand.stand = d.stand;
      await nachladen(d.ids ?? [], { quelle: d.quelle, akteur: d.akteur, akteurArt: d.akteurArt, paket: d.paket });
      break;
    case 'projekt':
      await projektGewechselt(await holen('/api/status'));
      break;
    case 'abgleich':
      melden('abgleich', d);
      break;
    case 'konflikt':
      melden('konflikt', d);
      break;
    case 'auftrag':   // KI-Lauf «Familie aus Unterlagen» (Modul 2): {auftrag: {id, status, schritte, …}} – familien/auftrag.js führt den Stand nach
      melden('auftrag', d);
      break;
  }
}

// Projekt auf dem Server geöffnet, gewechselt oder geschlossen – auch aus einem anderen Fenster oder von der KI.
// Hat dieses Fenster den Wechsel selbst ausgelöst, ist zustand.geladen schon nachgeführt und es bleibt beim Status.
async function projektGewechselt(status) {
  const dialoge = await import('./oberflaeche/dialoge.js');
  if (status.offen && status.ort !== zustand.geladen) { await dialoge.projektGeoeffnet(status); return; }
  zustand.status = status;
  melden('status', status);
  if (status.offen) { await modellLaden(); return; }
  if (zustand.geladen === null) return;
  const fenster = await import('./fenster.js');
  for (const o of fenster.alle()) fenster.schliessen(o.id);
  projektLeeren();
  document.getElementById('titel').textContent = 'HLKS-CAD';
  dialoge.projektStart().catch(e => console.error('Startdialog', e));   // nicht warten: der Dialog bleibt offen, Ereignisse laufen weiter
}
