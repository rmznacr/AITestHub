// Central API client. All backend URLs are built here (never hardcoded in components).

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;

export const API_BASE_URL = (configuredBaseUrl ?? 'http://localhost:5098').replace(/\/+$/, '');

/** Error thrown for any failed API call; carries a user-friendly message. */
export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'ApiError';
  }
}

/** Builds an absolute API URL from a path starting with '/'. */
export function apiUrl(path: string): string {
  if (!path.startsWith('/')) {
    path = '/' + path;
  }
  return API_BASE_URL + path;
}

/** Extracts a user-friendly error message from a JSON error body. */
export function extractErrorMessage(body: unknown, fallback: string): string {
  if (body && typeof body === 'object') {
    const record = body as Record<string, unknown>;
    if (typeof record.error === 'string' && record.error.length > 0) {
      return record.error;
    }
    // ASP.NET Core ProblemDetails
    if (typeof record.title === 'string' && record.title.length > 0) {
      return record.title;
    }
  }
  if (typeof body === 'string' && body.length > 0 && body.length < 500) {
    return body;
  }
  return fallback;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(apiUrl(path), {
      headers: { 'Content-Type': 'application/json' },
      ...init,
    });
  } catch {
    throw new ApiError('The backend is not reachable. Make sure the API is running.', 0);
  }

  if (!response.ok) {
    let body: unknown = null;
    try {
      body = await response.json();
    } catch {
      // Non-JSON error body; fall through to the fallback message.
    }
    throw new ApiError(
      extractErrorMessage(body, `The request failed with status ${response.status}.`),
      response.status,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const http = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PATCH', body: body === undefined ? undefined : JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};
