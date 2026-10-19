// Dialog «Liste als CSV» (Nachbearbeitung N3, Punkt 3): Knopf «CSV …» in der Kopfzeile jeder Listenansicht (liste.js).
// Fragt nur den Dateinamen und lädt die Datei über den Browser herunter (Blob, kein Server nötig; sie landet im Download-Ordner des Browsers).
// Den Inhalt macht liste-csv.js (UTF-8 mit BOM, Semikolon, Kopfzeile). Wird von liste.js erst beim Klick geladen (import()).

import { zeigen, meldung } from '../oberflaeche/dialoge.js';
import { esc, knoten } from '../oberflaeche/werkzeug.js';
import { csvAus, csvZeilenzahl, csvDateiname, csvDateinameSauber, MIME } from './liste-csv.js';

/** Lädt einen Text unter dem Dateinamen herunter. */
export function herunterladen(text, dateiname) {
  const blob = new Blob([text], { type: MIME });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = dateiname;
  a.hidden = true;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10000);
}

/** Zeigt den Dialog zu einer aufbereiteten Liste und erzeugt bei «Erzeugen» die Datei. sortierung: Map Tabellenindex → { schluessel, absteigend } der Ansicht. */
export async function csvDialog(daten, sortierung = new Map()) {
  if (!daten) { meldung('Die Liste ist noch nicht geladen.', 'info'); return false; }
  const n = csvZeilenzahl(daten);
  const w = knoten(`<div class="csv-dlg">
    <p class="dlg-text">«${esc(daten.titel)}» mit ${n} ${n === 1 ? 'Zeile' : 'Zeilen'} als CSV-Datei speichern. Spalten und Reihenfolge wie in der Liste.</p>
    <label class="fz"><span>Dateiname</span><input type="text" class="csv-name" value="${esc(csvDateiname(daten))}"></label>
    <p class="leise csv-format">Format: UTF-8 mit BOM, Semikolon als Trenner, Dezimalkomma, erste Zeile = Spaltentitel. Die Datei wird vom Browser in den Download-Ordner gelegt.</p>
  </div>`);
  const name = () => w.querySelector('.csv-name').value;
  const ok = await zeigen({
    titel: 'Liste als CSV', inhalt: w, breite: 520, art: 'liste-csv',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Erzeugen', wert: true, primaer: true, pruefen: () => (name().trim() ? true : 'Der Dateiname darf nicht leer sein.') }],
  });
  if (ok !== true) return false;
  const datei = csvDateinameSauber(name(), csvDateiname(daten));
  herunterladen(csvAus(daten, sortierung), datei);
  meldung(`CSV erzeugt: ${datei} (${n} ${n === 1 ? 'Zeile' : 'Zeilen'}).`, 'ok');
  return true;
}
