# Nuclear Evaluation Test Cases

Last reviewed: 2026-06-14

## Scope

- Purpose: durable manual/regression QA checklist for Nuclear Evaluation.
- Execute against a disposable local or staging environment with seeded data.
- Use a fresh browser session when checking captcha, cookie, and theme behavior.
- For STEM upload checks, use small valid STEM CSV files and one larger valid CSV for cancellation behavior.


## Summary

- Executed cases: 21
- Passed: 19
- Failed: 2
- Main failures: UI-04 preset-filter save/check button state; API-01 unknown /api/pmi route returning SPA shell.

## Findings

| Priority | Finding                                                                | Detail                                                                                                                                                                                                                                                    |
| -------- | ---------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| P1       | Preset-filter save/check button is enabled before valid input          | After clicking the preset save icon in Evaluation > Query Builder, the check button is immediately enabled while FilterName is blank. Expected disabled until the validator has a valid 5-25 character unique name. This is the UI-4 class of regression. |
| P2       | API-looking removed PMI route returns app shell with HTTP 200          | GET /api/pmi returns index.html with HTTP 200 because the SPA fallback catches the path after MapControllers. If /api/* is part of the server contract, unknown API routes should return 404/405 or JSON error, not the client shell.                     |
| P3       | README still mentions PMI                                              | README.md still says the product has PMI report uploads and PMI retention cleanup, while current UI/controllers/entities appear to have removed PMI. This is stale documentation.                                                                         |
| P3       | STEM delete emitted one requestfailed event despite correct UI cleanup | During STEM-02, Chrome reported net::ERR_ABORTED for DELETE /api/stem/{session}/files/{fileId}, but the UI and preview grid still reflected correct cleanup. Retest around network handling if this becomes flaky.                                        |
| P3       | Sample ExternalCode filter is hard to visually verify                  | SampleQueryBuilderFilter exposes Sample.ExternalCode, but SampleGrid does not display ExternalCode, so the UI can show count changes but not the matching value in the active Sample grid.                                                                |

## Historical Regression Checklist

| ID | Regression | Result | Evidence |
|---|---|---|---|
| BE-1 | Filtering Projects by id | PASS | BE-01 returned one project for Id == 1 and included ProjectSeries. |
| BE-2 | Opening /projects/1 | PASS | BE-02 rendered project detail instead of not-found. |
| BE-3 | Upload 2 STEM files, delete 1 | PASS | STEM-02 removed only stem-a.csv rows and kept stem-b.csv rows. |
| BE-4 | PMI dashboard removed | MIXED | Visible UI has no PMI affordance. Suspicious: /api/pmi returns the SPA shell with HTTP 200, and README still mentions PMI. |
| UI-1 | Dark/light toggle | PASS | UI-01 swapped Radzen stylesheet and colors. |
| UI-2 | Series delete tooltip overflow | PASS | DM-04 uses native title and no horizontal overflow. |
| UI-3 | Sample External Code filter | PASS | BE-03 verified direct and query-builder-compatible Sample.ExternalCode filtering. |
| UI-4 | Preset-filter Save button disabled logic | FAIL | UI-04 found the check button enabled before a valid name is entered. |
| UI-5 | Grid empty flash/teary resize | PASS | DM-03 verified cached results and reserved grid height. |
| UI-6 | STEM drag/drop target | PASS | STEM-01 verified file input overlay and non-interactive visible dropzone content. |
| UI-7 | Multi-file upload | PASS | STEM-01/STEM-02 selected and uploaded two files at once. |
| UI-8 | Remove File during upload | PASS | UI-08 removed a larger in-flight upload immediately. |
| UI-9 | Child sample grid teary expand | PASS | UI-09 showed nested grid min-height 128px and no overflow. |
| UI-10 | Uploaded-files list empty gap | PASS | STEM-02 compact upload grid min-height was 0px. |
| UI-11 | Series totals cached | PASS | DM-03 found ne-grid-cache:series-counts in localStorage. |

## Executed Test Cases

| ID | Area | Scenario | Steps | Expected | Result | Evidence |
|---|---|---|---|---|---|---|
| CAP-01 | Gate | Captcha status remains verified in a fresh browser session | Open a verified application session<br>Read /api/captcha/status from the page context | The API reports verified=true and the gate is not visible. | PASS | Captcha status API returned verified=true; gate was not visible. |
| NAV-01 | Navigation | Home page and sidebar navigation render | Navigate to /<br>Check home copy and sidebar menu labels | Home page renders, sidebar includes Home, Data Management, and Evaluation. | PASS | Home page and sidebar rendered expected copy and links. |
| NAV-02 | Navigation | Unknown route shows friendly not-found page | Navigate to /definitely-not-a-real-route | The route displays Page not found rather than crashing. | PASS | Unknown route rendered friendly Page not found without Blazor error UI. |
| UI-01 | Theme | Dark/light appearance toggle changes the active theme | Navigate to /data-management<br>Capture theme state<br>Click the Radzen appearance toggle<br>Capture theme state again | The theme state changes visibly or through the persisted theme cookie. | PASS | Theme stylesheet changed from software-dark-base.css to software-base.css; body background changed. |
| DM-01 | Data Management | Series tab loads totals and first page without fetch errors | Navigate to /data-management<br>Wait for series totals and grid rows | Series totals are numeric, first page displays 10 rows, and no fetch/blazor error is visible. | PASS | Seed totals visible: 100,000 series, 299,868 samples, 599,495 subsamples, 1,799,316 APM, 1,198,679 particles; first series page rendered. |
| DM-02 | Data Management | No PMI upload/dashboard affordances remain in the app shell | Inspect Data Management and Evaluation visible text | The removed PMI feature is not exposed through visible tabs, nav, dashboard, upload, or grid copy. | PASS | No visible PMI text/links/tabs on Data Management or Evaluation. |
| DM-03 | Data Management | Grid result cache and reserved grid height are active | Load Data Management series tab<br>Inspect localStorage cache keys and computed grid heights | Series and series-count results are cached, main grids reserve vertical space, compact upload grid opt-out remains possible. | PASS | localStorage contains SeriesGrid and series-counts cache entries; main grid min-height 384px; compact grid min-height 0px. |
| DM-04 | Data Management | Disabled delete tooltip is native title and does not create horizontal overflow | Load Series grid<br>Find a row with samples<br>Hover disabled delete wrapper<br>Measure page scroll width | A native title exists for Series contains samples, no Radzen popup appears, and document width does not overflow. | PASS | span[title="Series contains samples"] exists; no Radzen popup text; page scrollWidth stayed 1440px. |
| BE-01 | Backend/Data | Project view filtering by id succeeds with includes applied last | POST /api/views/projects with filter Id == 1 | The API returns success, one project, and populated project-series navigation data; no IncludeOptimized/dynamic-LINQ fault. | PASS | POST /api/views/projects with Id == 1 returned one project and ProjectSeries entries. |
| BE-02 | Backend/Data | Opening /projects/1 loads project detail instead of not-found | Navigate to /projects/1<br>Inspect tabs and project fields | Project detail renders Overview, Series, Samples, SubSamples, Apm, and Particles tabs with no 404. | PASS | /projects/1 rendered project detail tabs and did not route to not-found. |
| BE-03 | Backend/Data | Sample External Code filtering works through data/query-builder-compatible API | Fetch a sample row<br>Filter samples by its ExternalCode<br>Filter query-builder composite by Sample.ExternalCode | Direct sample filtering succeeds, and preset-filter-box query-builder mode accepts Sample.ExternalCode without error. | PASS | ExternalCode 2F5 direct sample filter and Sample.ExternalCode query-builder preset filter both returned totalCount 72. |
| BE-04 | Backend/Data | Project-scoped grids and charts return data for project 1 | Call series/sample/subsample/apm/particle project-scoped grids<br>Call APM and particle bin chart APIs | All scoped endpoints succeed, and chart APIs return arrays rather than errors. | PASS | Project 1 scoped totals: series 4, samples 13, subsamples 26, apm 79, particles 45; chart APIs returned arrays. |
| UI-02 | Evaluation | Evaluation page query-builder shell renders expected controls | Navigate to /evaluation<br>Open Query Builder tab<br>Inspect filter sections and controls | Query Builder contains filter sections for Series, Sample, SubSample, Apm, Particle, Apply, grid selector, and preset filter controls. | PASS | Query Builder showed Series, Sample, SubSample, Apm, Particle, Apply, grid selector, and preset controls. |
| STEM-01 | STEM Upload | STEM tab exposes sample downloads and multi-file drop target | Navigate to Data Management<br>Open STEM Preview tab<br>Inspect download links and file input attributes | Three sample download links exist, file input has multiple enabled, dropzone content does not intercept pointer events. | PASS | Three sample download links present; input[type=file] has multiple; .stem-dropzone-content pointer-events is none. |
| STEM-02 | STEM Upload | Uploading two STEM CSV files previews both, deleting one removes only its rows | Open STEM Preview tab<br>Select two valid CSV files at once<br>Upload files<br>Delete the first uploaded file<br>Check preview grid by file name | Both files upload, preview rows show both filenames, deleting one removes only that file rows while keeping the other. | PASS | Two CSVs uploaded; deleting stem-a.csv removed LAB-A rows and left LAB-B/stem-b.csv rows; compact upload grid min-height 0px. |
| STEM-03 | STEM Upload | Invalid STEM file reports upload error without breaking the page | Select malformed CSV<br>Upload file<br>Inspect status | The file row reaches UploadError/error text and the app stays usable. | PASS | Malformed CSV showed Error processing the file and no Blazor error UI. |
| CRUD-01 | Backend/Data | Series create/delete API round trip keeps seeded grid usable | Create a temporary no-sample series through the app API<br>Verify it can be fetched<br>Delete it<br>Verify it is gone | Series CRUD endpoints work for a no-sample row and cleanup succeeds. | PASS | Temporary no-sample series was created, fetched by id, deleted, and no longer fetchable. |
| API-01 | Backend/Data | Enum filter options and removed PMI routes behave predictably | Call series enum options endpoint<br>Probe known removed PMI paths | Enum options endpoint returns numeric values; PMI routes are absent/not found rather than half-working. | FAIL | /api/views/series/enum-options passed, but /api/pmi returned HTTP 200 with index.html shell instead of 404/405. |
| UI-09 | Data Management | Series row expansion loads child sample grid without layout tear | Navigate to Data Management<br>Expand the first series row<br>Inspect nested sample grid and layout width | Child sample grid shows rows, reserves nested height, and no horizontal page overflow is introduced. | PASS | Expanding first series row showed nested sample grid with min-height 128px and no horizontal overflow. |
| UI-04 | Evaluation | Preset-filter save button enables only after valid name and creates filter | Open Evaluation > Query Builder<br>Click preset save<br>Observe disabled check button<br>Enter valid unique name<br>Save<br>Verify and cleanup via API | The save/check button is disabled for invalid input, enables for a valid name, and the filter can be created and deleted. | FAIL | After clicking preset save, the check button was enabled before a valid FilterName was entered. |
| UI-08 | STEM Upload | Remove File during large upload cancels immediately | Open STEM Preview<br>Select a large valid CSV<br>Start upload<br>Click remove while status is uploading<br>Confirm delete<br>Observe immediate removal/no preview rows | The in-flight upload is cancelled/removed from the list without waiting for completion, and no slow-file rows remain visible. | PASS | larger STEM upload was removed while in progress; file disappeared immediately and no LAB-SLOW rows appeared. |

## Notes

- No browser console errors or page errors were captured in the final main run.
- CRUD-01 created and deleted temporary series ids 110002 in the final run; cleanup succeeded. Earlier dry runs also cleaned up their temporary ids.
- Backend/data assertions should use the same authenticated browser/session context as the real client when practical.


