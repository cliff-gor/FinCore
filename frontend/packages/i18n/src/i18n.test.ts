import { describe, expect, it } from 'vitest';
import { createI18n, translateErrorCode } from './index.ts';

describe('i18n', () => {
  const i18n = createI18n();

  it('loads English resources synchronously', () => {
    expect(i18n.t('session.signIn')).toBe('Sign in');
    expect(i18n.t('footer.version', { version: '0.1.0', sha: 'abc1234', env: 'Development' })).toBe(
      'v0.1.0 · abc1234 · Development',
    );
  });

  it('translates backend error codes with a fallback', () => {
    expect(translateErrorCode(i18n, 'Organization.InstitutionCodeTaken')).toBe(
      'An institution with this code already exists.',
    );
    expect(translateErrorCode(i18n, 'Something.Unknown')).toBe(
      'Something went wrong. Please try again.',
    );
  });
});
