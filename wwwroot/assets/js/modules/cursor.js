/**
 * Custom cursor module.
 * Manages the dot cursor and trailing ring with click states.
 */

let mx, my, rx, ry;
let cursorEl, ringEl;

/**
 * Initialize the custom cursor system.
 * @param {HTMLElement} cursor - The cursor dot element
 * @param {HTMLElement} ring - The cursor ring element
 */
export function initCursor(cursor, ring) {
  cursorEl = cursor;
  ringEl = ring;
  mx = window.innerWidth / 2;
  my = window.innerHeight / 2;
  rx = mx;
  ry = my;

  document.addEventListener('mousemove', handleMouseMove);
  document.addEventListener('mousedown', handleMouseDown);
  document.addEventListener('mouseup', handleMouseUp);

  animateRing();
}

function handleMouseMove(e) {
  mx = e.clientX;
  my = e.clientY;
  cursorEl.style.left = mx + 'px';
  cursorEl.style.top = my + 'px';
}

function handleMouseDown() {
  cursorEl.classList.add('clicking');
  ringEl.classList.add('clicking');
}

function handleMouseUp() {
  cursorEl.classList.remove('clicking');
  ringEl.classList.remove('clicking');
}

function animateRing() {
  rx += (mx - rx) * 0.1;
  ry += (my - ry) * 0.1;
  ringEl.style.left = rx + 'px';
  ringEl.style.top = ry + 'px';
  requestAnimationFrame(animateRing);
}
