import '../global.css';
import { configureApi } from '@/api/configureApi';
import { i18n } from '@/lib/i18n';
import { createQueryClient } from '@/lib/query-client';
import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useState } from 'react';
import { I18nextProvider } from 'react-i18next';

// Module scope: runs once per JS bundle load, before any screen can fetch.
configureApi();

export default function RootLayout() {
  // Lazy initial state keeps one client for the app's lifetime across re-renders and fast refresh.
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <StatusBar style="auto" />
        <Stack screenOptions={{ headerShown: false }} />
      </I18nextProvider>
    </QueryClientProvider>
  );
}
