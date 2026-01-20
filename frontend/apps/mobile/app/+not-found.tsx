import { Link, Stack } from 'expo-router';
import { Text, View } from 'react-native';

export default function NotFound() {
  return (
    <View className="flex-1 items-center justify-center gap-4 bg-bg p-6">
      <Stack.Screen options={{ title: 'Not found' }} />
      <Text className="text-h2 text-text">This screen does not exist.</Text>
      <Link href="/" className="text-body text-brand">
        Go to home
      </Link>
    </View>
  );
}
