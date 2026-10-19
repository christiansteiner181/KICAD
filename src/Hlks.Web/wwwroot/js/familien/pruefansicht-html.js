// Prüfansicht eines Entwurfs (Modul 2, C2): das HTML der Abschnitte als reine Funktionen (ohne DOM, in Node getestet). Die Bedienung steht in
// pruefansicht.js. Alle Daten kommen vom Kern (Abfrage familie: definition.abdeckung, konflikte, bericht, unterlagen; familie_masse; familie_vergleichen);
// hier wird nur dargestellt. Schaltflächen und Felder tragen data-pa (Aktion) bzw. data-pa-feld (Eingabe) und data-vg (Kontrollbild).

import { esc } from './familien-text.js';
import * as U from './unterlagen-text.js';

const zeitText = (iso) => { const m = /^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})/.exec(String(iso ?? '')); return m ? `${m[1]} ${m[2]}` : String(iso ?? ''); };
export const attr = (name, wert) => `[${name}="${String(wert).replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"]`;

// ------------------------------------------------------------------------------------------------ Kopf und Freigabe

export function kopfHtml(fam, gruende, ui = {}) {
  const def = fam.definition ?? {};
  const frei = fam.status === 'freigegeben';
  const summen = U.abdeckungSummen(def);
  const chips = U.STATUS.map((st) => `<span class="fu-chip fu-st-${st.klasse}" title="${esc(st.hinweis)}">${summen[st.id] ?? 0} ${esc(st.text)}</span>`).join('');
  const bild = U.bildHinweis(def);
  const f = fam.freigabe;
  let unten;
  if (frei) {
    unten = `<p class="fu-frei-info">Freigegeben${f ? ` von ${esc(f.akteur ?? '?')} am ${esc(zeitText(f.zeit))} (Version ${esc(f.version ?? fam.version)}): ${f.geprueft ?? 0} Wert(e) gegen die Quelle geprüft, ${f.ausBild ?? 0} aus Bildern (ungeprüft), ${f.mensch ?? 0} von einem Menschen (davon ${f.bestaetigt ?? 0} bestätigt), ${f.konflikteEntschieden ?? 0} Konflikt(e) entschieden, ${f.masseBestanden ?? 0} Mass(e) je Typ bestanden.` : '.'}</p>`
      + (fam.bibliothek?.datei ? `<p class="fu-bib">In der Bibliothek: <b>${esc(fam.bibliothek.datei)}</b></p>` : '<p class="leise">Noch nicht in der Bibliothek gespeichert.</p>');
  } else if (gruende.length) {
    unten = `<div class="fu-gruende-kopf">Freigabe gesperrt – ${gruende.length} ${gruende.length === 1 ? 'Grund' : 'Gründe'}:</div><ul class="fu-gruende">${gruende.map((g, i) => `<li class="fu-grund fu-grund-${esc(g.art)}" data-pa="grund" data-i="${i}" tabindex="0" role="button" title="Zur Stelle springen">${esc(g.text)} <span class="fu-sprung">→ zeigen</span></li>`).join('')}</ul>`;
  } else {
    unten = '<p class="fu-bereit">Keine offenen Konflikte, Fehler oder Masse ausserhalb der Toleranz – die Familie lässt sich freigeben.</p>';
  }
  return `<div class="fu-kopf-zeile"><span class="${frei ? 'fe-status frei' : 'fe-status entwurf'}">${frei ? 'freigegeben' : 'Entwurf'}</span>
      <b>${esc(fam.kennzeichen)} ${esc(fam.name ?? '')}</b><span class="leise"> · ${esc(fam.kategorie ?? '')} · Version ${esc(fam.version)}${def.hersteller ? ' · ' + esc(def.hersteller) : ''}${def.produkt ? ' · ' + esc(def.produkt) : ''}</span></div>
    <div class="fu-summen" title="Abdeckung: Stand der Werte (Parameter und Typen)">${chips}</div>${bild ? `<p class="fu-bildhinweis leise">${esc(bild)}</p>` : ''}
    <div class="fu-freigabe ${frei ? 'freigegeben' : gruende.length ? 'gesperrt' : 'bereit'}">
      <div class="fu-freigabe-zeile">
        <button type="button" class="fe-knopf primaer fu-freigeben" data-pa="freigeben" ${frei || gruende.length ? 'disabled' : ''} title="${esc(frei ? 'Schon freigegeben' : gruende.length ? 'Gesperrt: siehe die Gründe unten' : 'Familie freigeben (nur ein Mensch)')}">Freigeben</button>
        <button type="button" class="fe-knopf fu-speichern" data-pa="speichern" ${frei ? '' : 'disabled'} title="${esc(frei ? 'Freigegebene Familie in die Familienbibliothek schreiben (Feld leer) bzw. als Datei ausführen (voller Pfad im Feld)' : 'Erst freigeben')}">In Bibliothek speichern</button>
        <input type="text" class="fe-in fu-datei-in" data-pa-feld="datei" data-kein-enter="1" value="${esc(ui.datei ?? '')}" placeholder="leer = in die Familienbibliothek · oder voller Pfad für eine Ausfuhr als Datei" ${frei ? '' : 'disabled'} spellcheck="false">
      </div>${unten}
    </div>`;
}

export function berichtHtml(fam, kandidaten = [], internet = []) {
  const def = fam.definition ?? {};
  const unterlagen = (def.unterlagen ?? []).map((u) => `<li>${esc(u.datei ?? '?')} <span class="leise">${esc(U.ART_NAMEN[u.art] ?? u.art ?? '')}${u.rolle ? ' · ' + esc(u.rolle) : ''}${kandidaten.some((k) => k.praefix && String(u.pruefsumme ?? '').startsWith(k.praefix)) ? '' : ' · liegt nicht im Projekt'}</span></li>`).join('');
  return `<h3>Bericht der KI</h3>${def.bericht ? `<pre class="fu-bericht">${esc(def.bericht)}</pre>` : '<p class="leise">Kein Bericht in der Familie gespeichert (Bericht des letzten KI-Laufs: Dialog «Familie aus Unterlagen»).</p>'}
    ${unterlagen ? `<div class="fu-ul-kopf">Unterlagen dieser Familie</div><ul class="fu-ul">${unterlagen}</ul>` : ''}
    ${U.internetListeHtml(internet)}`;
}

// ------------------------------------------------------------------------------------------------ Konflikte

export function konflikteHtml(def, ui = {}) {
  const liste = def?.konflikte ?? [];
  const offen = liste.filter((k) => !k.entscheid).length;
  if (liste.length === 0) return '<h3>Konflikte</h3><p class="leise">Keine Konflikte: die Quellen widersprechen sich nicht.</p>';
  return `<h3>Konflikte (${liste.length}, davon ${offen} offen)</h3><p class="leise">Widersprechen sich zwei Quellen über die Toleranz, entscheidet ein Mensch. Der Vorschlag nach Vorrang ist hervorgehoben, aber nicht gewählt.</p>${liste.map((k) => konfliktHtml(k, ui)).join('')}`;
}

function konfliktHtml(k, ui) {
  const opt = U.konfliktOptionen(k);
  const w = ui.wahl?.[k.id] ?? {};
  const gewaehlt = w.index !== undefined ? String(w.index) : k.entscheid ? String(opt.find((o) => Math.abs(Number(o.wert) - Number(k.entscheid.wert)) < 1e-9)?.index ?? 'anders') : '';
  const anders = w.anders ?? (gewaehlt === 'anders' && k.entscheid ? String(k.entscheid.wert) : '');
  const begr = w.begr ?? '';
  const ent = k.entscheid;
  return `<div class="fu-konflikt ${ent ? 'entschieden' : 'offen'}" data-k="${esc(k.id)}">
    <div class="fu-k-kopf"><b>${esc(U.konfliktTitel(k))}</b><span class="leise"> · ${esc(k.toleranz ?? '')}${k.vorrang ? ' · Vorrang: ' + esc(k.vorrang) : ''}</span>
      <span class="fu-k-stand ${ent ? 'ok' : 'fehler'}">${ent ? 'entschieden' : 'offen'}</span></div>
    <ul class="fu-k-werte">${opt.map((o) => `<li class="fu-k-wert${o.vorschlag ? ' vorschlag' : ''}">
        <label class="fu-k-wahl"><input type="radio" name="kw-${esc(k.id)}" value="${o.index}" data-pa-feld="k-wahl" data-k="${esc(k.id)}" ${gewaehlt === String(o.index) ? 'checked' : ''}> <b class="fu-k-zahl">${esc(o.text)}</b>${o.vorschlag ? ' <span class="fu-vorschlag-marke">Vorschlag</span>' : ''}</label>
        ${o.vorrang ? `<span class="fu-k-vorrang leise">${esc(o.vorrang)}</span>` : ''}
        <ul class="fu-k-quellen">${o.quellen.map((id, i) => `<li><span class="fu-k-stelle">${esc(o.stellen[i] ?? id)}</span> <button type="button" class="fe-knopf klein" data-pa="quelle" data-id="${esc(id)}">Quelle ansehen</button></li>`).join('')}</ul></li>`).join('')}
      <li class="fu-k-wert"><label class="fu-k-wahl"><input type="radio" name="kw-${esc(k.id)}" value="anders" data-pa-feld="k-wahl" data-k="${esc(k.id)}" ${gewaehlt === 'anders' ? 'checked' : ''}> anderer Wert</label>
        <input type="text" class="fe-in fu-k-anders" data-pa-feld="k-anders" data-k="${esc(k.id)}" data-kein-enter="1" value="${esc(anders)}" placeholder="${esc(k.einheit ?? '')}" size="8"></li></ul>
    <div class="fu-k-fuss"><input type="text" class="fe-in breit fu-k-begr" data-pa-feld="k-begr" data-k="${esc(k.id)}" data-kein-enter="1" value="${esc(begr)}" placeholder="Begründung (z. B. «DXF ist massstäblich, Rücksprache Hersteller»)" spellcheck="false">
      <button type="button" class="fe-knopf primaer" data-pa="entscheiden" data-k="${esc(k.id)}">${ent ? 'Neu entscheiden' : 'Entscheiden'}</button></div>
    ${ent ? `<p class="fu-k-entscheid">Entschieden: <b>${esc(U.wertText(ent.wert))}${k.einheit ? ' ' + esc(k.einheit) : ''}</b> von ${esc(ent.akteur ?? '?')} am ${esc(zeitText(ent.zeit))}${ent.begruendung ? ' – ' + esc(ent.begruendung) : ' (ohne Begründung)'}</p>` : ''}
  </div>`;
}

// ------------------------------------------------------------------------------------------------ Abdeckung

function zelleHtml(z, ui) {
  if (!z) return '<td class="fu-st-leer" title="kein Eintrag (Formel, Exemplar- oder Vorgabewert ohne Quelle)">–</td>';
  const st = U.statusInfo(z.status);
  const gewaehlt = ui.zelle === z.schluessel;
  return `<td class="fu-st fu-st-${st.klasse}${gewaehlt ? ' gewaehlt' : ''}" data-pa="zelle" data-s="${esc(z.schluessel)}" tabindex="0" role="button" title="${esc(`${st.text}${z.vermerk ? ' – ' + z.vermerk : ''}`)}">${esc(U.wertText(z.wert) || '·')}${z.quellen.length ? `<sup>${z.quellen.length}</sup>` : ''}</td>`;
}

export function abdeckungHtml(def, ui = {}) {
  const t = U.abdeckungTabelle(def);
  if (t.zeilen.length === 0 && t.uebrig.length === 0) return '<h3>Abdeckung</h3><p class="leise">Die Familie hat keine Abdeckung: sie stützt sich auf keine Unterlage (Werte von Hand im Editor oder noch nicht geprüft).</p>';
  const legende = U.STATUS.map((st) => `<span class="fu-chip fu-st-${st.klasse}" title="${esc(st.hinweis)}">${esc(st.text)}</span>`).join('');
  const kopf = `<tr><th>Parameter</th><th>Vorgabe</th>${t.typen.map((n) => `<th>${esc(n)}</th>`).join('')}</tr>`;
  const zeilen = t.zeilen.map((z) => `<tr><th class="fu-abd-p" title="${esc(z.gruppe)}">${esc(z.parameter)}</th>${zelleHtml(z.vorgabe, ui)}${z.typen.map((c) => zelleHtml(c, ui)).join('')}</tr>`).join('');
  const uebrig = t.uebrig.length ? `<tr><th class="fu-abd-p leise">weitere</th><td colspan="${t.typen.length + 1}">${t.uebrig.map((z) => `<span class="fu-st fu-st-${U.statusInfo(z.status).klasse}" data-pa="zelle" data-s="${esc(z.schluessel)}" tabindex="0">${esc(z.schluessel)}</span>`).join(' ')}</td></tr>` : '';
  return `<h3>Abdeckung</h3><div class="fu-legende">${legende}<span class="leise">Klick auf einen Wert zeigt seine Quelle(n); hochgestellt: Zahl der Quellen</span></div>
    <div class="fu-abd-rahmen"><table class="tab fu-abd"><thead>${kopf}</thead><tbody>${zeilen}${uebrig}</tbody></table></div>`;
}

export function abdeckungDetailHtml(def, schluessel, ui = {}) {
  const t = U.abdeckungTabelle(def);
  const z = [...t.zeilen.flatMap((r) => [r.vorgabe, ...r.typen]), ...t.uebrig].find((x) => x?.schluessel === schluessel);
  if (!z) return '';
  const st = U.statusInfo(z.status);
  const best = z.bestaetigt;
  return `<div class="fu-abd-detail" data-s="${esc(z.schluessel)}">
    <div class="fu-abd-detail-kopf"><span class="fu-chip fu-st-${st.klasse}">${esc(st.text)}</span> <b>«${esc(z.parameter)}»</b>${z.typ ? ` · Typ ${esc(z.typ)}` : ' · Vorgabewert'} · Wert <b>${esc(U.wertText(z.wert) || '–')}</b>${z.pflicht ? '' : ' <span class="leise">(keine Pflicht)</span>'}</div>
    <p class="leise">${esc(st.hinweis)}${z.vermerk ? ' – ' + esc(z.vermerk) : ''}</p>
    ${best ? `<p class="fu-best-info">Bestätigt von ${esc(best.akteur ?? '?')} am ${esc(zeitText(best.zeit))}${best.begruendung ? ' – ' + esc(best.begruendung) : ''}</p>` : ''}
    <div class="fu-quellen-kopf">Quellen (${z.quellen.length})</div>
    ${z.quellen.length ? `<ul class="fu-quellen">${z.quellen.map((id) => `<li data-id="${esc(id)}"><span class="fu-quelle-text">${esc(id)}</span> <button type="button" class="fe-knopf klein" data-pa="quelle" data-id="${esc(id)}">Quelle ansehen</button></li>`).join('')}</ul>`
      : '<p class="leise">Keine Quelle genannt.</p>'}
    ${z.konflikt ? `<p><button type="button" class="fe-knopf" data-pa="zum-konflikt" data-id="${esc(z.konflikt)}">Zum Konflikt</button></p>` : ''}
    ${U.bestaetigbar(z) ? `<div class="fu-best"><input type="text" class="fe-in breit fu-best-begr" data-pa-feld="best-begr" data-kein-enter="1" value="${esc(ui.bestBegr ?? '')}" placeholder="Begründung (z. B. «Wert vom Hersteller telefonisch bestätigt»)" spellcheck="false">
      <button type="button" class="fe-knopf primaer" data-pa="bestaetigen" data-s="${esc(z.schluessel)}" title="Ein Mensch nimmt diesen Wert ausdrücklich an">Wert bestätigen</button></div>
      <p class="leise">${z.status === 'fehlt' ? 'Der Wert hat keine Quelle: bestätigen, oder im Reiter «Unterlagen» eine Quelle zuordnen.' : 'Der Wert ist nur aus einem Bild gelesen: bestätigen, wenn er stimmt.'}</p>` : ''}
  </div>`;
}

// ------------------------------------------------------------------------------------------------ Massvergleich

const f3 = (x) => (x === null || x === undefined ? '–' : String(Math.round(Number(x) * 1000) / 1000));

export function masseZeilenHtml(zeilen, mitTyp = true) {
  return zeilen.map((a) => {
    const s = U.massStatus(a);
    return `<tr class="fu-masse-${s}" data-mk="${esc(`${a.typ}/${a.mass}`)}">${mitTyp ? `<td>${esc(a.typ)}</td>` : ''}<td><b>${esc(a.mass)}</b></td><td>${esc(a.ansicht ?? '')}</td>
      <td class="num">${f3(a.soll)}</td><td class="num">${f3(a.ist)}</td><td class="num">${a.abweichung === null || a.abweichung === undefined ? '–' : (a.abweichung > 0 ? '+' : '') + f3(a.abweichung)}</td>
      <td class="num">${a.toleranz === null || a.toleranz === undefined ? '–' : '±' + f3(a.toleranz)}</td>
      <td class="fu-masse-ergebnis">${s === 'ok' ? '✓ bestanden' : s === 'schlecht' ? '✗ ausserhalb' : '– ohne Vergleich'}</td>
      <td>${a.befund ? `<button type="button" class="fe-knopf klein" data-pa="quelle" data-id="${esc(a.befund)}" title="${esc(a.quelle ?? '')}">Quelle</button>` : esc(a.quelle ?? '')}${(a.hinweise ?? []).length ? ` <span class="leise" title="${esc(a.hinweise.join(' · '))}">ⓘ</span>` : ''}${(a.weitereQuellen ?? []).length ? ` <span class="leise" title="${esc(a.weitereQuellen.map((w) => `${w.wert}: ${w.stelle}`).join('\n'))}">+${a.weitereQuellen.length}</span>` : ''}</td></tr>`;
  }).join('');
}

export function masseHtml(masse, fehler) {
  if (fehler) return `<h3>Massvergleich</h3><p class="fehlertext">${esc(fehler)}</p>`;
  if (!masse) return '<h3>Massvergleich</h3><p class="leise">Wird geladen …</p>';
  const zeilen = U.masseZeilen(masse);
  const titel = `Massvergleich (${masse.bestanden ?? 0} bestanden${masse.ausserhalb ? `, <span class="fehlertext">${masse.ausserhalb} ausserhalb</span>` : ''}${masse.ohneVergleich ? `, ${masse.ohneVergleich} ohne Vergleich` : ''})`;
  if (masse.hinweis || zeilen.length === 0) return `<h3>${titel}</h3><p class="leise">${esc(masse.hinweis ?? 'Keine Masse zu vergleichen.')}</p>`;
  return `<h3>${titel}</h3><p class="leise">Mass an der Familie (Ist) gegen den Wert der Unterlage (Soll) je Typ. Massbilder in Datenblättern sind schematisch: verglichen wird über die Werte.</p>
    <div class="fu-abd-rahmen"><table class="tab fu-masse"><thead><tr><th>Typ</th><th>Mass</th><th>Ansicht</th><th>Soll</th><th>Ist</th><th>Abweichung</th><th>Toleranz</th><th>Ergebnis</th><th>Quelle</th></tr></thead><tbody>${masseZeilenHtml(zeilen)}</tbody></table></div>`;
}

// ------------------------------------------------------------------------------------------------ Kontrollbild

export const VG_ANSICHTEN = [['vorne', 'Vorne'], ['hinten', 'Hinten'], ['links', 'Links'], ['rechts', 'Rechts'], ['oben', 'Oben'], ['unten', 'Unten']];
const VG_ARTEN = new Set(['pdf', 'dwg', 'dxf', 'bild', 'step', 'iges', 'stl', 'obj', 'ifc']);
export const vergleichbareUnterlagen = (kandidaten) => (kandidaten ?? []).filter((u) => !u.art || VG_ARTEN.has(u.art));

export function vergleichFormHtml(fam, kandidaten, vg, offen) {
  const uls = vergleichbareUnterlagen(kandidaten);
  const typen = (fam?.typen ?? []).map((t) => t.name);
  const u = uls.find((x) => x.ref === vg.unterlage);
  const sel = (name, optionen, wert) => `<select class="fe-in" data-vg="${name}">${optionen.map(([w, t]) => `<option value="${esc(w)}"${w === wert ? ' selected' : ''}>${esc(t)}</option>`).join('')}</select>`;
  const feld = (n, label) => `<label class="fu-pp-feld">${label} <input type="text" class="fe-in xyz" data-vg-pp="${n}" data-kein-enter="1" value="${esc(vg.felder?.[n] ?? '')}" size="7" spellcheck="false"></label>`;
  return `<div class="fu-vg-zeile">
      <label>Unterlage ${uls.length ? sel('unterlage', uls.map((x) => [x.ref, `${x.datei}${x.art ? ' (' + (U.ART_NAMEN[x.art] ?? x.art) + ')' : ''}`]), vg.unterlage) : '<select class="fe-in" data-vg="unterlage" disabled><option>keine Unterlage</option></select>'}</label>
      <label class="fu-vg-seite"${u && u.art && u.art !== 'pdf' ? ' hidden' : ''}>Seite <input type="text" class="fe-in" data-vg="seite" data-kein-enter="1" value="${esc(vg.seite ?? '1')}" size="3"></label>
      <label>Ansicht ${sel('ansicht', VG_ANSICHTEN, vg.ansicht)}</label>
      <label>Typ ${typen.length ? sel('typ', typen.map((n) => [n, n]), vg.typ) : '<select class="fe-in" data-vg="typ" disabled><option>kein Typ</option></select>'}</label>
      <label>Detailgrad ${sel('detailgrad', [['grob', 'grob'], ['mittel', 'mittel'], ['fein', 'fein']], vg.detailgrad)}</label>
    </div>
    <details class="fu-passpunkte"${offen ? ' open' : ''}><summary>Passpunkte (zwei Punktpaare; leer = Einpassung über ein Mass der Familie)</summary>
      <table class="fu-pp"><tbody>
        <tr><th>Punkt 1</th><td>Familie ${feld('a_u', 'u')} ${feld('a_v', 'v')} <span class="leise">mm</span></td><td>Unterlage ${feld('a_x', 'x')} ${feld('a_y', 'y')}</td></tr>
        <tr><th>Punkt 2</th><td>Familie ${feld('b_u', 'u')} ${feld('b_v', 'v')} <span class="leise">mm</span></td><td>Unterlage ${feld('b_x', 'x')} ${feld('b_y', 'y')}</td></tr>
      </tbody></table>
      <div class="fu-pick-zeile"><button type="button" class="fe-knopf" data-pa="pick-start" title="Abwechselnd in der Familienansicht (rechts) und im Bild der Unterlage klicken">Punkte anklicken …</button>
        <button type="button" class="fe-knopf" data-pa="pick-ende" hidden>Beenden</button><button type="button" class="fe-knopf" data-pa="pp-leeren">Felder leeren</button>
        <button type="button" class="fe-knopf" data-pa="unterlage-zeigen">Unterlage zeigen</button><span class="fu-pick-hinweis"></span></div>
    </details>
    <div class="fu-vg-start"><button type="button" class="fe-knopf primaer" data-pa="vergleichen">Kontrollbild erzeugen</button><span class="fu-vg-status leise"></span></div>`;
}

export function vergleichErgebnisHtml(r, veraltet = false) {
  if (!r) return '';
  const schem = U.istSchematisch(r);
  const z = U.masseZeilen(r.abweichungen ?? []);
  const geo = U.geometrischZeilen(r.geometrisch);
  const ein = r.einpassung;
  return `<figure class="fu-vg-fig"><img class="fu-vg-img" src="data:${esc(r.mime ?? 'image/png')};base64,${r.base64}" alt="Kontrollbild: Unterlage mit der Familie in Rot">
      <figcaption>${esc(r.familie)} «${esc(r.name ?? '')}», Typ ${esc(r.typ)}, Ansicht ${esc(r.ansicht)} über ${esc(r.datei ?? r.unterlage ?? '')}${r.seite ? ` (Seite ${esc(r.seite)})` : ''}
        ${schem ? `<span class="fu-schematisch" title="${esc(r.hinweis ?? U.SCHEMATISCH_TEXT)}">schematisch</span>` : '<span class="fu-massstaeblich">massstäblich</span>'}
        ${veraltet ? '<span class="fu-veraltet">Stand vor der letzten Änderung der Familie</span>' : ''}</figcaption></figure>
    ${schem ? `<p class="fu-schem-hinweis">${esc(r.hinweis ?? U.SCHEMATISCH_TEXT)}</p>` : ''}
    ${ein ? `<p class="leise">Einpassung: ${esc(ein.quelle)} · Massstab ${esc(ein.massstab)} (${esc(ein.einheit ?? '')}) · Drehung ${esc(ein.drehung)}°</p>` : ''}
    ${z.length ? `<div class="fu-abd-rahmen"><table class="tab fu-masse fu-vg-masse"><thead><tr><th>Mass</th><th>Ansicht</th><th>Soll</th><th>Ist</th><th>Abweichung</th><th>Toleranz</th><th>Ergebnis</th><th>Quelle</th></tr></thead><tbody>${masseZeilenHtml(z, false)}</tbody></table></div>` : '<p class="leise">Keine Massbezüge in der Abweichungsliste.</p>'}
    ${geo.length ? `<ul class="fu-geo">${geo.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : ''}
    ${(r.hinweise ?? []).length ? `<ul class="fu-vg-hinweise leise">${r.hinweise.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : ''}`;
}
