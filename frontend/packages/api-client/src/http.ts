/**
 * Configurable fetch wrapper shared by web and mobile (master spec §30.4, §31.3).
 *
 * - Cookie + CSRF mode (web, behind the BFF): `credentials: 'include'` and `X-CSRF: 1`.
 * - Bearer mode (mobile, through the Mobile Gateway): `Authorization` from `getAuthHeader`.
 * Both send `X-Client-Version`. Error responses are parsed as RFC 9457 ProblemDetails into `ApiProblem`.
 * Orval-generated clients call `customFetch` (configured as the Orval mutator).
 */

export type HeaderValue = string | null | undefined;

export interface HttpConfig {
  /** Origin (and optional path prefix) prepended to relative request paths. Empty string = same origin. */
  baseUrl: string;
  /** Bearer mode: returns the full `Authorization` header value (e.g. `Bearer eyJ…`), or nothing when signed out. */
  getAuthHeader?: () => HeaderValue | Promise<HeaderValue>;
  /** Cookie + CSRF mode: send cookies and the `X-CSRF: 1` header required by the BFF. */
  csrfHeader?: boolean;
  /** Sent as `X-Client-Version`, e.g. `backoffice/0.1.0`. */
  clientVersion?: string;
  /** Additional static headers, e.g. the mobile `X-App-Version`. */
  headers?: Readonly<Record<string, string>>;
  /** Override for tests or platforms with a custom fetch. Defaults to the global `fetch`. */
  fetch?: typeof fetch;
}

/** RFC 9457 ProblemDetails with FinCore's `code` and `traceId` extensions (master spec §28). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  [extension: string]: unknown;
}

/** Thrown for every non-2xx response and for network failures (`status` 0, `code` `Network`). */
export class ApiProblem extends Error {
  readonly status: number;
  readonly code: string | undefined;
  readonly title: string | undefined;
  readonly detail: string | undefined;
  readonly errors: Readonly<Record<string, string[]>>;
  readonly traceId: string | undefined;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem?: ProblemDetails, message?: string) {
    super(message ?? problem?.detail ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiProblem';
    this.status = status;
    this.problem = problem;
    this.code = problem?.code;
    this.title = problem?.title;
    this.detail = problem?.detail;
    this.errors = problem?.errors ?? {};
    this.traceId = problem?.traceId;
  }

  get isValidation(): boolean {
    return this.status === 400 && Object.keys(this.errors).length > 0;
  }
}

export function isApiProblem(error: unknown): error is ApiProblem {
  return error instanceof ApiProblem;
}

let config: HttpConfig = { baseUrl: '' };

export function configureHttp(next: HttpConfig): void {
  config = { ...next };
}

export function getHttpConfig(): Readonly<HttpConfig> {
  return config;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function toProblemDetails(value: unknown): ProblemDetails | undefined {
  if (!isRecord(value)) return undefined;
  const errors: Record<string, string[]> = {};
  if (isRecord(value.errors)) {
    for (const [field, messages] of Object.entries(value.errors)) {
      if (Array.isArray(messages)) errors[field] = messages.map(String);
      else if (typeof messages === 'string') errors[field] = [messages];
    }
  }
  const str = (v: unknown): string | undefined => (typeof v === 'string' ? v : undefined);
  return {
    ...value,
    type: str(value.type),
    title: str(value.title),
    status: typeof value.status === 'number' ? value.status : undefined,
    detail: str(value.detail),
    instance: str(value.instance),
    code: str(value.code),
    traceId: str(value.traceId),
    errors,
  };
}

function resolveUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  const base = config.baseUrl.replace(/\/+$/, '');
  return `${base}${path.startsWith('/') ? path : `/${path}`}`;
}

async function buildHeaders(init: RequestInit | undefined): Promise<Headers> {
  const headers = new Headers(init?.headers);
  if (!headers.has('Accept')) headers.set('Accept', 'application/json, application/problem+json');
  if (config.clientVersion !== undefined) headers.set('X-Client-Version', config.clientVersion);
  for (const [name, value] of Object.entries(config.headers ?? {})) headers.set(name, value);
  if (config.csrfHeader === true) headers.set('X-CSRF', '1');
  if (config.getAuthHeader !== undefined) {
    const auth = await config.getAuthHeader();
    if (auth !== null && auth !== undefined && auth.length > 0) headers.set('Authorization', auth);
  }
  if (typeof init?.body === 'string' && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  return headers;
}

async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204 || response.status === 205) return undefined;
  const text = await response.text();
  if (text.length === 0) return undefined;
  const contentType = response.headers.get('Content-Type') ?? '';
  if (contentType.includes('json')) {
    try {
      return JSON.parse(text) as unknown;
    } catch {
      return text;
    }
  }
  return text;
}

/** Performs a request and returns the parsed body; throws `ApiProblem` on failure. */
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const doFetch = config.fetch ?? globalThis.fetch;
  let response: Response;
  try {
    response = await doFetch(resolveUrl(path), {
      ...init,
      headers: await buildHeaders(init),
      credentials: config.csrfHeader === true ? 'include' : (init?.credentials ?? 'omit'),
    });
  } catch (error) {
    // DOMException is not available on every React Native runtime, so check by name.
    if (isRecord(error) && error.name === 'AbortError') throw error;
    throw new ApiProblem(0, { code: 'Network' }, 'Network request failed');
  }

  const body = await readBody(response);
  if (!response.ok) {
    throw new ApiProblem(response.status, toProblemDetails(body) ?? { status: response.status });
  }
  return body as T;
}

/** Error type used by generated hooks: every failure is an `ApiProblem` (Orval reads this export). */
export type ErrorType<_Problem = ProblemDetails> = ApiProblem;

/** Orval mutator (see orval.config.ts). */
export const customFetch = <T>(url: string, init?: RequestInit): Promise<T> =>
  request<T>(url, init);

export default customFetch;
