# Plan

**tl;dr:** Milestones 1 to 4 are done, 5 and 6 are half finished. See [Status](#status) for the details and [Open points](#open-points) for what needs a decision. `README.md` covers how to run the thing, this file covers what it is and what's left.

## Why it exists

Starting a local Firefall server used to be a page of manual steps: bring up RIN's docker compose, apply `webapi_schema.sql` and the seed data through batch scripts that call `psql -s` (single-step mode, of all things), import `clientdb.sd2` through the SDBrowser GUI, then start four processes in the right order from the right working directories, with three of PIN's four `Start*.bat` pointing at the wrong target framework. After that edit `firefall.ini`, make sure the client bypasses RedHanded, and create an account.

On top of that nothing had health checks or telemetry, GameServer could only be configured through XML, and the ports collided in two places: RIN's pgAdmin with PIN's Seq on 8081, RIN.WebAPI with PIN's `WebHostManager` on 5000/5001.

The goal is one `aspire run`, aimed at devs and contributors, i.e. people who already have the source trees, a .NET SDK and Docker. An end-user bundle is explicitly not a goal.

Decisions taken along the way:

- The name comes from Oilspill calling down thumpers.
- RIN is the web tier. PIN's `WebHostManager` is legacy and stays out of the graph.
- Changes in PIN, RIN.WebAPI and SDBrowser are allowed. All of them live on local `calldown` branches.
- The scope includes the headless SDB import, configuring the client and launching it.
- Calldown doesn't own a certificate. The client doesn't check the one RIN presents, so the ASP.NET development certificate does the job, see [TLS](#tls).

## Why Aspire

The dashboard shows console output even for resources without any telemetry, and gives start, stop and restart per resource, which replaces Seq and the `start /D` batch files in one go. `WaitFor` and `WaitForCompletion` plus health checks enforce the dependency chain that nothing enforced before. `AddPostgres` with a data volume and a generated password removes the compose file. `WithWorkingDirectory` and `WithEnvironment` fix the CWD-relative data paths. Resource commands and the interaction service cover "Create account", "Configure client" and friends, and parameters land in user secrets so the dashboard can ask for anything that's missing.

Keep in mind:

- UDP endpoints need `isProxied: false`, because the DCP proxy only speaks TCP. As such the UDP servers report health through a small HTTP endpoint instead of anything UDP based.
- The AppHost needs the .NET 10 SDK. The resources themselves stay on net8 (RIN) and net9 (PIN).
- Postgres needs Docker, e.g. Rancher Desktop.
- This is a dev-time orchestrator. `aspire publish` to compose exists, but GameServer's Windows paths and the client keep the whole thing Windows-local.

Rejected alternatives: plain docker compose (no dashboard, no commands, PIN is heavy on Windows paths), an ImTool GUI launcher (rebuilds what the dashboard gives for free) and yet more scripts (that's what keeps breaking today).

## Resource graph

`src/Calldown.AppHost/AppHost.cs` is the source of truth, this is the shape of it.

| Resource             | Waits for                                   | Notes                                                                                                                                                                                                                                |
|----------------------|---------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `postgres` + `pgweb` |                                             | Persistent containers with the `calldown-postgres` volume, user `tmwadmin` because RIN's schema dump assigns every object to that role                                                                                               |
| `db-setup`           | `tmw` database                              | Runs to completion. Schema, then `ZoneSettings`, `ZoneCertificates`, `ZoneDifficulty`, `Leaderboards` in that order, then the SDB. Each step is skipped when its marker says it's done, the SDB one keyed on the `clientdb.sd2` hash |
| `rin-internal`       | `db-setup` completed                        | Connection strings with `Search Path=webapi` and `=sdb`, health checked over cleartext HTTP/2                                                                                                                                        |
| `rin-webapi`         | `db-setup` completed                        | `WebApiConfig__BaseURL` points at its own https endpoint, `DevServer__DevGameServerURL` at the Matrix port                                                                                                                           |
| `collision-cache`    |                                             | Only exists when the `collision` parameter isn't `off`. Runs PIN's `CollisionGenerator` into `%LocalAppData%\Calldown\collision\1962`                                                                                                |
| `pin-gameserver`     | `rin-internal`, `collision-cache` completed | UDP 25001 unproxied, health on 25081, paths and zone from the parameters                                                                                                                                                             |
| `pin-matrix`         | `pin-gameserver`                            | UDP 25000 unproxied, health on 25080, hands out the game server port in its `Hugg` reply                                                                                                                                             |
| `firefall`           | `rin-webapi`, `pin-matrix`                  | The client as an explicit-start executable, so starting the resource is the launch                                                                                                                                                   |

Parameters, ports and the dashboard commands are documented in `README.md`.

## Status

### Milestones

1. **Skeleton** - done. AppHost with Postgres, `db-setup` for schema and seeds, both RIN services.
2. **Headless SDB** - done. The import moved out of SDBrowser's WinForms code into a UI-free library that `db-setup` references.
3. **PIN in the graph** - done. Configurable ports, the health endpoints, the collision cache resource and the full ordering.
4. **Client** - done. Install discovery, `firefall.ini` handling with backups, the RedHanded check, preflight notifications, and the Configure, Restore and Create account commands. The TLS question is answered, see [TLS](#tls). Remnant's TF369 auto-login still isn't wired up, but that's convenience, not a blocker.
5. **Observability** - half. RIN has health checks and OpenTelemetry, PIN exports its logs the same way. PIN has no metrics and no gRPC client tracing, so its Traces tab stays empty.
6. **Docs** - half. `README.md` is written and `OVERVIEW.md` mentions Calldown. The wiki page "Running a server with Calldown" isn't written.

### PIN (`calldown` branch, four commits on top of `master`)

Done:

- Both UDP hosts read `appsettings.json`, with environment variables and CLI options layered on top. This replaced the planned "App.config first, env vars on top" approach entirely, without a fallback and without a migration path. That work sits on PIN's `master` now, not on the branch.
- `GameServer__Port` and `MatrixServer__Port` are configurable, and the hard-coded `MatrixPacketHugg(1, 25001)` uses the configured `GameServerPort`.
- An opt-in health endpoint in `Lib/Shared.Udp/PacketServer.cs`, enabled through `GameServer__HealthUrl` and `MatrixServer__HealthUrl`. It answers 503 until the shard reports ready, and it doesn't exist when the setting is empty.
- `CollisionGenerator` writes a `manifest.json`, skips a current cache unless `--force` is given, and returns a non-zero exit code when items fail.
- The Serilog OpenTelemetry sink in `Lib/Shared.Common/OpenTelemetryLogging.cs`, active when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- The `Start*.bat` paths are correct again.

Open:

- No OTel metrics and no gRPC client tracing.
- Aspire stops processes with Ctrl+C and Ctrl+Break, and neither UDP host handles `Console.CancelKeyPress` or `AppDomain.ProcessExit`. Whether they shut down cleanly or just die is unverified.
- `Lib/Shared.Common/LocationProvider.cs` still isn't used as the fallback when `StaticDBPath` is empty.

### RIN.WebAPI (`calldown` branch)

Done:

- Health checks and OpenTelemetry in `RIN.Core/Hosting/ObservabilityExtensions.cs`, gated on `OTEL_EXPORTER_OTLP_ENDPOINT`.
- The dev `RegisterAccount` endpoint returns its result, so account creation can be scripted.
- Npgsql fixes for local dev: GSS encryption off for the local connection strings, stored procedure compat mode on at startup.
- Protobuf and gRPC packages updated, and the event stream moved to the code-first duplex signature.
- `webapi_schema.sql` stays the source of truth, `db-setup` applies it through Npgsql rather than `psql`.

Open:

- Which TLS version the client actually negotiates, see [TLS](#tls). Nothing sets `SslProtocols`, PIN's `Lib/Shared.Web/BaseWebServer.cs:47` does.
- `POST clientapi/api/v2/accounts` in `ClientApiV2.Account.cs:93` doesn't await `Db.RegisterNewAccount` and returns `new { }` no matter what happens, so creating an account from the client UI fails quietly.

### SDBrowser (`calldown` branch)

Done: the Postgres import lives in a UI-free library that both the WinForms UI and `db-setup` use, with the composite type mapping moved onto the data source.

Open: whether the branch should be retargeted from `net9.0-windows` to `net10.0-windows`.

## Open points

- **Seq.** PIN's Serilog setup has a Seq sink with a `controlLevelSwitch`, which lets you raise a running server's log level from Seq's UI. The dashboard has no equivalent and keeps its telemetry in memory, so restarting the AppHost throws the logs away. An opt-in `AddSeq("seq").WithDataVolume()` resource next to the OpenTelemetry export would cover both, off by default so a plain `aspire run` needs no extra container. Note that Seq ingests OTLP logs and traces, but has no metrics endpoint, so it can't be the only backend.
- **Account creation** through the real client UI, see the RIN item above. Fixing it also lets the dashboard command drop the DEBUG-only `DevTestController` route.
- **Preflight** currently checks the install, the RedHanded state and `firefall.ini`. Docker not running, a port already in use and the development certificate aren't checked yet. The certificate check is about it existing at all, Kestrel needs one to serve HTTPS, not about it being trusted, see [TLS](#tls).
- **Patching the client** writes into the install folder. Under `Program Files` that needs write access, and the command should say so rather than fail quietly.

## TLS

Settled on 2026-09-28 with client build 1962 against `rin-webapi` on `https://localhost:5001`: **the client doesn't verify the certificate.** Calldown needs none of its own, the ASP.NET development certificate is enough, and nothing has to be appended to the client's `curl-ca-bundle.crt`.

The client talks HTTPS through `libcurl/7.26.0` built against OpenSSL 1.0.1c, i.e. `libeay32.dll` and `ssleay32.dll` next to `FirefallClient.exe`. That build reads `curl-ca-bundle.crt` and never touches the Windows certificate store, so the original plan of running `dotnet dev-certs https --clean` couldn't have answered anything - trusted or not, the client sees the same thing either way.

The state on disk turned out to be the experiment already. `curl-ca-bundle.crt` is the untouched Mozilla bundle from 2012-06-28 with 155 roots and no `localhost` entry, while RIN presents a self-signed `CN=localhost`, RSA-2048, sha256RSA. A client that verified the chain couldn't have completed that handshake, and logging in worked, so `CURLOPT_SSL_VERIFYPEER` is off or `CAINFO` never takes effect. Note that this only rules out chain verification. Two things stay open. Hostname checking may well be on, `CN=localhost` matches either way, which stays irrelevant as long as Calldown serves on localhost. And the protocol version is unknown: RIN sets no `SslProtocols`, but this machine has no Schannel registry overrides, so TLS 1.0 is likely still enabled by OS default and the client may have used it. PIN enables `SslProtocols.Tls` explicitly in `Lib/Shared.Web/BaseWebServer.cs:47` with the comment "Required by FF itself", and `libcurl/7.26.0` is old enough, that `CURLOPT_SSLVERSION` set to `CURL_SSLVERSION_TLSv1` means TLS 1.0 only. As such RIN might be leaning on a default that Windows is in the process of dropping. To settle it, raise `Microsoft.AspNetCore.Server.Kestrel.Https` to Debug and read the negotiated protocol off the next login, or pin RIN to TLS 1.2 and see whether the client still gets in.

Calldown itself never trusts anything and shouldn't start. It runs no `dotnet dev-certs` command, and `Firefall/FirefallClient.cs:122` accepts whatever certificate RIN presents. What it does rely on is a development certificate existing in `CurrentUser\My`, because RIN sets no `Kestrel:Certificates` and Kestrel picks it up from there. Without one, `rin-webapi` doesn't come up on 5001 at all. As such `dotnet dev-certs https` is a prerequisite, `--trust` is not. Trusting only buys a warning-free browser, for RIN's Swagger on 5001 and for the dashboard on 17241, and the `http` launch profile avoids even that.

Keep in mind, that `dotnet dev-certs https --clean` removes every certificate carrying the ASP.NET development certificate marker, including company ones that happen to carry it, and it isn't needed for anything here. Don't run it.

## Verification

What a full pass looks like, with the Postgres volume removed first:

- Every resource reaches Running or Healthy in order, `db-setup` exits 0, and a second run skips every step.
- `webapi."ZoneSettings"` has rows, `sdb."dbvisualrecords::WarpaintPalette"` exists and isn't empty, and `calldown.setup_state` holds the SDB hash.
- `GET {rin-webapi}/check` answers, and `POST /clientapi/api/v1/oracle/ticket` returns `matrix_url` as `localhost:25000`.
- With `collision=zone` the first run bakes the zone chunks and exits 0, a second run skips within a second, and GameServer logs that collision came from the cache. With `collision=off` the resource isn't in the graph at all.
- Create account, then Configure client, then start `firefall`: log in, create a character with colours from `WarpaintPalette`, and enter the world. GameServer logs the Matrix handoff.
- Stop and restart `pin-gameserver` from the dashboard and it rejoins cleanly. `matrix_connectionTest 127.0.0.1` in the client console works.
- Running PIN and RIN directly, without Calldown, still works.

The end-to-end part of this list hasn't been run since the configuration rewrite.
