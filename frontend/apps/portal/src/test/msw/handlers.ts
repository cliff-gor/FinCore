import { http, HttpResponse } from 'msw';

/** Default mocked BFF: signed out, so /bff/user answers 401 (spec 002). */
export const handlers = [
  http.get('*/bff/user', () =>
    HttpResponse.json({ status: 401, title: 'Unauthorized' }, { status: 401 }),
  ),
];
