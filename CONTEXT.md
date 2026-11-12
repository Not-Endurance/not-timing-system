# Glossary

The domain terms used in this repo. Architecture and layering conventions are in [docs/architecture.md](docs/architecture.md).

## Applications
Deployable hosts and the capabilities they are built from.
- **NoTiming** - the single AspNetCore shell, deployed as one Azure Web App. It composes **Api**, **EventBus** and **Ui**. Replaces the former **Nexus Warp** server.
- **Api** - the REST capability: host configuration and endpoints.
- **EventBus** - the SignalR capability: host configuration and hubs only. It holds no business logic.
- **Ui** - the Blazor WebAssembly client served by NoTiming. Replaces the former **Witness** web app.
- **Judge** - the MAUI app. Talks to Api over REST and to EventBus over SignalR. Not changed by the rebrand.

## Sign-in
- **Session** - a server-side sign-in. The cookie holds the token; the database holds only its hash.
- **Security stamp** - a value on the user that each session is bound to. Changing it ends every session of the user.
- **Passkey** - a WebAuthn credential registered to a user. It signs the user in without a code.
- **Code challenge** - the pending one-time code for an address. It holds the MAC of the code, never the code, the attempts left and its expiry. There is one per address, and a new request replaces it.
- **Send limit** - the rule that caps code emails: a cooldown per address, and a maximum per address, per IP address and overall in each hour.
- **Proved address** - an email address confirmed by a code. The user's `ProvedAtUtc` is set. Only a proved address is unique.
- **Legacy user** - a user row from before identity, with no proved address. It is adopted in place when a code for its address is verified.
- **Outbox** - the non-production email provider. It stores each message in a collection, so a test can read the code.

## Persistence
- **Repository** - a class that reads and writes one collection. Services depend on its interface, so the store behind it can change with minimal change to them. Named `*Repository`, not `*Store`.
- **Document** - the shape of a row as stored in MongoDB. It carries no database attributes; how it is stored is set by a class map in `Stores/MongoDB/Mapping`.
