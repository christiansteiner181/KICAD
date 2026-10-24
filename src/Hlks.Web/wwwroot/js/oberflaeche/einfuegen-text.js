// Reine Funktionen für das Register «Einfügen» (Modul 1b, C1): Texte und HTML der Dialoge (Übersicht der IFC-Datei,
// Lagebericht, Vorschau der Änderungen), Parameter der Befehle ifc_uebernehmen und unterlage_einfuegen. Kein Browser,
// keine Fachlogik: gezeigt wird, was der Kern liefert (ifc_pruefen, Unterlage-Eigenschaft «lage», BefehlsErgebnis);
// gerechnet wird nichts ausser Rundung und Gruppierung. In Node getestet (einfuegen.test.mjs).

export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const istZahl = (x) => typeof x === 'number' && Number.isFinite(x);

/** Zahl mit festen Nachkommastellen («12.500»); Nicht-Zahlen als «–». */
export function zahl(x, stellen = 3) { return istZahl(x) ? x.toFixed(stellen) : '–'; }

/** Punkt [x, y, z] als «Ost 1.000 · Nord 2.000 · Höhe 3.000». Einheit steht in der Spaltenüberschrift bzw. im Text. */
export function punktText(p, einheit = 'm') {
  if (!Array.isArray(p)) return '–';
  const [x, y, z] = p;
  return `Ost ${zahl(x)} · Nord ${zahl(y)} · Höhe ${zahl(z)} ${einheit}`;
}

/** Bezug { x, y, z (mm), drehung } als Text in m. */
export function bezugText(b) {
  if (!b || !istZahl(b.x)) return '–';
  return `Ost ${zahl(b.x / 1000)} · Nord ${zahl(b.y / 1000)} · Höhe ${zahl(b.z / 1000)} m · Drehung ${zahl(b.drehung ?? 0, 4)}°`;
}

export const WAHL_TEXT = {
  projekt: 'Projektbezug (Weltkoordinaten der Datei wie sie sind)',
  gebaeude: 'Gebäudeursprung der Datei auf den Projektursprung',
  koordinationskoerper: 'Koordinationskörper aufeinander',
  'erste Datei': 'Erste Datei: sie setzt den Projektbezug',
  ausdruecklich: 'Ausdrücklich vorgegebener Bezug',
  einpassen: 'Eingepasst über Wände und Raumkanten',
  offen: 'noch nicht gewählt',
};

/** Anteil 0…1 als ganze Prozent («97 %»); Nicht-Zahlen (und Werte ausserhalb 0…1) als null. Nur Rundung, der Kern rechnet. */
export function prozent(anteil) {
  return istZahl(anteil) && anteil >= 0 && anteil <= 1 ? `${Math.round(anteil * 100)} %` : null;
}

/** «97 % der Bauteile im bestehenden Modell» oder null, wenn der Anteil fehlt. */
export function anteilText(anteil) {
  const p = prozent(anteil);
  return p ? `${p} der Bauteile im bestehenden Modell` : null;
}

/**
 * Wahl der Ausrichtung: vier Möglichkeiten mit je einem erklärenden Satz. moeglich = false (mit grund), wenn der Kern die Wahl
 * nicht rechnen kann (Koordinationskörper fehlt in der Datei oder im Projekt; Einpassen: das Projekt hat weder Wände noch Räume).
 * anteil: Inhaltsvergleich des Kerns (lage.inhaltJeWahl, 0…1) oder null (Koordinationskörper, erste Datei, ältere Unterlage);
 * empfohlen: lage.empfehlung nennt diese Wahl. Vorgewählt wird nie.
 * projekt: Anzahl { waende, raeume } im Projekt (Einpassen braucht eines davon) oder null, wenn unbekannt (dann gilt Einpassen als möglich).
 * sofort (nur Einpassen): auch wählbar, wenn die anderen gesperrt sind – erste Datei, bei der der Kern das Einpassen gerechnet hat.
 */
export function ausrichtungOptionen(lage, bezugProjekt, projekt = null) {
  const kkDatei = !!lage?.koordinationskoerper;
  const kkProjekt = !!bezugProjekt?.koordinationskoerper;
  const anteil = (w) => (istZahl(lage?.inhaltJeWahl?.[w]) ? lage.inhaltJeWahl[w] : null);
  const empfohlen = (w) => lage?.empfehlung === w;
  const einpassenMoeglich = !projekt || (Number(projekt.waende) || 0) + (Number(projekt.raeume) || 0) > 0;
  return [
    { wert: 'projekt', titel: 'Projektbezug', moeglich: true, anteil: anteil('projekt'), empfohlen: empfohlen('projekt'),
      satz: 'Die Weltkoordinaten der Datei gelten, wie sie sind: nichts wird gegen den Projektbezug verschoben.' },
    { wert: 'gebaeude', titel: 'Gebäudeursprung', moeglich: true, anteil: anteil('gebaeude'), empfohlen: empfohlen('gebaeude'),
      satz: 'Der Gebäudeursprung der Datei (mit ±0.00) wird auf den Projektursprung gelegt; die Datei wird um die angezeigte Abweichung verschoben.' },
    { wert: 'koordinationskoerper', titel: 'Koordinationskörper', moeglich: kkDatei && kkProjekt, anteil: null, empfohlen: empfohlen('koordinationskoerper'),
      grund: !kkDatei ? 'Die Datei hat keinen Koordinationskörper.' : !kkProjekt ? 'Das Projekt hat noch keinen Koordinationskörper.' : '',
      satz: 'Der Koordinationskörper der Datei wird auf den Koordinationskörper des Projekts gelegt (Lage und Drehung).' },
    { wert: 'einpassen', titel: 'Einpassen (automatisch)', moeglich: einpassenMoeglich, anteil: anteil('einpassen'), empfohlen: empfohlen('einpassen'),
      sofort: !!lage?.einpassen && lage?.bewertung === 'erster',
      grund: einpassenMoeglich ? '' : 'Das Projekt hat noch keine Wände oder Räume, auf die sich einpassen liesse.',
      satz: 'KICAD sucht Drehung und Verschiebung selbst: Wände und Raumkanten der Datei werden auf Wände und Räume des Projekts gelegt. Übernommen wird nur bei eindeutiger Lage.' },
  ];
}

/** Sind die Geschossnamen der Datei eindeutig? Der Kern wählt Geschosse über den Namen; bei doppelten Namen (Archicad: zweimal «OG»)
 *  lässt sich ein einzelnes Geschoss nicht eindeutig wählen. */
export function geschossNamenEindeutig(geschosse) {
  const namen = (geschosse ?? []).map((g) => String(g?.name ?? '').trim().toLowerCase());
  return new Set(namen).size === namen.length;
}

/** Hat die Datei eine Abweichung, die eine ausdrückliche Wahl verlangt? */
export const verlangtWahl = (lage) => lage?.bewertung === 'abweichung';

/** Kann die Wahl geändert werden (Abweichung oder frühere Wahl)? Sonst sind die Optionen ausgegraut. */
export const wahlMoeglich = (lage) => lage?.bewertung === 'abweichung' || lage?.bewertung === 'gewaehlt';

/** Vorbelegung der Wahl: eine frühere Wahl gilt weiter (bewertung = gewaehlt), sonst keine. */
export function vorgabeWahl(lage) {
  const w = lage?.bewertung === 'gewaehlt' ? lage?.ausrichtung : null;
  return w && ['projekt', 'gebaeude', 'koordinationskoerper', 'einpassen'].includes(w) ? w : null;
}

// ---------------------------------------------------------------------------------------------- Lagebericht

/** Zeilen des Lageberichts: [{ label, wert, klasse? }] in der Reihenfolge Gelände, Gebäude, ±0.00, Land, Koordinationskörper, Bezüge. */
export function lageZeilen(lage, bezugProjekt) {
  if (!lage) return [];
  const z = [];
  const drehung = (d) => (istZahl(d) ? ` · Drehung ${zahl(d, 4)}°` : '');
  z.push({ label: 'Gelände (IfcSite)', wert: `${punktText(lage.gelaende?.lage)}${drehung(lage.gelaende?.drehung)}`, tipp: 'Weltkoordinaten der Datei' });
  z.push({ label: 'Gebäude (IfcBuilding)', wert: `${punktText(lage.gebaeude?.lage)}${drehung(lage.gebaeude?.drehung)}`, tipp: 'Weltkoordinaten der Datei' });
  z.push({ label: '±0.00 in der Datei', wert: istZahl(lage.nullHoehe) ? `Höhe ${zahl(lage.nullHoehe)} m (Weltkoordinate z der Datei)` : '–' });
  const l = lage.landes, g = lage.geografisch;
  const hoeheMeer = istZahl(l?.gebaeudeNullpunkt?.hoehe) ? `${zahl(l.gebaeudeNullpunkt.hoehe)} m (aus den Landeskoordinaten)`
    : istZahl(g?.hoehe) ? `${zahl(g.hoehe)} m (Referenzhöhe IfcSite)` : 'nicht angegeben';
  z.push({ label: '±0.00 über Meer', wert: hoeheMeer });
  if (g && (istZahl(g.breite) || istZahl(g.laenge)))
    z.push({ label: 'Geografische Lage', wert: `Breite ${zahl(g.breite, 6)}° · Länge ${zahl(g.laenge, 6)}°` });
  z.push({
    label: 'Landeskoordinaten',
    wert: l
      ? `Ost ${zahl(l.gebaeudeNullpunkt?.ost)} · Nord ${zahl(l.gebaeudeNullpunkt?.nord)} · Höhe ${zahl(l.gebaeudeNullpunkt?.hoehe)} m (Gebäudenullpunkt) · Nordwinkel ${zahl(l.nordwinkel, 4)}°`
        + `${l.bezugssystem ? ` · ${l.bezugssystem}` : ''}${l.quelle ? ` · ${l.quelle}` : ''}`
      : 'Die Datei ist nicht georeferenziert.',
  });
  const kk = lage.koordinationskoerper;
  z.push({ label: 'Koordinationskörper', wert: kk ? `${kk.name ? kk.name + ' · ' : ''}${punktText(kk.lage)}${drehung(kk.drehung)}` : 'Die Datei hat keinen.' });
  z.push({ label: 'Eigener Bezug', wert: `${bezugText(lage.eigenerBezug)}`, tipp: 'Weltpunkt der Datei, der Ursprung und ±0.00 wäre (Gebäudeursprung)' });
  const pb = bezugProjekt;
  z.push({
    label: 'Projektbezug',
    wert: pb && istZahl(pb.x) ? `${bezugText(pb)}${pb.quelle ? ` · Quelle: ${pb.quelle}` : ''}` : 'Noch keiner – diese Datei setzt ihn.',
  });
  const inh = inhaltText(lage.inhalt);
  if (inh) z.push({ label: 'Inhalt', wert: inh, tipp: `Inhaltsvergleich mit dem bestehenden Modell (${lage.inhalt.bauteile ?? '?'} Bauteile der Datei)` });
  return z;
}

/**
 * Inhaltsvergleich der Datei mit dem bestehenden Modell (lage.inhalt): «97 % der Bauteile im bestehenden Modell»; unter 50 % dazu
 * der Versatz der Mitten («Mitte +29.7 m Ost, +21.1 m Nord daneben»). Leer, wenn der Bericht keinen Inhaltsvergleich hat.
 */
export function inhaltText(inhalt) {
  const a = anteilText(inhalt?.ueberdeckung);
  if (!a) return '';
  const v = inhalt.versatzM;
  if (inhalt.ueberdeckung < 0.5 && Array.isArray(v) && istZahl(v[0]) && istZahl(v[1])) {
    const m = (x) => `${x >= 0 ? '+' : '-'}${Math.abs(x).toFixed(1)}`;
    return `${a} · Mitte ${m(v[0])} m Ost, ${m(v[1])} m Nord daneben`;
  }
  return a;
}

/** Abweichung als Zahlen in m: «Ost −1.250 · Nord 0.000 · Höhe 0.000 m · Drehung 0.000°». */
export function abweichungZahlen(a) {
  if (!a) return '';
  const v = (x) => (istZahl(x) ? (x >= 0 ? '+' : '') + (x / 1000).toFixed(3) : '–');
  return `Ost ${v(a.dx)} · Nord ${v(a.dy)} · Höhe ${v(a.dz)} m · Drehung ${istZahl(a.drehung) ? (a.drehung >= 0 ? '+' : '') + a.drehung.toFixed(3) : '–'}°`;
}

// ---------------------------------------------------------------------------------------------- Einpassen (lage.einpassen)

const mitVorzeichen = (x, stellen = 3) => (istZahl(x) ? (x >= 0 ? '+' : '') + x.toFixed(stellen) : '–');
const ERGEBNIS_TEXT = {
  eindeutig: 'eindeutig',
  unsicher: 'unsicher – nicht eindeutig, es wird nichts übernommen',
  'keine Merkmale': 'keine Merkmale – es gibt nichts zum Einpassen, es wird nichts übernommen',
};

/** Ist das Einpassen gerechnet, aber nicht eindeutig (unsicher oder keine Merkmale)? */
export const einpassenUnsicher = (e) => !!e && e.ergebnis !== 'eindeutig';

/** Empfiehlt der Kern «einpassen»? (Feld empfehlung oder der Satz «Empfehlung: ausrichtung = einpassen» im Kerntext) */
export const einpassenEmpfohlen = (lage) => lage?.empfehlung === 'einpassen' || /Empfehlung: ausrichtung = einpassen/.test(lage?.text ?? '');

/** Verschiebung [x, y] in mm als «Ost +0.030 · Nord +0.016 m». */
function verschiebungText(v) {
  return Array.isArray(v) ? `Ost ${mitVorzeichen(v[0] / 1000)} · Nord ${mitVorzeichen(v[1] / 1000)} m` : '–';
}

const merkmaleText = (m) => (m && typeof m === 'object'
  ? `${m.waende ?? 0} Wände, ${m.raeume ?? 0} Räume, ${m.kanten ?? 0} Kanten` : '–');

/** Treffer als «50 mm: 61 % · 100 mm: 81 % · 150 mm: 88 % · Median 21.4 mm · 3469 Proben»; leer, wenn der Bericht keine hat. */
export function trefferText(e) {
  const a = e?.anteil ?? {};
  const t = [['50', a.mm50], ['100', a.mm100], ['150', a.mm150]].filter(([, x]) => prozent(x)).map(([mm, x]) => `${mm} mm: ${prozent(x)}`);
  if (istZahl(e?.medianMm)) t.push(`Median ${zahl(e.medianMm, 1)} mm`);
  if (istZahl(e?.proben)) t.push(`${e.proben} Proben`);
  return t.join(' · ');
}

/**
 * Zeilen des Abschnitts «Einpassen» aus lage.einpassen: [{ label, wert, klasse?, tipp? }]. Nur Zahlen des Kerns, gerundet.
 * klasse = 'schwere-fehler' beim Ergebnis, wenn es nicht eindeutig ist.
 */
export function einpassenZeilen(e) {
  if (!e || typeof e !== 'object') return [];
  const z = [];
  const ok = e.ergebnis === 'eindeutig';
  z.push({ label: 'Ergebnis', wert: ERGEBNIS_TEXT[e.ergebnis] ?? String(e.ergebnis ?? '–'), klasse: ok ? '' : 'schwere-fehler' });
  if (e.text) z.push({ label: 'Text', wert: e.text });
  if (e.paarung) z.push({ label: 'Paarung', wert: e.paarung });
  const abb = 'Abbildung von der Lage «Gebäudeursprung» ins Projekt';
  if (istZahl(e.drehung)) z.push({ label: 'Drehung', wert: `${mitVorzeichen(e.drehung)}°`, tipp: abb });
  if (Array.isArray(e.verschiebungMm)) z.push({ label: 'Verschiebung', wert: verschiebungText(e.verschiebungMm), tipp: abb });
  if (e.bezug && istZahl(e.bezug.x)) z.push({ label: ok ? 'Bezug' : 'Bezug (Vorschlag, nicht übernommen)', wert: bezugText(e.bezug), tipp: 'Diesen Bezug verwendet ifc_uebernehmen für die Datei' });
  const treffer = trefferText(e);
  if (treffer) z.push({ label: 'Treffer', wert: treffer, tipp: 'Anteil der Proben auf den Kanten der Datei, die innert 50, 100 bzw. 150 mm an einer Kante des Projekts liegen' });
  const zw = e.zweitbester;
  z.push({
    label: 'Nächstbeste Lage',
    wert: zw
      ? `Drehung ${mitVorzeichen(zw.drehung)}° · ${verschiebungText(zw.verschiebungMm)}${prozent(zw.anteil150) ? ` · ${prozent(zw.anteil150)} innert 150 mm` : ''}`
      : 'keine andere Lage gefunden',
    tipp: 'Eindeutig heisst: die nächstbeste Lage erreicht höchstens 70 % des Anteils der besten',
  });
  if (e.merkmale?.datei || e.merkmale?.projekt)
    z.push({ label: 'Merkmale', wert: `Datei: ${merkmaleText(e.merkmale.datei)} · Projekt: ${merkmaleText(e.merkmale.projekt)}` });
  const ue = anteilText(e.ueberdeckung);
  if (ue) z.push({ label: 'Gegenprobe', wert: `${ue} (mit dem eingepassten Bezug)` });
  return z;
}

/** Kurzfassung für Listen (Koordinaten): «eindeutig · Drehung −42.718° · 88 % innert 150 mm» oder ''. */
export function einpassenKurz(e) {
  if (!e || typeof e !== 'object') return '';
  const t = [e.ergebnis ?? '–'];
  if (istZahl(e.drehung)) t.push(`Drehung ${mitVorzeichen(e.drehung)}°`);
  if (prozent(e.anteil?.mm150)) t.push(`${prozent(e.anteil.mm150)} innert 150 mm`);
  return t.join(' · ');
}

/** HTML des Abschnitts «Einpassen»: Tabelle der Zeilen und – wenn vorhanden – Tabelle je Geschoss. Leer ohne lage.einpassen. */
export function einpassenHtml(e) {
  const zeilen = einpassenZeilen(e);
  if (zeilen.length === 0) return '';
  let h = `<div class="lage-einpassen${einpassenUnsicher(e) ? ' unsicher' : ''}"><h4>Einpassen</h4>`
    + `<table class="wtab lage-tab"><tbody>${zeilen.map((r) => `<tr${r.tipp ? ` title="${esc(r.tipp)}"` : ''}><th>${esc(r.label)}</th><td${r.klasse ? ` class="${r.klasse}"` : ''}>${esc(r.wert)}</td></tr>`).join('')}</tbody></table>`;
  if (Array.isArray(e.geschosse)) {
    h += e.geschosse.length === 0
      ? '<p class="leise">Alle Merkmale wurden zusammen verglichen (nicht nach Geschossen getrennt).</p>'
      : '<table class="tab kompakt einpassen-geschosse"><thead><tr><th>Geschoss</th><th class="zahl">Kote</th><th class="zahl">Proben</th><th class="zahl">Innert 150 mm</th><th class="zahl">Median [mm]</th></tr></thead><tbody>'
        + e.geschosse.map((g) => `<tr><td>${esc(g.geschoss ?? '–')}</td><td class="zahl">${esc(zahl(g.kote, 2))}</td><td class="zahl">${esc(istZahl(g.proben) ? g.proben : '–')}</td>`
          + `<td class="zahl">${esc(prozent(g.anteil150) ?? '–')}</td><td class="zahl">${esc(istZahl(g.medianMm) ? zahl(g.medianMm, 1) : '–')}</td></tr>`).join('')
        + '</tbody></table>';
  }
  return h + '</div>';
}

/** Anzahl Wände und Räume im Projekt aus einer Elementliste: { waende, raeume } (Einpassen braucht eines davon). */
export function projektMerkmale(elemente) {
  let waende = 0, raeume = 0;
  for (const e of elemente ?? []) { if (e?.klasse === 'Wand') waende++; else if (e?.klasse === 'Raum') raeume++; }
  return { waende, raeume };
}

/**
 * Lagebericht «Einpassen» aus dem Ergebnis eines (Trocken-)Laufs von ifc_uebernehmen. Der Kern legt ihn in lage.einpassen ab
 * (die Eigenschaft «lage» der Unterlage); die Oberfläche liest auch ein Feld einpassen auf oberster Ebene. Fehlt beides: null
 * (dann stehen die Zahlen nur in den Hinweisen des Kerns).
 */
export function einpassenAusErgebnis(r) {
  const e = r?.lage?.einpassen ?? r?.einpassen ?? null;
  return e && typeof e === 'object' ? e : null;
}

/** Hinweis im roten Kasten, wenn der Kern «einpassen» empfiehlt: nennt die Wahl und – wenn bekannt – den Anteil. */
export function einpassenHinweis(lage) {
  const a = prozent(lage?.inhaltJeWahl?.einpassen) ?? prozent(lage?.einpassen?.ueberdeckung);
  return `Empfehlung: Ausrichtung «Einpassen (automatisch)» wählen${a ? ` – damit liegen ${a} der Bauteile im bestehenden Modell` : ''}. KICAD übernimmt nur bei eindeutiger Lage.`;
}

const WARNDREIECK = '<svg class="lage-abw-sym" viewBox="0 0 32 32" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M16 4l13 23H3z"/><path d="M16 13v7M16 23.5v.5"/></svg>';

/** Roter Kasten «Lage weicht ab» mit Warnsymbol, dem Text und den Zahlen des Kerns (Schrift bleibt bei der Designvorgabe 12 px; gross wirkt der Kasten durch Rand, Fläche und Symbol). */
export function abweichungKasten(lage) {
  const abw = lage?.abweichung;
  const text = lage?.text ?? '';
  // Der Kern lehnt auch ab, wenn der Ursprung passt, der Inhalt aber neben dem bestehenden Modell liegt («ACHTUNG Inhalt: …»)
  const inhaltDaneben = text.startsWith('ACHTUNG Inhalt');
  const titel = inhaltDaneben ? 'Achtung: Inhalt liegt neben dem bestehenden Modell' : 'Achtung: Lage weicht vom Projektbezug ab';
  // Bei Inhalt daneben ist abweichung.text der (bestandene) Ursprungsvergleich: gezeigt wird der erste Satz des Kerntextes
  const haupt = inhaltDaneben
    ? (text.replace(/^ACHTUNG Inhalt:\s*/, '').split(/\s(?=Inhalt mit «|Keine der beiden Lagen|Empfehlung:|Ohne ausdrückliche Wahl)/)[0] || (abw?.text ?? text))
    : (abw?.text ?? text);
  const je = lage?.inhaltJeWahl;
  const zeilen = [['Projektbezug', je?.projekt], ['Gebäudeursprung', je?.gebaeude], ['Eingepasst', je?.einpassen]].filter(([, a]) => prozent(a));
  // Trifft keine Wahl das Modell, steht der Hinweis im Kerntext; ohne Kerntext gleichwertig aus den Anteilen (nicht berechenbare Wahl zählt als nicht treffend)
  const keineTreffer = prozent(je?.projekt) && je.projekt < 0.1 && (!prozent(je?.gebaeude) || je.gebaeude < 0.1);
  const keineSatz = /Keine der beiden Lagen[^.]*\./.exec(text)?.[0];
  const empfehlungEinpassen = einpassenEmpfohlen(lage);
  const keine = keineSatz ?? (keineTreffer && !empfehlungEinpassen
    ? 'Keine der beiden Lagen trifft das bestehende Modell – Koordinationskörper in beiden Modellen setzen oder den Bezug ausdrücklich angeben.' : '');
  return `<div class="lage-abweichung gross" role="alert">${WARNDREIECK}<div class="lage-abw-inhalt"><div class="lage-abw-titel">${titel}</div>`
    + `<div class="lage-abw-text">${esc(haupt)}</div>`
    + (abw ? `<div class="lage-abw-zahlen">${esc(abweichungZahlen(abw))}</div>` : '')
    + zeilen.map(([name, a]) => `<div class="lage-abw-text lage-abw-anteil">${esc(name)}: ${esc(anteilText(a))}</div>`).join('')
    + (keine ? `<div class="lage-abw-text">${esc(keine)}</div>` : '')
    + (empfehlungEinpassen ? `<div class="lage-abw-text lage-abw-einpassen">${esc(einpassenHinweis(lage))}</div>`
      : einpassenUnsicher(lage?.einpassen) ? '<div class="lage-abw-text lage-abw-einpassen">Automatisches Einpassen ist hier nicht eindeutig (siehe Abschnitt «Einpassen» im Lagebericht).</div>' : '')
    + (lage?.geaendert ? `<div class="lage-abw-text">${esc(lage.geaendert)}</div>` : '')
    + '<div class="lage-abw-text">Ohne ausdrückliche Wahl der Ausrichtung wird nichts übernommen.</div></div></div>';
}

/**
 * HTML des Lageberichts: Tabelle, bei Abweichung ein roter Kasten mit den Zahlen, darunter der Text des Kerns.
 * opt.ohneKasten: Abweichungskasten weglassen.
 */
export function lageHtml(lage, bezugProjekt, opt = {}) {
  if (!lage) return '<p class="leise">Kein Lagebericht vorhanden.</p>';
  const zeilen = lageZeilen(lage, bezugProjekt);
  const abw = lage.abweichung;
  const rot = lage.bewertung === 'abweichung';
  let h = '';
  if (rot && !opt.ohneKasten) h += abweichungKasten(lage);
  h += `<table class="wtab lage-tab"><tbody>${zeilen.map((r) => `<tr${r.tipp ? ` title="${esc(r.tipp)}"` : ''}><th>${esc(r.label)}</th><td>${esc(r.wert)}</td></tr>`).join('')}`;
  if (abw && !rot) {
    h += `<tr><th>Abweichung</th><td>${esc(abw.text ?? '')}<br><span class="leise">${esc(abweichungZahlen(abw))}</span></td></tr>`;
  } else if (!abw && lage.bewertung === 'erster') {
    h += '<tr><th>Abweichung</th><td>keine – die erste IFC-Datei legt den Projektbezug fest</td></tr>';
  }
  const gewaehlt = lage.ausrichtung && lage.ausrichtung !== 'offen' ? (WAHL_TEXT[lage.ausrichtung] ?? lage.ausrichtung) : null;
  if (gewaehlt) h += `<tr><th>Ausrichtung</th><td>${esc(gewaehlt)}</td></tr>`;
  h += '</tbody></table>';
  if (lage.text && !rot) h += `<p class="lage-text">${esc(lage.text)}</p>`;
  h += einpassenHtml(lage.einpassen);
  if (lage.hinweis) h +=`<p class="leise lage-hinweis">${esc(lage.hinweis)}</p>`;
  return h;
}

// ---------------------------------------------------------------------------------------------- Übersicht der Datei

export const BEREICHE = [
  ['architektur', 'Architektur', 'Wände, Decken, Dächer, Stützen, Unterzüge, Türen, Fenster, Achsen'],
  ['raeume', 'Räume', 'Räume aus der Datei (ohne Räume: aus den Wänden)'],
  ['hlks', 'HLKS', 'Leitungen, Formteile, Armaturen, Apparate, Systeme mit Anschlüssen und Verbindungen'],
];

/** Tabelle «Klasse – Anzahl» aus einem Objekt { name: anzahl }. */
export function zaehlTabelle(obj, kopf = ['Klasse', 'Anzahl']) {
  const eintraege = Object.entries(obj ?? {}).sort((a, b) => b[1] - a[1] || String(a[0]).localeCompare(String(b[0]), 'de'));
  if (eintraege.length === 0) return '<p class="leise">Keine.</p>';
  return `<table class="tab kompakt"><thead><tr><th>${esc(kopf[0])}</th><th class="zahl">${esc(kopf[1])}</th></tr></thead><tbody>${
    eintraege.map(([k, n]) => `<tr><td>${esc(k)}</td><td class="zahl">${esc(n)}</td></tr>`).join('')}</tbody></table>`;
}

/** Dateiname ohne die Prüfsumme, die die Ablage davorsetzt («59341ea2214d_architekt.ifc» → «architekt.ifc»). */
export const ohnePruefsumme = (n) => String(n ?? '').replace(/^[0-9a-f]{12}_/i, '');

/** Kopfzeilen der Übersicht: Fassung, Programm, Einheit, Räume. */
export function kopfZeilen(p) {
  return [
    ['Datei', ohnePruefsumme(p?.datei)],
    ['Fassung', p?.schema],
    ['Programm', p?.programm || '–'],
    ['Einheit der Datei', p?.einheit],
    ['Räume (IfcSpace)', p?.raeume],
  ].map(([k, v]) => [k, v === undefined || v === null || v === '' ? '–' : String(v)]);
}

// ---------------------------------------------------------------------------------------------- Vorschau der Änderungen

const ART_SPALTE = { angelegt: 'neu', geaendert: 'geaendert', geloescht: 'geloescht' };

/** Gruppiert die Änderungen eines (Trocken-)Laufs nach Klasse: { zeilen: [{ klasse, neu, geaendert, geloescht }], summe }. */
export function aenderungenGruppiert(aenderungen) {
  const m = new Map();
  const summe = { neu: 0, geaendert: 0, geloescht: 0 };
  for (const a of aenderungen ?? []) {
    const klasse = a.klasse || '(ohne Klasse)';
    if (!m.has(klasse)) m.set(klasse, { klasse, neu: 0, geaendert: 0, geloescht: 0 });
    const r = m.get(klasse);
    let spalte = ART_SPALTE[a.art];
    // Verbindungen: verbunden zählt als neu, getrennt als gelöscht
    if (a.art === 'verbunden') spalte = 'neu';
    else if (a.art === 'getrennt') spalte = 'geloescht';
    if (!spalte) continue;
    r[spalte]++; summe[spalte]++;
  }
  const zeilen = [...m.values()].sort((a, b) => (b.neu + b.geaendert + b.geloescht) - (a.neu + a.geaendert + a.geloescht) || a.klasse.localeCompare(b.klasse, 'de'));
  return { zeilen, summe };
}

/** HTML der Vorschau: Tabelle je Klasse (neu / geändert / gelöscht) und Summe. */
export function aenderungenTabelle(aenderungen) {
  const { zeilen, summe } = aenderungenGruppiert(aenderungen);
  if (zeilen.length === 0) return '<p class="leise">Keine Änderungen: alles ist schon so übernommen.</p>';
  const z = (n, klasse) => `<td class="zahl ${n ? klasse : 'null'}">${n || '·'}</td>`;
  return `<table class="tab kompakt vorschau-tab"><thead><tr><th>Klasse</th><th class="zahl">neu</th><th class="zahl">geändert</th><th class="zahl">gelöscht</th></tr></thead><tbody>${
    zeilen.map((r) => `<tr><td>${esc(r.klasse)}</td>${z(r.neu, 'art-neu')}${z(r.geaendert, 'art-geaendert')}${z(r.geloescht, 'art-geloescht')}</tr>`).join('')}</tbody>`
    + `<tfoot><tr><th>Summe</th><th class="zahl">${summe.neu}</th><th class="zahl">${summe.geaendert}</th><th class="zahl">${summe.geloescht}</th></tr></tfoot></table>`;
}

/** Hinweise des Kerns als Liste (die erste Zeile ist die Zusammenfassung). */
export function hinweiseHtml(hinweise) {
  const h = (hinweise ?? []).filter(Boolean);
  if (h.length === 0) return '';
  return `<ul class="hinweisliste">${h.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`;
}

// ---------------------------------------------------------------------------------------------- Parameter der Befehle

/**
 * Parameter für ifc_uebernehmen. Bereiche und Geschosse nur senden, wenn eine Auswahl getroffen ist (sonst gilt die Vorgabe «alle»);
 * ausrichtung nur, wenn gewählt.
 */
export function uebernahmeParameter({ datei, bereiche, alleBereiche, geschosse, alleGeschosse, grundlage = true, ausrichtung = null,
  koordinationskoerper = '', konflikte = 'kicad' }) {
  const p = { datei };
  if (bereiche && alleBereiche && bereiche.length < alleBereiche.length) p.bereiche = [...bereiche];
  if (geschosse && alleGeschosse && geschosse.length < alleGeschosse.length) p.geschosse = [...geschosse];
  if (grundlage === false) p.grundlage = false;
  if (ausrichtung) p.ausrichtung = ausrichtung;
  if (koordinationskoerper && String(koordinationskoerper).trim()) p.koordinationskoerper = String(koordinationskoerper).trim();
  if (konflikte && konflikte !== 'kicad') p.konflikte = konflikte;
  return p;
}

/** Prüft die Auswahl im Dialog und liefert einen Fehlertext oder true. */
export function uebernahmePruefen({ bereiche, geschosse, lage, ausrichtung }) {
  if (!bereiche || bereiche.length === 0) return 'Mindestens einen Bereich wählen (Architektur, Räume oder HLKS).';
  if (!geschosse || geschosse.length === 0) return 'Mindestens ein Geschoss wählen.';
  if (verlangtWahl(lage) && !ausrichtung) return 'Die Lage der Datei weicht vom Projektbezug ab: bitte die Ausrichtung ausdrücklich wählen.';
  return true;
}

const ENDUNGEN = { ifc: 'ifc', dwg: 'dwg', dxf: 'dxf', pdf: 'pdf', png: 'bild', jpg: 'bild', jpeg: 'bild' };

/** Art der Unterlage aus dem Dateinamen: ifc | dwg | dxf | pdf | bild | null. */
export function artVonDatei(name) {
  const m = /\.([a-z0-9]+)$/i.exec(String(name ?? '').trim());
  return m ? (ENDUNGEN[m[1].toLowerCase()] ?? null) : null;
}

export const ART_NAMEN = { ifc: 'IFC', dwg: 'DWG', dxf: 'DXF', pdf: 'PDF', bild: 'Bild' };

/** Zahl aus einem Eingabefeld («1:100» oder «100», Komma oder Punkt) oder null. */
export function zahlAusFeld(text) {
  const t = String(text ?? '').trim().replace(/^1\s*:\s*/, '').replace(',', '.');
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/** Parameter für unterlage_einfuegen. Massstab, Seite und dpi nur, wenn angegeben und für die Art sinnvoll. */
export function einfuegenParameter({ datei, geschoss, art, seite = null, massstab = null, dpi = null }) {
  const p = { datei, geschoss };
  if (art) p.art = art;
  if (art === 'pdf' && seite !== null && seite !== undefined) p.seite = seite;
  if ((art === 'pdf' || art === 'bild') && massstab !== null && massstab !== undefined) p.massstab = massstab;
  if (art === 'pdf' && dpi !== null && dpi !== undefined) p.dpi = dpi;
  return p;
}

/** Prüft die Eingaben des Einfügen-Dialogs: Fehlertext oder true. */
export function einfuegenPruefen({ art, geschoss, seite, massstab, massstabText, seiteText, dpi, dpiText }) {
  if (!geschoss) return 'Bitte ein Geschoss wählen.';
  if (art === 'pdf' && String(seiteText ?? '').trim() !== '' && !(Number.isInteger(seite) && seite >= 1)) return 'Die Seite muss eine ganze Zahl ab 1 sein.';
  if ((art === 'pdf' || art === 'bild') && String(massstabText ?? '').trim() !== '' && !(massstab > 0)) return 'Der Massstab muss eine Zahl über 0 sein (z. B. 100 für 1:100).';
  if (art === 'pdf' && String(dpiText ?? '').trim() !== '' && !(dpi > 0)) return 'Die Auflösung muss eine Zahl über 0 sein (dpi).';
  return true;
}

// ---------------------------------------------------------------------------------------------- Unterlagen im Projekt

/** Name einer Unterlage für Listen: Name oder Kennzeichen, ohne die Prüfsumme der Ablage. */
export const unterlageName = (u) => ohnePruefsumme(u?.name || u?.kennzeichen || '');

/** Kurztext zu einer Unterlage für Listen: «IFC · EG». */
export function unterlageKurz(u) {
  const art = ART_NAMEN[u?.werte?.art] ?? u?.werte?.art ?? '?';
  return [art, u?.geschoss].filter(Boolean).join(' · ');
}

/** Sortierte Unterlagen aus einer Elementliste. */
export function unterlagenSortiert(liste) {
  return [...liste].sort((a, b) => unterlageName(a).localeCompare(unterlageName(b), 'de', { numeric: true }));
}

/** Projektbezug (ifcBezug der Projektinformation) aus einer Elementliste oder null. */
export function ifcBezugVon(elemente) {
  const pi = (elemente ?? []).find((e) => e.klasse === 'Projektinformation');
  const b = pi?.werte?.ifcBezug;
  return b && typeof b === 'object' && istZahl(b.x) ? b : null;
}

/** Zeilen der Koordinaten-Übersicht zum Projektbezug: [[label, wert]]. */
export function bezugZeilen(b) {
  if (!b) return [['Projektbezug', 'Noch keiner: der erste IFC-Übertrag legt ihn fest (Gebäudeursprung und ±0.00 dieser Datei).']];
  const z = [
    ['Projektbezug', bezugText(b)],
    ['Quelle', b.quelle || '–'],
  ];
  const l = b.landes;
  z.push(['Landeskoordinaten', l
    ? `Ost ${zahl(l.ost)} · Nord ${zahl(l.nord)} · Höhe ${zahl(l.hoehe)} m · Nordwinkel ${zahl(l.nordwinkel, 4)}°${l.bezugssystem ? ` · ${l.bezugssystem}` : ''}${l.quelle ? ` · ${l.quelle}` : ''}`
    : 'Die Quelle ist nicht georeferenziert.']);
  const k = b.koordinationskoerper;
  z.push(['Koordinationskörper', k
    ? `${k.name ? k.name + ' · ' : ''}Ost ${zahl(k.x / 1000)} · Nord ${zahl(k.y / 1000)} · Höhe ${zahl(k.z / 1000)} m (Projektkoordinaten) · Drehung ${zahl(k.drehung ?? 0, 4)}°`
    : 'Die Quelle hat keinen.']);
  return z;
}
