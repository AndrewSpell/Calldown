# Calldown

Oilspill calls down thumpers, Calldown calls down a Firefall server.

**tl;dr:** Check out PIN, RIN.WebAPI and SDBrowser next to this repo, start Docker, then run `aspire run` in this folder.
Once everything is green in the dashboard, run "Create account" on `rin-webapi`, "Configure client" on `firefall`, and start `firefall`.

<img width="1606" height="670" alt="image" src="https://github.com/user-attachments/assets/7821b842-7847-4a90-8df2-489479b5e617" />

Calldown is an [Aspire](https://aspire.dev) AppHost that runs the whole stack with one command:

- `postgres` with pgweb, running in Docker with a persistent volume
- `db-setup`, which applies RIN's `webapi_schema.sql` and seed data, then imports the client's `clientdb.sd2` into the `sdb` schema
- `rin-internal` and `rin-webapi`
- `collision-cache`, which pre-generates the collision data with PIN's `CollisionGenerator`
- `pin-gameserver` and `pin-matrix`
- `firefall`, the game client itself, which only starts when you tell it to

The resources wait on each other in that order. So the Matrix server only hands out clients once the game server says, that it's ready.

## Prerequisites

- .NET 10 SDK (the AppHost), plus the .NET 8 and 9 runtimes for RIN and PIN
- An ASP.NET Core development certificate, i.e. `dotnet dev-certs https` once
- Docker or a compatible runtime for Postgres. On Windows, Rancher Desktop works well, while Podman 6 has issues with published ports on WSL
- A Firefall 1962 client install - Calldown finds it through the `firefall://` handler or your Steam libraries
- Write access to the install folder, "Configure client" edits `firefall.ini` and patches `FirefallClient.exe` to bypass RedHanded
- The Aspire CLI is optional but handy: `dotnet tool install -g Aspire.Cli`

The repos are expected as siblings:

```
TMW/
  Calldown/
  PIN/
  RIN.WebAPI/
  SDBrowser/
```

If yours live elsewhere, set the MSBuild properties `PinRoot`, `RinRoot` and `SDBrowserRoot`, e.g. with `dotnet build -p:PinRoot=D:\code\PIN\`.

## Running It

```
aspire run
```

`dotnet run --project src/Calldown.AppHost` or F5 in Rider/Visual Studio work just as well.

The first start takes a bit longer:
- The SDB import takes about 20s
- The collision cache for zone 448 about a minute and 5.6 GB in `%LocalAppData%\Calldown\collision`

Every later start skips both, as long as the client files haven't changed.

## Parameters

Set them in the AppHost's user secrets, e.g. `aspire secret set Parameters:zone-id 1030` or `dotnet user-secrets set Parameters:zone-id 1030 --project src/Calldown.AppHost`.

| Parameter       | Default          |                                                                      |
|-----------------|------------------|----------------------------------------------------------------------|
| `firefall-path` | detected install | Folder that contains `system\` and `firefall.ini`                    |
| `zone-id`       | `448`            | Default zone for characters without a last zone; the collision cache is built for it |
| `preload-zones` | `1030`           | Comma separated zones hosted from startup. Other zones load on first use, and their collision cache is built then |
| `collision`     | `zone+assets`    | `off`, `zone` or `zone+assets`. Changing it needs an AppHost restart |
| `pg-user`       | `tmwadmin`       | Has to stay `tmwadmin`, RIN's schema assigns everything to that role |

## Dashboard Commands

| Resource          | Command                 |                                                                                                                                                                           |
|-------------------|-------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `firefall`        | Configure client        | Sets `OperatorHost` in `firefall.ini` to `https://localhost:5001` and patches `FirefallClient.exe` to bypass RedHanded, keeps the originals with a `.calldown.bak` suffix |
| `firefall`        | Client options          | Screen mode and a key to leave a vehicle                                                                                                                                  |
| `firefall`        | Restore client          | Puts the backups back, the unpatched client included                                                                                                                      |
| `rin-webapi`      | Create account          | Registers an account with email, password and country                                                                                                                     |
| `db-setup`        | Re-import SDB           | Imports `clientdb.sd2` again                                                                                                                                              |
| `db-setup`        | Reset database          | Drops everything, accounts and characters included, and sets it up from scratch                                                                                           |
| `collision-cache` | Rebuild collision cache | Regenerates the cache even if it looks current                                                                                                                            |

All of them also work from the terminal, e.g. `aspire resource rin-webapi create-account --email me@example.com --password hunter22 --country US`.

## Ports

| Port          |                                                        |
|---------------|--------------------------------------------------------|
| 5000 / 5001   | RIN.WebAPI (http / https), the client's `OperatorHost` |
| 5201          | RIN.InternalAPI, gRPC over cleartext HTTP/2            |
| 25000 / 25001 | Matrix and game server, UDP                            |
| 25080 / 25081 | Health endpoints of the Matrix and game server         |
