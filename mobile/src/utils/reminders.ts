import * as Clipboard from 'expo-clipboard';
import { Linking, Platform, Share } from 'react-native';

import type { ReminderChannel } from '@/api/reminders';

/**
 * Phone number for WhatsApp click-to-chat (wa.me): digits with the country code. Indian 10-digit mobile numbers
 * (optionally with a leading 0) get 91; numbers that already have a country code are kept.
 */
export function whatsAppNumber(phone: string): string {
  const digits = phone.replace(/\D/g, '');
  if (digits.length === 10) return `91${digits}`;
  if (digits.length === 11 && digits.startsWith('0')) return `91${digits.slice(1)}`;
  return digits;
}

export function whatsAppUrl(phone: string, message: string): string {
  return `https://wa.me/${whatsAppNumber(phone)}?text=${encodeURIComponent(message)}`;
}

export function smsUrl(phone: string, message: string, os: string = Platform.OS): string {
  const number = phone.replace(/[^\d+]/g, '');
  // iOS reads the body after "&", Android after "?".
  return `sms:${number}${os === 'ios' ? '&' : '?'}body=${encodeURIComponent(message)}`;
}

/**
 * Hands the message to the chosen app. A person always presses Send there; nothing is sent automatically
 * (spec: no unofficial WhatsApp automation). Returns false if the person backed out of the share sheet.
 */
export async function deliverReminder(channel: ReminderChannel, phone: string, message: string): Promise<boolean> {
  switch (channel) {
    case 'Copy':
      await Clipboard.setStringAsync(message);
      return true;
    case 'Share': {
      const result = await Share.share({ message });
      return result.action !== Share.dismissedAction;
    }
    case 'WhatsApp':
      await Linking.openURL(whatsAppUrl(phone, message));
      return true;
    case 'Sms':
      await Linking.openURL(smsUrl(phone, message));
      return true;
  }
}
