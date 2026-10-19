// Ansichtsreiter ohne Rollbalken (Teil R, entwurf/menueband-revit.md, Punkt 6): Passen nicht alle Reiter in die Zeile, werden sie
// schmaler (bis zu einer Mindestbreite, der Titel kürzt mit «…»); reicht auch das nicht, steht rechts «▾ n weitere» mit der Liste
// aller offenen Ansichten (wie Revit «Fenster wechseln»). Der aktive Reiter bleibt immer sichtbar.
// Die Reiter selbst legt fenster.js an; dieses Modul beobachtet nur eine Reiterleiste (kein Eingriff in fenster.js). Seit W2 hat jede Gruppe
// ihre eigene Leiste und ruft einrichten() für sich auf; als «aktiv» (bleibt immer sichtbar) gilt der aktive Reiter und der sichtbare Reiter der Gruppe.
// Die Berechnung (reiterPlan) ist eine reine Funktion und in Node testbar.

export const MINDESTBREITE = 96;   // px; kürzere Reiter (von Natur aus) bleiben, wie sie sind
export const ABSTAND = 1;          // px zwischen den Reitern (wie hlks.css)

/**
 * Verteilt den Platz der Reiterzeile.
 *   breiten       natürliche Breiten der Reiter in px (Reihenfolge der Ansichten)
 *   aktiv         Index des aktiven Reiters (oder -1)
 *   verfuegbar    Breite der Zeile in px
 *   weitereBreite Breite des Knopfs «▾ n weitere»
 * Ergebnis: { sichtbar: [Index …], versteckt: [Index …], breiten: { Index: px } }
 */
export function reiterPlan({ breiten, aktiv = -1, verfuegbar, weitereBreite = 0, mindest = MINDESTBREITE, abstand = ABSTAND }) {
  const n = breiten.length;
  const alle = breiten.map((_, i) => i);
  const minVon = (i) => Math.min(breiten[i], mindest);
  const plan = (sichtbar, platz) => {
    // Wasserstandsverfahren: kurze Reiter bleiben natürlich breit, die längeren teilen sich den Rest zu gleicher Breite (mindestens Mindestbreite)
    const ordnung = [...sichtbar].sort((a, b) => breiten[a] - breiten[b]);
    let rest = platz - abstand * Math.max(0, sichtbar.length - 1);
    let anzahl = ordnung.length;
    const ergebnis = {};
    let deckel = Infinity;
    for (const i of ordnung) {
      if (breiten[i] * anzahl <= rest) { ergebnis[i] = breiten[i]; rest -= breiten[i]; anzahl--; }
      else { deckel = Math.floor(rest / anzahl); break; }
    }
    for (const i of ordnung) if (!(i in ergebnis)) ergebnis[i] = Math.max(minVon(i), Math.min(breiten[i], deckel));
    return ergebnis;
  };
  if (n === 0) return { sichtbar: [], versteckt: [], breiten: {} };
  const naturlich = breiten.reduce((s, b) => s + b, 0) + abstand * (n - 1);
  if (!Number.isFinite(verfuegbar) || verfuegbar <= 0 || naturlich <= verfuegbar) return { sichtbar: alle, versteckt: [], breiten: Object.fromEntries(alle.map((i) => [i, breiten[i]])) };

  const mindestSumme = (idx) => idx.reduce((s, i) => s + minVon(i), 0) + abstand * Math.max(0, idx.length - 1);
  if (mindestSumme(alle) <= verfuegbar) return { sichtbar: alle, versteckt: [], breiten: plan(alle, verfuegbar) };

  // Es passen nicht alle: so viele wie möglich von vorn, der aktive Reiter bleibt sichtbar
  const platz = verfuegbar - weitereBreite - abstand;
  let k = 0;
  while (k < n && mindestSumme(alle.slice(0, k + 1)) <= platz) k++;
  k = Math.max(k, 1);
  let sichtbar = alle.slice(0, k);
  if (aktiv >= 0 && !sichtbar.includes(aktiv)) sichtbar = [...sichtbar.slice(0, k - 1), aktiv].sort((a, b) => a - b);
  const versteckt = alle.filter((i) => !sichtbar.includes(i));
  return { sichtbar, versteckt, breiten: plan(sichtbar, Math.max(platz, mindestSumme(sichtbar))) };
}

/** Text des Knopfs. */
export const weitereText = (n) => `▾ ${n} weitere`;

// ---------------------------------------------------------------------------------------------- DOM

/**
 * Richtet eine Reiterzeile ein: beobachtet Grösse und Inhalt, verteilt die Reiter und pflegt «▾ n weitere».
 * Rückgabe: { anpassen, schliessen, abbauen } (anpassen/schliessen für Tests; abbauen meldet alle Beobachter ab, wenn die Leiste entfällt).
 */
export function einrichten(leiste) {
  const knopf = document.createElement('button');
  knopf.type = 'button';
  knopf.className = 'reiter-weitere';
  knopf.hidden = true;
  knopf.setAttribute('aria-haspopup', 'listbox');
  knopf.setAttribute('aria-expanded', 'false');
  const liste = document.createElement('div');
  liste.className = 'reiter-liste';
  liste.setAttribute('role', 'listbox');
  liste.hidden = true;
  leiste.append(knopf, liste);

  let signatur = '';
  const reiter = () => [...leiste.querySelectorAll(':scope > .reiter')];
  const istAktiv = (r) => r.classList.contains('aktiv') || r.classList.contains('gruppen-aktiv');
  const titelVon = (r) => r.querySelector('.titel')?.textContent ?? '';

  function listeFuellen(alle, versteckt) {
    liste.innerHTML = '';
    alle.forEach((r, i) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'rl-eintrag' + (istAktiv(r) ? ' aktiv' : '') + (versteckt.includes(i) ? ' verdeckt' : '');
      b.setAttribute('role', 'option');
      b.textContent = titelVon(r);
      b.title = r.title || '';
      b.addEventListener('click', () => { schliessen(); r.click(); });
      liste.appendChild(b);
    });
  }

  function anpassen(erzwingen = false) {
    const alle = reiter();
    const sig = `${leiste.clientWidth}|${alle.map((r) => titelVon(r) + (istAktiv(r) ? '*' : '')).join('\u0001')}`;
    if (!erzwingen && sig === signatur) return;
    signatur = sig;
    // natürliche Breiten messen
    for (const r of alle) { r.classList.remove('weg'); r.style.width = ''; }
    knopf.hidden = false; knopf.textContent = weitereText(alle.length);
    const weitereBreite = knopf.offsetWidth;
    knopf.hidden = true;
    const breiten = alle.map((r) => r.getBoundingClientRect().width);
    const plan = reiterPlan({ breiten, aktiv: alle.findIndex(istAktiv), verfuegbar: leiste.clientWidth, weitereBreite });
    alle.forEach((r, i) => {
      const sichtbar = plan.sichtbar.includes(i);
      r.classList.toggle('weg', !sichtbar);
      r.style.width = sichtbar && plan.breiten[i] < breiten[i] - 0.5 ? `${plan.breiten[i]}px` : '';
      r.classList.toggle('schmal', sichtbar && plan.breiten[i] < breiten[i] - 0.5);
    });
    if (plan.versteckt.length > 0) {
      knopf.hidden = false;
      knopf.textContent = weitereText(plan.versteckt.length);
    }
    leiste.classList.toggle('mit-weitere', plan.versteckt.length > 0);
    listeFuellen(alle, plan.versteckt);
    if (plan.versteckt.length === 0) schliessen();
  }

  function schliessen() { liste.hidden = true; knopf.setAttribute('aria-expanded', 'false'); }
  function oeffnen() {
    liste.hidden = false;
    knopf.setAttribute('aria-expanded', 'true');
    // rechtsbündig unter dem Knopf, soweit der Platz reicht
    const l = leiste.getBoundingClientRect(), k = knopf.getBoundingClientRect();
    liste.style.right = `${Math.max(0, Math.round(l.right - k.right))}px`;
  }
  knopf.addEventListener('click', () => (liste.hidden ? oeffnen() : schliessen()));
  const beiZeiger = (e) => { if (!liste.hidden && !e.target.closest?.('.reiter-liste, .reiter-weitere')) schliessen(); };
  const beiTaste = (e) => { if (e.key === 'Escape' && !liste.hidden) { schliessen(); e.stopPropagation(); } };
  document.addEventListener('pointerdown', beiZeiger, true);
  window.addEventListener('keydown', beiTaste, true);

  const groesse = typeof ResizeObserver === 'function' ? new ResizeObserver(() => anpassen()) : null;
  groesse?.observe(leiste);
  const inhalt = typeof MutationObserver === 'function' ? new MutationObserver(() => anpassen()) : null;
  inhalt?.observe(leiste, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ['class'] });
  function abbauen() {
    document.removeEventListener('pointerdown', beiZeiger, true);
    window.removeEventListener('keydown', beiTaste, true);
    groesse?.disconnect();
    inhalt?.disconnect();
  }
  anpassen(true);
  return { anpassen, schliessen, abbauen };
}
