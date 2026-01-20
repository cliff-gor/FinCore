/**
 * Runs Orval programmatically with orval.config.ts (one project per backend module).
 * The Orval CLI loads its config through jiti with an unescaped file URL, which breaks when the checkout
 * path contains `#` (e.g. `C#-Projects`). Node >= 22.18 imports the TypeScript config natively instead.
 */
import { generate } from 'orval';
import config from '../orval.config.ts';

for (const [project, options] of Object.entries(config)) {
  await generate(options, process.cwd(), { projectName: project });
}
