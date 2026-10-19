// Dialog «Einlageplan anlegen» / «Grundleitungsplan anlegen» (Register Ansicht › Erstellen; Nachbearbeitung N3, Punkt 1). Ruft den Kernbefehl einlageplaene_anlegen
// (Modul 3, B10): je Planschlüssel der Verlegung eine Ansicht und ein Planblatt. Der Dialog zeigt die erkannten Pläne (Abfrage verlegung, mit Planschlüssel als Name,
// Bauteil Decke/Bodenplatte/Wand, Geschoss), lässt Geschoss, Massstab und Format wählen (ohne Wahl gelten die Vorgaben des Kerns) und zeigt vorab als Trockenlauf,
// was entsteht. Danach wird der (erste neue) Plan geöffnet. Keine Fachlogik: Erkennung, Namen, Nummern und Gewerk sind Sache des Kerns. Texte: einlageplaene-text.js.

import { zustand, element, derKlasse } from '../zustand.js';
import * as fenster from '../fenster.js';
import { abfrage } from '../api.js';
import { zeigen, meldung } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { esc, knoten, entprellen, aufElement } from './werkzeug.js';
import * as T from './einlageplaene-text.js';

const DARSTELLUNG = { einlage: 'einlagen', erdreich: 'grundleitungen' };   // Ansichtseigenschaft darstellung (Katalog)

export async function einlageplanDialog(art) {
  const A = T.ARTEN[art];
  if (!A) throw new Error(`Unbekannte Planart «${art}».`);
  let antwort;
  try { antwort = await abfrage('verlegung', { art }); }
  catch (e) { meldung(e.message, 'warnung', { vorschlag: e.vorschlag }); return false; }
  const zeilen = T.planZeilen(antwort);
  const reihenfolge = derKlasse('Geschoss').sort((a, b) => (a.werte?.kote ?? 0) - (b.werte?.kote ?? 0)).map((g) => g.kennzeichen);
  const geschosse = T.geschossOptionen(zeilen, reihenfolge);
  const aktiv = element(zustand.aktiveAnsicht);
  const vorgabeGeschoss = aktiv?.geschoss && geschosse.includes(aktiv.geschoss) ? aktiv.geschoss : '';

  const w = knoten(`<div class="epl">
    <p class="dlg-text epl-text">${art === 'erdreich'
      ? 'Legt je Grundleitungsplan (Leitungen im Erdreich) eine Ansicht und ein Planblatt an.'
      : 'Legt je Einlageplan (Einlagen in Decke, Bodenplatte und Wand) eine Ansicht und ein Planblatt an.'}
      Name, Kennzeichen und Plannummer bestimmt der Kern nach dem Planschlüssel; vorhandene Ansichten und Blätter werden nicht doppelt angelegt.</p>
    <div class="epl-wahl">
      <label class="fz"><span>Geschoss</span><select class="epl-geschoss"><option value="">alle Geschosse</option>${geschosse.map((g) => `<option value="${esc(g)}"${g === vorgabeGeschoss ? ' selected' : ''}>${esc(g)}</option>`).join('')}</select></label>
      <label class="fz"><span>Massstab 1:</span><select class="epl-massstab"><option value="">Vorgabe des Kerns</option>${T.MASSSTAEBE.map((m) => `<option>${m}</option>`).join('')}</select></label>
      <label class="fz"><span>Blattformat</span><select class="epl-format"><option value="">Vorgabe des Kerns</option>${T.FORMATE.map((f) => `<option>${f}</option>`).join('')}</select></label>
    </div>
    <div class="epl-plaene"></div>
    <div class="epl-vorschau"><div class="epl-vorschau-kopf">Vorschau (Trockenlauf)</div><div class="epl-vorschau-inhalt leise">Wird berechnet …</div></div>
  </div>`);
  const q = (s) => w.querySelector(s);
  const auswahl = () => ({ art, geschoss: q('.epl-geschoss').value, massstab: q('.epl-massstab').value, format: q('.epl-format').value });
  const sichtbar = () => T.planFilter(zeilen, q('.epl-geschoss').value);

  function listeZeichnen() {
    const z = sichtbar();
    q('.epl-plaene').innerHTML = z.length === 0
      ? `<p class="leise epl-leer">Keine ${esc(A.planWort)} erkannt${q('.epl-geschoss').value ? ` für ${esc(q('.epl-geschoss').value)}` : ''}. Die Abfrage «verlegung» zeigt die Erkennung; Einlagen entstehen in Betonbauteilen (Decke, Bodenplatte, Wand), Grundleitungen unter der Bodenplatte.</p>`
      : `<table class="tab epl-tabelle"><thead><tr><th>Plan (Name)</th><th>Bauteil</th><th>Geschoss</th><th>Inhalt</th></tr></thead><tbody>${z.map((p) =>
        `<tr data-plan="${esc(p.plan)}"><td>${esc(p.plan)}</td><td>${esc(p.bauteil)}</td><td>${esc(p.geschoss)}</td><td>${esc(T.planSumme(p))}</td></tr>`).join('')}</tbody></table>`;
  }

  let nr = 0;
  async function vorschau() {
    const mein = ++nr;
    const ziel = q('.epl-vorschau-inhalt');
    if (sichtbar().length === 0) { ziel.classList.add('leise'); ziel.textContent = 'Es entsteht nichts.'; return; }
    ziel.classList.add('leise'); ziel.textContent = 'Wird berechnet …';
    const r = await sende(T.BEFEHL, T.befehlParameter(auswahl()), { trockenlauf: true });
    if (mein !== nr) return;
    ziel.classList.remove('leise');
    if (!r.angenommen) { ziel.innerHTML = `<span class="fehlertext">${esc(r.fehler || r.meldung || 'Abgelehnt.')}</span>${r.vorschlag ? `<br><span class="leise">${esc(r.vorschlag)}</span>` : ''}`; return; }
    const h = T.hinweisZeilen(r);
    ziel.innerHTML = `<div class="epl-summe">Es entstehen: ${esc(T.laufText(r))}.</div>${h.length ? `<ul class="epl-hinweise">${h.map((x) => `<li>${esc(x)}</li>`).join('')}</ul>` : ''}`;
  }
  const spaeter = entprellen(vorschau, 120);
  for (const s of ['.epl-geschoss', '.epl-massstab', '.epl-format']) q(s).addEventListener('change', () => { listeZeichnen(); spaeter(); });
  listeZeichnen();
  vorschau();

  const ok = await zeigen({
    titel: A.dialogTitel, inhalt: w, breite: 720, art: 'einlageplan',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Anlegen', wert: true, primaer: true, pruefen: () => (sichtbar().length === 0 ? `Es gibt keine ${A.planWort} zum Anlegen.` : true) }],
  });
  if (ok !== true) return false;

  const wahl = auswahl();
  const r = await sende(T.BEFEHL, T.befehlParameter(wahl));
  if (!ergebnisMelden(r, `${A.titel}: ${T.laufText(r)} angelegt.`)) return false;
  // Öffnen: die erste neue Ansicht, sonst eine vorhandene derselben Darstellung im gewählten Geschoss
  let kz = T.angelegt(r).ansichten[0];
  if (!kz) {
    kz = derKlasse('Ansicht').filter((a) => a.werte?.art === 'grundriss' && a.werte?.darstellung === DARSTELLUNG[art] && (!wahl.geschoss || a.geschoss === wahl.geschoss))
      .sort((a, b) => (a.kennzeichen < b.kennzeichen ? -1 : 1))[0]?.kennzeichen;
  }
  if (kz && await aufElement(kz)) await fenster.ansichtOeffnen(kz);
  return true;
}
