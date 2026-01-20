import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { configureApi } from './app/http.ts';
import { Providers } from './app/providers.tsx';
import { createQueryClient } from './app/queryClient.ts';
import { createAppRouter } from './app/router.tsx';
import './styles.css';

configureApi();
const queryClient = createQueryClient();
const router = createAppRouter(queryClient);

const container = document.getElementById('root');
if (container === null) throw new Error('Root element #root not found');

createRoot(container).render(
  <StrictMode>
    <Providers queryClient={queryClient} router={router} />
  </StrictMode>,
);
