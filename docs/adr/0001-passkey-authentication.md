# Passkey authentication

Users sign in with a passkey, with a six-digit emailed code as the fallback, and the session is a host-only `__Host-` cookie on the app host. The passkey RP ID is the product's apex domain (`timing.no-company.eu` in production), with the app on a subdomain, so passkeys stay scoped to this product and survive moving the app within it. Sign-in and registration are Ui routes that complete in the app, not server-rendered pages, so they share the MudBlazor styling and the app boots once per visit. Legacy `users` documents keep their Ids and are adopted by emailed code, because operators and officials reference users by integer `UserId`; that adoption is transitional and lives in one standalone unit, to be removed by hand.

Consequences: every subdomain of the RP ID is a valid passkey origin, so no untrusted content may be served under it, and the origin check is pinned to the exact app origin. Changing the RP ID invalidates enrolled passkeys, so it is fixed before the first enrolment.

Rejected: a shared `no-company.eu` RP ID, which would share passkey scope and origins with sibling products; an auth-subdomain RP ID with a sign-in handoff, which adds a cross-host step; and wiping `users` as the issue proposed, which would orphan every event role.
