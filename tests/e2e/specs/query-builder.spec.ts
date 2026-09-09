import { expect, test, type Locator, type Page, type Response } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import {
  apiDelete,
  apiGet,
  confirmDialogYes,
  expectNoVisibleAppError,
  openQueryBuilder
} from '../support/app';

type EntityName = 'Series' | 'Sample' | 'SubSample' | 'Apm' | 'Particle';
type EditorValue =
  | { kind: 'text' | 'date' | 'number'; value: string }
  | { kind: 'enum'; value: string }
  | { kind: 'boolean'; value: boolean }
  | { kind: 'none' };

type FilterCase = {
  id: string;
  entity: EntityName;
  property: string;
  propertyPath: string;
  operator: string;
  value: EditorValue;
  expectNarrowed?: boolean;
};

type FetchDataResult = {
  entries: unknown[];
  totalCount: number;
  isSuccessful: boolean;
  errorMessage?: string | null;
};

type StoredDescriptor = {
  Property: string;
  Type?: string;
  FilterValue?: unknown;
  FilterOperator?: number;
};

type PresetFilterEntry = {
  presetFilterEntryType: number;
  logicalFilterOperator: number;
  isEnabled: boolean;
  serializedDescriptors: string;
};

type PresetFilter = {
  id: number;
  name: string;
  entries: PresetFilterEntry[];
};

const filterCases: FilterCase[] = [
  {
    id: 'QB-02',
    entity: 'Series',
    property: 'Series Id',
    propertyPath: 'Series.Id',
    operator: 'Greater than',
    value: { kind: 'number', value: '10000' }
  },
  {
    id: 'QB-03',
    entity: 'Series',
    property: 'Sgas Comment',
    propertyPath: 'Series.SgasComment',
    operator: 'Starts with',
    value: { kind: 'text', value: 'Sample' }
  },
  {
    id: 'QB-04',
    entity: 'Series',
    property: 'Series Type',
    propertyPath: 'Series.SeriesType',
    operator: 'Equals',
    value: { kind: 'enum', value: 'Regular' }
  },
  {
    id: 'QB-05',
    entity: 'Series',
    property: 'Created At',
    propertyPath: 'Series.CreatedAt',
    operator: 'Less than',
    value: { kind: 'date', value: daysAgo(45) }
  },
  {
    id: 'QB-06',
    entity: 'Series',
    property: 'Is Du',
    propertyPath: 'Series.IsDu',
    operator: 'Equals',
    value: { kind: 'boolean', value: true }
  },
  {
    id: 'QB-07',
    entity: 'Sample',
    property: 'External Code',
    propertyPath: 'Sample.ExternalCode',
    operator: 'Equals',
    value: { kind: 'text', value: '005' }
  },
  {
    id: 'QB-08',
    entity: 'Sample',
    property: 'Sample Type',
    propertyPath: 'Sample.SampleType',
    operator: 'Equals',
    value: { kind: 'enum', value: 'Qc' }
  },
  {
    id: 'QB-09',
    entity: 'Sample',
    property: 'Latitude',
    propertyPath: 'Sample.Latitude',
    operator: 'Is null',
    value: { kind: 'none' }
  },
  {
    id: 'QB-10',
    entity: 'Sample',
    property: 'Sampling Date',
    propertyPath: 'Sample.SamplingDate',
    operator: 'Greater than',
    value: { kind: 'date', value: daysAgo(120) }
  },
  {
    id: 'QB-11',
    entity: 'SubSample',
    property: 'Activity Notes',
    propertyPath: 'SubSample.ActivityNotes',
    operator: 'Starts with',
    value: { kind: 'text', value: 'Awaiting' }
  },
  {
    id: 'QB-12',
    entity: 'SubSample',
    property: 'Is From Legacy System',
    propertyPath: 'SubSample.IsFromLegacySystem',
    operator: 'Equals',
    value: { kind: 'boolean', value: false }
  },
  {
    id: 'QB-13',
    entity: 'SubSample',
    property: 'Screening Date',
    propertyPath: 'SubSample.ScreeningDate',
    operator: 'Less than',
    value: { kind: 'date', value: daysAgo(180) }
  },
  {
    id: 'QB-14',
    entity: 'Apm',
    property: 'U234',
    propertyPath: 'Apm.U234',
    operator: 'Greater than',
    value: { kind: 'number', value: '5' }
  },
  {
    id: 'QB-15',
    entity: 'Apm',
    property: 'U235',
    propertyPath: 'Apm.U235',
    operator: 'Is null',
    value: { kind: 'none' }
  },
  {
    id: 'QB-16',
    entity: 'Particle',
    property: 'Comment',
    propertyPath: 'Particle.Comment',
    operator: 'Ends with',
    value: { kind: 'text', value: 'results' }
  },
  {
    id: 'QB-17',
    entity: 'Particle',
    property: 'Particle External Id',
    propertyPath: 'Particle.ParticleExternalId',
    operator: 'Less than',
    value: { kind: 'number', value: '1000' }
  },
  {
    id: 'QB-18',
    entity: 'Particle',
    property: 'Analysis Date',
    propertyPath: 'Particle.AnalysisDate',
    operator: 'Greater than',
    value: { kind: 'date', value: daysAgo(120) }
  },
  {
    id: 'QB-19',
    entity: 'Particle',
    property: 'Is Nu',
    propertyPath: 'Particle.IsNu',
    operator: 'Equals',
    value: { kind: 'boolean', value: true }
  },
  {
    id: 'QB-22',
    entity: 'Series',
    property: 'Series Id',
    propertyPath: 'Series.Id',
    operator: 'Equals',
    value: { kind: 'number', value: '10000' }
  },
  {
    id: 'QB-23',
    entity: 'Series',
    property: 'Series Id',
    propertyPath: 'Series.Id',
    operator: 'Not equals',
    value: { kind: 'number', value: '10000' }
  },
  {
    id: 'QB-24',
    entity: 'Series',
    property: 'Series Id',
    propertyPath: 'Series.Id',
    operator: 'Less than',
    value: { kind: 'number', value: '10001' }
  },
  {
    id: 'QB-25',
    entity: 'Series',
    property: 'Series Id',
    propertyPath: 'Series.Id',
    operator: 'Less than or equals',
    value: { kind: 'number', value: '10000' }
  },
  {
    id: 'QB-26',
    entity: 'Series',
    property: 'Series Id',
    propertyPath: 'Series.Id',
    operator: 'Greater than or equals',
    value: { kind: 'number', value: '109999' }
  },
  {
    id: 'QB-27',
    entity: 'Series',
    property: 'Sgas Comment',
    propertyPath: 'Series.SgasComment',
    operator: 'Contains',
    value: { kind: 'text', value: 'Sample' }
  },
  {
    id: 'QB-28',
    entity: 'Series',
    property: 'Sgas Comment',
    propertyPath: 'Series.SgasComment',
    operator: 'Does not contain',
    value: { kind: 'text', value: 'Sample' }
  },
  {
    id: 'QB-29',
    entity: 'Series',
    property: 'Sgas Comment',
    propertyPath: 'Series.SgasComment',
    operator: 'Ends with',
    value: { kind: 'text', value: 'results' }
  },
  {
    id: 'QB-30',
    entity: 'Series',
    property: 'Sgas Comment',
    propertyPath: 'Series.SgasComment',
    operator: 'Equals',
    value: { kind: 'text', value: 'Recheck results' }
  },
  {
    id: 'QB-31',
    entity: 'Series',
    property: 'Sgas Comment',
    propertyPath: 'Series.SgasComment',
    operator: 'Is not empty',
    value: { kind: 'none' },
    expectNarrowed: false
  },
  {
    id: 'QB-32',
    entity: 'Series',
    property: 'Working Paper Link',
    propertyPath: 'Series.WorkingPaperLink',
    operator: 'Starts with',
    value: { kind: 'text', value: '/results' }
  },
  {
    id: 'QB-33',
    entity: 'Series',
    property: 'Working Paper Link',
    propertyPath: 'Series.WorkingPaperLink',
    operator: 'Ends with',
    value: { kind: 'text', value: 'H9' }
  },
  {
    id: 'QB-34',
    entity: 'Series',
    property: 'Analysis Complete Date',
    propertyPath: 'Series.AnalysisCompleteDate',
    operator: 'Is null',
    value: { kind: 'none' }
  },
  {
    id: 'QB-35',
    entity: 'Series',
    property: 'Analysis Complete Date',
    propertyPath: 'Series.AnalysisCompleteDate',
    operator: 'Is not null',
    value: { kind: 'none' }
  },
  {
    id: 'QB-36',
    entity: 'Series',
    property: 'Is Nu',
    propertyPath: 'Series.IsNu',
    operator: 'Equals',
    value: { kind: 'boolean', value: false }
  },
  {
    id: 'QB-37',
    entity: 'Series',
    property: 'Series Type',
    propertyPath: 'Series.SeriesType',
    operator: 'Not equals',
    value: { kind: 'enum', value: 'Regular' }
  },
  {
    id: 'QB-38',
    entity: 'Sample',
    property: 'Sequence',
    propertyPath: 'Sample.Sequence',
    operator: 'Starts with',
    value: { kind: 'text', value: '10000-' }
  },
  {
    id: 'QB-39',
    entity: 'Sample',
    property: 'External Code',
    propertyPath: 'Sample.ExternalCode',
    operator: 'Not equals',
    value: { kind: 'text', value: '005' },
    expectNarrowed: false
  },
  {
    id: 'QB-40',
    entity: 'Sample',
    property: 'Sample Class',
    propertyPath: 'Sample.SampleClass',
    operator: 'Contains',
    value: { kind: 'text', value: 'qc' }
  },
  {
    id: 'QB-41',
    entity: 'Sample',
    property: 'Series Id',
    propertyPath: 'Sample.SeriesId',
    operator: 'Equals',
    value: { kind: 'number', value: '10000' }
  },
  {
    id: 'QB-42',
    entity: 'Sample',
    property: 'SubSample Count',
    propertyPath: 'Sample.SubSampleCount',
    operator: 'Greater than',
    value: { kind: 'number', value: '2' }
  },
  {
    id: 'QB-43',
    entity: 'Sample',
    property: 'Longitude',
    propertyPath: 'Sample.Longitude',
    operator: 'Is not null',
    value: { kind: 'none' },
    expectNarrowed: false
  },
  {
    id: 'QB-44',
    entity: 'Sample',
    property: 'Sampling Date',
    propertyPath: 'Sample.SamplingDate',
    operator: 'Less than',
    value: { kind: 'date', value: daysAgo(300) }
  },
  {
    id: 'QB-45',
    entity: 'Sample',
    property: 'Sample Type',
    propertyPath: 'Sample.SampleType',
    operator: 'Not equals',
    value: { kind: 'enum', value: 'Qc' },
    expectNarrowed: false
  },
  {
    id: 'QB-46',
    entity: 'SubSample',
    property: 'Sequence',
    propertyPath: 'SubSample.Sequence',
    operator: 'Contains',
    value: { kind: 'text', value: '-005-' }
  },
  {
    id: 'QB-47',
    entity: 'SubSample',
    property: 'External Code',
    propertyPath: 'SubSample.ExternalCode',
    operator: 'Equals',
    value: { kind: 'text', value: '003' }
  },
  {
    id: 'QB-48',
    entity: 'SubSample',
    property: 'Activity Notes',
    propertyPath: 'SubSample.ActivityNotes',
    operator: 'Does not contain',
    value: { kind: 'text', value: 'Awaiting' },
    expectNarrowed: false
  },
  {
    id: 'QB-49',
    entity: 'SubSample',
    property: 'Tracking Number',
    propertyPath: 'SubSample.TrackingNumber',
    operator: 'Equals',
    value: { kind: 'text', value: 'TN0002' }
  },
  {
    id: 'QB-50',
    entity: 'SubSample',
    property: 'Screening Date',
    propertyPath: 'SubSample.ScreeningDate',
    operator: 'Greater than',
    value: { kind: 'date', value: daysAgo(60) }
  },
  {
    id: 'QB-51',
    entity: 'SubSample',
    property: 'Is From Legacy System',
    propertyPath: 'SubSample.IsFromLegacySystem',
    operator: 'Not equals',
    value: { kind: 'boolean', value: false }
  },
  {
    id: 'QB-52',
    entity: 'Apm',
    property: 'Apm Id',
    propertyPath: 'Apm.Id',
    operator: 'Equals',
    value: { kind: 'number', value: '1' }
  },
  {
    id: 'QB-53',
    entity: 'Apm',
    property: 'U234',
    propertyPath: 'Apm.U234',
    operator: 'Less than',
    value: { kind: 'number', value: '5' }
  },
  {
    id: 'QB-54',
    entity: 'Apm',
    property: 'U234',
    propertyPath: 'Apm.U234',
    operator: 'Is not null',
    value: { kind: 'none' },
    expectNarrowed: false
  },
  {
    id: 'QB-55',
    entity: 'Apm',
    property: 'Comment',
    propertyPath: 'Apm.Comment',
    operator: 'Contains',
    value: { kind: 'text', value: 'Sample' }
  },
  {
    id: 'QB-56',
    entity: 'Apm',
    property: 'Comment',
    propertyPath: 'Apm.Comment',
    operator: 'Does not contain',
    value: { kind: 'text', value: 'Sample' },
    expectNarrowed: false
  },
  {
    id: 'QB-57',
    entity: 'Particle',
    property: 'Particle External Id',
    propertyPath: 'Particle.ParticleExternalId',
    operator: 'Greater than',
    value: { kind: 'number', value: '2500' }
  },
  {
    id: 'QB-58',
    entity: 'Particle',
    property: 'U234',
    propertyPath: 'Particle.U234',
    operator: 'Is null',
    value: { kind: 'none' }
  },
  {
    id: 'QB-59',
    entity: 'Particle',
    property: 'U234',
    propertyPath: 'Particle.U234',
    operator: 'Is not null',
    value: { kind: 'none' },
    expectNarrowed: false
  },
  {
    id: 'QB-60',
    entity: 'Particle',
    property: 'Laboratory Code',
    propertyPath: 'Particle.LaboratoryCode',
    operator: 'Is not empty',
    value: { kind: 'none' },
    expectNarrowed: false
  },
  {
    id: 'QB-61',
    entity: 'Particle',
    property: 'Comment',
    propertyPath: 'Particle.Comment',
    operator: 'Contains',
    value: { kind: 'text', value: 'Sample' }
  },
  {
    id: 'QB-62',
    entity: 'Particle',
    property: 'Comment',
    propertyPath: 'Particle.Comment',
    operator: 'Does not contain',
    value: { kind: 'text', value: 'Sample' },
    expectNarrowed: false
  },
  {
    id: 'QB-63',
    entity: 'Particle',
    property: 'Analysis Date',
    propertyPath: 'Particle.AnalysisDate',
    operator: 'Less than',
    value: { kind: 'date', value: daysAgo(250) }
  },
  {
    id: 'QB-64',
    entity: 'Particle',
    property: 'Is Nu',
    propertyPath: 'Particle.IsNu',
    operator: 'Not equals',
    value: { kind: 'boolean', value: true }
  }
];

test('QB-01 filter edits wait for Apply query and enum conditions execute', async ({ page }) => {
  const requests: string[] = [];
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/api/views/series')) {
      requests.push(request.postData() ?? '');
    }
  });
  await openQueryBuilder(page);
  await expect(page.getByRole('gridcell', { name: '10000', exact: true })).toBeVisible();
  const initialRequests = requests.length;
  const sampleToggle = entityToggle(page, 'Sample');

  await sampleToggle.click();
  await expect(page.locator('.rz-datafilter:visible')).toHaveCount(1);
  await sampleToggle.click();
  await expect(page.locator('.rz-datafilter:visible')).toHaveCount(0);
  await sampleToggle.click();
  const filter = page.locator('.rz-datafilter:visible').last();
  await addCondition(page, filter, 'External Code', 'Equals', { kind: 'text', value: '001' });
  await addCondition(page, filter, 'Sample Type', 'Equals', { kind: 'enum', value: 'Qc' });
  await filter.getByRole('radio', { name: 'Or', exact: true }).click();

  expect(requests).toHaveLength(initialRequests);
  await expect(page.getByRole('gridcell', { name: '10000', exact: true })).toBeVisible();
  const { result } = await applyQuery(page, 'series');
  expect(result.totalCount).toBe(100_000);
  expect(requests.at(-1)).toContain('SampleType');

  const appliedRequests = requests.length;
  await filter.getByRole('radio', { name: 'And', exact: true }).click();
  expect(requests).toHaveLength(appliedRequests);
  const narrowed = await applyQuery(page, 'series');
  expect(narrowed.result.totalCount).toBeGreaterThan(0);
  expect(narrowed.result.totalCount).toBeLessThan(result.totalCount);
  await expectNoVisibleAppError(page);
});

test.describe('Query Builder scalar types and operators', () => {
  for (const scenario of filterCases) {
    test(`${scenario.id} ${scenario.entity} ${scenario.property} ${scenario.operator}`, async ({ page }) => {
      await openQueryBuilder(page);
      const filter = await enableEntity(page, scenario.entity);
      await addCondition(page, filter, scenario.property, scenario.operator, scenario.value);

      const { requestBody, result } = await applyQuery(page, 'series');
      const applied = requestBody.presetFilterBox?.filters[scenario.entity];

      if (!applied) throw new Error(`Missing filter in request: ${JSON.stringify(requestBody)}`);
      expect(applied).toContain(scenario.propertyPath);
      expect(result.totalCount).toBeGreaterThan(0);
      if (scenario.expectNarrowed !== false) {
        expect(result.totalCount).toBeLessThan(100_000);
      } else {
        expect(result.totalCount).toBeLessThanOrEqual(100_000);
      }
      expect(result.entries.length).toBeGreaterThan(0);
      await expectNoVisibleAppError(page);
    });
  }
});

test('QB-20 combines all entity filters and applies them to every result grid', async ({ page }) => {
  await openQueryBuilder(page);
  const series = await enableEntity(page, 'Series');
  await addCondition(page, series, 'Series Id', 'Equals', { kind: 'number', value: '10004' });
  const sample = await enableEntity(page, 'Sample');
  await addCondition(page, sample, 'External Code', 'Equals', { kind: 'text', value: '005' });
  const subSample = await enableEntity(page, 'SubSample');
  await addCondition(page, subSample, 'External Code', 'Equals', { kind: 'text', value: '001' });
  const apm = await enableEntity(page, 'Apm');
  await addCondition(page, apm, 'Apm Id', 'Greater than', { kind: 'number', value: '0' });
  const particle = await enableEntity(page, 'Particle');
  await addCondition(page, particle, 'Particle External Id', 'Greater than or equals', {
    kind: 'number',
    value: '0'
  });

  const seriesResponse = await applyQuery(page, 'series');
  expect(seriesResponse.result.totalCount).toBe(1);
  expectFilterEntries(seriesResponse.requestBody, ['Series', 'Sample', 'SubSample', 'Apm', 'Particle']);

  const grids = [
    { name: 'Sample', endpoint: 'samples' },
    { name: 'SubSample', endpoint: 'subsamples' },
    { name: 'Apm', endpoint: 'apm' },
    { name: 'Particle', endpoint: 'particles' }
  ];
  for (const grid of grids) {
    const responsePromise = waitForViewResponse(page, grid.endpoint);
    await resultEntitySelect(page).click();
    await page.getByRole('option', { name: grid.name, exact: true }).click();
    const response = await responsePromise;
    const result = await parseViewResponse(response);
    const requestBody = response.request().postDataJSON() as QueryRequest;

    expect(result.totalCount, grid.name).toBeGreaterThan(0);
    expectFilterEntries(requestBody, ['Series', 'Sample', 'SubSample', 'Apm', 'Particle']);
  }
  await expectNoVisibleAppError(page);
});

test('QB-21 saves, retrieves, renames, and deletes a rich multi-entity preset', async ({ page }) => {
  const name = uniquePresetName('QBRich');
  const renamed = uniquePresetName('QBRen');
  let presetId = 0;

  try {
    await test.step('save filters containing every supported scalar type', async () => {
      await openQueryBuilder(page);
      await configureRichPreset(page);
      presetId = await savePreset(page, name);

      const preset = await getPreset(page, presetId);
      expect(preset.name).toBe(name);
      expect(preset.entries.map(entry => entry.presetFilterEntryType).sort()).toEqual([1, 2, 3, 4, 5]);
      expect(preset.entries.every(entry => entry.isEnabled)).toBe(true);

      const seriesEntry = preset.entries.find(entry => entry.presetFilterEntryType === 1)!;
      expect(seriesEntry.logicalFilterOperator).toBe(1);
      const descriptors = parseDescriptors(seriesEntry);
      expect(descriptors.map(descriptor => descriptor.Property)).toEqual([
        'Series.Id',
        'Series.SgasComment',
        'Series.CreatedAt',
        'Series.IsDu',
        'Series.SeriesType'
      ]);
      expect(descriptors.find(descriptor => descriptor.Property === 'Series.Id')?.Type).toBe('System.Int32');
      expect(descriptors.find(descriptor => descriptor.Property === 'Series.SgasComment')?.Type).toBe('System.String');
      expect(descriptors.find(descriptor => descriptor.Property === 'Series.CreatedAt')?.Type).toBe('System.DateTime');
      expect(descriptors.find(descriptor => descriptor.Property === 'Series.IsDu')?.Type).toBe('System.Boolean');
      expect(descriptors.find(descriptor => descriptor.Property === 'Series.SeriesType')?.Type).toBe(
        'System.Int32'
      );
    });

    await test.step('retrieve the preset after a full page reload and apply all entries', async () => {
      await page.reload({ waitUntil: 'domcontentloaded' });
      await openQueryBuilder(page);
      const responsePromise = waitForViewResponse(page, 'series');
      await savedFilterSelect(page).click();
      await page.getByRole('option', { name, exact: true }).click();
      const response = await responsePromise;
      const result = await parseViewResponse(response);
      const requestBody = response.request().postDataJSON() as QueryRequest;

      for (const entity of ['Series', 'Sample', 'SubSample', 'Apm', 'Particle'] as EntityName[]) {
        await expect(entityToggle(page, entity)).toHaveAttribute('aria-checked', 'true');
      }
      await expect(page.locator('.rz-datafilter:visible')).toHaveCount(5);
      await expect(page.locator('.rz-datafilter-property[aria-label="Sgas Comment"]')).toBeVisible();
      await expect(page.locator('.rz-datafilter-property[aria-label="Sample Type"]')).toBeVisible();
      expect(result.isSuccessful).toBe(true);
      expectFilterEntries(requestBody, ['Series', 'Sample', 'SubSample', 'Apm', 'Particle']);
      await expectNoVisibleAppError(page);
    });

    await test.step('rename the selected preset without losing its entries', async () => {
      await page.getByRole('button', {
        name: 'Rename or update the selected saved filter.',
        exact: true
      }).click();
      const input = page.locator('input[name="FilterName"]');
      await expect(input).toBeVisible();
      const validationResponse = page.waitForResponse(response => {
        if (!response.url().includes('/api/preset-filters/name-available')) {
          return false;
        }
        return new URL(response.url()).searchParams.get('name') === renamed;
      });
      await input.fill(renamed);
      expect((await validationResponse).ok()).toBe(true);
      const submit = page.getByRole('button', {
        name: 'Save the current query builder filters.',
        exact: true
      });
      await expect(submit).toBeEnabled({ timeout: 10_000 });
      const updateResponse = page.waitForResponse(response =>
        response.url().endsWith('/api/preset-filters') && response.request().method() === 'PUT'
      );
      await submit.click();
      expect((await updateResponse).ok()).toBe(true);

      const preset = await getPreset(page, presetId);
      expect(preset.name).toBe(renamed);
      expect(preset.entries.map(entry => entry.presetFilterEntryType).sort()).toEqual([1, 2, 3, 4, 5]);
      await expect(savedFilterSelect(page).locator('.rz-dropdown-label')).toHaveText(renamed);
    });

    await test.step('delete the selected preset and remove it from the dropdown', async () => {
      const deleteResponse = page.waitForResponse(response =>
        response.url().endsWith(`/api/preset-filters/${presetId}`) && response.request().method() === 'DELETE'
      );
      await page.getByRole('button', { name: 'Delete the selected saved filter.', exact: true }).click();
      await confirmDialogYes(page);
      expect((await deleteResponse).ok()).toBe(true);

      const presets = await apiGet<PresetFilter[]>(page, '/api/preset-filters');
      expect(presets.ok).toBe(true);
      expect(presets.json?.some(preset => preset.id === presetId)).toBe(false);
      await expect(savedFilterSelect(page).locator('.rz-dropdown-label')).toHaveText('Select saved filter');
      await expect(page.getByRole('button', {
        name: 'Delete the selected saved filter.',
        exact: true
      })).toBeDisabled();
      presetId = 0;
    });
  } finally {
    if (presetId > 0) {
      await apiDelete(page, `/api/preset-filters/${presetId}`);
    }
  }
});

type QueryRequest = {
  presetFilterBox?: { filters: Record<string, string> };
};

function daysAgo(days: number) {
  return new Date(Date.now() - days * 24 * 60 * 60 * 1_000).toISOString().slice(0, 10);
}

function uniquePresetName(prefix: string) {
  return `${prefix}-${Date.now().toString(36)}-${randomUUID().slice(0, 4)}`;
}

function entityToggle(page: Page, entity: EntityName) {
  return page.getByRole('heading', { name: entity, exact: true }).locator('..').getByRole('checkbox');
}

async function enableEntity(page: Page, entity: EntityName) {
  const toggle = entityToggle(page, entity);
  if ((await toggle.getAttribute('aria-checked')) !== 'true') {
    await toggle.click();
  }
  await expect(toggle).toHaveAttribute('aria-checked', 'true');
  return page.locator('.rz-datafilter:visible').last();
}

async function addCondition(
  page: Page,
  filter: Locator,
  property: string,
  operator: string,
  value: EditorValue
) {
  const existingProperties = await filter.locator('.rz-datafilter-property').count();
  await filter.getByRole('button', { name: 'Button', exact: true }).click();
  const propertySelect = filter.locator('.rz-datafilter-property').nth(existingProperties);
  await propertySelect.click();
  await page.getByRole('option', { name: property, exact: true }).click();
  const row = propertySelect.locator('xpath=ancestor::li[contains(@class,"rz-datafilter-item")][1]');

  await row.locator('.rz-datafilter-operator').click();
  await page.getByRole('option', { name: operator, exact: true }).click();
  await setEditorValue(page, row, value);
  return row;
}

async function setEditorValue(page: Page, row: Locator, value: EditorValue) {
  if (value.kind === 'none') {
    return;
  }
  if (value.kind === 'enum') {
    await row.locator('.rz-datafilter-editor[role="combobox"]').click();
    await page.getByRole('option', { name: value.value, exact: true }).click();
    return;
  }
  if (value.kind === 'boolean') {
    const checkbox = row.locator('.rz-datafilter-check');
    for (let attempt = 0; attempt < 3; attempt++) {
      if ((await checkbox.getAttribute('aria-checked')) === String(value.value)) {
        return;
      }
      await checkbox.click();
    }
    await expect(checkbox).toHaveAttribute('aria-checked', String(value.value));
    return;
  }
  if (value.kind === 'number') {
    await row.getByRole('spinbutton').fill(value.value);
    await row.getByRole('spinbutton').press('Tab');
    return;
  }

  const input = row.getByRole('textbox', { name: 'Filter value', exact: true });
  await input.fill(value.value);
  await input.press('Tab');
}

async function applyQuery(page: Page, endpoint: string) {
  const responsePromise = waitForViewResponse(page, endpoint);
  await page.getByRole('button', { name: 'Apply query', exact: false }).click();
  const response = await responsePromise;
  const result = await parseViewResponse(response);
  return {
    response,
    result,
    requestBody: response.request().postDataJSON() as QueryRequest
  };
}

function waitForViewResponse(page: Page, endpoint: string) {
  return page.waitForResponse(response =>
    response.url().endsWith(`/api/views/${endpoint}`) && response.request().method() === 'POST'
  );
}

async function parseViewResponse(response: Response) {
  expect(response.ok()).toBe(true);
  const result = await response.json() as FetchDataResult;
  expect(result.isSuccessful, result.errorMessage ?? undefined).toBe(true);
  return result;
}

function expectFilterEntries(request: QueryRequest, expected: EntityName[]) {
  const actual = Object.keys(request.presetFilterBox?.filters ?? {}).sort();
  expect(actual).toEqual([...expected].sort());
}

function resultEntitySelect(page: Page) {
  return page.getByRole('combobox', { name: 'Result entity', exact: true });
}

function savedFilterSelect(page: Page) {
  return page.getByRole('combobox', { name: 'Saved filter', exact: true });
}

async function configureRichPreset(page: Page) {
  const series = await enableEntity(page, 'Series');
  await addCondition(page, series, 'Series Id', 'Greater than', { kind: 'number', value: '9999' });
  await addCondition(page, series, 'Sgas Comment', 'Contains', { kind: 'text', value: 'Sample' });
  await addCondition(page, series, 'Created At', 'Less than', { kind: 'date', value: daysAgo(-1) });
  await addCondition(page, series, 'Is Du', 'Equals', { kind: 'boolean', value: true });
  await addCondition(page, series, 'Series Type', 'Equals', { kind: 'enum', value: 'Regular' });
  await series.getByRole('radio', { name: 'Or', exact: true }).click();

  const sample = await enableEntity(page, 'Sample');
  await addCondition(page, sample, 'External Code', 'Equals', { kind: 'text', value: '005' });
  await addCondition(page, sample, 'Sample Type', 'Equals', { kind: 'enum', value: 'Qc' });
  await sample.getByRole('radio', { name: 'Or', exact: true }).click();

  const subSample = await enableEntity(page, 'SubSample');
  await addCondition(page, subSample, 'Tracking Number', 'Starts with', { kind: 'text', value: 'TN' });
  const apm = await enableEntity(page, 'Apm');
  await addCondition(page, apm, 'U234', 'Is not null', { kind: 'none' });
  const particle = await enableEntity(page, 'Particle');
  await addCondition(page, particle, 'Is Nu', 'Equals', { kind: 'boolean', value: true });
}

async function savePreset(page: Page, name: string) {
  await page.getByRole('button', {
    name: 'Save the current query builder filters as a preset.',
    exact: true
  }).click();
  const input = page.locator('input[name="FilterName"]');
  await expect(input).toBeVisible();
  await input.fill(name);
  const submit = page.getByRole('button', {
    name: 'Save the current query builder filters.',
    exact: true
  });
  await expect(submit).toBeEnabled({ timeout: 10_000 });
  const createResponse = page.waitForResponse(response =>
    response.url().endsWith('/api/preset-filters') && response.request().method() === 'POST'
  );
  await submit.click();
  const response = await createResponse;
  expect(response.ok()).toBe(true);
  const id = Number(await response.text());
  expect(id).toBeGreaterThan(0);
  return id;
}

async function getPreset(page: Page, id: number) {
  const result = await apiGet<PresetFilter[]>(page, '/api/preset-filters');
  expect(result.ok).toBe(true);
  const preset = result.json?.find(candidate => candidate.id === id);
  expect(preset).toBeTruthy();
  return preset!;
}

function parseDescriptors(entry: PresetFilterEntry) {
  return JSON.parse(entry.serializedDescriptors) as StoredDescriptor[];
}
