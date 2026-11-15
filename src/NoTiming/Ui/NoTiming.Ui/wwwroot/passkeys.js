// WebAuthn ceremonies for the Ui. The server speaks the WebAuthn JSON form, in which binary values are base64url
// strings. The browser needs ArrayBuffers, so the options are converted going in and the answer is converted going out.
window.noTimingPasskeys = {
    isSupported: function () {
        return !!window.PublicKeyCredential && !!navigator.credentials;
    },

    create: async function (optionsJson) {
        var options = JSON.parse(optionsJson);
        var publicKey = Object.assign({}, options, {
            challenge: fromBase64Url(options.challenge),
            user: Object.assign({}, options.user, { id: fromBase64Url(options.user.id) }),
            excludeCredentials: (options.excludeCredentials || []).map(function (credential) {
                return Object.assign({}, credential, { id: fromBase64Url(credential.id) });
            })
        });

        var credential = await navigator.credentials.create({ publicKey: publicKey });
        return JSON.stringify(toJson(credential));
    },

    get: async function (optionsJson) {
        var options = JSON.parse(optionsJson);
        var publicKey = Object.assign({}, options, {
            challenge: fromBase64Url(options.challenge),
            allowCredentials: (options.allowCredentials || []).map(function (credential) {
                return Object.assign({}, credential, { id: fromBase64Url(credential.id) });
            })
        });

        var assertion = await navigator.credentials.get({ publicKey: publicKey });
        return JSON.stringify(toJson(assertion));
    }
};

function toJson(credential) {
    var response = credential.response;
    var answer = {};
    ["clientDataJSON", "attestationObject", "authenticatorData", "signature", "userHandle"].forEach(function (key) {
        if (response[key]) {
            answer[key] = toBase64Url(response[key]);
        }
    });

    if (typeof response.getTransports === "function") {
        answer.transports = response.getTransports();
    }

    return {
        id: credential.id,
        rawId: toBase64Url(credential.rawId),
        type: credential.type,
        response: answer,
        clientExtensionResults: credential.getClientExtensionResults ? credential.getClientExtensionResults() : {},
        authenticatorAttachment: credential.authenticatorAttachment || undefined
    };
}

function fromBase64Url(value) {
    var base64 = value.replace(/-/g, "+").replace(/_/g, "/");
    while (base64.length % 4 !== 0) {
        base64 += "=";
    }

    var binary = atob(base64);
    var bytes = new Uint8Array(binary.length);
    for (var i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    return bytes.buffer;
}

function toBase64Url(buffer) {
    var bytes = new Uint8Array(buffer);
    var binary = "";
    for (var i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }

    return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
