import { fetchMe, useAuth } from '@/auth/AuthProvider';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Pressable, Text, View } from 'react-native';

// Minimal sign-in plumbing (spec 002); the real welcome and sign-in screens are spec 003.
export function SessionPanel() {
  const { t } = useTranslation();
  const auth = useAuth();
  const me = useQuery({
    queryKey: ['me'],
    queryFn: fetchMe,
    enabled: auth.status === 'signed-in',
  });

  if (auth.status === 'restoring') {
    return <Text className="text-body text-text-muted">{t('session.checking')}</Text>;
  }

  if (auth.status === 'signed-in') {
    return (
      <View className="gap-3">
        {me.data !== undefined ? (
          <Text className="text-body text-text">
            {t('session.signedInAsKind', {
              name: me.data.name,
              kind: t(me.data.kind === 'staff' ? 'session.kindStaff' : 'session.kindCustomer'),
            })}
          </Text>
        ) : me.isError ? (
          <Text className="text-body text-text-muted">{t('session.unavailable')}</Text>
        ) : (
          <Text className="text-body text-text-muted">{t('session.checking')}</Text>
        )}
        <SessionButton label={t('session.signOut')} onPress={() => void auth.signOut()} />
      </View>
    );
  }

  return (
    <View className="gap-3">
      <Text className="text-body text-text">
        {auth.status === 'error' ? t('session.unavailable') : t('session.signedOut')}
      </Text>
      <SessionButton
        label={t('session.signIn')}
        disabled={auth.status === 'signing-in'}
        onPress={() => void auth.signIn()}
      />
    </View>
  );
}

function SessionButton({
  label,
  onPress,
  disabled = false,
}: {
  label: string;
  onPress: () => void;
  disabled?: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      className="h-11 items-center justify-center self-start rounded-pill bg-primary px-5"
    >
      <Text className="text-label font-medium text-primary-foreground">{label}</Text>
    </Pressable>
  );
}
