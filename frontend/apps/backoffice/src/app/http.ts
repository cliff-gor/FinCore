import { configureHttp } from '@fincore/api-client';
import { buildInfo } from './build-info.ts';

/** Same-origin calls through the BackOffice BFF: cookie session + `X-CSRF: 1` (master spec §30.3). */
export function configureApi(baseUrl = ''): void {
  configureHttp({
    baseUrl,
    csrfHeader: true,
    clientVersion: `backoffice/${buildInfo.version}`,
  });
}
