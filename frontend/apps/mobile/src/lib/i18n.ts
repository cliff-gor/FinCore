import { createI18n } from '@fincore/i18n';
import { getLocales } from 'expo-localization';
import { initReactI18next } from 'react-i18next';

export const i18n = createI18n({
  lng: getLocales()[0]?.languageCode ?? 'en',
  plugins: [initReactI18next],
});
