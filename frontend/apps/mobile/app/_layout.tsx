import '../global.css';
import { configureApi } from '@/api/configureApi';
import { AuthProvider } from '@/auth/AuthProvider';
import { authTokens } from '@/auth/authSession';
import { i18n } from '@/lib/i18n';
import { createQueryClient } from '@/lib/query-client';
import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useState } from 'react';
import { I18nextProvider } from 'react-i18next';

// Module scope: runs once per JS bundle load, before any screen can fetch. Every request asks the refresher for a
// valid access token, so the token lives in memory only (spec §31.4).
configureApi(() => authTokens.getAccessToken());

export default function RootLayout() {
  // Lazy initial state keeps one client for the app's lifetime across re-renders and fast refresh.
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <I18nextProvider i18n={i18n}>
          <StatusBar style="auto" />
          <Stack screenOptions={{ headerShown: false }} />
        </I18nextProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
