import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { toHealthState, useHealth } from './use-health';

export function HealthStatus() {
  const { t } = useTranslation();
  const query = useHealth();
  const state = toHealthState(query);

  if (state === 'update-required') {
    return (
      <View
        accessibilityRole="alert"
        className="gap-2 rounded-lg bg-panel-dark p-5"
        testID="update-required"
      >
        <Text className="text-h2 text-primary-on-dark">{t('health.updateRequiredTitle')}</Text>
        <Text className="text-body text-white">{t('health.updateRequiredBody')}</Text>
      </View>
    );
  }

  return (
    <View className="gap-3 rounded-lg bg-surface p-5" testID="health-status">
      <Text className="text-label text-text-muted">{t('health.title')}</Text>
      {state === 'checking' && (
        <View className="flex-row items-center gap-2">
          <ActivityIndicator />
          <Text className="text-body text-text">{t('health.checking')}</Text>
        </View>
      )}
      {/* A filled or hollow dot as well as colour, so status is never colour-only (spec §32.2). */}
      {state === 'healthy' && (
        <Text className="text-h3 text-positive">{`● ${t('health.healthy')}`}</Text>
      )}
      {state === 'unhealthy' && (
        <>
          <Text className="text-h3 text-negative">{`○ ${t('health.unhealthy')}`}</Text>
          <Pressable
            accessibilityRole="button"
            // h-11 = 44 pt, the minimum touch target (spec §31.7).
            className="h-11 items-center justify-center self-start rounded-pill bg-primary px-5"
            onPress={() => void query.refetch()}
          >
            <Text className="text-label text-primary-foreground">{t('health.retry')}</Text>
          </Pressable>
        </>
      )}
    </View>
  );
}
