// The passkeys page: the person's passkeys, adding one, renaming and removing (ADR-0002). After a code sign-in it opens
// as an offer to add one, and after the first passkey it offers a second, so that losing a device never locks anyone
// out. The last passkey cannot be removed: the server says no, and the page shows why.
(() => {
  'use strict';

  const script = document.currentScript;
  const data = script.dataset;
  const text = {
    offer: data.textOffer,
    offerSecond: data.textOfferSecond,
    add: data.textAdd,
    addAnother: data.textAddAnother,
    skip: data.textSkip,
    done: data.textDone,
    rename: data.textRename,
    save: data.textSave,
    remove: data.textRemove,
    confirmRemove: data.textConfirmRemove,
    default: data.textDefault,
    created: data.textCreated,
    unsupported: data.textUnsupported,
    unavailable: data.textUnavailable,
    cancelled: data.textCancelled,
    failed: data.textFailed,
    last: data.textLast,
    generic: data.textGeneric,
  };
  const token = data.antiforgery;
  const offerMode = data.offer === 'true';
  const MEDIA_TYPE = 'application/vnd.api+json';

  const passkeys = window.NoTimingPasskeys;
  const usable = data.enabled === 'true' && Boolean(passkeys) && passkeys.supported();

  const list = document.getElementById('list');
  const empty = document.getElementById('empty');
  const add = document.getElementById('add');
  const offer = document.getElementById('offer');
  const offerText = document.getElementById('offer-text');
  const message = document.getElementById('message');
  const done = document.getElementById('done');

  let justAdded = false;

  function show(value) {
    message.textContent = value || '';
    message.hidden = !value;
  }

  // A write is refused unless it carries the header that only a page of this site sends (#602).
  async function call(method, path, body) {
    const headers = { Accept: MEDIA_TYPE, 'X-XSRF-TOKEN': token, 'X-Requested-With': 'NoTiming' };
    if (body) {
      headers['Content-Type'] = MEDIA_TYPE;
    }
    return fetch(path, {
      method,
      credentials: 'same-origin',
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  // What the person will recognise in the list: the browser and the system it was made on.
  function deviceName() {
    const agent = navigator.userAgent;
    const browser = /Edg\//.test(agent)
      ? 'Edge'
      : /Firefox\//.test(agent)
        ? 'Firefox'
        : /Chrome\//.test(agent)
          ? 'Chrome'
          : /Safari\//.test(agent)
            ? 'Safari'
            : 'Browser';
    const system = /Windows/.test(agent)
      ? 'Windows'
      : /Android/.test(agent)
        ? 'Android'
        : /iPhone|iPad/.test(agent)
          ? 'iOS'
          : /Mac OS X/.test(agent)
            ? 'macOS'
            : /Linux/.test(agent)
              ? 'Linux'
              : '';
    return system ? browser + ' · ' + system : browser;
  }

  function button(label, className, onClick) {
    const element = document.createElement('button');
    element.type = 'button';
    element.className = className;
    element.textContent = label;
    element.addEventListener('click', onClick);
    return element;
  }

  function row(item) {
    const name = item.attributes.name || text.default;
    const created = new Date(item.attributes.createdAt).toLocaleDateString(data.lang);
    const entry = document.createElement('li');
    entry.dataset.id = item.id;

    const label = document.createElement('div');
    const title = document.createElement('strong');
    title.textContent = name;
    const when = document.createElement('span');
    when.className = 'hint';
    when.textContent = text.created.replace('{date}', created);
    label.append(title, when);

    const actions = document.createElement('div');
    actions.className = 'actions';
    actions.append(
      button(text.rename, 'link', () => edit(entry, label, item, name)),
      button(text.remove, 'link', () => removePasskey(item)),
    );

    entry.append(label, actions);
    return entry;
  }

  function edit(entry, label, item, current) {
    const input = document.createElement('input');
    input.type = 'text';
    input.value = current;
    input.maxLength = 60;
    input.setAttribute('aria-label', text.rename);
    const save = button(text.save, 'link', async () => {
      const response = await call('PATCH', '/api/passkeys/' + encodeURIComponent(item.id), {
        data: { type: 'passkeys', id: item.id, attributes: { name: input.value } },
      });
      if (response.ok) {
        await load();
      } else {
        show(text.generic);
      }
    });
    label.replaceChildren(input);
    entry.querySelector('.actions').replaceChildren(save);
    input.focus();
    input.select();
  }

  async function removePasskey(item) {
    show('');
    if (!window.confirm(text.confirmRemove)) {
      return;
    }
    const response = await call('DELETE', '/api/passkeys/' + encodeURIComponent(item.id));
    if (response.status === 204) {
      await load();
    } else if (response.status === 409) {
      show(text.last);
    } else {
      show(text.generic);
    }
  }

  function render(items) {
    list.replaceChildren(...items.map(row));
    empty.hidden = items.length > 0;
    add.hidden = !usable;
    add.textContent = items.length === 0 ? text.add : text.addAnother;
    const offering = usable && items.length < 2 && (offerMode || justAdded);
    offer.hidden = !offering;
    if (offering) {
      offerText.textContent = items.length === 0 ? text.offer : text.offerSecond;
    }
    done.textContent = offering ? text.skip : text.done;
  }

  async function load() {
    const response = await call('GET', '/api/passkeys');
    if (response.status === 401) {
      window.location.assign('/sign-in?returnUrl=' + encodeURIComponent(location.pathname + location.search));
      return;
    }
    render((await response.json()).data);
  }

  add.addEventListener('click', async () => {
    show('');
    add.disabled = true;
    try {
      const credential = await passkeys.createCredential(token);
      const response = await call('POST', '/api/passkeys', {
        data: { type: 'passkeys', attributes: { credential, name: deviceName() } },
      });
      if (response.status === 201) {
        justAdded = true;
        await load();
      } else {
        show(text.failed);
      }
    } catch (error) {
      show(error && error.name === 'NotAllowedError' ? text.cancelled : text.failed);
    } finally {
      add.disabled = false;
    }
  });

  if (!usable) {
    show(data.enabled === 'true' ? text.unsupported : text.unavailable);
  }
  load();
})();
