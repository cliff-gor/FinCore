module.exports = function (api) {
  api.cache(true);
  return {
    // NativeWind's JSX runtime is what makes `className` work on React Native components.
    presets: [['babel-preset-expo', { jsxImportSource: 'nativewind' }], 'nativewind/babel'],
  };
};
