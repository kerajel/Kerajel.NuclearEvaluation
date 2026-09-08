# Test checklist

This checklist defines the application scenarios and their expected behavior.

## Environment

- Run against a disposable local or staging environment with seeded data.
- Use a fresh browser session to check captcha, cookie, and theme behavior.
- For STEM uploads, include valid CSV/TSV/DAT files, malformed files, and a file above the upload limit.
- Use the verified browser session for API checks that require the captcha cookie.
- Clean up temporary projects, presets, series, and uploaded files after each case.

## Browser suite

- Runner: `tests/e2e/specs/nuclear-evaluation.spec.ts`
- Docker command: `docker compose --profile e2e up --build --abort-on-container-exit --exit-code-from e2e e2e`
- Docker defaults to two Playwright workers. Set `E2E_WORKERS` to change parallelism, or use `npm run test:serial` for one worker.

## Scenarios

| ID | Area | Scenario | Steps | Expected |
|---|---|---|---|---|
| CAP-01 | Gate | Captcha status remains verified with a valid verification cookie | Open a verified application session; read `/api/captcha/status` from page context | The API reports `verified=true` and the gate is not visible. |
| NAV-01 | Navigation | Home page and sidebar navigation render | Navigate to Home; check home copy and sidebar menu labels | Home renders and sidebar includes Home, Data Management, and Evaluation. |
| NAV-02 | Navigation | Unknown route shows friendly not-found page | Navigate to an unknown client route | The route displays Page not found rather than crashing. |
| UI-01 | Theme | Dark/light appearance toggle changes the active theme | Open Data Management; capture theme state; click appearance toggle; capture theme state again | The theme state changes visibly or through the active stylesheet. |
| DM-01 | Data Management | Series tab loads totals and first page without fetch errors | Open Data Management; wait for series totals and grid rows | Series totals are numeric, first page displays rows, and no fetch/blazor error is visible. |
| DM-02 | Data Management | Data Management and Evaluation expose the supported features | Inspect Data Management and Evaluation visible text | The visible tabs and navigation provide access to series, STEM preview, projects, and the query builder. |
| DM-03 | Data Management | Grid result cache and reserved grid height are active | Load Data Management series tab; inspect cache keys and grid heights | Series grid results are cached; main grids reserve vertical space; compact upload grid can opt out. |
| DM-04 | Data Management | Disabled delete tooltip is native title and does not create horizontal overflow | Load Series grid; find a row with samples; hover disabled delete wrapper; measure page width | Native title exists, no Radzen popup appears, and document width does not overflow. |
| BE-01 | Backend/Data | Project view filtering by id succeeds with includes applied last | POST `/api/views/projects` with filter `Id == 1` | API returns success, one project, and populated project-series navigation data. |
| BE-02 | Backend/Data | Opening `/projects/1` loads project details and tabs | Navigate to `/projects/1`; inspect tabs and project fields | Project detail renders Overview, Series, Samples, SubSamples, Apm, and Particles tabs with no 404. |
| BE-03 | Backend/Data | Sample External Code filtering works through data/query-builder-compatible API | Fetch a sample row; filter samples by its `ExternalCode`; filter query-builder composite by `Sample.ExternalCode` | Direct sample filtering succeeds, and query-builder mode accepts `Sample.ExternalCode` without error. |
| BE-04 | Backend/Data | Project-scoped grids and charts return data for project 1 | Call series/sample/subsample/apm/particle scoped grids; call APM and particle chart APIs | All scoped endpoints succeed and chart APIs return arrays. |
| QB-01 | Query Builder | Filter edits wait for Apply query; enum conditions execute | Toggle Sample on/off, add External Code equals 001 OR Sample Type equals Qc, then Apply query | Editing preserves the current grid without data requests; applying succeeds with the matching series. |
| UI-02 | Evaluation | Evaluation page query-builder shell renders expected controls | Open Evaluation; open Query Builder tab; inspect filter sections and controls | Query Builder contains filter sections, Apply, grid selector, and preset controls. |
| STEM-01 | STEM Upload | STEM tab exposes sample downloads and multi-file drop target | Open STEM Preview tab; inspect download links and file input attributes | Sample downloads exist, file input has multiple enabled, supported extensions are advertised, and visible dropzone content does not intercept pointer events. |
| STEM-02 | STEM Upload | Uploading two STEM CSV files previews both, deleting one removes only its rows | Select two valid CSV files; upload; delete first uploaded file; check preview grid | Both files upload; deleting one removes only that file rows while keeping the other. |
| STEM-03 | STEM Upload | Invalid STEM file reports upload error without breaking the page | Select malformed CSV; upload; inspect status | File row reaches error status and app stays usable. |
| STEM-04 | STEM Upload | Uploading TSV and tab-delimited DAT files previews both | Select valid TSV and DAT files; upload; inspect preview grid | Both delimited text files upload and their rows appear in the preview grid. |
| CRUD-01 | Backend/Data | Series create/delete API round trip keeps seeded grid usable | Create a temporary no-sample series through app API; fetch it; delete it; verify it is gone | Series CRUD endpoints work for a no-sample row and cleanup succeeds. |
| UI-09 | Data Management | Series row expansion loads child sample grid without layout tear | Expand the first series row; inspect nested sample grid and layout width | Child sample grid shows rows, reserves nested height, and no page overflow is introduced. |
| UI-04 | Evaluation | Preset-filter save button enables only after valid name and creates filter | Open Query Builder; click preset save; observe disabled check button; enter valid unique name; save; cleanup via API | Check button is disabled for invalid input, enables for valid unique name, and created filter can be deleted. |
| UI-08 | STEM Upload | Remove File during large upload cancels immediately | Select a larger valid CSV; start upload; remove while uploading; confirm delete | In-flight upload is cancelled/removed without waiting for completion and no rows remain visible. |
| ADD-01 | Backend/API | Unmapped `/api/pmi` route returns HTTP 404 | GET `/api/pmi` | Returns API miss, not index.html HTTP 200. |
| ADD-02 | Backend/API | Unknown `/api` route does not return SPA shell | GET `/api/not-a-real-endpoint` | Returns API miss, not index.html HTTP 200. |
| ADD-03 | Navigation | Unmapped `/pmi` client route displays the not-found page | Navigate to `/pmi` | Client route shows Page not found. |
| ADD-04 | Navigation | Data Management reload is stable | Open Data Management and reload | Tabs, totals, and grid rows still render after reload. |
| ADD-05 | Navigation | Evaluation reload is stable | Open Evaluation and reload | Projects and Query Builder tabs still render after reload. |
| ADD-06 | Layout | Sidebar toggle changes width and restores | Click sidebar toggle; wait for collapse; click again; wait for restore | Sidebar collapses and expands. |
| ADD-07 | Responsive | Home fits mobile viewport | Set mobile viewport and open Home | No page-level horizontal overflow. |
| ADD-08 | Project Detail | Missing project id routes to not-found | Open `/projects/999999999`; wait for app route transition | Page not found is shown. |
| ADD-09 | Project Detail | Project tabs load without visible errors | Open project 1; click Series, Samples, SubSamples, Apm, Particles | Each tab remains usable, Series grid sizes to content, APM/Particle grids render, pager copy does not show raw markup, and no visible app error appears. |
| ADD-10 | Project Detail | Back to Projects returns to Evaluation | Open project 1; click Back to Projects | URL returns to Evaluation. |
| ADD-11 | Project Detail | Blank project name disables save | Edit project name; clear input | Save button is disabled. |
| ADD-12 | Backend/Data | Project scoped endpoints return nonzero totals | Call scoped grid endpoints for project 1 | All scoped endpoints succeed with nonzero totals. |
| ADD-13 | Backend/Data | Project chart APIs return arrays | Call APM and particle chart endpoints for project 1 | Both endpoints return arrays. |
| ADD-14 | Backend/Data | Series sort by id descending works | Request series ordered by `Id desc` | Returned IDs are descending. |
| ADD-15 | Backend/Data | Project filter and order combination succeeds | Filter projects by `SampleCount > 0`; order by `Id desc` | Request succeeds with populated `ProjectSeries`. |
| ADD-16 | Backend/API | Preset name availability detects duplicates | Create preset via API; check name availability; cleanup | Existing name returns `available=false`; cleanup succeeds. |
| ADD-17 | Backend/API | STEM upload endpoint rejects missing file | POST STEM upload form without a file | Request is rejected without server crash. |
| ADD-18 | Evaluation | Query Builder switches to Sample grid | Open Query Builder and select Sample grid | Sample grid columns render. |
| ADD-19 | Evaluation | Query Builder Apply with empty filters keeps grid usable | Open Query Builder; click Apply without filters | Grid remains visible and no fetch error appears. |
| ADD-20 | CRUD | Series update API persists SGAS comment | Create temp series with SGAS comment; update comment; fetch by id; delete temp row | Updated SGAS comment is returned by the view endpoint. |
| ADD-21 | STEM Upload | Pending STEM file can be removed before upload | Select valid file; remove before upload | File disappears and no preview rows appear. |
| ADD-22 | STEM Upload | Oversized STEM file is rejected client-side | Select a file larger than the STEM preview limit | Size error appears and upload button is unavailable. |
| ADD-23 | STEM Upload | Preview grid stays hidden before upload | Select valid file but do not upload | No preview rows are shown. |
| ADD-24 | STEM Upload | Deleting last uploaded STEM file hides preview rows | Upload one valid file; delete it; confirm | Uploaded rows disappear after delete. |
| ADD-25 | CRUD | Series create API persists SGAS comment on insert | Create temp series with SGAS comment; fetch by id; delete temp row | Created SGAS comment is returned by the view endpoint. |
| ADD-26 | CRUD | Series update API persists working paper link and DU/NU flags | Create temp series; update working paper link and DU/NU flags; fetch by id; delete temp row | Updated scalar fields are returned by the view endpoint. |
| ADD-27 | CRUD | Series update API can clear nullable analysis date | Create temp series with analysis date; update it to null; fetch by id; delete temp row | Nullable analysis date can be cleared. |
| ADD-28 | Backend/API | Series delete endpoint is idempotent for missing ids | DELETE a non-existent series id | Endpoint succeeds without affecting existing rows. |
| ADD-29 | Backend/API | Project name availability respects existing rows and exclude id | Fetch project 1 name; check name availability with and without its id excluded | Existing name is unavailable globally and available when excluding the same project id. |
| ADD-30 | Backend/API | Preset name availability respects exclude id | Create temp preset; check name availability with and without its id excluded; cleanup | Existing preset name is unavailable globally and available when excluding itself. |
| ADD-31 | Backend/API | Preset update renames an empty preset | Create temp preset; update its name; fetch; cleanup | Updated preset payload has the new name and remains entry-free. |
| ADD-32 | Backend/API | Preset delete removes row from dropdown payload | Create temp preset; delete it; fetch all presets | Deleted preset is absent from the list payload. |
| ADD-33 | Backend/Data | Series priority ids are ordered before normal sort | Fetch lowest and highest existing series ids; request descending series with the low id prioritized | Priority id appears first even when normal sort would put another id first. |
| ADD-34 | Backend/Data | Series counts respect impossible filters | POST series-counts with impossible id filter | All aggregate counts are zero. |
| ADD-35 | Backend/API | Series enum option endpoint returns series type values | POST enum-options for SeriesType | Endpoint returns integer enum values. |
| ADD-36 | Backend/API | Sample enum option endpoint returns sample type values | POST enum-options for SampleType | Endpoint returns integer enum values. |
| ADD-37 | STEM API | STEM entries endpoint without a session returns empty result | POST stem-entries without `stemSessionId` | Endpoint succeeds with zero rows. |
| ADD-38 | STEM API | STEM entries endpoint with an unknown session returns empty result | POST stem-entries with random `stemSessionId` | Endpoint succeeds with zero rows. |
| ADD-39 | Backend/API | Chart APIs return empty arrays for missing project ids | GET APM and particle chart APIs for a missing project id | Endpoints return HTTP 200 with empty arrays. |
| ADD-40 | UI/Layout | Project detail fits a mobile viewport | Open `/projects/1` at 390px width; measure document width | Project detail renders without horizontal overflow or app error UI. |
| ADD-41 | Backend/API | Query-aware chart APIs respect grid filters | POST APM and particle chart APIs with an impossible grid filter | Endpoints return HTTP 200 with empty arrays. |
| ADD-42 | Backend/API | Chart APIs accept Radzen nullable comparison filters | POST APM and particle chart APIs with `x => ((x.U234 ?? null) > 1)` | Endpoints return HTTP 200 with chart arrays instead of a dynamic-LINQ comparison error. |
| ADD-43 | Backend/API | Preset create accepts enabled entries with null navigation payloads | POST a preset filter with enabled sample descriptors and a null navigation property | Endpoint creates the preset and returns it with its entry payload. |
