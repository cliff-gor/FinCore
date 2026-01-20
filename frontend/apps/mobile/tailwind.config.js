// NativeWind (Tailwind v3) theme generated from the shared design tokens (master spec §32.9).
const tokens = require('@fincore/design-tokens/nativewind');

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./app/**/*.{ts,tsx}', './src/**/*.{ts,tsx}'],
  presets: [require('nativewind/preset')],
  theme: {
    extend: tokens,
  },
  plugins: [],
};
