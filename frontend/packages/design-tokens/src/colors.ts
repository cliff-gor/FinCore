/** Brand palette — master spec §32.2. */
export const palette = {
  'pine-950': '#00262B',
  'pine-800': '#0E3B42',
  'pine-600': '#2E5E66',
  'cyan-100': '#CDE3E6',
  'mint-300': '#ABFFAE',
  'mint-100': '#E4FFE5',
  'navy-800': '#1F2A5C',
  canvas: '#F8F6F2',
  'greige-100': '#F1EEE9',
  'greige-200': '#E6E2DC',
  'stone-500': '#8A8580',
  'stone-700': '#4A4744',
  'honey-400': '#F4B740',
  'honey-100': '#FDF1D6',
  'aura-peach': '#FBD9A8',
  'aura-blush': '#F5C2D3',
  'aura-lilac': '#D6CCF6',
  'aura-sky': '#CDE7F7',
  'cat-blue': '#5E9FD3',
  'cat-sand': '#C4B394',
  'cat-sun': '#F7D468',
  'cat-leaf': '#74C97B',
  'cat-lilac': '#BDB4F2',
  'graphite-950': '#0F0F12',
  'graphite-900': '#18181D',
  'graphite-800': '#24242B',
  white: '#FFFFFF',
} as const;

export type PaletteToken = keyof typeof palette;

/** Semantic tokens — components only use these (§32.2). */
export interface SemanticColors {
  bg: string;
  surface: string;
  'surface-sunken': string;
  'surface-glass': string;
  border: string;
  text: string;
  'text-muted': string;
  primary: string;
  'primary-foreground': string;
  'primary-on-dark': string;
  'primary-on-dark-foreground': string;
  'panel-dark': string;
  brand: string;
  highlight: string;
  positive: string;
  'positive-tint': string;
  negative: string;
  warning: string;
  info: string;
  'info-tint': string;
}

export type SemanticToken = keyof SemanticColors;

export const semanticLight: SemanticColors = {
  bg: palette.canvas,
  surface: palette.white,
  'surface-sunken': palette['greige-100'],
  'surface-glass': 'rgba(255,255,255,0.65)',
  border: palette['greige-200'],
  text: palette['pine-950'],
  'text-muted': palette['stone-500'],
  primary: palette['pine-950'],
  'primary-foreground': palette.white,
  'primary-on-dark': palette['mint-300'],
  'primary-on-dark-foreground': palette['pine-950'],
  'panel-dark': palette['pine-950'],
  brand: palette['pine-600'],
  highlight: palette['honey-400'],
  positive: '#1F8F4E',
  'positive-tint': palette['mint-100'],
  negative: '#D64545',
  warning: '#C98A12',
  info: '#2F6FB5',
  'info-tint': palette['cyan-100'],
};

export const semanticDark: SemanticColors = {
  bg: palette['graphite-950'],
  surface: palette['graphite-900'],
  'surface-sunken': '#131317',
  'surface-glass': 'rgba(36,36,43,0.6)',
  border: palette['graphite-800'],
  text: '#F4F3F0',
  'text-muted': '#9C988F',
  primary: palette['mint-300'],
  'primary-foreground': palette['pine-950'],
  'primary-on-dark': palette['mint-300'],
  'primary-on-dark-foreground': palette['pine-950'],
  'panel-dark': palette['pine-800'],
  brand: '#7FB8BF',
  highlight: palette['honey-400'],
  positive: palette['mint-300'],
  'positive-tint': 'rgba(171,255,174,0.12)',
  negative: '#F87171',
  warning: '#FBBF24',
  info: '#7DB8F0',
  'info-tint': 'rgba(125,184,240,0.14)',
};
