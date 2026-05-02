/**
 * Landing page entry point.
 * Initializes cursor, 3D scene, and terminal modules.
 */
import { initCursor } from './modules/cursor.js';
import { initScene } from './modules/scene.js';
import { initTerminal } from './modules/terminal.js';

// ── Init Cursor ──
const cursorEl = document.getElementById('cursor');
const cursorRing = document.getElementById('cursor-ring');
initCursor(cursorEl, cursorRing);

// ── Init Three.js Scene ──
const canvas = document.getElementById('three');
initScene(canvas);

// ── Init Terminal ──
const triggerBtn = document.querySelector('.btn-primary');
const terminalEl = document.getElementById('terminal');
const termLogEl = document.getElementById('term-log');
initTerminal(triggerBtn, terminalEl, termLogEl, 'pages/editor.html');
