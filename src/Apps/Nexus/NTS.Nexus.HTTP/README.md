# NTS.Nexus.HTTP

## Playwright browser setup for Static Web Apps

`NTS.Nexus.HTTP` renders print PDFs with Playwright Chromium. The CD workflows publish the API with a package-local browser install by running Playwright with `PLAYWRIGHT_BROWSERS_PATH=0`.

Set the same value as an application setting on each Azure Static Web Apps resource that hosts this API:

```powershell
az staticwebapp appsettings set --name <static-web-app-name> --setting-names PLAYWRIGHT_BROWSERS_PATH=0
```

The current deployment token names suggest these Static Web Apps names, but confirm them in Azure before running the command:

```powershell
az staticwebapp appsettings set --name lively-rock-07e20cd03 --setting-names PLAYWRIGHT_BROWSERS_PATH=0
az staticwebapp appsettings set --name victorious-island-0dc61c703 --setting-names PLAYWRIGHT_BROWSERS_PATH=0
```

You can also set this in the Azure portal under the Static Web App's environment variables or application settings. Without this setting, the deployed Functions runtime may look for Playwright browsers in the default user cache instead of the browser payload packaged with the API.
