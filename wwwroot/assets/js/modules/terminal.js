/**
 * Terminal panel module.
 * Handles the compiler output animation and redirect to editor.
 */

const LOG_SEQUENCES = [
  [
    { delay: 0,    text: '<span class="tag">[INIT]</span>      orphinel runtime v0.1.0 loaded' },
    { delay: 120,  text: '<span class="tag">[PARSE]</span>     scanning token stream<span class="dim"> ············</span> <span class="ok">done</span>' },
    { delay: 280,  text: '<span class="tag">[PARSE]</span>     resolving symbol table — <span class="warn">312 tokens</span>' },
    { delay: 460,  text: '<span class="tag">[TYPECHECK]</span> inferring types<span class="dim"> ················</span> <span class="ok">pass</span>' },
    { delay: 620,  text: '<span class="tag">[LINK]</span>      binding 3 modules — core · std · user' },
    { delay: 820,  text: '<span class="tag">[CODEGEN]</span>   emitting IR for target <span class="warn">orphin-vm/x64</span>' },
    { delay: 1050, text: '<span class="tag">[CODEGEN]</span>   optimising passes: DCE · inlining · CSE' },
    { delay: 1280, text: '<span class="tag">[EMIT]</span>      writing bytecode<span class="dim"> ···············</span> <span class="ok">1.4 kb</span>' },
    { delay: 1480, text: '<span class="tag">[BUILD]</span>     <span class="ok">✓ compiled successfully in 147ms</span><span class="term-cursor"></span>' },
  ],
  [
    { delay: 0,    text: '<span class="tag">[INIT]</span>      re-entry detected — hot reload' },
    { delay: 140,  text: '<span class="tag">[PARSE]</span>     diffing AST<span class="dim"> ····················</span> <span class="ok">2 changed</span>' },
    { delay: 320,  text: '<span class="tag">[TYPECHECK]</span> re-checking delta scope<span class="dim"> ·········</span> <span class="ok">pass</span>' },
    { delay: 500,  text: '<span class="tag">[LINK]</span>      incremental link — skipping 2 modules' },
    { delay: 700,  text: '<span class="tag">[CODEGEN]</span>   patching IR blocks: 4 affected' },
    { delay: 900,  text: '<span class="tag">[EMIT]</span>      delta bytecode<span class="dim"> ·················</span> <span class="ok">0.3 kb</span>' },
    { delay: 1060, text: '<span class="tag">[BUILD]</span>     <span class="ok">✓ incremental build OK in 62ms</span><span class="term-cursor"></span>' },
  ],
  [
    { delay: 0,    text: '<span class="tag">[INIT]</span>      spawning worker thread #3' },
    { delay: 150,  text: '<span class="tag">[PARSE]</span>     tokenising<span class="dim"> ·····················</span> <span class="ok">done</span>' },
    { delay: 310,  text: '<span class="tag">[PARSE]</span>     constructing AST — depth <span class="warn">14</span>' },
    { delay: 480,  text: '<span class="tag">[TYPECHECK]</span> <span class="warn">warning:</span> implicit coercion at line 42' },
    { delay: 650,  text: '<span class="tag">[TYPECHECK]</span> all constraints satisfied<span class="dim"> ·········</span> <span class="ok">ok</span>' },
    { delay: 830,  text: '<span class="tag">[LINK]</span>      resolving extern "math.orphin"' },
    { delay: 1020, text: '<span class="tag">[CODEGEN]</span>   vectorising loops: 3 / 3 eligible' },
    { delay: 1240, text: '<span class="tag">[EMIT]</span>      stripping debug symbols<span class="dim"> ··········</span> <span class="ok">done</span>' },
    { delay: 1430, text: '<span class="tag">[BUILD]</span>     <span class="ok">✓ release build complete in 209ms</span><span class="term-cursor"></span>' },
  ],
];

let termTimer = null;
let termHideTimer = null;
let termRunning = false;
let seqIndex = 0;

/**
 * Initialize terminal panel.
 * Binds the "Open Compiler" button to trigger the animation,
 * then redirects to editor page after the animation completes.
 * @param {HTMLElement} triggerBtn - The button that triggers the terminal
 * @param {HTMLElement} terminalEl - The terminal panel element
 * @param {HTMLElement} termLogEl - The log list element
 * @param {string} editorUrl - URL to redirect to after animation
 */
export function initTerminal(triggerBtn, terminalEl, termLogEl, editorUrl) {
  triggerBtn.addEventListener('click', (e) => {
    e.preventDefault();
    runTerminal(terminalEl, termLogEl, editorUrl);
  });
}

function runTerminal(terminalEl, termLogEl, editorUrl) {
  if (termRunning) return;
  termRunning = true;

  clearTimeout(termHideTimer);
  termLogEl.innerHTML = '';
  terminalEl.classList.add('visible');

  const seq = LOG_SEQUENCES[seqIndex % LOG_SEQUENCES.length];
  seqIndex++;

  seq.forEach(({ delay, text }) => {
    const t = setTimeout(() => {
      const li = document.createElement('li');
      li.innerHTML = text;
      termLogEl.appendChild(li);
      requestAnimationFrame(() => requestAnimationFrame(() => li.classList.add('show')));
      li.scrollIntoView({ block: 'nearest' });
    }, delay);
    if (!termTimer) termTimer = [];
    termTimer.push(t);
  });

  const totalDuration = seq[seq.length - 1].delay + 2200;
  termHideTimer = setTimeout(() => {
    terminalEl.classList.remove('visible');
    termRunning = false;
    termTimer = null;
    // Redirect to editor page after animation completes
    if (editorUrl) {
      setTimeout(() => { window.location.href = editorUrl; }, 400);
    }
  }, totalDuration);
}
