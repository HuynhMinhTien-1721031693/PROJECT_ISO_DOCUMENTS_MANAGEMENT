import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.ISODOC_BASE_URL ?? 'http://localhost:5062';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command:
        'dotnet run --project src/04_WebAPI/IsoDoc.WebAPI/IsoDoc.WebAPI.csproj --no-launch-profile --urls http://127.0.0.1:5075 --no-build',
      url: 'http://127.0.0.1:5075/health',
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      cwd: '..',
    },
    {
      command:
        'dotnet run --project src/05_Frontend/IsoDoc.Blazor/IsoDoc.Blazor.csproj --no-launch-profile --urls http://127.0.0.1:5062 --no-build',
      url: 'http://127.0.0.1:5062/',
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      cwd: '..',
    },
  ],
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
