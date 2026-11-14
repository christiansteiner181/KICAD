// Lagemanager (Christian 05.11.2026: «zum zeigen von höhen in grundrissplänen, arbeiten wir oft mit lagen. diese werden immer auf dem Plan angezeigt
// und können etagenweise vergeben werden. es bräuchte also eine art lagemanager.»). Je Geschoss: UK roh Decke (aus dem Modell oder von Hand) und die Lagen
// als Abstand UK roh Decke bis Rohrachse. Daten: Abfrage lagen (Kern Leitungen/Lagen.cs), gespeichert als Geschoss-Eigenschaften ukRohdecke und lagen.
// Beim Zeichnen von Rohren setzt das Feld «Lage» der Optionsleiste die Höhe (lagenFuer).
// N40 (Christian 13.11.2026: «Je Lage ein eigenes Textfeld»): je Lage ein Feld L1, L2 … (Knopf «+» für eine weitere); gespeichert wird wie bisher der Text
// «120, 240, …» (leere Felder fallen weg). Was der Kern nicht lesen kann, steht unter der Zeile (Abfrage lagen › hinweis).

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

/** Teile eines gespeicherten Lagentexts, je Lage einer (Trennung wie im Kern: Komma, Strichpunkt, Zeilenwechsel). */
export const lagenTeile = (t) => String(t ?? '').split(/[,;\n]/).map((x) => x.trim()).filter(Boolean);

/** Felder einer Zeile: je Lage ein Feld, mindestens 4; Platzhalter = Wert, der ohne Eingabe gilt. */
function lagenFelder(klasse, werte, platzhalter) {
  const n = Math.max(4, werte.length, platzhalter.length);
  const feld = (i) => `<input type="text" class="lm-lage" title="Lage ${i + 1}" aria-label="Lage ${i + 1}" value="${esc(werte[i] ?? '')}" placeholder="${esc(platzhalter[i] ?? '')}" data-kein-enter="1" style="width:5.2em">`;
  return `<span class="${klasse}" style="display:inline-flex;flex-wrap:wrap;gap:4px;align-items:center">${Array.from({ length: n }, (_, i) => feld(i)).join('')}<button type="button" class="lm-plus" title="Weitere Lage">+</button></span>`;
}

/** Inhalt der Felder als Text für den Kern; null = ein Feld enthält mehrere Werte. */
function felderText(span) {
  const werte = [...span.querySelectorAll('.lm-lage')].map((i) => i.value.trim()).filter(Boolean);
  return werte.some((x) => /[,;]/.test(x)) ? null : werte.join(', ');
}

export async function lagemanager() {
  const gs = await lagenLaden(true);
  const quelleText = (g) => (g.quelle === 'modell' ? `aus dem Modell${g.decke ? ' (' + g.decke + ')' : ''}` : g.quelle === 'eingabe' ? 'von Hand' : g.quelle === 'fehlt' ? 'fehlt – bitte eingeben' : '');
  // Lagen für alle Geschosse am Gebäude (Christian 09.11.2026: «lagen gelten default für alle etagen»); am Geschoss nur Abweichungen
  const geb = gs.find((g) => g.gebaeude)?.gebaeude ?? null;
  const alleAlt = gs.find((g) => g.gebaeude)?.lagenGebaeude ?? '';
  const alleWerte = lagenTeile(alleAlt), alleZeigen = alleWerte.length ? alleWerte : ['120', '240', '360', '480'];
  const w = knoten(`<div style="display:grid;gap:10px">
    <div class="leise">Lage = Abstand UK roh Decke bis Rohrachse (nur Rohre). UK roh Decke leer = aus dem Modell (Unterkante der Betondecke darüber, sonst der grössten Decke).
      Je Lage ein Feld (L1 zuerst; mm, cm oder m, ohne Einheit mm); «+» gibt ein weiteres. Die Lagen gelten für alle Geschosse, Einträge beim Geschoss weichen davon ab
      (alle Felder leer = wie alle). Im Grundriss steht die Lage als Schrägstriche (Anzahl = Lage).</div>
    ${geb ? `<div class="fz"><span>Lagen für alle Geschosse [unter UK roh Decke]</span>${lagenFelder('lm-lagen-alle', alleWerte, ['120', '240', '360', '480'])}</div>` : ''}
    <table class="tabelle"><thead><tr><th>Geschoss</th><th>Kote</th><th>UK roh Decke [mm über Kote]</th><th>Lagen [mm unter UK roh Decke]</th><th>Achshöhen [mm über Kote]</th></tr></thead>
    <tbody>${gs.map((g) => `<tr data-geschoss="${esc(g.geschoss)}">
      <td>${esc(g.name || g.geschoss)}</td><td>${esc(g.kote)}</td>
      <td><input type="text" inputmode="decimal" class="lm-ukd" value="${g.quelle === 'eingabe' ? esc(g.ukRohdecke) : ''}" placeholder="${g.ukRohdecke ?? ''}" data-kein-enter="1">
        <span class="leise">${esc(quelleText(g))}</span></td>
      <td>${lagenFelder('lm-lagen', g.vorgabe ? [] : g.lagen.map((l) => String(l.abstand)), geb ? alleZeigen : ['120', '240', '360', '480'])}</td>
      <td class="leise">${g.lagen.map((l) => `L${l.nr}: ${Number.isFinite(l.hoehe) ? Math.round(l.hoehe) : '–'}`).join(' · ')}${g.hinweis ? `<div class="lm-hinweis" style="color:var(--warnung, #b45309)">Nicht lesbar: ${esc(g.hinweis)}</div>` : ''}</td></tr>`).join('')}</tbody></table>
  </div>`);

  // «+»: ein weiteres Feld an der Zeile
  w.addEventListener('click', (ev) => {
    const k = ev.target.closest('.lm-plus');
    if (!k) return;
    const felder = k.parentElement.querySelectorAll('.lm-lage');
    const neu = felder[felder.length - 1].cloneNode();
    neu.value = ''; neu.placeholder = ''; neu.title = neu.ariaLabel = `Lage ${felder.length + 1}`; neu.setAttribute('aria-label', neu.title);
    k.before(neu); neu.focus();
  });

  async function speichern(ctrl) {
    const alleSpan = w.querySelector('.lm-lagen-alle');
    const alle = alleSpan ? felderText(alleSpan) : undefined;
    if (alle === null) { ctrl.fehler('Lagen für alle Geschosse: je Feld eine Lage (ohne Komma).'); return; }
    if (geb && alle !== undefined && alle !== alleWerte.join(', ')) {
      const r = await sende('element_aendern', { element: geb, eigenschaften: { lagen: alle === '' ? null : alle } });
      if (!ergebnisMelden(r)) return;
    }
    for (const tr of w.querySelectorAll('tr[data-geschoss]')) {
      const g = gs.find((x) => x.geschoss === tr.dataset.geschoss);
      const ukd = tr.querySelector('.lm-ukd').value.trim().replace(',', '.');
      const lagen = felderText(tr.querySelector('.lm-lagen'));
      if (lagen === null) { ctrl.fehler(`${g.geschoss}: je Feld eine Lage (ohne Komma).`); return; }
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
    titel: 'Lagemanager', inhalt: w, breite: 1040,
    knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Speichern', primaer: true, schliesst: false, klick: (ctrl) => speichern(ctrl) }],
  });
}
