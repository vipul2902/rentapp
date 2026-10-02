import { router } from 'expo-router';

import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { FormScreen } from '@/components/FormScreen';

/**
 * V1 has no email provider, so there is no self-service reset yet (see ROADMAP.md). Staff passwords are
 * reset by the owner from the Staff screen.
 */
export function ForgotPasswordScreen() {
  return (
    <FormScreen>
      <AppText variant="title">Forgot password?</AppText>

      <Card>
        <AppText variant="heading">Staff and managers</AppText>
        <AppText muted>Ask the property owner to reset your password from the Staff screen in the app.</AppText>
      </Card>

      <Card>
        <AppText variant="heading">Owners</AppText>
        <AppText muted>
          Self-service password reset is coming soon. Until then, please contact RentApp support from the email address
          you registered with.
        </AppText>
      </Card>

      <Button label="Back to sign in" variant="secondary" onPress={() => router.back()} />
    </FormScreen>
  );
}
