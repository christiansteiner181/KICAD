// Leitungen ändern über die Palette und das Register (Modul 3, C1): Grösse bzw. Serie einer Leitung ändern mit der Rückfrage «nur diese Leitung
// oder ganzer Leitungszug», und «Aufteilen mit Serie …» für Altbestand-Polylinien. Jeder Schritt ist ein Befehl des Kerns (leitung_groesse,
// leitung_serie, leitung_aufteilen) – ein Paket, rückgängig mit Strg+Z. Abgelehnt wird mit Text und Vorschlag des Kerns (der Aufrufer zeigt ihn).
// Der Dialog «Aufteilen» rechnet vorab einen Trockenlauf und zeigt die Vorschau des Kerns (Geraden und Formteile).

import { element } from '../zustand.js';
import { abfrage } from '../api.js';
import { sende, ergebnisMelden } from '../oberflaeche/befehle.js';
import { zeigen, meldung } from '../oberflaeche/dialoge.js';
import { esc, knoten, entprellen } from '../oberflaeche/werkzeug.js';
import { familienLaden } from '../familien/daten.js';
import { serienListe, serieLaden } from './leitung-daten.js';
import * as L from './leitung-logik.js';
import { zugText } from './leitung-palette.js';

const zugHolen = (kz) => abfrage('leitungszug', { leitung: kz }).catch(() => null);

/** Rückfrage: nur diese Leitung oder ganzer Zug? → 'eine' | 'zug' | null (abgebrochen). */
async function umfangFragen(kz, was, wert) {
  const zug = await zugHolen(kz);
  const t = zugText(zug);
  const w = knoten(`<div><p class="dlg-text">${esc(was)} von <b>${esc(kz)}</b> auf <b>${esc(wert)}</b> ändern.</p>`
    + `<p class="dlg-text leise">${t ? `Der Leitungszug von ${esc(kz)} hat ${esc(t)}. ` : ''}Bögen und Reduktionen im Zug werden dabei nachgeführt, die Kennzeichen der Geraden bleiben.</p></div>`);
  return zeigen({
    titel: `${was} ändern`, inhalt: w, breite: 480,
    knoepfe: [{ text: 'Abbrechen', wert: null }, { text: 'Nur diese Leitung', wert: 'eine' }, { text: 'Ganzer Leitungszug', wert: 'zug', primaer: true }],
  });
}

/** Ergebnis für die Palette: { ok: true } | { fehler, vorschlag } | { abgebrochen: true }. */
const ergebnis = (r, okText) => {
  if (r.angenommen) { ergebnisMelden(r, okText); return { ok: true }; }
  const a = L.ablehnung(r);
  return { fehler: a.text, vorschlag: a.vorschlag };
};

/** Grösse einer Leitung ändern (leitung_groesse). */
export async function groesseAendern(kz, groesse) {
  const umfang = await umfangFragen(kz, 'Grösse', groesse);
  if (!umfang) return { abgebrochen: true };
  const r = await sende('leitung_groesse', L.groessenParameter({ leitung: kz, groesse, zug: umfang === 'zug' }));
  return ergebnis(r, `${kz}: Grösse ${groesse}${umfang === 'zug' ? ' (ganzer Zug)' : ''}.`);
}

/** Serie einer Leitung ändern (leitung_serie). */
export async function serieAendern(kz, serie) {
  const name = serienListe(L.kategorieVon(element(kz)?.klasse)).find((s) => s.kz === serie)?.name ?? serie;
  const umfang = await umfangFragen(kz, 'Serie', name);
  if (!umfang) return { abgebrochen: true };
  const r = await sende('leitung_serie', L.serienParameter({ leitung: kz, serie, zug: umfang === 'zug' }));
  return ergebnis(r, `${kz}: Serie ${name}${umfang === 'zug' ? ' (ganzer Zug)' : ''}.`);
}

/** Palette: Änderung eines Feldes «lt:serie» oder «lt:groesse». */
export async function leitungAenderung(was, kz, wert) {
  if (!wert) return { abgebrochen: true };
  return was === 'serie' ? serieAendern(kz, wert) : groesseAendern(kz, wert);
}

/** «Aufteilen mit Serie …» für eine Altbestand-Leitung: Serie (und Grösse) wählen, Vorschau des Kerns, aufteilen (leitung_aufteilen). */
export async function aufteilenDialog(kz) {
  const el = element(kz);
  if (!el) { meldung(`Leitung «${kz}» gibt es nicht.`, 'warnung'); return false; }
  if (!L.istLeitungsklasse(el.klasse)) { meldung(`${kz} ist keine Leitung.`, 'warnung'); return false; }
  await familienLaden();
  const kategorie = L.kategorieVon(el.klasse);
  const serien = serienListe(kategorie);
  if (!serien.length) {
    meldung(`Keine Leitungsserie der Kategorie ${kategorie} im Projekt.`, 'warnung', { vorschlag: 'Zuerst eine Leitungsserie als Familie laden (Projektbrowser › Familien › Familie laden).' });
    return false;
  }
  const w = knoten(`<div class="lt-dlg">
    <p class="dlg-text"><b>${esc(kz)}</b> ist eine Polylinie ohne Serie${el.werte?.dn ? ` (DN ${esc(el.werte.dn)})` : ''}. Sie wird in Geraden mit Bögen der gewählten Serie zerlegt; die Verbindungen bleiben.</p>
    <label class="fz"><span>Serie *</span><select class="lt-serie">${serien.map((s) => `<option value="${esc(s.kz)}">${esc(s.name)}</option>`).join('')}</select></label>
    <label class="fz"><span>Grösse</span><select class="lt-groesse"><option value="">(aus der Leitung)</option></select></label>
    <div class="lt-vorschau leise">Vorschau wird gerechnet …</div></div>`);
  const q = (s) => w.querySelector(s);
  const param = () => L.aufteilenParameter({ leitung: kz, serie: q('.lt-serie').value, groesse: q('.lt-groesse').value });
  let nr = 0, bereit = false;
  async function groessenFuellen() {
    const d = await serieLaden(q('.lt-serie').value);
    const alt = q('.lt-groesse').value;
    q('.lt-groesse').innerHTML = `<option value="">(aus der Leitung)</option>${L.groessenListe(d).map((g) => `<option value="${esc(g.typ)}"${g.typ === alt ? ' selected' : ''}>${esc(g.typ)}</option>`).join('')}`;
  }
  async function vorschau() {
    const mein = ++nr; bereit = false;
    q('.lt-vorschau').textContent = 'Vorschau wird gerechnet …';
    const r = await sende('leitung_aufteilen', param(), { trockenlauf: true });
    if (mein !== nr) return;
    bereit = !!r.angenommen;
    if (r.angenommen) {
      const v = L.vorschauAus(r);
      q('.lt-vorschau').innerHTML = `<p class="huelle-ok">Vorschau (Trockenlauf – nichts wurde geändert): <b>${esc(L.vorschauZeile(v))}</b></p><ul class="lt-liste">${L.vorschauListe(v).map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`;
    } else {
      const a = L.ablehnung(r);
      q('.lt-vorschau').innerHTML = `<p class="fehlertext">${esc(a.text)}${a.vorschlag ? `<br><span class="leise">Vorschlag: ${esc(a.vorschlag)}</span>` : ''}</p>`;
    }
  }
  const neuRechnen = entprellen(vorschau, 200);
  q('.lt-serie').addEventListener('change', async () => { await groessenFuellen(); neuRechnen(); });
  q('.lt-groesse').addEventListener('change', neuRechnen);
  const ok = await zeigen({
    titel: 'Aufteilen mit Serie', inhalt: w, breite: 560, klasse: 'dlg-hoch',
    beiOeffnen: async () => { await groessenFuellen(); vorschau(); },
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Aufteilen', primaer: true, schliesst: false,
        pruefen: () => (bereit ? true : 'Die Vorschau ist noch nicht da oder wurde abgelehnt – anderen Serie oder Grösse wählen.'),
        async klick(c) {
          ++nr;
          const r = await sende('leitung_aufteilen', param());
          if (!r.angenommen) { const a = L.ablehnung(r); c.fehler(a.vorschlag ? `${a.text} – ${a.vorschlag}` : a.text); return; }
          ergebnisMelden(r, `${kz}: ${L.vorschauZeile(L.vorschauAus(r))}.`);
          c.schliessen(true);
        } }],
  });
  nr++;
  return ok === true;
}
