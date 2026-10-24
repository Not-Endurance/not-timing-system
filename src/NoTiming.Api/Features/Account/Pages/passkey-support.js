// WebAuthn for the account pages (ADR-0002). The server sends the options of a ceremony as JSON and takes the
// credential back as JSON. The browser's API works with buffers, and not every browser has the helpers that convert,
// so the conversion is done here when it has to be. Nothing is kept: the session is a cookie the server sets.
(() => {
  'use strict';

  const MEDIA_TYPE = 'application/vnd.api+json';

  const toBuffer = (value) => {
    const padded = value + '='.repeat((4 - (value.length % 4)) % 4);
    const binary = atob(padded.replace(/-/g, '+').replace(/_/g, '/'));
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) {
      bytes[index] = binary.charCodeAt(index);
    }
    return bytes.buffer;
  };

  const toText = (buffer) => {
    if (!buffer) {
      return buffer;
    }
    let binary = '';
    for (const byte of new Uint8Array(buffer)) {
      binary += String.fromCharCode(byte);
    }
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  };

  const supported = () => Boolean(window.PublicKeyCredential && navigator.credentials);

  const conditionalAvailable = async () =>
    Boolean(
      window.PublicKeyCredential &&
        PublicKeyCredential.isConditionalMediationAvailable &&
        (await PublicKeyCredential.isConditionalMediationAvailable()),
    );

  function creationOptions(json) {
    if (PublicKeyCredential.parseCreationOptionsFromJSON) {
      return PublicKeyCredential.parseCreationOptionsFromJSON(json);
    }
    return {
      ...json,
      challenge: toBuffer(json.challenge),
      user: { ...json.user, id: toBuffer(json.user.id) },
      excludeCredentials: (json.excludeCredentials || []).map((item) => ({ ...item, id: toBuffer(item.id) })),
    };
  }

  function requestOptions(json) {
    if (PublicKeyCredential.parseRequestOptionsFromJSON) {
      return PublicKeyCredential.parseRequestOptionsFromJSON(json);
    }
    return {
      ...json,
      challenge: toBuffer(json.challenge),
      allowCredentials: (json.allowCredentials || []).map((item) => ({ ...item, id: toBuffer(item.id) })),
    };
  }

  function credentialJson(credential) {
    if (credential.toJSON) {
      return credential.toJSON();
    }
    const response = credential.response;
    const json = {
      id: credential.id,
      rawId: toText(credential.rawId),
      type: credential.type,
      authenticatorAttachment: credential.authenticatorAttachment || undefined,
      clientExtensionResults: credential.getClientExtensionResults ? credential.getClientExtensionResults() : {},
      response: { clientDataJSON: toText(response.clientDataJSON) },
    };
    if (response.attestationObject) {
      json.response.attestationObject = toText(response.attestationObject);
      json.response.transports = response.getTransports ? response.getTransports() : [];
    } else {
      json.response.authenticatorData = toText(response.authenticatorData);
      json.response.signature = toText(response.signature);
      json.response.userHandle = toText(response.userHandle);
    }
    return json;
  }

  // The options of a ceremony. The token is the one the server put in the page: the same request fails without it. The
  // other header is the one a write to a protected route needs (#602), which only a page of this site sends.
  async function fetchOptions(path, token) {
    const response = await fetch(path, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: MEDIA_TYPE, 'X-XSRF-TOKEN': token, 'X-Requested-With': 'NoTiming' },
    });
    if (!response.ok) {
      throw new Error('The options were refused: ' + response.status);
    }
    return (await response.json()).data.attributes.options;
  }

  // Makes a passkey on this device and returns it as the server wants it.
  async function createCredential(token) {
    const options = await fetchOptions('/api/passkeys/actions/creation-options', token);
    const credential = await navigator.credentials.create({ publicKey: creationOptions(options) });
    return credentialJson(credential);
  }

  // Asks for a passkey for this site: modal, or conditional (offered in the email field's autofill) when asked to.
  async function getCredential(token, { conditional = false, signal } = {}) {
    const options = await fetchOptions('/api/passkeys/actions/request-options', token);
    const request = { publicKey: requestOptions(options), signal };
    if (conditional) {
      request.mediation = 'conditional';
    }
    const credential = await navigator.credentials.get(request);
    return credential ? credentialJson(credential) : null;
  }

  window.NoTimingPasskeys = { MEDIA_TYPE, supported, conditionalAvailable, createCredential, getCredential };
})();
