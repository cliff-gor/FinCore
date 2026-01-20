/// <reference types="vitest/config" />
import { fincoreVersionPlugin, readBuildInfo } from '@fincore/config/vite';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv, type ProxyOptions } from 'vite';
import pkg from './package.json' with { type: 'json' };

const APP = 'backoffice';
/** BackOffice BFF launch profile (spec 001: gateway BackOffice :7200). */
const GATEWAY = 'https://localhost:7200';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_');
  const buildInfo = readBuildInfo(APP, pkg.version, env.VITE_APP_ENV);
  // Proxying /api and /bff keeps the session cookie same-origin in dev, as CloudFront does on AWS (spec §30.3).
  // `secure: false` accepts the self-signed ASP.NET Core dev certificate on the local gateway.
  const toGateway: ProxyOptions = { target: GATEWAY, changeOrigin: true, secure: false };
  const proxy = { '/api': toGateway, '/bff': toGateway };

  return {
    plugins: [
      // autoCodeSplitting emits absolute `?tsr-split` imports that Rolldown cannot resolve when the checkout
      // path contains `#` (e.g. `C#-Projects`). Re-enable once routes grow or the path issue is fixed upstream.
      tanstackRouter({ target: 'react', autoCodeSplitting: false }),
      react(),
      tailwindcss(),
      fincoreVersionPlugin(buildInfo),
    ],
    // Fixed ports (Playwright targets the preview port), so fail fast instead of moving to a free one.
    server: { port: 5173, strictPort: true, proxy },
    preview: { port: 4173, strictPort: true, proxy },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      include: ['src/**/*.test.{ts,tsx}'],
      // Component tests assert behaviour, not styling, so CSS is not processed.
      css: false,
    },
  };
});
