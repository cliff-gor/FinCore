import { palette } from './colors.ts';

/** Gradient "moments" — §32.2. Stops are shared by CSS (web) and expo-linear-gradient (mobile). */
export const gradients = {
  aura: [palette['aura-peach'], palette['aura-blush'], palette['aura-lilac'], palette['aura-sky']],
  auraWarm: ['#FFF6E3', palette['aura-peach'], '#E8E4F8'],
} as const;
