/** Typography scale — §32.3. Sizes and line heights in px. */
export const fontFamilies = {
  display: ['Urbanist', 'ui-sans-serif', 'system-ui', 'sans-serif'],
  sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
  serif: ['"Instrument Serif"', 'ui-serif', 'Georgia', 'serif'],
} as const;

/** One step of the type scale. */
export interface TypeStyle {
  family: keyof typeof fontFamilies;
  weight: number;
  size: number;
  lineHeight: number;
}

// TODO(M1): add the mobile display size from spec §32.3 (40/48) with the mobile UI kit.
/** Named text styles. Sizes are the web values from spec §32.3. */
export const typeScale = {
  'display-number': { family: 'display', weight: 300, size: 48, lineHeight: 56 },
  'display-headline': { family: 'display', weight: 600, size: 40, lineHeight: 48 },
  h1: { family: 'display', weight: 600, size: 28, lineHeight: 36 },
  h2: { family: 'display', weight: 600, size: 22, lineHeight: 30 },
  h3: { family: 'sans', weight: 600, size: 17, lineHeight: 24 },
  body: { family: 'sans', weight: 400, size: 15, lineHeight: 22 },
  'body-sm': { family: 'sans', weight: 400, size: 13, lineHeight: 18 },
  label: { family: 'sans', weight: 500, size: 12, lineHeight: 16 },
  'table-number': { family: 'sans', weight: 500, size: 14, lineHeight: 20 },
} as const satisfies Record<string, TypeStyle>;
