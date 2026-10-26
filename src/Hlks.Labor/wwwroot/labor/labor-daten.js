// Register «Daten» der Labor-Seite (L1): wo die Labor-Daten liegen (OneDrive, labor\), was an bisherigen Daten da ist, Stand der Übernahme mit Zahlen und der Knopf «Erneut
// übernehmen». Keine Logik hier: alles läuft über GET /api/labor/daten und POST /api/labor/uebernahme (LaborUebernahme.cs); die Texte macht labor-text.js.
// Werte vom Server gehen nur als Text (textContent) auf die Seite.

import { holen, senden } from '../js/api.js';
import { ablageZeilen, altZeilen, uebernahmeZeilen, UEBERNAHME_ERKLAERUNG } from './labor-text.js';

function el(tag, klasse, text) {
  const e = document.createElement(tag);
  if (klasse) e.className = klasse;
  if (text !== undefined) e.textContent = text;
  return e;
}

/** Eine Tabelle Bezeichnung/Wert; Werte nur als Text. */
function tabelle(titel, zeilen) {
  const t = el('table', 'xl-tabelle labor-daten-tabelle');
  t.setAttribute('aria-label', titel);
  const body = el('tbody');
  for (const [k, w] of zeilen) {
    const tr = el('tr');
    const th = el('th', null, k);
    th.scope = 'row';
    tr.append(th, el('td', null, w));
    body.append(tr);
  }
  t.append(body);
  return t;
}

/** Baut den Bereich «Daten». Gibt { knoten, beiAnzeigen } zurück: beiAnzeigen() liest die Daten neu (beim Wechsel auf den Reiter). */
export function datenBereich() {
  const wurzel = el('div', 'labor-daten');
  const meldung = el('div', 'xl-meldung');
  meldung.hidden = true;
  meldung.setAttribute('role', 'status');
  const ablage = el('div', 'labor-daten-block');
  const alt = el('div', 'labor-daten-block');
  const uebernahme = el('div', 'labor-daten-block');
  const knopf = el('button', 'dlg-knopf primaer labor-uebernehmen', 'Erneut übernehmen');
  knopf.type = 'button';
  knopf.title = 'Kopiert die bisherigen Messprotokolle, Fragebogen und Profile erneut in die Ablage des Labors (nie verschieben, nie löschen)';

  const block = (titel, platz, vorspann) => {
    const b = el('section', 'labor-daten-teil');
    b.append(el('h3', 'xl-gruppe-titel', titel));
    if (vorspann) b.append(el('div', 'leise xl-klein', vorspann));
    b.append(platz);
    return b;
  };
  wurzel.append(
    meldung,
    block('Ablage der Labor-Daten', ablage, 'Je Benutzer über OneDrive (%OneDrive%\\HLKS-CAD\\labor); Dateinamen tragen den Rechnernamen, damit zwei Rechner nie dieselbe Datei schreiben.'),
    block('Bisherige Daten auf diesem PC', alt, 'Was KICAD bisher gesammelt hat (%LOCALAPPDATA%\\HLKS_CAD\\xr und die Benutzereinstellungen).'),
    block('Übernahme', uebernahme, UEBERNAHME_ERKLAERUNG),
  );
  wurzel.append(knopf);

  let laeuft = false;
  let meldungTimer = null;
  function meldungZeigen(art, text) {
    meldung.hidden = !text;
    meldung.className = `xl-meldung ${art === 'fehler' ? 'fehler' : ''}`.trim();
    meldung.textContent = text ?? '';
    clearTimeout(meldungTimer);
    if (text && art === 'ok') meldungTimer = setTimeout(() => { meldung.hidden = true; }, 8000);
  }

  function zeichnen(daten) {
    ablage.replaceChildren(tabelle('Ablage', ablageZeilen(daten)));
    alt.replaceChildren(tabelle('Bisherige Daten', altZeilen(daten)));
    uebernahme.replaceChildren(tabelle('Übernahme', uebernahmeZeilen(daten?.uebernahme)));
  }

  async function lesen() {
    try { zeichnen(await holen('/api/labor/daten')); }
    catch (err) { meldungZeigen('fehler', err?.message || String(err)); }
  }

  knopf.addEventListener('click', async () => {
    if (laeuft) return;
    laeuft = true;
    knopf.disabled = true;
    try {
      const m = await senden('/api/labor/uebernahme', {});
      const k = (m.messungen?.kopiert ?? 0) + (m.messungen?.aktualisiert ?? 0);
      meldungZeigen('ok', `Übernommen: ${k} Messprotokolle, ${m.fragebogen?.kopiert ?? 0} Fragebogen, ${m.profile?.kopiert ?? 0} Profile neu kopiert.`);
      await lesen();
    } catch (err) { meldungZeigen('fehler', `${err?.message || String(err)}${err?.vorschlag ? ` Vorschlag: ${err.vorschlag}` : ''}`); }
    finally { laeuft = false; knopf.disabled = false; }
  });

  return { knoten: wurzel, beiAnzeigen: lesen };
}
