// The registration page: the details of a new person, then the six-digit code that was sent to their address, and a
// session when it comes back (ADR-0002, #601). Nothing is stored for them until it does. No framework, no stored
// state: the session is a cookie the server sets.
(() => {
  'use strict';

  const script = document.currentScript;
  const messages = {
    email: script.dataset.errorEmail,
    name: script.dataset.errorName,
    country: script.dataset.errorCountry,
    code: script.dataset.errorCode,
    limited: script.dataset.errorLimited,
    generic: script.dataset.errorGeneric,
    resendIn: script.dataset.resendIn,
  };
  const COOLDOWN_SECONDS = 60;
  const MEDIA_TYPE = 'application/vnd.api+json';

  const registerForm = document.getElementById('register-form');
  const codeForm = document.getElementById('code-form');
  const message = document.getElementById('message');
  const resend = document.getElementById('resend');
  const changeDetails = document.getElementById('change-details');
  const codeInput = document.getElementById('code');
  const signInLink = document.getElementById('sign-in-link');

  const passkeys = window.NoTimingPasskeys;
  const passkeysUsable = script.dataset.passkeys === 'true' && Boolean(passkeys) && passkeys.supported();

  let details = null;
  let countdown = null;

  // Only a path of this site: anything that could leave it falls back to the start page. The server has checked it
  // already; a control character is refused here too, because a browser removes tabs and line breaks from a URL and
  // '/<tab>/host' would become '//host'.
  const returnUrl = (() => {
    const value = script.dataset.returnUrl || '/';
    const leavesTheSite = !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\');
    return leavesTheSite || /[\u0000-\u001f\u007f-\u009f]/.test(value) ? '/' : value;
  })();
  if (returnUrl !== '/') {
    signInLink.href = '/sign-in?returnUrl=' + encodeURIComponent(returnUrl);
  }

  function show(text) {
    message.textContent = text || '';
    message.hidden = !text;
  }

  async function post(path, type, attributes) {
    return fetch(path, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': MEDIA_TYPE, Accept: MEDIA_TYPE },
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

  function why(response, errors) {
    if (response.status === 429) {
      return messages.limited;
    }
    if (response.status === 400) {
      const code = errors && errors[0] && errors[0].code;
      return code === 'invalid-email'
        ? messages.email
        : code === 'invalid-name'
          ? messages.name
          : code === 'invalid-country'
            ? messages.country
            : messages.generic;
    }
    return messages.generic;
  }

  async function requestCode() {
    try {
      const response = await post('/api/registrations', 'registrations', details);
      if (response.status === 202) {
        return true;
      }
      const body = await response.json().catch(() => null);
      show(why(response, body && body.errors));
    } catch {
      show(messages.generic);
    }
    return false;
  }

  // After a code the person is offered a passkey, which they can skip. A browser that cannot make one is not offered
  // it.
  function goOn(session) {
    const offer = passkeysUsable && session.passkeyCount < 2;
    window.location.assign(
      offer ? '/account/passkeys?offer=1&returnUrl=' + encodeURIComponent(returnUrl) : returnUrl,
    );
  }

  registerForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    show('');
    const form = new FormData(registerForm);
    const email = String(form.get('email') || '').trim();
    const givenName = String(form.get('givenName') || '').trim();
    const surname = String(form.get('surname') || '').trim();
    const countryId = String(form.get('country') || '');
    if (!email || !registerForm.elements.email.checkValidity()) {
      show(messages.email);
      return;
    }
    if (!givenName || !surname) {
      show(messages.name);
      return;
    }
    if (!countryId) {
      show(messages.country);
      return;
    }
    details = { email, givenName, surname, countryId, website: String(form.get('website') || '') };
    if (await requestCode()) {
      registerForm.hidden = true;
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
      const response = await post('/api/sessions', 'sessions', { email: details.email, code });
      if (response.status === 201) {
        goOn((await response.json()).data.attributes);
        return;
      }
      show(response.status === 401 ? messages.code : response.status === 429 ? messages.limited : messages.generic);
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

  changeDetails.addEventListener('click', () => {
    clearInterval(countdown);
    show('');
    codeForm.hidden = true;
    registerForm.hidden = false;
    registerForm.elements.givenName.focus();
  });
})();
