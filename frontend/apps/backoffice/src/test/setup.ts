import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { configureApi } from '../app/http.ts';
import { server } from './msw/server.ts';

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' });
  // Node's fetch needs absolute URLs; the browser uses same-origin relative URLs.
  configureApi('http://localhost');
});
afterEach(() => {
  cleanup();
  server.resetHandlers();
});
afterAll(() => server.close());
