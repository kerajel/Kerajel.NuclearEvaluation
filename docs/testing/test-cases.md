# Nuclear Evaluation Test Cases

Last reviewed: 2026-06-14

## Scope

- Purpose: durable manual/regression QA checklist for Nuclear Evaluation.
- Execute against a disposable local or staging environment with seeded data.
- Use a fresh browser session when checking captcha, cookie, and theme behavior.
- For STEM upload checks, use small valid STEM CSV/TSV/DAT files, malformed delimited files, and a file larger than the configured STEM preview limit.
- Do not run destructive CRUD or upload-delete cases against production data.

## Executable Suite

- Runner: `tests/e2e/specs/nuclear-evaluation.spec.ts`
- Docker entrypoint: `docker compose --profile e2e up --build --abort-on-container-exit --exit-code-from e2e e2e`
- Parallelism: Docker defaults to 2 Playwright workers; override with `E2E_WORKERS` for stress runs, or use `npm run test:serial` for one-worker debugging.
- All executable cases are intended to pass; regressions should fail the Docker suite.

## Summary

- Captured cases: 64
- Executed cases: 62
- Passed: 62
- Failed: 0
- Superseded runner assertion: 1 old combined PMI/API check was replaced by explicit API and client-route checks.

The previously failing rebuild checks now pass:

- Preset-filter save/check state: PASS. The check button is disabled before a valid name and enables after a valid unique name.
- Removed PMI API behavior: PASS. `/api/pmi` returns a real API 404 instead of the SPA shell, and `/pmi` client navigation lands on the app not-found page.

## Findings

| Priority | Finding | Detail |
|---|---|---|
| P3 | STEM delete/cancel requests emit aborted network events while UI cleanup succeeds | STEM delete and in-flight cancel flows passed visually and by data cleanup, but browser network events included `net::ERR_ABORTED` for some `/api/stem/{session}/files/{fileId}` requests. Treat as a watch item if the flow becomes flaky. |
| P3 | Seeded project dates include suspicious future/out-of-order values | The Evaluation project grid showed synthetic rows with dates beyond the review date and at least one row where `CreatedAt` appeared later than `UpdatedAt`. Not counted as a failure because the seed appears synthetic, but it is worth validating if date ordering matters. |

## Historical Regression Checklist

| ID | Regression | Result | Evidence |
|---|---|---|---|
| BE-1 | Filtering Projects by id | PASS | Project filter `Id == 1` returned one project with populated `ProjectSeries`. |
| BE-2 | Opening `/projects/1` | PASS | Project detail rendered instead of routing to not-found. |
| BE-3 | Upload 2 STEM files, delete 1 | PASS | Deleting the first uploaded file removed only that file's preview rows and left the second file visible. |
| BE-4 | PMI dashboard/API removed | PASS | No PMI UI affordance remains; `/api/pmi` returns 404; `/pmi` renders the client not-found page. |
| UI-1 | Dark/light toggle | PASS | Radzen stylesheet and visual theme changed after the appearance toggle. |
| UI-2 | Series delete tooltip overflow | PASS | Native title exists and no horizontal overflow was introduced. |
| UI-3 | Sample External Code filter | PASS | Direct sample filtering and query-builder-compatible `Sample.ExternalCode` filtering returned matching rows. |
| UI-4 | Preset-filter Save button disabled logic | PASS | Save/check stayed disabled for blank input and enabled for a valid unique name. |
| UI-5 | Grid empty flash/teary resize | PASS | Cached results and reserved grid heights were present. |
| UI-6 | STEM drag/drop target | PASS | The actual file input accepts drops and visible dropzone content does not intercept pointer events. |
| UI-7 | Multi-file upload | PASS | Two STEM files were selected and uploaded together. |
| UI-8 | Remove File during upload | PASS | In-flight upload was cancelled/removed immediately. |
| UI-9 | Child sample grid teary expand | PASS | Expanded child sample grid reserved height and did not overflow the page. |
| UI-10 | Uploaded-files list empty gap | PASS | Compact upload grid opted out of the large min-height. |
| UI-11 | Series totals cached | PASS | Series totals cache entry exists alongside grid-result cache entries. |

## Executed Test Cases

| ID | Area | Scenario | Steps | Expected | Result | Evidence |
|---|---|---|---|---|---|---|
| CAP-01 | Gate | Captcha status remains verified in a fresh browser session | Open a verified application session; read `/api/captcha/status` from page context | The API reports `verified=true` and the gate is not visible. | PASS | Captcha status API returned verified=true; gate was not visible. |
| NAV-01 | Navigation | Home page and sidebar navigation render | Navigate to Home; check home copy and sidebar menu labels | Home renders and sidebar includes Home, Data Management, and Evaluation. | PASS | Home page and sidebar rendered expected copy and links. |
| NAV-02 | Navigation | Unknown route shows friendly not-found page | Navigate to an unknown client route | The route displays Page not found rather than crashing. | PASS | Unknown route rendered friendly Page not found without Blazor error UI. |
| UI-01 | Theme | Dark/light appearance toggle changes the active theme | Open Data Management; capture theme state; click appearance toggle; capture theme state again | The theme state changes visibly or through the active stylesheet. | PASS | Theme stylesheet changed and body background changed. |
| DM-01 | Data Management | Series tab loads totals and first page without fetch errors | Open Data Management; wait for series totals and grid rows | Series totals are numeric, first page displays rows, and no fetch/blazor error is visible. | PASS | Seed shows 100,000 series; randomized dependent totals are nonzero and match the counts API. |
| DM-02 | Data Management | No PMI upload/dashboard affordances remain in the app shell | Inspect Data Management and Evaluation visible text | Removed PMI feature is not exposed through visible tabs, nav, dashboard, upload, or grid copy. | PASS | No visible PMI text, links, or tabs on Data Management or Evaluation. |
| DM-03 | Data Management | Grid result cache and reserved grid height are active | Load Data Management series tab; inspect cache keys and grid heights | Series grid results are cached; main grids reserve vertical space; compact upload grid can opt out. | PASS | Series grid cache entry exists; main grid min-height was reserved; compact grid min-height was zero. |
| DM-04 | Data Management | Disabled delete tooltip is native title and does not create horizontal overflow | Load Series grid; find a row with samples; hover disabled delete wrapper; measure page width | Native title exists, no Radzen popup appears, and document width does not overflow. | PASS | `title="Series contains samples"` exists; no popup text; page width stayed within viewport. |
| BE-01 | Backend/Data | Project view filtering by id succeeds with includes applied last | POST `/api/views/projects` with filter `Id == 1` | API returns success, one project, and populated project-series navigation data. | PASS | One project returned with `ProjectSeries` entries. |
| BE-02 | Backend/Data | Opening `/projects/1` loads project detail instead of not-found | Navigate to `/projects/1`; inspect tabs and project fields | Project detail renders Overview, Series, Samples, SubSamples, Apm, and Particles tabs with no 404. | PASS | Project detail tabs rendered. |
| BE-03 | Backend/Data | Sample External Code filtering works through data/query-builder-compatible API | Fetch a sample row; filter samples by its `ExternalCode`; filter query-builder composite by `Sample.ExternalCode` | Direct sample filtering succeeds, and query-builder mode accepts `Sample.ExternalCode` without error. | PASS | Direct and query-builder-compatible filters returned matching rows. |
| BE-04 | Backend/Data | Project-scoped grids and charts return data for project 1 | Call series/sample/subsample/apm/particle scoped grids; call APM and particle chart APIs | All scoped endpoints succeed and chart APIs return arrays. | PASS | Project 1 returned nonzero scoped totals; chart APIs returned arrays. |
| UI-02 | Evaluation | Evaluation page query-builder shell renders expected controls | Open Evaluation; open Query Builder tab; inspect filter sections and controls | Query Builder contains filter sections, Apply, grid selector, and preset controls. | PASS | Series, Sample, SubSample, Apm, Particle, Apply, grid selector, and preset controls rendered. |
| STEM-01 | STEM Upload | STEM tab exposes sample downloads and multi-file drop target | Open STEM Preview tab; inspect download links and file input attributes | Sample downloads exist, file input has multiple enabled, supported extensions are advertised, and visible dropzone content does not intercept pointer events. | PASS | Three sample download links present; file input has `multiple` plus CSV/TSV/DAT/XLSB accept hints; dropzone content pointer-events is none. |
| STEM-02 | STEM Upload | Uploading two STEM CSV files previews both, deleting one removes only its rows | Select two valid CSV files; upload; delete first uploaded file; check preview grid | Both files upload; deleting one removes only that file rows while keeping the other. | PASS | Deleting the first file removed its rows and left the second file rows visible. |
| STEM-03 | STEM Upload | Invalid STEM file reports upload error without breaking the page | Select malformed CSV; upload; inspect status | File row reaches error status and app stays usable. | PASS | Malformed CSV showed processing error and no Blazor error UI. |
| STEM-04 | STEM Upload | Uploading TSV and tab-delimited DAT files previews both | Select valid TSV and DAT files; upload; inspect preview grid | Both delimited text files upload and their rows appear in the preview grid. | PASS | TSV and DAT fixture rows rendered with expected lab codes. |
| CRUD-01 | Backend/Data | Series create/delete API round trip keeps seeded grid usable | Create a temporary no-sample series through app API; fetch it; delete it; verify it is gone | Series CRUD endpoints work for a no-sample row and cleanup succeeds. | PASS | Temporary series was created, fetched, deleted, and no longer fetchable. |
| UI-09 | Data Management | Series row expansion loads child sample grid without layout tear | Expand the first series row; inspect nested sample grid and layout width | Child sample grid shows rows, reserves nested height, and no page overflow is introduced. | PASS | Nested sample grid showed rows with reserved height and no horizontal overflow. |
| UI-04 | Evaluation | Preset-filter save button enables only after valid name and creates filter | Open Query Builder; click preset save; observe disabled check button; enter valid unique name; save; cleanup via API | Check button is disabled for invalid input, enables for valid unique name, and created filter can be deleted. | PASS | Disabled before input, enabled after valid name; temporary preset was created and deleted. |
| UI-08 | STEM Upload | Remove File during large upload cancels immediately | Select a larger valid CSV; start upload; remove while uploading; confirm delete | In-flight upload is cancelled/removed without waiting for completion and no rows remain visible. | PASS | Upload row disappeared immediately and no rows from the cancelled file appeared. |
| ADD-01 | Backend/API | Removed `/api/pmi` route does not return SPA shell | GET `/api/pmi` | Returns API miss, not index.html HTTP 200. | PASS | Returned HTTP 404 with empty body, not the app shell. |
| ADD-02 | Backend/API | Unknown `/api` route does not return SPA shell | GET `/api/not-a-real-endpoint` | Returns API miss, not index.html HTTP 200. | PASS | Returned HTTP 404 with empty body. |
| ADD-03 | Navigation | Removed `/pmi` client route lands on app not-found | Navigate to `/pmi` | Client route shows Page not found. | PASS | Page not found text rendered. |
| ADD-04 | Navigation | Data Management reload is stable | Open Data Management and reload | Tabs, totals, and grid rows still render after reload. | PASS | Series and STEM Preview tabs rendered after reload; no visible errors. |
| ADD-05 | Navigation | Evaluation reload is stable | Open Evaluation and reload | Projects and Query Builder tabs still render after reload. | PASS | Projects and Query Builder tabs rendered after reload; no visible errors. |
| ADD-06 | Layout | Sidebar toggle changes width and restores | Click sidebar toggle; wait for collapse; click again; wait for restore | Sidebar collapses and expands. | PASS | Width changed from 250px to near 0px, then restored to 250px. |
| ADD-07 | Responsive | Home fits mobile viewport | Set mobile viewport and open Home | No page-level horizontal overflow. | PASS | Document scroll width matched client width. |
| ADD-08 | Project Detail | Missing project id routes to not-found | Open `/projects/999999999`; wait for app route transition | Page not found is shown. | PASS | URL became `/not-found` and Page not found rendered. |
| ADD-09 | Project Detail | Project tabs load without visible errors | Open project 1; click Series, Samples, SubSamples, Apm, Particles | Each tab remains usable, Series grid sizes to content, APM/Particle grids render, pager copy does not show raw markup, and no visible app error appears. | PASS | All five project tabs rendered; APM and Particle grid captions/headers appeared. |
| ADD-10 | Project Detail | Back to Projects returns to Evaluation | Open project 1; click Back to Projects | URL returns to Evaluation. | PASS | Navigation returned to Evaluation. |
| ADD-11 | Project Detail | Blank project name disables save | Edit project name; clear input | Save button is disabled. | PASS | Save button disabled for blank project name. |
| ADD-12 | Backend/Data | Project scoped endpoints return nonzero totals | Call scoped grid endpoints for project 1 | All scoped endpoints succeed with nonzero totals. | PASS | Exact totals are not fixed because project series membership can be changed during exploratory UI runs. |
| ADD-13 | Backend/Data | Project chart APIs return arrays | Call APM and particle chart endpoints for project 1 | Both endpoints return arrays. | PASS | APM chart returned 4 entries; particle chart returned 2 entries. |
| ADD-14 | Backend/Data | Series sort by id descending works | Request series ordered by `Id desc` | Returned IDs are descending. | PASS | IDs began 109999, 109998, 109997, 109996, 109995. |
| ADD-15 | Backend/Data | Project filter and order combination succeeds | Filter projects by `SampleCount > 0`; order by `Id desc` | Request succeeds with populated `ProjectSeries`. | PASS | Returned total 31,665 and included `ProjectSeries`. |
| ADD-16 | Backend/API | Preset name availability detects duplicates | Create preset via API; check name availability; cleanup | Existing name returns `available=false`; cleanup succeeds. | PASS | Duplicate name availability returned false; temporary preset deleted. |
| ADD-17 | Backend/API | STEM upload endpoint rejects missing file | POST STEM upload form without a file | Request is rejected without server crash. | PASS | Returned HTTP 400 validation response for missing `file`. |
| ADD-18 | Evaluation | Query Builder switches to Sample grid | Open Query Builder and select Sample grid | Sample grid columns render. | PASS | Sample grid showed External Code, Sample Type, Sampling Date, and SubSample Count columns. |
| ADD-19 | Evaluation | Query Builder Apply with empty filters keeps grid usable | Open Query Builder; click Apply without filters | Grid remains visible and no fetch error appears. | PASS | Grid stayed populated after Apply. |
| ADD-20 | CRUD | Series update API persists SGAS comment | Create temp series with SGAS comment; update comment; fetch by id; delete temp row | Updated SGAS comment is returned by the view endpoint. | PASS | Fetch after update returned the changed SGAS comment and cleanup succeeded. |
| ADD-21 | STEM Upload | Pending STEM file can be removed before upload | Select valid file; remove before upload | File disappears and no preview rows appear. | PASS | Pending file row disappeared; preview rows did not appear. |
| ADD-22 | STEM Upload | Oversized STEM file is rejected client-side | Select a file larger than the STEM preview limit | Size error appears and upload button is unavailable. | PASS | File row showed `Size exceeds 64 MB`; Upload Files button was not shown. |
| ADD-23 | STEM Upload | Preview grid stays hidden before upload | Select valid file but do not upload | No preview rows are shown. | PASS | Pending file appeared, but preview row content was absent. |
| ADD-24 | STEM Upload | Deleting last uploaded STEM file hides preview rows | Upload one valid file; delete it; confirm | Uploaded rows disappear after delete. | PASS | Uploaded file row and preview rows disappeared. |
| ADD-25 | CRUD | Series create API persists SGAS comment on insert | Create temp series with SGAS comment; fetch by id; delete temp row | Created SGAS comment is returned by the view endpoint. | PASS | Created row returned the inserted SGAS comment and cleanup succeeded. |
| ADD-26 | CRUD | Series update API persists working paper link and DU/NU flags | Create temp series; update working paper link and DU/NU flags; fetch by id; delete temp row | Updated scalar fields are returned by the view endpoint. | PASS | Updated link, DU flag, NU flag, and comment were persisted. |
| ADD-27 | CRUD | Series update API can clear nullable analysis date | Create temp series with analysis date; update it to null; fetch by id; delete temp row | Nullable analysis date can be cleared. | PASS | Fetch after update returned `analysisCompleteDate=null`. |
| ADD-28 | Backend/API | Series delete endpoint is idempotent for missing ids | DELETE a non-existent series id | Endpoint succeeds without affecting existing rows. | PASS | Missing-id delete returned success. |
| ADD-29 | Backend/API | Project name availability respects existing rows and exclude id | Fetch project 1 name; check name availability with and without its id excluded | Existing name is unavailable globally and available when excluding the same project id. | PASS | Duplicate check returned false; self-excluded check returned true. |
| ADD-30 | Backend/API | Preset name availability respects exclude id | Create temp preset; check name availability with and without its id excluded; cleanup | Existing preset name is unavailable globally and available when excluding itself. | PASS | Duplicate check returned false; self-excluded check returned true. |
| ADD-31 | Backend/API | Preset update renames an empty preset | Create temp preset; update its name; fetch; cleanup | Updated preset payload has the new name and remains entry-free. | PASS | Fetch returned the updated name with no entries. |
| ADD-32 | Backend/API | Preset delete removes row from dropdown payload | Create temp preset; delete it; fetch all presets | Deleted preset is absent from the list payload. | PASS | Deleted id was no longer returned. |
| ADD-33 | Backend/Data | Series priority ids are ordered before normal sort | Fetch lowest and highest existing series ids; request descending series with the low id prioritized | Priority id appears first even when normal sort would put another id first. | PASS | Prioritized response returned the discovered low series id first. |
| ADD-34 | Backend/Data | Series counts respect impossible filters | POST series-counts with impossible id filter | All aggregate counts are zero. | PASS | Series, sample, subsample, APM, and particle counts returned zero. |
| ADD-35 | Backend/API | Series enum option endpoint returns series type values | POST enum-options for SeriesType | Endpoint returns integer enum values. | PASS | SeriesType options returned one or more integer values. |
| ADD-36 | Backend/API | Sample enum option endpoint returns sample type values | POST enum-options for SampleType | Endpoint returns integer enum values. | PASS | SampleType options returned one or more integer values. |
| ADD-37 | STEM API | STEM entries endpoint without a session returns empty result | POST stem-entries without `stemSessionId` | Endpoint succeeds with zero rows. | PASS | Returned totalCount 0 and empty entries. |
| ADD-38 | STEM API | STEM entries endpoint with an unknown session returns empty result | POST stem-entries with random `stemSessionId` | Endpoint succeeds with zero rows. | PASS | Returned totalCount 0 and empty entries. |
| ADD-39 | Backend/API | Chart APIs return empty arrays for missing project ids | GET APM and particle chart APIs for a missing project id | Endpoints return HTTP 200 with empty arrays. | PASS | Both chart APIs returned empty arrays. |
| ADD-40 | UI/Layout | Project detail fits a mobile viewport | Open `/projects/1` at 390px width; measure document width | Project detail renders without horizontal overflow or app error UI. | PASS | Mobile project detail stayed within viewport. |
| ADD-41 | Backend/API | Query-aware chart APIs respect grid filters | POST APM and particle chart APIs with an impossible grid filter | Endpoints return HTTP 200 with empty arrays. | PASS | Both query-aware chart APIs returned empty arrays for an impossible `Id` filter. |
| ADD-42 | Backend/API | Chart APIs accept Radzen nullable comparison filters | POST APM and particle chart APIs with `x => ((x.U234 ?? null) > 1)` | Endpoints return HTTP 200 with chart arrays instead of a dynamic-LINQ comparison error. | NOT RUN | Added as executable coverage; not run per request to skip UI/e2e tests unless explicitly asked. |
| ADD-43 | Backend/API | Preset create accepts enabled entries with null navigation payloads | POST a preset filter with enabled sample descriptors and a null navigation property | Endpoint creates the preset and returns it with its entry payload. | NOT RUN | Added as executable coverage; not run per request to skip UI/e2e tests unless explicitly asked. |

## Superseded Checks

| ID | Status | Reason |
|---|---|---|
| API-01 | Superseded | The earlier runner mixed enum-option validation with removed PMI route behavior and treated raw `/pmi` route fetching as an API failure. The rebuilt behavior is now covered by `ADD-01`, `ADD-02`, and `ADD-03`. |

## Retest Notes

- Temporary presets and temporary series rows created by the tests were cleaned up.
- The oversized STEM test used a synthetic browser-side file because debug-controlled browsers may refuse transferring files larger than 50 MB from the automation host. The app still received a normal file-selection event and reported the configured 64 MB limit.
- Backend/data assertions should use the same verified browser/session context as the real client when practical.
