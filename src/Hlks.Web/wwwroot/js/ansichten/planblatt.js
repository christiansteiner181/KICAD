// Planblatt-Ansicht: zeigt das PDF des Kerns (/api/plan/{kennzeichen}.pdf) in einem <iframe>; der eingebaute PDF-Betrachter
// des Browsers übernimmt Zoomen, Blättern und Drucken. Das Blatt ist Papier und bleibt in jedem Design hell. Nach jeder
// Änderung am Modell wird das PDF neu geholt (entprellt, 1 s); ist die Ansicht gerade verdeckt, erst beim Anzeigen.
// Keine Fachlogik: Aufbau, Plankopf und Ansichtsfenster erzeugt der Kern.

import { esc } from './hilfen.js';

const ENTPRELLUNG = 1000;   // ms zwischen Änderung und Neuladen des PDFs

/** Adresse des PDFs eines Planblatts. */
export const pdfUrl = (kennzeichen) => `/api/plan/${encodeURIComponent(kennzeichen)}.pdf`;

function erstellen(behaelter, blattElement) {
  let blatt = blattElement;
  let ladeNr = 0;
  let timer = null;
  let veraltet = false;        // Änderung kam, während die Ansicht verdeckt war
  let zerstoert = false;
  let blobUrl = null;

  behaelter.innerHTML = `<div class="bl-wurzel">
      <iframe class="bl-rahmen" title="Planblatt"></iframe>
      <div class="bl-hinweis" hidden></div>
    </div>`;
  const wurzel = behaelter.querySelector('.bl-wurzel');
  const rahmen = wurzel.querySelector('.bl-rahmen');
  const hinweis = wurzel.querySelector('.bl-hinweis');

  const sichtbar = () => wurzel.clientWidth > 1 && wurzel.clientHeight > 1;

  async function laden() {
    if (zerstoert) return;
    if (!sichtbar()) { veraltet = true; return; }
    veraltet = false;
    const nr = ++ladeNr;
    try {
      const antwort = await fetch(pdfUrl(blatt.kennzeichen), { cache: 'no-store' });
      if (nr !== ladeNr || zerstoert) return;
      if (!antwort.ok) {
        let f = {};
        try { f = await antwort.json(); } catch { /* kein JSON */ }
        throw Object.assign(new Error(f.fehler || `Fehler ${antwort.status}`), { vorschlag: f.vorschlag });
      }
      const url = URL.createObjectURL(await antwort.blob());
      if (nr !== ladeNr || zerstoert) { URL.revokeObjectURL(url); return; }
      const alt = blobUrl;
      blobUrl = url;
      rahmen.src = `${url}#toolbar=1&navpanes=0&view=Fit`;
      hinweis.hidden = true;
      if (alt) setTimeout(() => URL.revokeObjectURL(alt), 5000);
    } catch (e) {
      if (nr !== ladeNr || zerstoert) return;
      console.error('Planblatt: PDF nicht geladen', e);
      hinweis.hidden = false;
      hinweis.innerHTML = `Das Planblatt konnte nicht erzeugt werden:<br>${esc(e.message ?? e)}${e.vorschlag ? '<br>' + esc(e.vorschlag) : ''}`;
    }
  }

  function planen(ms = ENTPRELLUNG) {
    clearTimeout(timer);
    timer = setTimeout(laden, ms);
  }

  // Wieder sichtbar geworden (Reiter gewechselt): nachholen, was verpasst wurde
  const beobachter = typeof ResizeObserver === 'function' ? new ResizeObserver(() => { if (veraltet && sichtbar()) planen(0); }) : null;
  beobachter?.observe(wurzel);

  laden();

  return {
    aktualisieren(ereignis) { if (!ereignis || ereignis.alles) planen(0); else planen(); },
    ansichtGeaendert(b) { blatt = b ?? blatt; planen(); },
    auswahlGeaendert() { /* das Blatt kennt keine Auswahl */ },
    zoomAlles() { /* Zoom macht der PDF-Betrachter */ },
    zerstoeren() {
      zerstoert = true;
      ladeNr++;
      clearTimeout(timer);
      beobachter?.disconnect();
      rahmen.removeAttribute('src');
      if (blobUrl) URL.revokeObjectURL(blobUrl);
      behaelter.replaceChildren();
    },
  };
}

export default { erstellen };
