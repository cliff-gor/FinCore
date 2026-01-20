import { createI18n } from '@fincore/i18n';
import { initReactI18next } from 'react-i18next';

export const i18n = createI18n({ lng: 'en', plugins: [initReactI18next] });
