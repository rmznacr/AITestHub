import { describe, it, expect } from 'vitest';
import { apiUrl, extractErrorMessage, API_BASE_URL } from '../api/client';

describe('apiUrl', () => {
  it('should prefix the base URL to paths starting with /', () => {
    expect(apiUrl('/api/projects')).toBe(`${API_BASE_URL}/api/projects`);
  });

  it('should add a leading slash when missing', () => {
    expect(apiUrl('api/projects')).toBe(`${API_BASE_URL}/api/projects`);
  });
});

describe('extractErrorMessage', () => {
  it('should extract the error field from a JSON error body', () => {
    expect(extractErrorMessage({ error: 'Project was not found.' }, 'fallback')).toBe(
      'Project was not found.',
    );
  });

  it('should extract the title from a ProblemDetails body', () => {
    expect(extractErrorMessage({ title: 'One or more validation errors occurred.' }, 'fallback')).toBe(
      'One or more validation errors occurred.',
    );
  });

  it('should return the fallback for unexpected body shapes', () => {
    expect(extractErrorMessage({ unexpected: true }, 'fallback')).toBe('fallback');
    expect(extractErrorMessage(null, 'fallback')).toBe('fallback');
    expect(extractErrorMessage(42, 'fallback')).toBe('fallback');
  });

  it('should ignore empty error strings', () => {
    expect(extractErrorMessage({ error: '' }, 'fallback')).toBe('fallback');
  });

  it('should use short plain-text bodies', () => {
    expect(extractErrorMessage('Not Found', 'fallback')).toBe('Not Found');
  });

  it('should not use very long plain-text bodies', () => {
    const long = 'x'.repeat(600);
    expect(extractErrorMessage(long, 'fallback')).toBe('fallback');
  });
});
