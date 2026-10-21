// Eigenschaftenpalette, Nachbearbeitung N13 (3D-Schnittbereich): Zeilen für die Eigenschaft «schnittbereich» einer 3D-Ansicht (Schalter, Masse, Knöpfe
// «An Auswahl anpassen», «Auf Geschoss», «Zurücksetzen») und der Text zur «startansicht». Reine Funktionen, die Markup liefern (in Node getestet:
// ansichten/schnittbereich.test.mjs); die Klicks verdrahtet eigenschaften.js mit den Aktionen ansicht.schnittbereich* (aktionen-ansicht.js).

import { esc } from '../ansichten/hilfen.js';
import { massText, schnittbereichLesen } from '../ansichten/schnittbereich-logik.js';
import { startansichtLesen } from '../ansichten/wuerfel-logik.js';

const HINWEIS = 'Nur was im Bereich liegt, ist sichtbar (wie Revit). Beim Einschalten umschliesst er das ganze Modell; die Griffe an den Flächen verschieben sie.';

/** Zeilen für die Eigenschaft schnittbereich (roh = Wert der Ansicht oder undefined). H = { zeile(label, feld, opt), nurLesen(text) } der Palette. */
export function schnittbereichZeilen(roh, H) {
  const sb = schnittbereichLesen(roh);
  const z = [];
  z.push(H.zeile('Schnittbereich',
    `<label class="ep-sb-kopf" title="${esc(HINWEIS)}"><input type="checkbox" class="ep-sb-schalter" ${sb?.aktiv ? 'checked' : ''}> 3D-Schnittbereich</label>`,
    { titel: HINWEIS }));
  z.push(H.zeile('Masse', H.nurLesen(sb ? (sb.aktiv ? massText(sb) : `ausgeschaltet (${massText(sb)})`) : 'nicht gesetzt – beim Einschalten das ganze Modell'),
    { klasse: 'klein', titel: 'Länge × Breite × Höhe des Quaders in den Achsen der Gebäudeausrichtung' }));
  z.push(H.zeile('',
    `<span class="ep-sb-knoepfe"><button type="button" class="ep-sb-auswahl" title="Quader um das gewählte Element plus 300 mm Rand">An Auswahl anpassen</button>`
    + `<button type="button" class="ep-sb-geschoss" title="Höhe auf Oberkante und Unterkante eines Geschosses">Auf Geschoss</button>`
    + `<button type="button" class="ep-sb-zurueck" title="Zurück auf das ganze Modell">Zurücksetzen</button></span>`,
    { klasse: 'ep-breit' }));
  return z;
}

/** Text zur Eigenschaft startansicht: «Isometrie von vorne rechts oben (Vorgabe)» oder die gemerkten Winkel. */
export function startansichtText(roh) {
  const s = startansichtLesen(roh);
  if (!s) return 'Isometrie von vorne rechts oben (Vorgabe)';
  const f = (x) => (Math.round(x * 10) / 10).toFixed(1).replace('-', '−');
  return `Blick ${f(s.azimutGrad)}° / ${f(s.polarGrad)}° zur Gebäudeausrichtung (Haus-Knopf des Würfels)`;
}
