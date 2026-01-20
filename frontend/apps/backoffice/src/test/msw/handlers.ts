import { http, HttpResponse } from 'msw';

/** Default mocked BFF: M0 answers 401 for /bff/user (spec 001). */
export const handlers = [
  http.get('*/bff/user', () =>
    HttpResponse.json({ status: 401, title: 'Unauthorized' }, { status: 401 }),
  ),
];
