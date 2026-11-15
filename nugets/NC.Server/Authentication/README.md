# Authentication

Sign-in with a passkey, or with a six-digit code sent by email. A successful sign-in starts a server-side session. The browser gets an opaque cookie, and the server stores a hash of its token in MongoDB.

The package is wired in [Interface/AuthenticationServices.cs](Interface/AuthenticationServices.cs).

## Requirements

- **HTTPS.** The session cookie uses the `__Host-` prefix, so browsers accept it only over HTTPS.
- **A custom domain for passkeys.** On a default `*.azurewebsites.net` host, use email codes. Passkeys there are untested.
- **App Service:** a custom domain with a certificate needs a paid tier, Basic B1 or higher. Create a CNAME from the app host to the default host, and a TXT record `asuid.<host>` with the verification ID. Then bind an App Service managed certificate with SNI.
- **MongoDB.** The app creates its indexes at startup, so the database user needs permission to create indexes. If two proved users share an email address, the unique index can't be built, and startup fails.
- **Brevo,** with a verified sender domain. See [Brevo](#brevo).

## Settings

Set these as environment variables. In App Service, add them as application settings. `__` stands for `:`.

| Setting | Required | Purpose | Example |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` turns on the production checks. | `Production` |
| `Authentication__AppOrigin` | Yes | The exact origin the app is served from. Sign-in requests from any other origin are refused. | `https://app.example.com` |
| `Authentication__RelyingPartyId` | Yes | The passkey relying-party ID: the app host, or a parent domain of it. | `example.com` |
| `Authentication__CodeSecret` | Outside Development | Keys the email code check. Changing it invalidates codes already sent. | A long random value |
| `Authentication__EmailProvider` | Yes | `brevo`, `console` or `outbox`. | `brevo` |
| `Authentication__BrevoApiKey` | With `brevo` | The Brevo API key. | |
| `Authentication__EmailSenderAddress` | With `brevo` | A sender address on the verified Brevo domain. | `no-reply@example.com` |
| `MONGO_CONNECTION_STRING` | Yes | The MongoDB connection string. | |

Leave `CookieName`, the database name and the collection names at their defaults unless you have a reason to change them.

## Startup checks

The host refuses to start when a setting would make sign-in unsafe. The message names the setting. In Production:

- `AppOrigin` must use `https://`.
- `RelyingPartyId` must not be `localhost`, and must not start with `staging.`.
- `RelyingPartyId` must be a parent domain of the `AppOrigin` host.
- `CodeSecret` must be set.
- The provider must be `brevo`. `console` and `outbox` are refused.
- With `brevo`, `BrevoApiKey` must be set.

The checks don't cover `EmailSenderAddress`. Its default, `no-reply@localhost`, passes startup, and Brevo then rejects every email. Set it explicitly.

Source: [AuthenticationSettingsValidator.cs](Settings/AuthenticationSettingsValidator.cs).

## Passkeys

- A passkey is bound to the RP ID, not to the app. Choose an apex domain you'll keep, with the app on a subdomain.
- Use a different RP ID for each non-production environment, for example `sta.example.com` with the app at `app.sta.example.com`.
- Don't change the RP ID once passkeys are enrolled. Changing it invalidates every enrolled passkey.
- Passkey ceremonies and state-changing sign-in requests must come from exactly `AppOrigin`. State-changing requests also need the `X-NTS-Request` header ([Requests/StateChangeGuard.cs](Requests/StateChangeGuard.cs)).

## Brevo

Codes are sent through Brevo's transactional email API ([Email/BrevoEmailSender.cs](Email/BrevoEmailSender.cs)).

1. **API key.** In Brevo, go to **SMTP & API → API Keys**. Choose **Restricted** access, set **Transactional email** to **All**, and set every other resource to **None**. Create one key per environment.
2. **Sender domain.** Add the domain in Brevo, and create the DNS records it shows: a verification TXT record, DKIM and DMARC. Wait until Brevo shows the domain as verified.
3. **Sender address.** Set `Authentication__EmailSenderAddress` to an address on that domain.
4. **Test.** Request a code in the target environment. A rejected send throws from `BrevoEmailSender`, so check the log.

`console` writes codes to the log instead of sending them. Use it only where the logs are private.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| The host won't start | The message names the setting to fix. |
| `foreign-origin` on sign-in | `AppOrigin` doesn't match the address in the browser. |
| `missing-request-header` on sign-in | The client doesn't send `X-NTS-Request`. |
| Passkey enrolment fails | The RP ID isn't a parent of the host, or the host is a default `*.azurewebsites.net` address. |
| Codes refused after a change | `CodeSecret` changed. Request new codes. |
| No email arrives | Brevo rejected the send. Check the key's access and the domain's verification. |

## Known gaps

- `EmailSenderAddress` isn't validated at startup.
- Passkeys on a default `*.azurewebsites.net` host are untested.
