// Aktion «Baustelle (Webseite)» (BIM to field Stufe 1, entwurf/bim-to-field.md): Knopf im Register Datei, Gruppe «Ausgabe». Kleiner Dialog mit
// Rolle (Monteur | Bauleitung) und Stand (freigegeben | Arbeitsstand – Monteur nur freigegeben, Entscheid Christian 02.11.2026). Der Export läuft
// im Kern (Abfrage baustelle_export, eine HTML-Datei in <Projekt>\ausgabe\baustelle); danach öffnet sich die Seite (GET /api/baustelle/datei).

import { abfrage, ApiFehler } from '../api.js';
import { zustand } from '../zustand.js';
import { registrieren } from './register.js';
import { zeigen, meldung } from './dialoge.js';
import { knoten } from './werkzeug.js';

registrieren({
  id: 'datei.baustelle-export', titel: 'Baustelle (Webseite)', symbol: 'bildschirm', kuerzel: null, register: 'datei', gruppe: 'Ausgabe',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => baustelleDialog(),
});

export function baustelleDialog() {
  const w = knoten(`<div class="bs-dialog" style="display:grid;gap:10px">
    <div><b>Rolle</b><br>
      <label class="haken"><input type="radio" name="bs-rolle" value="monteur" checked> Monteur – nur freigegebener Stand</label><br>
      <label class="haken"><input type="radio" name="bs-rolle" value="bauleitung"> Bauleitung (Projekt- und Bauleiter)</label></div>
    <div><b>Stand</b><br>
      <label class="haken"><input type="radio" name="bs-stand" value="freigegeben" checked> Freigegeben – mit meinem Namen als Freigabe</label><br>
      <label class="haken"><input type="radio" name="bs-stand" value="arbeitsstand"> Arbeitsstand – auf jeder Seite als «nicht freigegeben» vermerkt</label></div>
    <div class="leise">Eine einzige HTML-Datei mit 3D (inkl. Architekten-IFC), Grundrissen, Schemas, Elementinfo und Materialauszug. Läuft offline
      (Tablet, USB-Stick, Mailanhang). Ziel: Projektordner ausgabe\\baustelle; der Export kommt ins Ausgabeprotokoll.</div>
    <div class="bs-ergebnis"></div>
  </div>`);
  const q = (s) => w.querySelector(s);
  const stimmen = () => {
    const monteur = q('input[name="bs-rolle"]:checked').value === 'monteur';
    const arbeit = q('input[name="bs-stand"][value="arbeitsstand"]');
    arbeit.disabled = monteur; if (monteur) q('input[name="bs-stand"][value="freigegeben"]').checked = true;
  };
  for (const r of w.querySelectorAll('input[name="bs-rolle"]')) r.addEventListener('change', stimmen);
  stimmen();

  async function exportieren() {
    const par = { rolle: q('input[name="bs-rolle"]:checked').value, stand: q('input[name="bs-stand"]:checked').value };
    q('.bs-ergebnis').textContent = 'Webseite wird erzeugt …';
    try {
      const erg = await abfrage('baustelle_export', par);
      const mb = (erg.groesseBytes / 1048576).toFixed(1);
      q('.bs-ergebnis').innerHTML = '';
      q('.bs-ergebnis').append(knoten(`<div><b>Erzeugt:</b> <code></code><br>${erg.elemente} Elemente, ${erg.ansichten} Pläne, ${erg.geschosse3d} Geschosse in 3D, ${mb} MB</div>`));
      q('.bs-ergebnis code').textContent = erg.datei;
      window.open('/api/baustelle/datei?pfad=' + encodeURIComponent(erg.datei), '_blank');
      meldung('Baustellen-Webseite erzeugt: ' + erg.datei, 'ok');
    } catch (f) {
      q('.bs-ergebnis').textContent = f instanceof ApiFehler ? `${f.message}${f.vorschlag ? ' – ' + f.vorschlag : ''}` : String(f);
    }
  }

  return zeigen({
    titel: 'Baustelle (Webseite)', inhalt: w, breite: 560,
    knoepfe: [{ text: 'Schliessen', wert: true }, { text: 'Erzeugen', primaer: true, schliesst: false, klick: () => exportieren() }],
  });
}
