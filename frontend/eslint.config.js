// @ts-check
import { base, commonJsFiles, ignores, mobileBoundaries, react } from '@fincore/config/eslint';

export default [ignores, ...base, react, mobileBoundaries, commonJsFiles];
