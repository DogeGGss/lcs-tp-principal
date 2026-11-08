'use strict';
(() => {
  const panel = document.getElementById('calibration');
  const slide = document.getElementById('slide');
  const defaults = { size: 100, x: 0, y: 0 };
  let fit = { ...defaults };
  let previous;
  try {
    const saved = JSON.parse(sessionStorage.getItem('riftwalker-projector'));
    if (saved && Object.keys(defaults).every(key => Number.isFinite(saved[key]))) fit = saved;
  } catch {}
  function draw() {
    fit.size = Math.max(40, Math.min(100, fit.size));
    const margin = (100 - fit.size) / 2;
    fit.x = Math.max(-margin, Math.min(margin, fit.x));
    fit.y = Math.max(-margin, Math.min(margin, fit.y));
    document.body.style.setProperty('--projection', `translate(${fit.x}vw, ${fit.y}dvh) scale(${fit.size / 100})`);
    document.body.classList.toggle('fitted', fit.size < 100);
    document.getElementById('adjustSize').textContent = fit.size + ' %';
  }
  function open() {
    if (!panel.hidden) return;
    previous = { ...fit };
    panel.hidden = false;
    document.getElementById('applyAdjust').focus();
  }
  function close(save) {
    if (!save) fit = previous;
    else try { sessionStorage.setItem('riftwalker-projector', JSON.stringify(fit)); } catch {}
    panel.hidden = true;
    draw();
    slide.focus({preventScroll:true});
  }
  document.getElementById('calibrate').onclick = open;
  panel.querySelectorAll('[data-size]').forEach(button => button.onclick = () => {
    fit.size += Number(button.dataset.size); draw();
  });
  document.getElementById('applyAdjust').onclick = () => close(true);
  document.getElementById('centerAdjust').onclick = () => { fit = { ...defaults }; draw(); };
  document.addEventListener('keydown', event => {
    if (panel.hidden) {
      if (event.key.toLowerCase() === 'c' && !event.ctrlKey && !event.metaKey && !event.altKey &&
          !document.querySelector('dialog[open]') && !document.body.classList.contains('trailer-expanded') &&
          !event.target.closest('input,textarea,select,[contenteditable=true]')) open();
      return;
    }
    switch (event.key) {
      case '-': fit.size--; break;
      case '+': case '=': fit.size++; break;
      case 'ArrowLeft': fit.x -= 0.5; break;
      case 'ArrowRight': fit.x += 0.5; break;
      case 'ArrowUp': fit.y -= 0.5; break;
      case 'ArrowDown': fit.y += 0.5; break;
      case 'Enter': close(true); break;
      case 'Escape': close(false); break;
      default: return;
    }
    event.preventDefault(); event.stopImmediatePropagation(); draw();
  }, true);
  draw();
})();