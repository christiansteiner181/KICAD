// Familie aus IFC (Christian 03.11.2026: «Es soll auch einen flow geben; Familie aus IFC: IFC einlesen > Teile wählen > Familie nachmodellieren»).
// Ein Dialog in drei Schritten: 1. IFC wählen bzw. einlesen (Hochladen, ifc_uebernehmen mit bereiche ["keine"] – nur Unterlage und Netzdatei, ohne
// Elemente und ohne Projektbezug), 2. Teile wählen (Abfrage ifc_teile, Suche, Häkchen; vorgewählt aus der Auswahl im Modell), 3. Familie anlegen
// (familie_netz_uebernehmen mit objekte und anschluesse) – danach öffnet der Familieneditor zum Nachmodellieren (Parameter, Typen, Anschlüsse).
// Keine Fachlogik: Liste, Zusammenfassen, Lage und Anschlüsse rechnet der Kern.

import { zustand, element } from '../zustand.js';
import { abfrage } from '../api.js';
import { zeigen, meldung } from '../oberflaeche/dialoge.js';
import { sende } from '../oberflaeche/befehle.js';
import { knoten, aufElement, esc } from '../oberflaeche/werkzeug.js';
import { ansichtOeffnen } from '../fenster.js';
import { dateiWaehlen, hochladen } from '../oberflaeche/aktionen-einfuegen.js';
import { familienKategorien, kernFehler } from './familien-text.js';
import { familienLaden } from './daten.js';

/** Zeile der Teileliste (in Node getestet über teilZeile). */
export function teilZeile(t, gewaehlt) {
  const g = (t.groesse ?? []).map((x) => Math.round(x)).join(' × ');
  return `<tr><td><input type="checkbox" class="ai-teil" value="${esc(t.globalId)}"${gewaehlt ? ' checked' : ''}></td>`
    + `<td>${esc(t.typ ?? '–')}</td><td>${esc(t.entitaet)}</td><td class="zahl">${esc(g)}</td><td>${esc(t.element ?? '')}</td>`
    + `<td class="leise" title="${esc(t.globalId)}">${esc(String(t.globalId).slice(0, 8))}…</td></tr>`;
}

export async function familieAusIfc() {
  const kategorien = familienKategorien(zustand.katalog?.klassen);
  const w = knoten(`<div class="fe-dlg ai-dlg">
    <h4>1 · IFC</h4>
    <label class="fz"><span>Unterlage *</span><span class="ai-zeile"><select class="ai-ul"></select> <button type="button" class="ai-einlesen">IFC-Datei einlesen …</button></span></label>
    <h4>2 · Teile wählen</h4>
    <label class="fz"><span>Suche</span><input type="text" class="ai-suche" data-kein-enter="1" placeholder="Typname, IFC-Klasse oder GlobalId"></label>
    <div class="ai-liste" style="max-height:280px;overflow:auto;border:1px solid var(--rand);"><table class="tabelle"><thead><tr><th></th><th>Typ</th><th>IFC-Klasse</th><th>Grösse mm</th><th>Element</th><th>GlobalId</th></tr></thead><tbody class="ai-teile"></tbody></table></div>
    <p class="leise ai-anzahl"></p>
    <h4>3 · Familie</h4>
    <label class="fz"><span>Kategorie *</span><select class="ai-kat">${kategorien.map((k) => `<option>${esc(k)}</option>`).join('')}</select></label>
    <label class="fz"><span>Name *</span><input type="text" class="ai-name" data-kein-enter="1" placeholder="z. B. Geberit Duofix WC 112"></label>
    <label class="fz"><span>Nullpunkt</span><select class="ai-null"><option value="unten_mitte">unten Mitte</option><option value="unten_hinten_mitte">unten hinten Mitte (Wandobjekt)</option><option value="unveraendert">unverändert</option></select></label>
    <label class="fz"><span>Anschlüsse</span><label><input type="checkbox" class="ai-ports" checked> aus den IFC-Ports übernehmen</label></label>
    <p class="leise">Die gewählten Teile werden ein Körper; danach öffnet der Familieneditor zum Nachmodellieren (Parameter, Typen, Anschlüsse prüfen).</p></div>`);
  const q = (s) => w.querySelector(s);
  const gewaehlt = new Set();
  const auswahl = element(zustand.auswahl);
  if (auswahl?.kennungen?.ifc) gewaehlt.add(String(auswahl.kennungen.ifc).split(':')[0]);
  let teile = [];

  async function unterlagenLaden(vorgabe) {
    const r = await abfrage('ifc_teile', {});
    const ul = r.unterlagen ?? [];
    const quelle = auswahl?.kennungen?.quelle ? element(auswahl.kennungen.quelle)?.kennzeichen : null;
    q('.ai-ul').innerHTML = ul.length ? ul.map((u) => `<option value="${esc(u.kennzeichen)}">${esc(u.kennzeichen)} · ${esc(u.datei || u.name || '')}</option>`).join('')
      : '<option value="">– noch keine IFC eingelesen –</option>';
    const soll = vorgabe ?? quelle;
    if (soll && ul.some((u) => u.kennzeichen === soll)) q('.ai-ul').value = soll;
    await teileLaden();
  }

  async function teileLaden() {
    const ul = q('.ai-ul').value;
    if (!ul) { teile = []; zeichnen(); return; }
    try {
      const r = await abfrage('ifc_teile', { unterlage: ul, suche: q('.ai-suche').value.trim() || undefined });
      teile = r.teile ?? [];
      q('.ai-anzahl').textContent = `${teile.length} von ${r.gesamt ?? teile.length} Teilen` + (r.gesamt > teile.length ? ' (Suche eingrenzen)' : '');
    } catch (e) { teile = []; q('.ai-anzahl').textContent = e.message; }
    zeichnen();
  }

  function zeichnen() {
    q('.ai-teile').innerHTML = teile.map((t) => teilZeile(t, gewaehlt.has(t.globalId))).join('');
    if (!q('.ai-name').value.trim()) {
      const erstes = teile.find((t) => gewaehlt.has(t.globalId));
      if (erstes?.typ) q('.ai-name').value = erstes.typ;
    }
  }

  q('.ai-teile').addEventListener('change', (e) => {
    const c = e.target;
    if (!c.classList?.contains('ai-teil')) return;
    if (c.checked) gewaehlt.add(c.value); else gewaehlt.delete(c.value);
    zeichnen();
  });
  q('.ai-ul').addEventListener('change', () => { gewaehlt.clear(); teileLaden(); });
  let zeit = 0;
  q('.ai-suche').addEventListener('input', () => { clearTimeout(zeit); zeit = setTimeout(teileLaden, 250); });
  q('.ai-einlesen').addEventListener('click', async () => {
    const datei = await dateiWaehlen('.ifc');
    if (!datei) return;
    q('.ai-anzahl').textContent = `«${datei.name}» wird eingelesen …`;
    try {
      const h = await hochladen(datei);
      const r = await sende('ifc_uebernehmen', { datei: h.name, bereiche: ['keine'], grundlage: true });
      if (!r.angenommen) { const k = kernFehler(r); meldung(k.text, 'warnung', { vorschlag: k.vorschlag }); return; }
      const kz = r.aenderungen?.find((a) => a.klasse === 'Unterlage')?.kennzeichen;
      await unterlagenLaden(kz);
    } catch (e) { meldung(e.message, 'warnung', { vorschlag: e.vorschlag }); }
  });

  await unterlagenLaden();
  await zeigen({
    titel: 'Familie aus IFC', inhalt: w, breite: 760, klasse: 'familie',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Familie anlegen', primaer: true, schliesst: false,
        pruefen: () => (!q('.ai-ul').value ? 'Zuerst eine IFC einlesen.' : gewaehlt.size === 0 ? 'Mindestens ein Teil wählen.' : !q('.ai-name').value.trim() ? 'Name angeben.' : true),
        async klick(c) {
          const r = await sende('familie_netz_uebernehmen', {
            unterlage: q('.ai-ul').value, objekte: [...gewaehlt], anschluesse: q('.ai-ports').checked,
            kategorie: q('.ai-kat').value, name: q('.ai-name').value.trim(), nullpunkt: q('.ai-null').value,
          });
          if (!r.angenommen) { const k = kernFehler(r); c.fehler(`${k.text}${k.vorschlag ? ' – ' + k.vorschlag : ''}`); return; }
          c.schliessen(true);
          for (const h of r.hinweise ?? []) if (h.startsWith('Körper')) meldung(h, 'info');
          await familienLaden();
          const kz = r.aenderungen?.find((a) => a.klasse === 'Familie')?.kennzeichen;
          if (kz && (await aufElement(kz))) await ansichtOeffnen(kz);
        } }],
  });
}
