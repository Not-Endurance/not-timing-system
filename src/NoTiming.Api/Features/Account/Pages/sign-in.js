// The sign-in page: an email, then the six-digit code that was sent to it. No framework, no stored state: the
// session is a cookie the server sets (ADR-0002).
(() => {
  'use strict';

  const script = document.currentScript;
  const messages = {
    email: script.dataset.errorEmail,
    code: script.dataset.errorCode,
    generic: script.dataset.errorGeneric,
    resendIn: script.dataset.resendIn,
  };
  const COOLDOWN_SECONDS = 60;

  const emailForm = document.getElementById('email-form');
  const codeForm = document.getElementById('code-form');
  const message = document.getElementById('message');
  const emailInput = document.getElementById('email');
  const codeInput = document.getElementById('code');
  const resend = document.getElementById('resend');
  const changeEmail = document.getElementById('change-email');

  let email = '';
  let countdown = null;

  // Only a path of this site: anything that could leave it falls back to the start page.
  const returnUrl = (() => {
    const value = script.dataset.returnUrl || '/';
    return value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\') ? value : '/';
  })();

  function show(text) {
    message.textContent = text || '';
    message.hidden = !text;
  }

  async function post(path, type, attributes) {
    return fetch(path, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/vnd.api+json', Accept: 'application/vnd.api+json' },
      body: JSON.stringify({ data: { type, attributes } }),
    });
  }

  function startCooldown() {
    let seconds = COOLDOWN_SECONDS;
    resend.disabled = true;
    clearInterval(countdown);
    const tick = () => {
      if (seconds <= 0) {
        clearInterval(countdown);
        resend.disabled = false;
        resend.textContent = resend.dataset.label;
        return;
      }
      resend.textContent = messages.resendIn.replace('{seconds}', String(seconds));
      seconds -= 1;
    };
    tick();
    countdown = setInterval(tick, 1000);
  }

  async function requestCode() {
    try {
      const response = await post('/api/code-challenges', 'code-challenges', { email });
      if (response.status === 202) {
        return true;
      }
      show(response.status === 400 ? messages.email : messages.generic);
    } catch {
      show(messages.generic);
    }
    return false;
  }

  emailForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    show('');
    email = emailInput.value.trim();
    if (!email || !emailInput.checkValidity()) {
      show(messages.email);
      return;
    }
    if (await requestCode()) {
      emailForm.hidden = true;
      codeForm.hidden = false;
      codeInput.value = '';
      codeInput.focus();
      startCooldown();
    }
  });

  codeForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    show('');
    const code = codeInput.value.replace(/\s+/g, '');
    if (!code) {
      show(messages.code);
      return;
    }
    try {
      const response = await post('/api/sessions', 'sessions', { email, code });
      if (response.status === 201) {
        window.location.assign(returnUrl);
        return;
      }
      show(response.status === 401 ? messages.code : messages.generic);
    } catch {
      show(messages.generic);
    }
    codeInput.select();
  });

  resend.addEventListener('click', async () => {
    show('');
    if (await requestCode()) {
      startCooldown();
      codeInput.value = '';
      codeInput.focus();
    }
  });

  changeEmail.addEventListener('click', () => {
    clearInterval(countdown);
    show('');
    codeForm.hidden = true;
    emailForm.hidden = false;
    emailInput.focus();
  });
})();
