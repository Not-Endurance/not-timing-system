# Sign-in pages are Ui routes that complete in-app

Sign-in and registration are Blazor routes in the Ui, and sign-in completes without a full-page navigation, so the app boots once per visit. Server-rendered pages would need their own styling for a MudBlazor interface and would still boot the Ui after sign-in, so they save nothing on the path that matters. This departs from the issue's "full-page navigation to the sign-in pages".
