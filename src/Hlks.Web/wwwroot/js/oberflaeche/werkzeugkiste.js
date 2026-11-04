// Werkzeugkiste im Kontextmenü (Christian 04.11.2026: «im kontextmenü unten ein Rundbutton … bei hover radial angeordnete Hauptgruppen …, diese
// sollen dann bei hover die werkzeuge ausrollen»). Unten im Menü ein runder Knopf; fährt die Maus darüber, erscheinen die Hauptgruppen im Kreis um
// ihn, fährt sie über eine Gruppe, rollt deren Werkzeugliste nach aussen aus. Klick startet das Werkzeug und schliesst das Menü. Nicht verfügbare
// Werkzeuge stehen ausgegraut mit ihrem Hinweis. Der Kranz hängt im Menü (position fixed), schliesst also mit ihm (Esc, Klick daneben).
// Logik (Gruppen, Lagen): kontextmenue-logik.js.

import { aktionen, ausfuehren, istAktiv, hinweisText } from './register.js';
import { symbol } from './symbole.js';
import { esc } from './werkzeug.js';
import { kisteGruppen, radialLagen, ausrollSeite } from './kontextmenue-logik.js';

const RADIUS = 78;        // px, Abstand der Gruppen von der Knopfmitte (Bedienvorgabe)
const VERWEILEN_MS = 200; // Verweilzeit, bevor eine andere Gruppe ausrollt (Bedienvorgabe)

/** Rundknopf für den unteren Rand des Kontextmenüs; schliessen() schliesst das Menü. */
export function kisteKnopf(schliessen) {
  const fuss = document.createElement('div');
  fuss.className = 'wk-fuss';
  const knopf = document.createElement('button');
  knopf.type = 'button';
  knopf.className = 'wk-knopf';
  knopf.title = 'Werkzeugkiste – Maus darüber zeigt die Hauptgruppen';
  knopf.setAttribute('aria-label', 'Werkzeugkiste');
  knopf.innerHTML = symbol('werkzeug', 18);
  fuss.appendChild(knopf);
  let kranz = null;
  const oeffnen = () => {
    if (kranz) return;
    kranz = kranzBauen(knopf.getBoundingClientRect(), schliessen);
    fuss.appendChild(kranz);
  };
  knopf.addEventListener('pointerenter', oeffnen);
  knopf.addEventListener('click', oeffnen);   // Touch: Tippen öffnet
  return fuss;
}

function kranzBauen(r, schliessen) {
  const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
  const gruppen = kisteGruppen([...aktionen.values()]);
  const lagen = radialLagen(gruppen.length, RADIUS);
  const kranz = document.createElement('div');
  kranz.className = 'wk-kranz';
  // Kranz so verschieben, dass er ganz im Fenster liegt (Knopf bleibt Bezug)
  const rand = RADIUS + 40;
  const dx = Math.max(rand - cx, 0) - Math.max(cx + rand - window.innerWidth, 0);
  const dy = Math.max(rand - cy, 0) - Math.max(cy + rand - window.innerHeight, 0);
  const mx = cx + dx, my = cy + dy;
  let offen = null, warte = 0;
  gruppen.forEach((g, i) => {
    const l = lagen[i];
    const b = document.createElement('button');
    b.type = 'button';
    b.className = 'wk-gruppe';
    b.style.left = `${mx + l.x}px`;
    b.style.top = `${my + l.y}px`;
    b.innerHTML = `${symbol(g.symbol, 20)}<span>${esc(g.titel)}</span>`;
    const ausrollen = () => {
      offen?.remove();
      offen = listeBauen(g, b.getBoundingClientRect(), ausrollSeite(l), { links: mx - RADIUS - 40, rechts: mx + RADIUS + 40 }, schliessen);
      kranz.appendChild(offen);
      kranz.querySelectorAll('.wk-gruppe').forEach((x) => x.classList.toggle('offen', x === b));
    };
    // Ist schon eine Liste offen, wechselt die Gruppe erst nach kurzem Verweilen – der Weg zur offenen Liste darf eine andere Gruppe streifen
    b.addEventListener('pointerenter', () => { clearTimeout(warte); if (!offen) ausrollen(); else warte = setTimeout(ausrollen, VERWEILEN_MS); });
    b.addEventListener('pointerleave', () => clearTimeout(warte));
    b.addEventListener('click', ausrollen);
    kranz.appendChild(b);
  });
  return kranz;
}

function listeBauen(g, r, { seite, unten }, kranzRand, schliessen) {
  const l = document.createElement('div');
  l.className = `wk-liste wk-${seite}`;
  for (const w of g.werkzeuge) {
    const a = aktionen.get(w.id);
    const aktiv = a ? istAktiv(a) : false;
    const b = document.createElement('button');
    b.type = 'button';
    b.classList.toggle('gesperrt', !aktiv);
    if (!aktiv) b.title = hinweisText(a) || 'Im Moment nicht möglich.';
    b.innerHTML = `${symbol(w.symbol ?? 'werkzeug', 16)}<span>${esc(w.titel)}</span>${w.kuerzel ? `<kbd>${esc(w.kuerzel)}</kbd>` : ''}`;
    b.onclick = () => { if (!aktiv) return; schliessen(); ausfuehren(w.id); };
    l.appendChild(b);
  }
  document.body.appendChild(l);   // zum Messen
  const h = l.getBoundingClientRect();
  l.remove();
  const x = seite === 'links' ? kranzRand.links - h.width : kranzRand.rechts;   // ausserhalb des Kranzes
  let y = unten ? r.bottom - h.height : r.top;
  y = Math.max(4, Math.min(y, window.innerHeight - h.height - 4));
  l.style.left = `${Math.max(4, Math.min(x, window.innerWidth - h.width - 4))}px`;
  l.style.top = `${y}px`;
  return l;
}
