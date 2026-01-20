import { afterEach, describe, expect, it, vi } from 'vitest';
import { configureHttp, isApiProblem, request, type ApiProblem } from './http.ts';

function jsonResponse(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } });
}

describe('http', () => {
  afterEach(() => configureHttp({ baseUrl: '' }));

  it('cookie + CSRF mode sends credentials, X-CSRF and X-Client-Version', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, { ok: true }));
    configureHttp({
      baseUrl: '',
      csrfHeader: true,
      clientVersion: 'backoffice/0.1.0',
      fetch: fetchMock,
    });

    await expect(request('/api/v1/institutions')).resolves.toEqual({ ok: true });

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/institutions');
    expect(init?.credentials).toBe('include');
    const headers = new Headers(init?.headers);
    expect(headers.get('X-CSRF')).toBe('1');
    expect(headers.get('X-Client-Version')).toBe('backoffice/0.1.0');
    expect(headers.has('Authorization')).toBe(false);
  });

  it('bearer mode sends Authorization when a token exists and omits cookies', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response('Healthy'));
    configureHttp({
      baseUrl: 'https://localhost:7202/',
      getAuthHeader: () => Promise.resolve('Bearer token-123'),
      clientVersion: 'mobile/0.1.0',
      headers: { 'X-App-Version': 'mobile/0.1.0 (1)' },
      fetch: fetchMock,
    });

    await expect(request<string>('/health/live')).resolves.toBe('Healthy');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('https://localhost:7202/health/live');
    expect(init?.credentials).toBe('omit');
    const headers = new Headers(init?.headers);
    expect(headers.get('Authorization')).toBe('Bearer token-123');
    expect(headers.get('X-App-Version')).toBe('mobile/0.1.0 (1)');
    expect(headers.get('X-Client-Version')).toBe('mobile/0.1.0');
    expect(headers.has('X-CSRF')).toBe(false);
  });

  it('omits Authorization when there is no token', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }));
    configureHttp({ baseUrl: '', getAuthHeader: () => undefined, fetch: fetchMock });

    await expect(request('/bff/logout', { method: 'POST' })).resolves.toBeUndefined();
    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).has('Authorization')).toBe(false);
  });

  it('parses RFC 9457 ProblemDetails into ApiProblem', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(
        409,
        {
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
          title: 'Conflict',
          status: 409,
          detail: 'Institution code FCB2 is already taken.',
          code: 'Organization.InstitutionCodeTaken',
          traceId: '00-abc-01',
        },
        'application/problem+json',
      ),
    );
    configureHttp({ baseUrl: '', fetch: fetchMock });

    const error = await request('/api/v1/institutions', { method: 'POST', body: '{}' }).catch(
      (e: unknown) => e,
    );
    expect(isApiProblem(error)).toBe(true);
    const problem = error as ApiProblem;
    expect(problem.status).toBe(409);
    expect(problem.code).toBe('Organization.InstitutionCodeTaken');
    expect(problem.traceId).toBe('00-abc-01');
    expect(problem.message).toBe('Institution code FCB2 is already taken.');
  });

  it('exposes validation errors', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(400, {
        status: 400,
        title: 'Validation failed',
        errors: { baseCurrency: ['Unsupported currency.'] },
      }),
    );
    configureHttp({ baseUrl: '', fetch: fetchMock });

    const problem = (await request('/x').catch((e: unknown) => e)) as ApiProblem;
    expect(problem.isValidation).toBe(true);
    expect(problem.errors).toEqual({ baseCurrency: ['Unsupported currency.'] });
  });

  it('handles non-JSON errors and network failures', async () => {
    configureHttp({
      baseUrl: '',
      fetch: vi
        .fn<typeof fetch>()
        .mockResolvedValue(new Response('Upgrade Required', { status: 426 })),
    });
    const upgrade = (await request('/health/live').catch((e: unknown) => e)) as ApiProblem;
    expect(upgrade.status).toBe(426);
    expect(upgrade.code).toBeUndefined();

    configureHttp({
      baseUrl: '',
      fetch: vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')),
    });
    const network = (await request('/health/live').catch((e: unknown) => e)) as ApiProblem;
    expect(network.status).toBe(0);
    expect(network.code).toBe('Network');
  });
});
