import type * as ApiClient from '@fincore/api-client';
import { ApiProblem, request } from '@fincore/api-client';
import { createI18n } from '@fincore/i18n';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react-native';
import { I18nextProvider, initReactI18next } from 'react-i18next';
import { HomeScreen } from './home-screen';

jest.mock('@fincore/api-client', () => {
  const actual = jest.requireActual<typeof ApiClient>('@fincore/api-client');
  return { ...actual, request: jest.fn() };
});

const mockRequest = jest.mocked(request);
const i18n = createI18n({ lng: 'en', plugins: [initReactI18next] });

async function renderHome() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });
  await render(
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <HomeScreen />
      </I18nextProvider>
    </QueryClientProvider>,
  );
}

describe('HomeScreen', () => {
  afterEach(() => mockRequest.mockReset());

  it('shows the gateway health status', async () => {
    mockRequest.mockResolvedValue('Healthy');
    await renderHome();

    expect(screen.getByText('Welcome to FinCore')).toBeTruthy();
    expect(await screen.findByText('● FinCore is online')).toBeTruthy();
    expect(mockRequest).toHaveBeenCalledWith('/health/live', expect.anything());
  });

  it('shows "update required" when the gateway answers 426', async () => {
    mockRequest.mockRejectedValue(new ApiProblem(426));
    await renderHome();

    expect(await screen.findByText('Update required')).toBeTruthy();
  });

  it('shows an unreachable state with retry on other failures', async () => {
    mockRequest.mockRejectedValue(new ApiProblem(0, { code: 'Network' }));
    await renderHome();

    expect(await screen.findByText('○ FinCore is not reachable')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });
});
