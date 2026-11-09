// Lagemanager (Christian 05.11.2026: «zum zeigen von höhen in grundrissplänen, arbeiten wir oft mit lagen. diese werden immer auf dem Plan angezeigt
// und können etagenweise vergeben werden. es bräuchte also eine art lagemanager.»). Je Geschoss: UK roh Decke (aus dem Modell oder von Hand) und die Lagen
// als Abstand UK roh Decke bis Rohrachse. Daten: Abfrage lagen (Kern Leitungen/Lagen.cs), gespeichert als Geschoss-Eigenschaften ukRohdecke und lagen.
// Beim Zeichnen von Rohren setzt das Feld «Lage» der Optionsleiste die Höhe (lagenFuer).

import { zustand } from '../zustand.js';
import { lagenLaden } from '../werkzeuge/lagen-daten.js';
import { registrieren } from './register.js';
import { zeigen } from './dialoge.js';
import { knoten, esc } from './werkzeug.js';
import { sende, ergebnisMelden } from './befehle.js';

registrieren({
  id: 'gt.lagen', titel: 'Lagemanager', symbol: 'kote', kuerzel: null, register: 'koordination', gruppe: 'Vorgaben',   // Christian 09.11.2026: «lagen gelten für alle gewerke gleich und gehören in den tab koordination»
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => lagemanager(),
});

export async function lagemanager() {
  const gs = await lagenLaden(true);
  const quelleText = (g) => (g.quelle === 'modell' ? `aus dem Modell${g.decke ? ' (' + g.decke + ')' : ''}` : g.quelle === 'eingabe' ? 'von Hand' : g.quelle === 'fehlt' ? 'fehlt – bitte eingeben' : '');
  // Lagen für alle Geschosse am Gebäude (Christian 09.11.2026: «lagen gelten default für alle etagen»); am Geschoss nur Abweichungen
  const geb = gs.find((g) => g.gebaeude)?.gebaeude ?? null;
  const alleAlt = gs.find((g) => g.gebaeude)?.lagenGebaeude ?? '';
  const alleText = alleAlt || '120, 240, 360, 480';
  const w = knoten(`<div style="display:grid;gap:10px">
    <div class="leise">Lage = Abstand UK roh Decke bis Rohrachse (nur Rohre). UK roh Decke leer = aus dem Modell (Unterkante der Betondecke darüber, sonst der grössten Decke).
      Lagen in mm, durch Komma getrennt, Lage 1 zuerst; sie gelten für alle Geschosse, ein Eintrag beim Geschoss weicht davon ab. Im Grundriss steht die Lage als Schrägstriche (Anzahl = Lage).</div>
    ${geb ? `<label class="fz"><span>Lagen für alle Geschosse [mm unter UK roh Decke]</span><input type="text" class="lm-lagen-alle" value="${esc(alleAlt)}" placeholder="120, 240, 360, 480" data-kein-enter="1"></label>` : ''}
    <table class="tabelle"><thead><tr><th>Geschoss</th><th>Kote</th><th>UK roh Decke [mm über Kote]</th><th>Lagen [mm unter UK roh Decke]</th><th>Achshöhen [mm über Kote]</th></tr></thead>
    <tbody>${gs.map((g) => `<tr data-geschoss="${esc(g.geschoss)}">
      <td>${esc(g.name || g.geschoss)}</td><td>${esc(g.kote)}</td>
      <td><input type="text" inputmode="decimal" class="lm-ukd" value="${g.quelle === 'eingabe' ? esc(g.ukRohdecke) : ''}" placeholder="${g.ukRohdecke ?? ''}" data-kein-enter="1">
        <span class="leise">${esc(quelleText(g))}</span></td>
      <td><input type="text" class="lm-lagen" value="${g.vorgabe ? '' : esc(g.lagen.map((l) => l.abstand).join(', '))}" placeholder="${esc(geb ? 'wie alle: ' + alleText : '120, 240, 360, 480')}" data-kein-enter="1"></td>
      <td class="leise">${g.lagen.map((l) => `L${l.nr}: ${Number.isFinite(l.hoehe) ? Math.round(l.hoehe) : '–'}`).join(' · ')}</td></tr>`).join('')}</tbody></table>
  </div>`);

  async function speichern(ctrl) {
    const alle = w.querySelector('.lm-lagen-alle')?.value.trim();
    if (geb && alle !== undefined && alle !== alleAlt) {
      const r = await sende('element_aendern', { element: geb, eigenschaften: { lagen: alle === '' ? null : alle } });
      if (!ergebnisMelden(r)) return;
    }
    for (const tr of w.querySelectorAll('tr[data-geschoss]')) {
      const g = gs.find((x) => x.geschoss === tr.dataset.geschoss);
      const ukd = tr.querySelector('.lm-ukd').value.trim().replace(',', '.');
      const lagen = tr.querySelector('.lm-lagen').value.trim();
      const eig = {};
      const ukdAlt = g.quelle === 'eingabe' ? String(g.ukRohdecke) : '';
      if (ukd !== ukdAlt) eig.ukRohdecke = ukd === '' ? null : Number(ukd);
      const lagenAlt = g.vorgabe ? '' : g.lagen.map((l) => l.abstand).join(', ');
      if (lagen !== lagenAlt) eig.lagen = lagen === '' ? null : lagen;
      if (Object.keys(eig).length === 0) continue;
      if ('ukRohdecke' in eig && eig.ukRohdecke !== null && !Number.isFinite(eig.ukRohdecke)) { ctrl.fehler(`${g.geschoss}: UK roh Decke ist keine Zahl.`); return; }
      const r = await sende('element_aendern', { element: g.geschoss, eigenschaften: eig });
      if (!ergebnisMelden(r)) return;
    }
    await lagenLaden(true);
    ctrl.schliessen?.(true);
  }

  return zeigen({
    titel: 'Lagemanager', inhalt: w, breite: 900,
    knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Speichern', primaer: true, schliesst: false, klick: (ctrl) => speichern(ctrl) }],
  });
}
