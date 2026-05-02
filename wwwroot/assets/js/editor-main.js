/**
 * Editor page entry point.
 * Handles line numbers, stats, interactive WebSocket execution,
 * and the resizable divider.
 */

const editor = document.getElementById('editor');
const editorHighlight = document.querySelector('#editorHighlight code');
const activeLineIndicator = document.getElementById('activeLineIndicator');
const lineNumbers = document.getElementById('lineNumbers');
const outputBody = document.getElementById('outputBody');
const runBtn = document.getElementById('runBtn');

const statLines = document.getElementById('stat-lines');
const statChars = document.getElementById('stat-chars');
const statStatus = document.getElementById('stat-status');
const statusGroup = document.getElementById('status-group');
const statusIndicator = document.getElementById('status-indicator');

let socket = null;
let socketReadyPromise = null;
let activeInput = null;
let isRunning = false;

window.toggleTheme = function () {
  const isLight = document.documentElement.getAttribute('data-theme') === 'light';
  document.documentElement.setAttribute('data-theme', isLight ? 'dark' : 'light');
  const toLight = !isLight;
  document.getElementById('iconSun').style.display  = toLight ? 'inline-block' : 'none';
  document.getElementById('iconMoon').style.display = toLight ? 'none'  : 'inline-block';
};

function updateStats() {
  const val = editor.value;
  statLines.textContent = `${val.split('\n').length} lines`;
  statChars.textContent = `${val.length} chars`;
}

function escapeHtml(value) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

function highlightLexor(code) {
  const patterns = [
    { type: 'comment',  regex: /%%.*$/m },
    { type: 'string',   regex: /"[^"\n]*"|'[^'\n]*'/ },
    { type: 'number',   regex: /\b\d+(?:\.\d+)?\b/ },
    { type: 'boolean',  regex: /\b(?:TRUE|FALSE)\b/ },
    { type: 'keyword',  regex: /\b(?:SCRIPT AREA|START SCRIPT|END SCRIPT|START IF|END IF|ELSE IF|START FOR|END FOR|REPEAT WHEN|START REPEAT|END REPEAT|DECLARE|PRINT|SCAN|IF|ELSE|FOR)\b/ },
    { type: 'type',     regex: /\b(?:INT|CHAR|BOOL|FLOAT)\b/ },
    { type: 'operator', regex: /==|<>|>=|<=|=|>|<|\+|-|\*|\/|%|&|\b(?:AND|OR|NOT)\b/ },
    { type: 'special',  regex: /\$|\[#\]|\[\[\]/ }
  ];

  const fullRegex = new RegExp(
    patterns.map(p => `(${p.regex.source})`).join('|'),
    'gm'
  );

  let html = '';
  let lastIndex = 0;
  let match;

  while ((match = fullRegex.exec(code)) !== null) {
    // Add text before the match
    html += escapeHtml(code.substring(lastIndex, match.index));

    // Identify which group matched
    let matchedType = null;
    for (let i = 0; i < patterns.length; i++) {
      if (match[i + 1] !== undefined) {
        matchedType = patterns[i].type;
        break;
      }
    }

    if (matchedType) {
      html += `<span class="token-${matchedType}">${escapeHtml(match[0])}</span>`;
    } else {
      html += escapeHtml(match[0]);
    }

    lastIndex = fullRegex.lastIndex;
  }

  // Add remaining text
  html += escapeHtml(code.substring(lastIndex));
  return html;
}

function updateHighlight() {
  const code = editor.value || ' ';
  editorHighlight.innerHTML = highlightLexor(code);
  editorHighlight.parentElement.scrollTop = editor.scrollTop;
  editorHighlight.parentElement.scrollLeft = editor.scrollLeft;
}

function updateActiveLineIndicator() {
  if (!activeLineIndicator) return;
  const currentLine = editor.value.slice(0, editor.selectionStart).split('\n').length;
  activeLineIndicator.style.top = `calc(18px + ${currentLine - 1} * 13px * 1.75)`;
}

function updateLineNumbers() {
  const lines = editor.value.split('\n');
  const currentLine = editor.value.slice(0, editor.selectionStart).split('\n').length;
  let html = '';

  for (let i = 1; i <= lines.length; i++) {
    html += `<span class="${i === currentLine ? 'active' : ''}">${i}</span>`;
  }

  lineNumbers.innerHTML = html;
  updateActiveLineIndicator();
  updateStats();
  updateHighlight();
}

editor.addEventListener('input', updateLineNumbers);
editor.addEventListener('keyup', updateLineNumbers);
editor.addEventListener('click', updateLineNumbers);

// selectionchange fires instantly on every caret move (click, arrow keys, mouse drag)
document.addEventListener('selectionchange', () => {
  if (document.activeElement === editor) {
    updateActiveLineIndicator();
  }
});

editor.addEventListener('scroll', () => {
  lineNumbers.scrollTop = editor.scrollTop;
  updateHighlight();
});

editor.addEventListener('keydown', (e) => {
  if (e.key === 'Tab') {
    e.preventDefault();
    const start = editor.selectionStart;
    const end = editor.selectionEnd;
    editor.value = `${editor.value.substring(0, start)}  ${editor.value.substring(end)}`;
    editor.selectionStart = editor.selectionEnd = start + 2;
    updateLineNumbers();
  }
});

function setStatus(label, color = '', indicator = '') {
  statStatus.textContent = label;
  statusGroup.style.color = color;
  statusIndicator.style.background = indicator;
}

function setRunningState(running) {
  isRunning = running;

  if (running) {
    runBtn.classList.add('running');
    runBtn.innerHTML = '<svg viewBox="0 0 10 10" fill="currentColor" width="8" height="8"><rect x="1" y="1" width="8" height="8"/></svg> running';
    return;
  }

  runBtn.classList.remove('running');
  runBtn.innerHTML = '<svg viewBox="0 0 10 10" fill="currentColor" width="8" height="8"><polygon points="0,0 10,5 0,10"/></svg> run';
}

function appendOutputLine(text, type = 'normal') {
  const el = document.createElement('div');
  el.className = `output-line ${type === 'error' ? 'error' : ''} animate`;
  el.textContent = text;
  outputBody.appendChild(el);
  outputBody.scrollTop = outputBody.scrollHeight;
}

function appendCursorLine() {
  const cursorLine = document.createElement('div');
  cursorLine.className = 'cursor-line';
  cursorLine.innerHTML = '<span class="cursor"></span>';
  outputBody.appendChild(cursorLine);
  outputBody.scrollTop = outputBody.scrollHeight;
}

function closeActiveInput() {
  if (!activeInput) {
    return;
  }

  const { wrapper, input } = activeInput;
  wrapper.textContent = input.value;
  activeInput = null;
  outputBody.scrollTop = outputBody.scrollHeight;
}

function requestTerminalInput(variableNames) {
  const wrapper = document.createElement('div');
  wrapper.className = 'output-line animate';

  const input = document.createElement('input');
  input.className = 'terminal-input';
  input.type = 'text';
  input.autocomplete = 'off';
  input.spellcheck = false;
  input.setAttribute('aria-label', 'Terminal input');

  wrapper.appendChild(input);
  outputBody.appendChild(wrapper);
  outputBody.scrollTop = outputBody.scrollHeight;

  activeInput = { wrapper, input };
  setStatus('INPUT', '#61afef', '#61afef');
  input.focus();

  input.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter') {
      return;
    }

    event.preventDefault();
    sendSocketMessage({
      type: 'input',
      value: input.value
    });
    closeActiveInput();
    setStatus('RUNNING', '#e5c07b', '#e5c07b');
  });
}

function sendSocketMessage(message) {
  if (!socket || socket.readyState !== WebSocket.OPEN) {
    throw new Error('Interpreter socket is not connected.');
  }

  socket.send(JSON.stringify(message));
}

function handleSocketMessage(message) {
  switch ((message.type || '').toLowerCase()) {
    case 'connected':
      setStatus('READY');
      break;
    case 'started':
      setRunningState(true);
      setStatus('RUNNING', '#e5c07b', '#e5c07b');
      break;
    case 'output':
      if (typeof message.output === 'string' && message.output.length > 0) {
        message.output.split('\n').forEach((line) => appendOutputLine(line));
      }
      break;
    case 'input_request':
      requestTerminalInput(message.variablesRequested || []);
      break;
    case 'error':
      appendOutputLine(message.error || 'Socket error.', 'error');
      appendCursorLine();
      setRunningState(false);
      setStatus('READY');
      break;
    case 'completed':
      if (!message.success) {
        appendOutputLine(message.error || 'Execution failed.', 'error');
      } else if (!outputBody.querySelector('.output-line')) {
        appendOutputLine('// no output', 'dim');
        appendOutputLine('\n');
        appendOutputLine('\n');
        appendOutputLine('=== Code Execution Successful ===', 'dim');
      } else {
        appendOutputLine('\n');
        appendOutputLine('\n');
        appendOutputLine('=== Code Execution Successful ===', 'dim');
      }

      appendCursorLine();
      setRunningState(false);
      setStatus('READY');
      break;
  }
}

async function ensureSocket() {
  if (socket && socket.readyState === WebSocket.OPEN) {
    return socket;
  }

  if (socketReadyPromise) {
    return socketReadyPromise;
  }

  socketReadyPromise = new Promise((resolve, reject) => {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    socket = new WebSocket(`${protocol}//${window.location.host}/ws/interpreter`);

    socket.addEventListener('open', () => {
      resolve(socket);
    }, { once: true });

    socket.addEventListener('message', (event) => {
      const message = JSON.parse(event.data);
      handleSocketMessage(message);
    });

    socket.addEventListener('close', () => {
      socket = null;
      socketReadyPromise = null;

      if (isRunning) {
        appendOutputLine('Interpreter connection closed.', 'error');
        appendCursorLine();
        setRunningState(false);
        setStatus('DISCONNECTED', '#e06c75', '#e06c75');
      }
    });

    socket.addEventListener('error', () => {
      reject(new Error('Unable to connect to interpreter socket.'));
    }, { once: true });
  });

  return socketReadyPromise;
}

window.runCode = async function () {
  if (isRunning) {
    return;
  }

  outputBody.innerHTML = '';
  appendOutputLine('// connecting to interpreter...', 'dim');

  try {
    await ensureSocket();
    outputBody.innerHTML = '';
    sendSocketMessage({
      type: 'run',
      source: editor.value
    });
  } catch (error) {
    outputBody.innerHTML = '';
    appendOutputLine(error?.message || 'Unable to start interpreter session.', 'error');
    appendCursorLine();
    setRunningState(false);
    setStatus('READY');
  }
};

const divider = document.getElementById('divider');
const workspace = document.querySelector('.workspace');
const editorPane = document.querySelector('.editor-pane');
const outputPane = document.querySelector('.output-pane');
let isDragging = false;

divider.addEventListener('mousedown', () => {
  isDragging = true;
  document.body.style.cursor = 'col-resize';
  document.body.style.userSelect = 'none';
});

document.addEventListener('mousemove', (e) => {
  if (!isDragging) {
    return;
  }

  const rect = workspace.getBoundingClientRect();
  const left = Math.max(200, Math.min(rect.width - 200, e.clientX - rect.left));
  editorPane.style.flex = 'none';
  editorPane.style.width = `${(left / rect.width) * 100}%`;
  outputPane.style.flex = '1';
});

document.addEventListener('mouseup', () => {
  if (!isDragging) {
    return;
  }

  isDragging = false;
  document.body.style.cursor = '';
  document.body.style.userSelect = '';
});

updateLineNumbers();
updateStats();
updateHighlight();
