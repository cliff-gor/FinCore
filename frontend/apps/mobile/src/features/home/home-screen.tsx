import { HealthStatus } from '@/features/health/health-status';
import { SessionPanel } from './session-panel';
import { Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { SafeAreaView } from 'react-native-safe-area-context';

/**
 * Home: proves the app reaches the Mobile Gateway and that customer sign-in works (spec 002). Balances and quick
 * actions arrive with later milestones.
 */
export function HomeScreen() {
  const { t } = useTranslation();
  return (
    <SafeAreaView className="flex-1 bg-bg">
      <View className="flex-1 gap-6 p-6">
        <Text accessibilityRole="header" className="font-display text-h1 text-text">
          {t('home.title')}
        </Text>
        <HealthStatus />
        <SessionPanel />
      </View>
    </SafeAreaView>
  );
}
