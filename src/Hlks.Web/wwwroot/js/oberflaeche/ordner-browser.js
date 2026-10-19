// Ordner-Browser (S1, entwurf/startbildschirm.md): statt den Pfad nur einzutippen wählt man einen Ordner über den Server
// (GET /api/ordner: Laufwerke, Unterordner; nur lesend). KICAD-Projekte (projekt.json) und Ordner, die ein Projekt enthalten,
// sind hervorgehoben. Links Schnellzugriff (Projektablage, Laufwerke, zuletzt benutzte Ordner); den Pfad einzutippen bleibt möglich.
//   ordnerWaehlen({ titel, modus: 'oeffnen' | 'ziel', vorgabe, okText, ausfuehren(pfad) }) → Pfad oder null
// modus 'oeffnen': «Öffnen» liefert den Projektordner (e_KICAD); ausfuehren() (z. B. das Öffnen) läuft im Dialog, Fehler bleiben dort.
// modus 'ziel': liefert den gewählten oder eingetippten Ordner, auch wenn es ihn noch nicht gibt (neues Projekt).
// Keine Fachlogik: Erkennung und Auflösung des Projektordners stehen in start-text.js (oeffnenZiel).

import { zeigen } from './dialoge.js';
import { holen } from '../api.js';
import { merken } from '../zustand.js';
import { knoten, fehlerText } from './werkzeug.js';
import { sym, ordnerListeHtml, schnellzugriff, schnellzugriffHtml, ordnerMerken, oeffnenZiel } from './start-text.js';

const MERKEN = 'ordnerZuletzt';

export async function ordnerWaehlen({ titel = 'Ordner wählen', modus = 'oeffnen', vorgabe = '', okText = null, ausfuehren = null } = {}) {
  let daten = null;          // Antwort von /api/ordner für den aktuellen Ordner
  let laufwerke = [];        // aus der Wurzelabfrage
  let ablage = '';
  let gewaehlt = '';
  let nr = 0;                // gegen veraltete Antworten
  let ergebnis = null;
  let ctrlRef = null;

  const w = knoten(`<div class="ob">
    <div class="ob-pfadzeile"><button type="button" class="dlg-knopf ob-hoch" title="Eine Ebene höher">${sym('hoch', 14)} Höher</button>
      <input type="text" class="ob-pfad" spellcheck="false" placeholder="Pfad eintippen, z. B. W:\\02_Projektdateien Objekte" aria-label="Pfad">
      <button type="button" class="dlg-knopf ob-gehe">Gehe zu</button></div>
    <div class="ob-koerper"><nav class="ob-seite" aria-label="Orte"></nav><div class="ob-liste" role="listbox" aria-label="Ordner"><p class="leise">Wird geladen …</p></div></div>
    <p class="ob-fuss leise"></p></div>`);
  const eingabe = w.querySelector('.ob-pfad');
  eingabe.dataset.keinEnter = '1';
  const liste = w.querySelector('.ob-liste');
  const seite = w.querySelector('.ob-seite');
  const fuss = w.querySelector('.ob-fuss');
  const hoch = w.querySelector('.ob-hoch');
  const okKnopf = () => ctrlRef?.fuss.querySelector('.dlg-knopf.primaer');

  function seiteZeichnen() {
    seite.innerHTML = schnellzugriffHtml(schnellzugriff(laufwerke, ablage, merken.lesen(MERKEN, [])), daten?.pfad ?? '');
    seite.querySelectorAll('.ob-ziel').forEach((b) => { b.onclick = () => gehe(b.dataset.pfad).catch(() => {}); });
  }

  function listeZeichnen() {
    liste.innerHTML = ordnerListeHtml(daten, gewaehlt);
    liste.querySelectorAll('.ob-ordner').forEach((b) => {
      b.onclick = () => {
        gewaehlt = b.dataset.pfad;
        eingabe.value = b.dataset.pfad;
        liste.querySelectorAll('.ob-ordner.gewaehlt').forEach((x) => x.classList.remove('gewaehlt'));
        b.classList.add('gewaehlt');
      };
      b.ondblclick = () => {
        if (modus === 'oeffnen' && b.dataset.projekt === '1') { gewaehlt = b.dataset.pfad; eingabe.value = b.dataset.pfad; okKnopf()?.click(); return; }
        gehe(b.dataset.pfad).catch(() => {});
      };
    });
    hoch.disabled = !daten || !daten.pfad;
  }

  /** Zeigt einen Ordner (leer = die Laufwerke). Wirft bei Fehlern; still = ohne Fehlermeldung im Dialog. */
  async function gehe(pfad, still = false) {
    const mein = ++nr;
    liste.classList.add('laedt');
    try {
      const d = await holen(`/api/ordner?pfad=${encodeURIComponent(pfad ?? '')}`);
      if (mein !== nr) return;
      daten = d;
      if (Array.isArray(d.laufwerke)) laufwerke = d.laufwerke;
      if (d.ablage) ablage = d.ablage;
      gewaehlt = '';
      eingabe.value = d.pfad ?? '';
      seiteZeichnen(); listeZeichnen();
      fuss.textContent = modus === 'oeffnen'
        ? 'Ein KICAD-Projekt (blau) ist ein Ordner mit projekt.json; ein Objektordner mit «enthält Projekt» öffnet sein Projekt unter c_Pläne und Modelle\\e_KICAD.'
        : 'Den Ordner wählen oder den Pfad eintippen. Ein neues Projekt braucht einen Ordner, der noch kein Projekt enthält.';
      ctrlRef?.fehler(null);
    } catch (e) {
      if (mein === nr && !still) ctrlRef?.fehler(fehlerText(e));
      throw e;
    } finally { if (mein === nr) liste.classList.remove('laedt'); }
  }

  w.querySelector('.ob-gehe').onclick = () => gehe(eingabe.value.trim()).catch(() => {});
  hoch.onclick = () => { if (daten) gehe(daten.eltern ?? '').catch(() => {}); };
  eingabe.onkeydown = (e) => { if (e.key === 'Enter') { e.preventDefault(); gehe(eingabe.value.trim()).catch(() => {}); } };

  const start = vorgabe || merken.lesen(MERKEN, [])[0] || '';
  const ok = await zeigen({
    titel, inhalt: (ctrl) => { ctrlRef = ctrl; return w; }, breite: 860, klasse: 'dlg-ordner',
    beiOeffnen: () => {
      // erst die Laufwerke (Schnellzugriff), dann der Startordner bzw. die Projektablage; schlägt das fehl, bleibt die Liste der Laufwerke
      gehe('', true).catch(() => {})
        .then(() => { const z = start || ablage; return z ? gehe(z, true) : null; })
        .catch(() => { eingabe.value = ''; });
      eingabe.focus();
    },
    knoepfe: [
      { text: 'Abbrechen', wert: false },
      {
        text: okText ?? (modus === 'oeffnen' ? 'Öffnen' : 'Wählen'), wert: true, primaer: true,
        pruefen: () => {
          const pfad = modus === 'oeffnen' ? oeffnenZiel({ eingabe: eingabe.value, daten, gewaehlt }) : eingabe.value.trim();
          if (!pfad) return modus === 'oeffnen' ? 'Bitte ein Projekt wählen oder den Pfad eintippen.' : 'Bitte einen Ordner wählen oder den Pfad eintippen.';
          ergebnis = pfad;
          return true;
        },
        klick: async () => {
          if (ausfuehren) await ausfuehren(ergebnis);
          if (daten?.pfad) merken.schreiben(MERKEN, ordnerMerken(merken.lesen(MERKEN, []), daten.pfad));
        },
      },
    ],
  });
  return ok === true ? ergebnis : null;
}
