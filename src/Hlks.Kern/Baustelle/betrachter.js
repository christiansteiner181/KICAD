// KICAD Baustelle (BIM to field, Stufe 1): Betrachter im selben Modul wie three.js (Namen wie Scene, Mesh direkt im Gültigkeitsbereich).
// Daten aus <script id="kicad-daten">. Alles läuft offline; nichts wird gesendet.
(() => {
  const D = JSON.parse(document.getElementById('kicad-daten').textContent);
  const $ = (id) => document.getElementById(id);
  const el = (tag, attr = {}, ...kinder) => { const e = document.createElement(tag); Object.assign(e, attr); for (const k of kinder) e.append(k); return e; };
  const nachId = new Map(D.elemente.map((e) => [e.id, e]));
  const nachKz = new Map(D.elemente.map((e) => [e.kz, e]));
  let gewaehlt = null;

  // ------------------------------------------------------------ Kopf
  const k = D.kopf;
  $('k-projekt').textContent = k.projekt;
  $('k-rolle').textContent = k.rolle;
  const st = $('k-stand');
  if (k.stand === 'freigegeben') { st.textContent = 'FREIGEGEBEN – ' + (k.freigegebenVon || ''); st.className = 'stand frei'; }
  else { st.textContent = 'ARBEITSSTAND – nicht freigegeben'; st.className = 'stand arbeit'; }
  $('k-info').textContent = `Stand ${k.erstellt} · Modell ${k.modellPruefsumme}` + (k.letztesPaket ? ` · Paket ${k.letztesPaket.slice(0, 8)}` : '');
  document.title = `${k.projekt} – KICAD Baustelle`;

  // ------------------------------------------------------------ Filter
  const werte = (f) => [...new Set(D.elemente.map(f).filter((x) => x))].sort((a, b) => a.localeCompare(b, 'de', { numeric: true }));
  const fuellen = (id, liste, text = (x) => x) => { for (const w of liste) $(id).append(el('option', { value: w, textContent: text(w) })); };
  fuellen('f-geschoss', D.geschosse.map((g) => g.kz), (kz) => { const g = D.geschosse.find((x) => x.kz === kz); return g.name ? `${kz} – ${g.name}` : kz; });
  const abschnitte = D.abschnitte || [];
  const abIds = new Map(abschnitte.map((a) => [a.kz, new Set(a.ids)]));
  const statusText = { geplant: 'geplant', freigegeben: 'freigegeben zur Montage', montiert: 'montiert', geprueft: 'geprüft', abgenommen: 'abgenommen' };
  fuellen('f-abschnitt', abschnitte.map((a) => a.kz), (kz) => { const a = abschnitte.find((x) => x.kz === kz); return [kz, a.name, a.termin].filter((x) => x).join(' – '); });
  fuellen('f-status', D.statusStufen || [], (s) => statusText[s] || s);
  fuellen('f-gewerk', werte((e) => e.gewerk));
  fuellen('f-system', werte((e) => e.system));
  fuellen('f-klasse', werte((e) => e.klasse));
  const filter = () => ({ a: $('f-abschnitt').value, st: $('f-status').value, g: $('f-geschoss').value, w: $('f-gewerk').value, s: $('f-system').value, k: $('f-klasse').value, t: $('f-suche').value.trim().toLowerCase() });
  const passt = (e, f = filter()) => (!f.a || abIds.get(f.a).has(e.id)) && (!f.st || e.status === f.st) && (!f.g || e.geschoss === f.g) && (!f.w || e.gewerk === f.w) && (!f.s || e.system === f.s) && (!f.k || e.klasse === f.k)
    && (!f.t || [e.kz, e.name, e.raum, e.klasse, e.familie, e.typ].some((x) => x && String(x).toLowerCase().includes(f.t)));
  for (const id of ['f-abschnitt', 'f-status', 'f-geschoss', 'f-gewerk', 'f-system', 'f-klasse', 'f-suche']) $(id).addEventListener('input', () => { liste(); material(); d3Filter(); });

  // ------------------------------------------------------------ Reiter
  for (const b of document.querySelectorAll('#reiter button')) b.addEventListener('click', () => reiter(b.dataset.reiter));
  function reiter(name) {
    for (const b of document.querySelectorAll('#reiter button')) b.classList.toggle('aktiv', b.dataset.reiter === name);
    for (const r of document.querySelectorAll('.reiterinhalt')) r.classList.toggle('aktiv', r.id === 'r-' + name);
    if (name === 'd3') d3Groesse();
    if (name === 'plaene' && !planGezeigt) { planZeigen(); planMitElement(); }
  }

  // ------------------------------------------------------------ Zahlen
  const m = (mm) => (mm >= 0 ? '+' : '−') + (Math.abs(mm) / 1000).toFixed(3);
  const wert = (w) => typeof w === 'object' && w !== null ? JSON.stringify(w) : String(w);

  // ------------------------------------------------------------ Elementinfo
  function waehlen(e, quelle) {
    gewaehlt = e;
    const a = $('info'); a.innerHTML = '';
    if (!e) { a.append(el('div', { className: 'leer', textContent: 'Element antippen – im 3D, im Plan oder in der Liste.' })); return; }
    a.append(el('h3', { textContent: e.kz }), el('div', { className: 'unter', textContent: [e.klasse, e.name].filter((x) => x).join(' · ') }));
    const t = el('table'); const tb = el('tbody'); t.append(tb);
    const zeile = (n, w) => { if (w !== undefined && w !== null && w !== '') tb.append(el('tr', {}, el('td', { textContent: n }), el('td', { textContent: w }))); };
    zeile('Geschoss', e.geschoss); zeile('Raum', e.raum); zeile('System', e.system); zeile('Gehört zu', e.teilVon);
    if (e.status) zeile('Montagestatus', (statusText[e.status] || e.status) + (e.statusAm ? ` (${e.statusAm}, ${e.statusVon || ''})` : ''));
    const ab = abschnitte.filter((a) => abIds.get(a.kz).has(e.id)).map((a) => a.name || a.kz); if (ab.length) zeile('Abschnitt', ab.join(', '));
    zeile('Familie', e.familie); zeile('Typ', e.typ);
    const ar = e.artikel || {}; zeile('Hersteller', ar.hersteller?.wert); zeile('Produkt', ar.produkt?.wert); zeile('Artikelnummer', ar.artikelnummer?.wert);
    const h = e.hoehe;
    if (h) {
      const bereich = (a, b, null0 = 0) => a === b ? m(a - null0) + ' m' : `${m(a - null0)} … ${m(b - null0)} m`;
      zeile('Achse ü. ±0.00', bereich(h.achseMinMm, h.achseMaxMm));
      if (h.ukMm !== undefined) { zeile('UK ü. ±0.00', m(h.ukMm) + ' m'); zeile('OK ü. ±0.00', m(h.okMm) + ' m'); }
      if (h.geschossKoteMm !== undefined) { zeile('Achse ü. Geschoss', bereich(h.achseMinMm, h.achseMaxMm, h.geschossKoteMm)); if (h.ukMm !== undefined) zeile('UK ü. Geschoss', m(h.ukMm - h.geschossKoteMm) + ' m'); }
    }
    if (e.mat && e.mat.art === 'leitung') { zeile('Serie', e.mat.serie); zeile('Grösse', e.mat.groesse); zeile('Länge', e.mat.laengeM.toFixed(2) + ' m'); }
    a.append(t);
    const doks = e.eig && e.eig.dokumente && Array.isArray(e.eig.dokumente.wert) ? e.eig.dokumente.wert : [];
    if (doks.length) {
      a.append(el('h4', { textContent: 'Dokumente' }));
      for (const d of doks) { const ziel = d.url || d.datei; a.append(el('div', {}, ziel ? el('a', { href: ziel, target: '_blank', textContent: d.titel || ziel }) : (d.titel || ''), d.rolle ? ` (${d.rolle})` : '')); }
    }
    const eig = Object.entries(e.eig || {}).filter(([n]) => !['dokumente', 'montagestatus'].includes(n));
    if (eig.length) {
      a.append(el('h4', { textContent: 'Eigenschaften' }));
      const t2 = el('table'); const tb2 = el('tbody'); t2.append(tb2);
      for (const [n, v] of eig) tb2.append(el('tr', {}, el('td', { textContent: n }), el('td', { textContent: wert(v.wert) + (v.einheit ? ' ' + v.einheit : '') })));
      a.append(t2);
    }
    rueckmelden(a, e);
    for (const tr of document.querySelectorAll('#el-tabelle tr.gewaehlt')) tr.classList.remove('gewaehlt');
    const tr = document.querySelector(`#el-tabelle tr[data-id="${CSS.escape(e.id)}"]`); if (tr) { tr.classList.add('gewaehlt'); if (quelle !== 'liste') tr.scrollIntoView({ block: 'nearest' }); }
    if (planGezeigt && quelle !== "plan") planMitElement(); else planMarkieren();
    d3Markieren();
  }

  // ------------------------------------------------------------ Elementliste
  function liste() {
    const f = filter();
    const zeilen = D.elemente.filter((e) => passt(e, f));
    const t = el('table'); const kopf = el('tr');
    for (const n of ['Kennzeichen', 'Klasse', 'Name', 'Geschoss', 'Raum', 'System', 'Status']) kopf.append(el('th', { textContent: n }));
    t.append(el('thead', {}, kopf)); const tb = el('tbody'); t.append(tb);
    for (const e of zeilen.slice(0, 3000)) {
      const tr = el('tr'); tr.dataset.id = e.id;
      for (const w of [e.kz, e.klasse, e.name, e.geschoss, e.raum, e.system, e.status ? statusText[e.status] : '']) tr.append(el('td', { textContent: w || '' }));
      tr.addEventListener('click', () => waehlen(e, 'liste'));
      tb.append(tr);
    }
    $('el-tabelle').innerHTML = ''; $('el-tabelle').append(t);
    if (zeilen.length > 3000) $('el-tabelle').append(el('p', { textContent: `… ${zeilen.length - 3000} weitere – Filter enger setzen.` }));
  }

  // ------------------------------------------------------------ Material (nach Auswahl)
  let matZeilen = [];
  function material() {
    const f = filter();
    const leit = new Map(), teil = new Map();
    for (const e of D.elemente) {
      if (!e.mat || !passt(e, f)) continue;
      if (e.mat.art === 'leitung') { const s = `${e.mat.serie}\u0001${e.mat.groesse}`; const x = leit.get(s) || { serie: e.mat.serie, groesse: e.mat.groesse, stueck: 0, laenge: 0 }; x.stueck++; x.laenge += e.mat.laengeM; leit.set(s, x); }
      else { const s = `${e.mat.familie}\u0001${e.mat.typ || ''}`; const x = teil.get(s) || { familie: e.mat.familie, typ: e.mat.typ || '', art: e.mat.art, stueck: 0 }; x.stueck++; teil.set(s, x); }
    }
    const sort = (a, b) => a.localeCompare(b, 'de', { numeric: true });
    const L = [...leit.values()].sort((a, b) => sort(a.serie, b.serie) || sort(a.groesse, b.groesse));
    const T = [...teil.values()].sort((a, b) => (a.art === 'formteil' ? 0 : 1) - (b.art === 'formteil' ? 0 : 1) || sort(a.familie, b.familie) || sort(a.typ, b.typ));
    matZeilen = [['Art', 'Serie/Familie', 'Grösse/Typ', 'Stück', 'Länge m'], ...L.map((x) => ['Leitung', x.serie, x.groesse, x.stueck, x.laenge.toFixed(2)]),
      ...T.map((x) => [x.art === 'formteil' ? 'Formteil' : 'Bauteil', x.familie, x.typ, x.stueck, ''])];
    const t = el('table'); const kopf = el('tr');
    matZeilen[0].forEach((n, i) => kopf.append(el('th', { textContent: n, className: i >= 3 ? 'zahl' : '' })));
    t.append(el('thead', {}, kopf)); const tb = el('tbody'); t.append(tb);
    for (const z of matZeilen.slice(1)) { const tr = el('tr'); z.forEach((w, i) => tr.append(el('td', { textContent: w, className: i >= 3 ? 'zahl' : '' }))); tb.append(tr); }
    $('mat-tabelle').innerHTML = ''; $('mat-tabelle').append(t);
    const teile = [f.a && 'Abschnitt ' + f.a, f.st && 'Status ' + (statusText[f.st] || f.st), f.g && 'Geschoss ' + f.g, f.w && 'Gewerk ' + f.w, f.s && 'System ' + f.s, f.k && 'Klasse ' + f.k, f.t && `«${f.t}»`].filter((x) => x);
    $('mat-umfang').textContent = 'Auswahl: ' + (teile.length ? teile.join(', ') : 'ganzes Modell') + ` · ${L.reduce((s, x) => s + x.laenge, 0).toFixed(2)} m Leitungen`;
  }
  $('mat-csv').addEventListener('click', () => {
    const csv = '﻿' + matZeilen.map((z) => z.map((w) => `"${String(w).replace(/"/g, '""')}"`).join(';')).join('\r\n');
    const a = el('a', { href: URL.createObjectURL(new Blob([csv], { type: 'text/csv' })), download: `${k.projekt}_Material.csv` });
    document.body.append(a); a.click(); a.remove();
  });

  // ------------------------------------------------------------ Rückmeldungen (offline im Browser, als Datei an die Projektleitung)
  const rmSchluessel = 'kicad-rm-' + (k.projektId || k.projekt) + '-' + k.modellPruefsumme;
  const lesen = (s, v) => { try { return JSON.parse(localStorage.getItem(s)) ?? v; } catch { return v; } };
  let meldungen = lesen(rmSchluessel, []);
  $('rm-person').value = lesen('kicad-person', '');
  $('rm-person').addEventListener('change', () => { try { localStorage.setItem('kicad-person', JSON.stringify($('rm-person').value.trim())); } catch { /* nur diese Sitzung */ } });
  function rmSichern() {
    try { localStorage.setItem(rmSchluessel, JSON.stringify(meldungen)); }
    catch { alert('Der Speicher des Browsers ist voll – bitte jetzt «Datei speichern» (Reiter Meldungen) und die Datei weitergeben.'); }
    rmZeigen();
  }
  function rmNeu(e, art, daten) {
    const id = crypto.randomUUID ? crypto.randomUUID() : Date.now() + '-' + Math.random().toString(16).slice(2);
    meldungen.push({ id, zeit: new Date().toISOString(), art, element: e.id, kz: e.kz, ...daten });
    rmSichern();
  }
  async function fotoLesen(datei) {
    const bild = await createImageBitmap(datei);
    const f = Math.min(1, 1600 / Math.max(bild.width, bild.height));
    const cv = el('canvas', { width: Math.round(bild.width * f), height: Math.round(bild.height * f) });
    cv.getContext('2d').drawImage(bild, 0, 0, cv.width, cv.height);
    return cv.toDataURL('image/jpeg', 0.75);
  }
  const artText = { status: 'Status', mangel: 'Mangel', frage: 'Frage', abweichung: 'Abweichung' };
  function rmZeigen() {
    $('rm-zahl').textContent = meldungen.length;
    const t = el('table'); const kopf = el('tr');
    for (const n of ['Zeit', 'Element', 'Art', 'Inhalt', 'Fotos', '']) kopf.append(el('th', { textContent: n }));
    t.append(el('thead', {}, kopf)); const tb = el('tbody'); t.append(tb);
    for (const x of meldungen.slice().reverse()) {
      const tr = el('tr');
      const inhalt = x.art === 'status' ? (statusText[x.status] || x.status)
        : [x.text, x.mass ? `Δx ${x.mass.dx} Δy ${x.mass.dy} Δz ${x.mass.dz} mm` : ''].filter((s) => s).join(' · ');
      tr.append(el('td', { textContent: new Date(x.zeit).toLocaleString('de-CH') }), el('td', { textContent: x.kz }), el('td', { textContent: artText[x.art] }), el('td', { textContent: inhalt }));
      const fo = el('td'); for (const f of x.fotos || []) fo.append(el('img', { src: f })); tr.append(fo);
      const weg = el('button', { textContent: 'Löschen' });
      weg.addEventListener('click', (ev) => { ev.stopPropagation(); meldungen = meldungen.filter((y) => y.id !== x.id); rmSichern(); });
      tr.append(el('td', {}, weg));
      tr.addEventListener('click', () => { const e = nachId.get(x.element); if (e) waehlen(e, 'liste'); });
      tb.append(tr);
    }
    $('rm-tabelle').innerHTML = '';
    $('rm-tabelle').append(meldungen.length ? t : el('p', { textContent: 'Noch keine Meldungen. Element antippen und rechts «Rückmelden».', style: 'padding:12px' }));
  }
  $('rm-speichern').addEventListener('click', () => {
    const person = $('rm-person').value.trim();
    if (!person) { alert('Bitte zuerst den Namen eintragen.'); $('rm-person').focus(); return; }
    const jetzt = new Date();
    const datei = { format: 'kicad-baustelle-rueckmeldung', fassung: 1, projektId: k.projektId, projekt: k.projekt, modellPruefsumme: k.modellPruefsumme,
      exportErstellt: k.erstellt, person, gespeichert: jetzt.toISOString(), meldungen };
    const stempel = jetzt.toISOString().slice(0, 16).replace(/[-:]/g, '').replace('T', '-');
    const name = `KICAD_Rueckmeldung_${k.projekt}_${stempel}.json`.replace(/[^\w.\-]+/g, '_');
    const a = el('a', { href: URL.createObjectURL(new Blob([JSON.stringify(datei)], { type: 'application/json' })), download: name });
    document.body.append(a); a.click(); a.remove();
  });
  $('rm-leeren').addEventListener('click', () => {
    if (meldungen.length && confirm('Alle Meldungen in diesem Browser löschen? Erst, wenn die Datei abgegeben ist.')) { meldungen = []; rmSichern(); }
  });

  function rueckmelden(a, e) {
    const rm = el('div', { className: 'rm' }); rm.append(el('h4', { textContent: 'Rückmelden' }));
    if (e.status) {
      const kn = el('div', { className: 'knoepfe' });
      for (const s of ['montiert', 'geprueft', 'abgenommen']) {
        const b = el('button', { textContent: statusText[s] });
        b.addEventListener('click', () => { rmNeu(e, 'status', { status: s }); b.textContent = '✓ ' + statusText[s]; });
        kn.append(b);
      }
      rm.append(kn);
    }
    const text = el('textarea', { placeholder: 'Mangel, Frage oder Abweichung beschreiben' });
    const foto = el('input', { type: 'file', accept: 'image/*', multiple: true }); foto.setAttribute('capture', 'environment');
    const mass = el('div', { className: 'mass' }, 'Abweichung mm:');
    const m3 = ['dx', 'dy', 'dz'].map((n) => { const i = el('input', { type: 'number', placeholder: n }); mass.append(i); return i; });
    const kn2 = el('div', { className: 'knoepfe' });
    for (const art of ['mangel', 'frage', 'abweichung']) {
      const b = el('button', { textContent: artText[art] });
      b.addEventListener('click', async () => {
        const daten = { text: text.value.trim() };
        if (art === 'abweichung') {
          if (m3.every((i) => i.value === '')) { alert('Bitte die gemessene Abweichung (dx, dy, dz in mm) eintragen.'); return; }
          daten.mass = { dx: +m3[0].value || 0, dy: +m3[1].value || 0, dz: +m3[2].value || 0 };
        } else if (!daten.text && !foto.files.length) { alert('Bitte beschreiben oder ein Foto anhängen.'); return; }
        daten.fotos = []; for (const f of foto.files) daten.fotos.push(await fotoLesen(f));
        rmNeu(e, art, daten); text.value = ''; foto.value = ''; for (const i of m3) i.value = '';
        b.textContent = '✓ ' + artText[art];
      });
      kn2.append(b);
    }
    rm.append(text, foto, mass, kn2);
    a.append(rm);
  }

  // ------------------------------------------------------------ Arbeitsplan (Montageabschnitte)
  function arbeitsplan() {
    const t = el('table'); const kopf = el('tr');
    const stufen = D.statusStufen || [];
    for (const n of ['Nr.', 'Abschnitt', 'Termin', 'Kolonne', 'Elemente', ...stufen.map((s) => statusText[s] || s), 'Bemerkung']) kopf.append(el('th', { textContent: n }));
    t.append(el('thead', {}, kopf)); const tb = el('tbody'); t.append(tb);
    for (const a of abschnitte) {
      const tr = el('tr');
      const el2 = D.elemente.filter((e) => abIds.get(a.kz).has(e.id));
      const termin = [a.termin, a.terminBis].filter((x) => x).join(' – ');
      for (const w of [a.reihenfolge ?? '', [a.kz, a.name].filter((x) => x).join(' '), termin, a.kolonne || '', el2.length, ...stufen.map((s) => el2.filter((e) => e.status === s).length || ''), a.bemerkung || ''])
        tr.append(el('td', { textContent: w }));
      tr.addEventListener('click', () => { $('f-abschnitt').value = a.kz; liste(); material(); d3Filter(); reiter('elemente'); });
      tb.append(tr);
    }
    $('ab-tabelle').innerHTML = '';
    $('ab-tabelle').append(abschnitte.length ? t : el('p', { textContent: 'Keine Montageabschnitte im Modell.', style: 'padding:12px' }));
  }

  // ------------------------------------------------------------ Pläne (SVG)
  let planGezeigt = false, plan = { s: 1, x: 0, y: 0 };
  for (const a of D.ansichten) $('plan-wahl').append(el('option', { value: a.kz, textContent: a.name }));
  $('plan-wahl').addEventListener('change', planZeigen);
  $('plan-passend').addEventListener('click', planEinpassen);
  function planZeigen() {
    planGezeigt = true;
    const a = D.ansichten.find((x) => x.kz === $('plan-wahl').value) || D.ansichten[0];
    const f = $('plan-flaeche'); f.innerHTML = a ? a.svg : '<p style="padding:12px">Keine Ansichten im Modell.</p>';
    const svg = f.querySelector('svg'); if (!svg) return;
    const vb = svg.viewBox.baseVal; svg.setAttribute('width', vb.width); svg.setAttribute('height', vb.height);
    for (const g of svg.querySelectorAll('g[data-id]')) g.addEventListener('click', (ev) => { if (!planGezogen && nachId.has(g.dataset.id)) { ev.stopPropagation(); waehlen(nachId.get(g.dataset.id), 'plan'); } });
    planEinpassen(); planMarkieren();
  }
  // Plan mit dem gewählten Element: der aktuelle, wenn er es enthält, sonst der erste Grundriss bzw. die erste Ansicht damit
  function planMitElement() {
    if (gewaehlt) {
      const hat = (a) => a.svg.includes(`data-id="${gewaehlt.id}"`);
      const jetzt = D.ansichten.find((x) => x.kz === $("plan-wahl").value);
      if (!(jetzt && hat(jetzt))) { const a = D.ansichten.find((x) => x.art === "grundriss" && hat(x)) || D.ansichten.find(hat); if (a) { $("plan-wahl").value = a.kz; planZeigen(); return; } }
    }
    planMarkieren();
  }
  function planEinpassen() {
    const svg = $('plan-flaeche').querySelector('svg'); if (!svg) return;
    const vb = svg.viewBox.baseVal, r = $('plan-flaeche').getBoundingClientRect();
    plan.s = Math.min(r.width / vb.width, r.height / vb.height) * 0.95; plan.x = (r.width - vb.width * plan.s) / 2; plan.y = (r.height - vb.height * plan.s) / 2; planSetzen();
  }
  function planSetzen() { const svg = $('plan-flaeche').querySelector('svg'); if (svg) svg.style.transform = `translate(${plan.x}px,${plan.y}px) scale(${plan.s})`; }
  function planMarkieren() {
    for (const g of document.querySelectorAll('#plan-flaeche g.gewaehlt')) g.classList.remove('gewaehlt');
    if (gewaehlt) for (const g of document.querySelectorAll(`#plan-flaeche g[data-id="${CSS.escape(gewaehlt.id)}"]`)) g.classList.add('gewaehlt');
  }
  const zeiger = new Map(); let planGezogen = false, start = null;
  const pf = $('plan-flaeche');
  pf.addEventListener('wheel', (ev) => { ev.preventDefault(); const r = pf.getBoundingClientRect(); zoom(Math.exp(-ev.deltaY * 0.0015), ev.clientX - r.left, ev.clientY - r.top); }, { passive: false });
  function zoom(f, px, py) { plan.x = px - (px - plan.x) * f; plan.y = py - (py - plan.y) * f; plan.s *= f; planSetzen(); }
  pf.addEventListener('pointerdown', (ev) => { zeiger.set(ev.pointerId, [ev.clientX, ev.clientY]); planGezogen = false; start = { ...plan, z: [...zeiger.values()] }; pf.setPointerCapture(ev.pointerId); });
  pf.addEventListener('pointermove', (ev) => {
    if (!zeiger.has(ev.pointerId)) return; zeiger.set(ev.pointerId, [ev.clientX, ev.clientY]);
    const z = [...zeiger.values()];
    if (z.length === 1 && start.z.length === 1) { const dx = z[0][0] - start.z[0][0], dy = z[0][1] - start.z[0][1]; if (Math.hypot(dx, dy) > 4) planGezogen = true; plan.x = start.x + dx; plan.y = start.y + dy; planSetzen(); }
    else if (z.length === 2 && start.z.length === 2) {
      planGezogen = true; const d0 = Math.hypot(start.z[0][0] - start.z[1][0], start.z[0][1] - start.z[1][1]), d1 = Math.hypot(z[0][0] - z[1][0], z[0][1] - z[1][1]);
      const r = pf.getBoundingClientRect(); const mx = (z[0][0] + z[1][0]) / 2 - r.left, my = (z[0][1] + z[1][1]) / 2 - r.top;
      plan = { ...start }; zoom(d1 / d0, mx, my);
    }
  });
  const loslassen = (ev) => { zeiger.delete(ev.pointerId); start = { ...plan, z: [...zeiger.values()] }; };
  pf.addEventListener('pointerup', loslassen); pf.addEventListener('pointercancel', loslassen);

  // ------------------------------------------------------------ 3D
  const flaeche = $('d3-flaeche');
  let renderer = null;
  try { renderer = new WebGLRenderer({ antialias: true }); } catch { flaeche.append(el('p', { textContent: 'WebGL ist in diesem Browser nicht verfügbar – Pläne und Listen gehen trotzdem.', style: 'padding:12px' })); }
  const szene = new Scene(); szene.background = new Color(0xf2f4f7);
  const kamera = new PerspectiveCamera(45, 1, 0.05, 5000);
  szene.add(new HemisphereLight(0xffffff, 0x8a8f99, 2.2));
  const sonne = new DirectionalLight(0xffffff, 1.4); sonne.position.set(30, 60, 20); szene.add(sonne);
  const steuerung = renderer ? new OrbitControls(kamera, renderer.domElement) : null;
  if (renderer) { renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2)); flaeche.append(renderer.domElement); }
  const gruppen = []; // { kz, modell: Group, unterlage: Group }

  function glb(base64) {
    const bin = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)).buffer;
    const dv = new DataView(bin); const jsonLen = dv.getUint32(12, true);
    const gltf = JSON.parse(new TextDecoder().decode(new Uint8Array(bin, 20, jsonLen)));
    const binStart = 20 + jsonLen + 8;
    const acc = (i) => {
      const a = gltf.accessors[i], v = gltf.bufferViews[a.bufferView], off = binStart + (v.byteOffset || 0) + (a.byteOffset || 0);
      const n = a.count * ({ SCALAR: 1, VEC3: 3, VEC4: 4 }[a.type]);
      return a.componentType === 5126 ? new Float32Array(bin, off, n) : a.componentType === 5125 ? new Uint32Array(bin, off, n) : new Uint16Array(bin, off, n);
    };
    const g = new Group();
    for (const node of gltf.nodes || []) {
      const mesh = gltf.meshes[node.mesh];
      for (const pr of mesh.primitives) {
        const geo = new BufferGeometry();
        geo.setAttribute('position', new BufferAttribute(new Float32Array(acc(pr.attributes.POSITION)), 3));
        if (pr.attributes.NORMAL !== undefined) geo.setAttribute('normal', new BufferAttribute(new Float32Array(acc(pr.attributes.NORMAL)), 3));
        if (pr.indices !== undefined) { const ix = acc(pr.indices); geo.setIndex(new BufferAttribute(ix instanceof Uint16Array ? new Uint16Array(ix) : new Uint32Array(ix), 1)); }
        const mat = gltf.materials[pr.material] || {}; const c = (mat.pbrMetallicRoughness || {}).baseColorFactor || [0.7, 0.7, 0.7, 1];
        const material = new MeshLambertMaterial({ side: DoubleSide, transparent: mat.alphaMode === 'BLEND', opacity: c[3], depthWrite: mat.alphaMode !== 'BLEND' });
        material.color.setRGB(c[0], c[1], c[2]);
        const me = new Mesh(geo, material);
        me.userData.zuordnung = ((mesh.extras || {}).kicad || {}).elemente || null;
        g.add(me);
      }
    }
    return g;
  }

  const geschosseLeiste = $('d3-geschosse');
  for (const gs of D.modell3d.geschosse) {
    const eintrag = { kz: gs.kz, modell: glb(gs.modell), unterlage: gs.unterlage ? glb(gs.unterlage) : null };
    eintrag.unterlage && (eintrag.unterlage.userData.unterlage = true);
    szene.add(eintrag.modell); if (eintrag.unterlage) szene.add(eintrag.unterlage);
    const cb = el('input', { type: 'checkbox', checked: true }); cb.addEventListener('change', () => { eintrag.an = cb.checked; d3Filter(); });
    eintrag.an = true; eintrag.cb = cb;
    geschosseLeiste.append(el('label', {}, cb, ' ' + gs.kz));
    gruppen.push(eintrag);
  }
  $('d3-ifc').addEventListener('change', d3Filter);
  function d3Filter() {
    const fg = $('f-geschoss').value;
    for (const g of gruppen) {
      const sichtbar = g.an && (!fg || g.kz === fg);
      g.modell.visible = sichtbar; if (g.unterlage) g.unterlage.visible = sichtbar && $('d3-ifc').checked;
    }
  }
  function d3Einpassen() {
    const box = new Box3(); for (const g of gruppen) if (g.modell.visible) box.expandByObject(g.modell);
    if (box.isEmpty()) return;
    const c = box.getCenter(new Vector3()), r = box.getSize(new Vector3()).length() / 2 || 10;
    kamera.position.set(c.x + r * 0.9, c.y + r * 0.8, c.z + r * 1.1); kamera.near = r / 500; kamera.far = r * 50; kamera.updateProjectionMatrix();
    steuerung.target.copy(c); steuerung.update();
  }
  $('d3-alles').addEventListener('click', () => { for (const g of gruppen) { g.an = true; g.cb.checked = true; } $('f-geschoss').value = ''; d3Filter(); liste(); material(); d3Einpassen(); });
  function d3Groesse() { if (!renderer) return; const r = flaeche.getBoundingClientRect(); if (r.width < 2) return; renderer.setSize(r.width, r.height); kamera.aspect = r.width / r.height; kamera.updateProjectionMatrix(); }
  window.addEventListener('resize', d3Groesse);

  // Antippen: Dreieck → Element über extras.kicad.elemente { ids, ab, anzahl }
  let markierung = null;
  const strahl = new Raycaster(), maus = new Vector2();
  let down = null;
  if (renderer) {
    renderer.domElement.addEventListener('pointerdown', (ev) => { down = [ev.clientX, ev.clientY]; });
    renderer.domElement.addEventListener('pointerup', (ev) => {
      if (!down || Math.hypot(ev.clientX - down[0], ev.clientY - down[1]) > 5) return;
      const r = renderer.domElement.getBoundingClientRect();
      maus.set(((ev.clientX - r.left) / r.width) * 2 - 1, -((ev.clientY - r.top) / r.height) * 2 + 1);
      strahl.setFromCamera(maus, kamera);
      const treffer = strahl.intersectObjects(gruppen.flatMap((g) => [g.modell, g.unterlage].filter((x) => x && x.visible)), true);
      for (const t of treffer) {
        const z = t.object.userData.zuordnung; if (!z || !z.ids) continue;
        for (let i = 0; i < z.ids.length; i++) if (t.faceIndex >= z.ab[i] && t.faceIndex < z.ab[i] + z.anzahl[i]) {
          const e = nachId.get(z.ids[i]);
          if (e) { waehlen(e, '3d'); return; }
          if (t.object.parent && t.object.parent.userData.unterlage) { waehlen(null); $('info').innerHTML = '<div class="leer">Architekten-IFC (Hintergrund)</div>'; return; }
        }
      }
    });
  }
  function d3Markieren() {
    if (markierung) { szene.remove(markierung); markierung.geometry.dispose(); markierung = null; }
    if (!gewaehlt) return;
    const pos = [];
    for (const g of gruppen) g.modell.traverse((o) => {
      const z = o.userData.zuordnung; if (!o.isMesh || !z) return;
      const i = z.ids.indexOf(gewaehlt.id); if (i < 0) return;
      const p = o.geometry.attributes.position, ix = o.geometry.index;
      for (let f = z.ab[i]; f < z.ab[i] + z.anzahl[i]; f++) for (let j = 0; j < 3; j++) { const v = ix ? ix.getX(f * 3 + j) : f * 3 + j; pos.push(p.getX(v), p.getY(v), p.getZ(v)); }
    });
    if (!pos.length) return;
    const geo = new BufferGeometry(); geo.setAttribute('position', new BufferAttribute(new Float32Array(pos), 3));
    markierung = new Mesh(geo, new MeshBasicMaterial({ color: 0xff3b00, side: DoubleSide, depthTest: false, transparent: true, opacity: 0.85 }));
    markierung.renderOrder = 10; szene.add(markierung);
  }
  function zeichnen() { if (renderer) { steuerung.update(); renderer.render(szene, kamera); } requestAnimationFrame(zeichnen); }

  // ------------------------------------------------------------ Start
  rmZeigen(); arbeitsplan(); liste(); material(); d3Filter(); d3Groesse(); if (renderer) { d3Einpassen(); zeichnen(); }
  // Adresse: #d3 | #plaene | #elemente | #material, dazu ein Kennzeichen (#plaene/E.01 oder #E.01) – für Links auf ein Element
  for (const teil of decodeURIComponent(location.hash.slice(1)).split("/").filter((x) => x)) {
    if (["d3", "plaene", "elemente", "material", "abschnitte", "meldungen"].includes(teil)) reiter(teil); else if (nachKz.has(teil)) waehlen(nachKz.get(teil));
  }
  window.kicad = { daten: D, waehlen: (kz) => waehlen(nachKz.get(kz) || null), reiter };
})();
