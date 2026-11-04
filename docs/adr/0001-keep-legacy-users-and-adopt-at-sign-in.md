# Keep legacy users and adopt them at sign-in

Legacy `users` documents keep their Ids, because operators and officials reference users by integer `UserId`, and wiping the collection would orphan every event role. Sign-in by emailed code adopts a legacy document with a matching email, which overrides the "users register anew" rule in issue #597. The adoption logic is transitional and lives in one standalone unit, removed by hand once no legacy users remain.
