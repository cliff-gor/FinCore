import i18next, { type i18n as I18nInstance, type InitOptions } from 'i18next';
import common from './locales/en/common.json';
import errors from './locales/en/errors.json';

export const defaultNS = 'common';
export const supportedLanguages = ['en'] as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];

export const resources = {
  en: { common, errors },
} as const;

export type ErrorCode = keyof typeof errors;

/**
 * Creates an isolated i18next instance with the shared FinCore resources.
 * Apps pass framework plugins (e.g. react-i18next's `initReactI18next`) via `plugins`.
 */
export function createI18n(
  options: { lng?: string; plugins?: Parameters<I18nInstance['use']>[0][] } = {},
): I18nInstance {
  const instance = i18next.createInstance();
  for (const plugin of options.plugins ?? []) instance.use(plugin);
  const init: InitOptions = {
    resources,
    lng: options.lng ?? 'en',
    fallbackLng: 'en',
    supportedLngs: [...supportedLanguages],
    nonExplicitSupportedLngs: true,
    ns: ['common', 'errors'],
    defaultNS,
    interpolation: { escapeValue: false },
    initAsync: false,
  };
  void instance.init(init);
  return instance;
}

/** Translates a backend ProblemDetails `code` (e.g. `Organization.InstitutionCodeTaken`), falling back to a generic message. */
export function translateErrorCode(i18n: I18nInstance, code: string | undefined): string {
  // Error codes contain dots, so key nesting is disabled for this lookup.
  const opts = { ns: 'errors', keySeparator: false } as const;
  const key = code !== undefined && i18n.exists(code, opts) ? code : 'Unknown';
  return i18n.t(key, opts);
}
