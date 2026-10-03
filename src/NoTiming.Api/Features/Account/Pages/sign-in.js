// The sign-in page: an email, then the six-digit code that was sent to it, or a passkey. No framework, no stored
// state: the session is a cookie the server sets (ADR-0002).
(() => {
  'use strict';

  const script = document.currentScript;
  const messages = {
    email: script.dataset.errorEmail,
    code: script.dataset.errorCode,
    generic: script.dataset.errorGeneric,
    passkey: script.dataset.errorPasskey,
    resendIn: script.dataset.resendIn,
    passkeyBusy: script.dataset.passkeyBusy,
  };
  const antiforgery = script.dataset.antiforgery;
  const COOLDOWN_SECONDS = 60;
  const MEDIA_TYPE = 'application/vnd.api+json';

  const emailForm = document.getElementById('email-form');
  const codeForm = document.getElementById('code-form');
  const message = document.getElementById('message');
  const emailInput = document.getElementById('email');
  const codeInput = document.getElementById('code');
  const resend = document.getElementById('resend');
  const changeEmail = document.getElementById('change-email');
  const passkeyButton = document.getElementById('passkey');

  const passkeys = window.NoTimingPasskeys;
  const passkeysUsable = script.dataset.passkeys === 'true' && Boolean(passkeys) && passkeys.supported();

  let email = '';
  let countdown = null;
  let autofill = null; // the passkey request that waits in the background for the email field's autofill

  // Only a path of this site: anything that could leave it falls back to the start page.
  const returnUrl = (() => {
    const value = script.dataset.returnUrl || '/';
    return value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\') ? value : '/';
  })();

  function show(text) {
    message.textContent = text || '';
    message.hidden = !text;
  }

  async function post(path, type, attributes, token) {
    const headers = { 'Content-Type': MEDIA_TYPE, Accept: MEDIA_TYPE };
    if (token) {
      headers['X-XSRF-TOKEN'] = token;
    }
    return fetch(path, {
      method: 'POST',
      credentials: 'same-origin',
      headers,
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

  // After a code the person is offered a passkey, until they have two (a second one means losing a device is no
  // lock-out). After a passkey there is nothing to offer.
  function goOn(session) {
    const offer = session.method === 'code' && passkeysUsable && session.passkeyCount < 2;
    window.location.assign(
      offer ? '/account/passkeys?offer=1&returnUrl=' + encodeURIComponent(returnUrl) : returnUrl,
    );
  }

  async function signInWithPasskey(credential) {
    try {
      const response = await post('/api/sessions', 'sessions', { credential }, antiforgery);
      if (response.status === 201) {
        goOn((await response.json()).data.attributes);
        return true;
      }
      show(response.status === 401 ? messages.passkey : messages.generic);
    } catch {
      show(messages.generic);
    }
    return false;
  }

  // The passkeys of this device are offered in the email field's own autofill, where the browser has that.
  async function startAutofill() {
    if (!passkeysUsable || !(await passkeys.conditionalAvailable())) {
      return;
    }
    autofill = new AbortController();
    try {
      const credential = await passkeys.getCredential(antiforgery, { conditional: true, signal: autofill.signal });
      if (credential) {
        await signInWithPasskey(credential);
      }
    } catch {
      // Aborted, or the person chose to type: the email field and the button still work.
    }
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
      if (autofill) {
        autofill.abort();
      }
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
        goOn((await response.json()).data.attributes);
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
    startAutofill();
  });

  if (passkeysUsable) {
    const label = passkeyButton.textContent;
    passkeyButton.hidden = false;
    passkeyButton.addEventListener('click', async () => {
      show('');
      if (autofill) {
        autofill.abort(); // one passkey request at a time
      }
      passkeyButton.disabled = true;
      passkeyButton.textContent = messages.passkeyBusy;
      try {
        const credential = await passkeys.getCredential(antiforgery);
        if (credential && (await signInWithPasskey(credential))) {
          return;
        }
      } catch (error) {
        // The person closed the prompt, or has no passkey for this site: that is not an error to shout about.
        if (error && error.name !== 'NotAllowedError' && error.name !== 'AbortError') {
          show(messages.passkey);
        }
      }
      passkeyButton.disabled = false;
      passkeyButton.textContent = label;
      startAutofill();
    });
    startAutofill();
  }
})();
