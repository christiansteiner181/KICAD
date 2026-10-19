// Ziehen auf der Ansichtsfläche (Teil W2): Reiter ziehen mit Andockzielen, Trenner ziehen. Hier ist nur Zeigerbedienung und Zeichnen;
// gerechnet wird in anordnung.js (rein, getestet), die Anordnung hält fenster.js.
//
// Reiter ziehen: Ab 6 px Bewegung beginnt das Ziehen. Auf der Fläche erscheinen die Andockziele der Gruppe unter dem Zeiger (Kreuz aus
// Mitte/links/rechts/oben/unten) und drei Streifen am Rand der ganzen Fläche (links, rechts, unten); eine Vorschau zeigt, wohin die Ansicht
// käme; über einer Reiterleiste zeigt ein Strich die Einfügestelle. Loslassen setzt um, Esc bricht ab. Loslassen ausserhalb des
// Browserfensters öffnet die Ansicht in einem eigenen Fenster.
// Trenner ziehen: die Anteile ändern sich live, gemerkt wird beim Loslassen.

import * as A from './anordnung.js';

const SCHWELLE = 6;          // px, ab der ein Klick zum Ziehen wird
const MIN_PX = 90;           // kleinste Grösse einer Gruppe beim Verschieben eines Trenners (Breite bzw. Höhe)

/**
 * host:
 *   geo()                     aktuelle Rechtecke (A.layoutBerechnen)
 *   anordnung()               aktuelle Anordnung
 *   nameVon(reiterElement)    Name der Ansicht zum Reiter oder null
 *   tabs()                    { gruppenId: [{ x, b }] } Lage der Reiter (relativ zur Fläche)
 *   anwenden(neueAnordnung)   Ergebnis des Ziehens übernehmen und zeichnen
 *   trennerVorlaeufig(neu)    Anordnung während des Ziehens eines Trenners (nicht merken)
 *   trennerFertig()           Trenner losgelassen (merken)
 *   eigenesFenster(name, sx, sy)   Reiter ausserhalb des Fensters losgelassen
 *   kannAuslagern()           false im Zweitfenster
 */
export function einrichten(flaeche, host) {
  let ueberlagerung = null;     // Wurzel der Andockziele während des Ziehens
  let klickSperre = false;

  flaeche.addEventListener('click', (e) => { if (klickSperre) { e.stopPropagation(); e.preventDefault(); klickSperre = false; } }, true);
  flaeche.addEventListener('pointerdown', (e) => {
    if (e.button !== 0) return;
    const trenner = e.target.closest?.('.ag-trenner');
    if (trenner) { trennerZiehen(e, trenner); return; }
    const reiter = e.target.closest?.('.reiterleiste > .reiter');
    if (reiter && !e.target.closest('.zu')) reiterZiehen(e, reiter);
  });

  // ------------------------------------------------------------------------------------------ Trenner

  function trennerZiehen(e, el) {
    const nr = Number(el.dataset.n);
    const t = host.geo().trenner[nr];
    if (!t) return;
    e.preventDefault();
    try { el.setPointerCapture(e.pointerId); } catch { /* egal */ }
    el.classList.add('zieht');
    const mindest = Math.min(0.45, MIN_PX / Math.max(1, t.gesamt));
    const bewegen = (ev) => {
      const r = flaeche.getBoundingClientRect();
      const p = A.trennerPosition(t, ev.clientX - r.left, ev.clientY - r.top);
      host.trennerVorlaeufig(A.trennerSetzen(host.anordnung(), t.pfad, t.index, p, mindest));
    };
    const ende = () => {
      el.classList.remove('zieht');
      el.removeEventListener('pointermove', bewegen);
      el.removeEventListener('pointerup', ende);
      el.removeEventListener('pointercancel', ende);
      host.trennerFertig();
    };
    el.addEventListener('pointermove', bewegen);
    el.addEventListener('pointerup', ende);
    el.addEventListener('pointercancel', ende);
  }

  // ------------------------------------------------------------------------------------------ Reiter

  function reiterZiehen(e, reiter) {
    const name = host.nameVon(reiter);
    if (name == null) return;
    const start = { x: e.clientX, y: e.clientY };
    const quellGruppe = A.gruppeVon(host.anordnung(), name);
    const quelle = quellGruppe ? { gruppe: quellGruppe.id, nurEine: quellGruppe.ansichten.length === 1 } : null;
    let zieht = false, ziel = null, ausserhalb = false, schatten = null;
    try { reiter.setPointerCapture(e.pointerId); } catch { /* egal */ }

    const beginnen = () => {
      zieht = true;
      reiter.classList.add('gezogen');
      document.body.classList.add('ag-zieht');
      ueberlagerung = document.createElement('div');
      ueberlagerung.className = 'ag-ueberlagerung';
      ueberlagerung.innerHTML = '<div class="ag-vorschau" hidden></div><div class="ag-marke" hidden></div><div class="ag-kreuz" hidden></div><div class="ag-rand ag-rand-links" hidden></div><div class="ag-rand ag-rand-rechts" hidden></div><div class="ag-rand ag-rand-unten" hidden></div>';
      flaeche.appendChild(ueberlagerung);
      schatten = document.createElement('div');
      schatten.className = 'ag-schatten';
      schatten.textContent = reiter.querySelector('.titel')?.textContent ?? '';
      document.body.appendChild(schatten);
    };

    const zeichnen = (ev) => {
      const r = flaeche.getBoundingClientRect();
      const x = ev.clientX - r.left, y = ev.clientY - r.top;
      ausserhalb = ev.clientX < 0 || ev.clientY < 0 || ev.clientX >= window.innerWidth || ev.clientY >= window.innerHeight;
      const geo = host.geo();
      ziel = ausserhalb ? null : A.dropZiel(geo, x, y, { breite: r.width, hoehe: r.height, tabs: host.tabs(), quelle });
      schatten.style.left = ev.clientX + 12 + 'px';
      schatten.style.top = ev.clientY + 12 + 'px';
      schatten.classList.toggle('draussen', ausserhalb);
      schatten.textContent = ausserhalb ? (host.kannAuslagern() ? 'Loslassen: eigenes Fenster' : 'Hier loslassen geht nicht') : (reiter.querySelector('.titel')?.textContent ?? '');
      const u = ueberlagerung;
      const vorschau = u.querySelector('.ag-vorschau'), marke = u.querySelector('.ag-marke'), kreuz = u.querySelector('.ag-kreuz');
      const setze = (el, rect) => { el.hidden = !rect; if (rect) { el.style.left = rect.x + 'px'; el.style.top = rect.y + 'px'; el.style.width = rect.b + 'px'; el.style.height = rect.h + 'px'; } };
      setze(vorschau, ziel?.vorschau ?? null);
      setze(marke, ziel?.marke ?? null);
      // Kreuz der Andockziele in der Gruppe unter dem Zeiger (nur wenn mehr als nur die eigene Einzel-Ansicht)
      const g = geo.gruppen.find((q) => x >= q.inhalt.x && x < q.inhalt.x + q.inhalt.b && y >= q.inhalt.y && y < q.inhalt.y + q.inhalt.h);
      const eigeneEinzige = !!g && !!quelle && quelle.nurEine && quelle.gruppe === g.id;
      kreuz.hidden = !g || ausserhalb || eigeneEinzige;
      if (!kreuz.hidden) {
        const lp = A.leitpunkte(g.inhalt);
        kreuz.innerHTML = A.ORTE.map((o) => `<div class="ag-lp ag-lp-${o}${ziel?.art === 'gruppe' && ziel.gruppe === g.id && ziel.ort === o ? ' ziel' : ''}" style="left:${lp[o].x}px;top:${lp[o].y}px;width:${lp[o].b}px;height:${lp[o].h}px"></div>`).join('');
      }
      const einzigEigene = geo.gruppen.length === 1 && eigeneEinzige;
      for (const o of ['links', 'rechts', 'unten']) {
        const el = u.querySelector('.ag-rand-' + o);
        el.hidden = ausserhalb || einzigEigene;
        el.classList.toggle('ziel', ziel?.art === 'rand' && ziel.ort === o);
      }
    };

    const aufraeumen = () => {
      reiter.removeEventListener('pointermove', bewegen);
      reiter.removeEventListener('pointerup', ende);
      reiter.removeEventListener('pointercancel', abbrechen);
      window.removeEventListener('keydown', taste, true);
      try { reiter.releasePointerCapture(e.pointerId); } catch { /* egal */ }
      reiter.classList.remove('gezogen');
      document.body.classList.remove('ag-zieht');
      ueberlagerung?.remove(); ueberlagerung = null;
      schatten?.remove(); schatten = null;
    };
    const bewegen = (ev) => {
      if (!zieht) { if (Math.hypot(ev.clientX - start.x, ev.clientY - start.y) < SCHWELLE) return; beginnen(); }
      zeichnen(ev);
    };
    const ende = (ev) => {
      const war = zieht, z = ziel, draussen = ausserhalb;
      aufraeumen();
      if (!war) return;
      klickSperre = true;
      setTimeout(() => { klickSperre = false; }, 0);
      if (draussen) { if (host.kannAuslagern()) host.eigenesFenster(name, ev.screenX, ev.screenY); return; }
      if (z) host.anwenden(A.zielAnwenden(host.anordnung(), name, z), name);
    };
    const abbrechen = () => { const war = zieht; aufraeumen(); if (war) { klickSperre = true; setTimeout(() => { klickSperre = false; }, 0); } };
    const taste = (ev) => { if (ev.key === 'Escape') { ev.stopPropagation(); abbrechen(); } };
    reiter.addEventListener('pointermove', bewegen);
    reiter.addEventListener('pointerup', ende);
    reiter.addEventListener('pointercancel', abbrechen);
    window.addEventListener('keydown', taste, true);
  }
}
