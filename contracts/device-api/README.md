# Device <-> API contract (`contracts/device-api/`)

Narrow, machine-checkable JSON Schemas for the HTTP endpoints the AgrumyFirmware firmware
actually calls. This is deliberately **not** a full OpenAPI document - `Microsoft.AspNetCore.OpenApi`
already generates general API docs from the C# models. The point here is a tight contract
that a test can enforce, so that renaming/removing a JSON field on one side breaks a build
instead of a device in the field.

## Endpoints & files

| Endpoint | Method | Request schema | Response schema | DTO |
|---|---|---|---|---|
| `/api/Device/Register` | POST | `register.request.schema.json` | `register.response.schema.json` | `DeviceRegistration` / `DeviceConfig` |
| `/api/Device/Authenticate` | POST | `authenticate.request.schema.json` *(hand-written, empty body)* | `authenticate.response.schema.json` | - / `DeviceAuthentication` |
| `/api/Device/Config` | POST | `config.request.schema.json` | `config.response.schema.json` | `DeviceConfigPoll` / `DeviceConfig` |
| `/api/Device/Simulation` | GET | *(none)* | `simulation.response.schema.json` | `DeviceSimulation` |
| `/api/SensorData` | POST | `sensordata.request.schema.json` | *(none - bare cached ConfigVersion)* | `SensorDataPushReading[]` |
| `/api/ControllerData` | POST | `controllerdata.request.schema.json` | *(none)* | `ControllerDataPush[]` |

Draft-07. `register.response.schema.json` and `config.response.schema.json` are generated
from the same `DeviceConfig` DTO, so they are identical apart from `$id`/`title`.

## Generated from the DTOs - who owns what

Every file except `authenticate.request.schema.json` is written by
`tools/Agrumy.ContractGen` (`ContractSchemaGenerator.Specs` lists file -> DTO). The DTO in
`Agrumy.Shared` is the single source of truth for the **shape**:

* **Generator-owned** (never edit by hand, the next run overwrites it): `properties` and
  their key names (camelCase via `JsonNamingPolicy.CamelCase`, or as-declared for
  `config.request`, whose PascalCase keys MVC binds case-insensitively), `type` lists
  (nullable => `"null"`; request-body numbers also accept `"string"`, matching
  `JsonSerializerDefaults.Web`'s `AllowReadingFromString`), `items`, `$ref`/`oneOf`,
  `definitions`, `additionalProperties: false`, and `required` on **response** schemas
  (every property, since the server always emits every key with nulls kept).
* **Kept from the committed file** at the same JSON path: `description`, `$comment`,
  `enum`, `pattern`, `minLength`/`maxLength`, `minimum`/`maximum`, `minItems`/`maxItems`,
  `maxProperties`, `format`, `examples`, `default`, `deprecated` - and `required` on
  **request** schemas, which records what the firmware always sends (a contract decision
  the DTO cannot know). A property that disappears from the DTO takes its annotations with
  it; a new property arrives bare until someone describes it in the JSON.

Regenerate after any DTO change:

```
dotnet run --project tools/Agrumy.ContractGen              # rewrites the files in place
dotnet run --project tools/Agrumy.ContractGen -- --check   # exit 1 + list of stale files
```

## Enforcement

* **API side** - `Agrumy.Api.Tests/ContractGenerationTests.cs` fails when a committed file
  differs from what the generator produces from the current DTOs (i.e. someone changed a
  DTO and did not re-run the tool), and `ContractTests.cs` validates real MVC-serialized
  payloads against the schemas. Both run in the normal `dotnet test` CI step.
* **Firmware side** - `AgrumyFirmware/tools/contract-check/` keeps a hand-maintained list
  of the JSON keys the firmware sends/expects and checks it against a **copy** of these
  schemas, and its CI also re-runs that check against the live files from this repo's
  `master` so a contract change here shows up there before the copy is re-synced.

## When you change the contract

1. Change the DTO in `Agrumy.Shared` (this is the only place the shape lives).
2. `dotnet run --project tools/Agrumy.ContractGen`, then describe any new property in the
   JSON and adjust a request schema's `required` if the firmware's promise changed.
3. `dotnet test` - `ContractGenerationTests` and `ContractTests` must stay green.
4. In AgrumyFirmware: `python tools/contract-check/sync_schemas.py` (or copy the files by
   hand), update `tools/contract-check/firmware_fields.py`, and the parser/payload code if
   the firmware's real behaviour changed.
