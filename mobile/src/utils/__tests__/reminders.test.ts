import { smsUrl, whatsAppNumber, whatsAppUrl } from '../reminders';

describe('reminder links', () => {
  it.each([
    ['98765 43210', '919876543210'],
    ['09876543210', '919876543210'],
    ['+91-98765-43210', '919876543210'],
    ['+44 7700 900123', '447700900123'],
  ])('WhatsApp needs the country code: %s → %s', (phone, expected) => {
    expect(whatsAppNumber(phone)).toBe(expected);
  });

  it('puts the message in the link, encoded', () => {
    expect(whatsAppUrl('9876543210', 'Hi Rahul, ₹8,500 & thanks')).toBe(
      'https://wa.me/919876543210?text=Hi%20Rahul%2C%20%E2%82%B98%2C500%20%26%20thanks',
    );
    expect(smsUrl('+91 98765 43210', 'Hi', 'ios')).toBe('sms:+919876543210&body=Hi');
    expect(smsUrl('+91 98765 43210', 'Hi', 'android')).toBe('sms:+919876543210?body=Hi');
  });
});
